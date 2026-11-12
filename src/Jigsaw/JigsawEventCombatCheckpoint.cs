using System.Runtime.CompilerServices;
using AllMatters.Encounters;
using AllMatters.Events;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AllMatters.Jigsaw;

// Initial encounter and finite parent state, never a snapshot of battle entities.
public sealed class JigsawEventCombatPoint
{
    public required SerializableRoom Combat { get; set; }
    public required Dictionary<ulong, JigsawEventCombatRng> Owners { get; set; }
}

public sealed class JigsawEventCombatRng
{
#if STS2_PUBLIC
    public required uint Seed { get; set; }
    public required int Counter { get; set; }
#else
    public required SerializableRng State { get; set; }
#endif
}

internal static class JigsawEventCombatCheckpoint
{
    private sealed class Source(SerializableRun before) { internal SerializableRun Before = before; }
    private static readonly ConditionalWeakTable<RunState, Source> sources = new();

    internal static void RestoreSource(RunState native, SerializableRun? checkpoint)
    {
        if (checkpoint == null || !HasPoint(native))
            return;
        sources.Remove(native);
        sources.Add(native, new(JigsawSaveSource.Copy(checkpoint)));
    }

    internal static bool HasPoint(RunState native) =>
        JigsawCheckpoint.Slot.TryGet(native, out var data) && data.EventCombat != null;

    internal static SerializableRun? Before(RunState native) =>
        native.CurrentRoom is CombatRoom { IsPreFinished: false, ParentEventId: not null } &&
        HasPoint(native) && sources.TryGetValue(native, out var source) ? source.Before : null;

    internal static void ClearFinished(RunState native)
    {
        if (native.CurrentRoom is CombatRoom { IsPreFinished: true } &&
            JigsawCheckpoint.Slot.TryGet(native, out var data))
            data.EventCombat = null;
    }

    internal static bool CanCapture(AbstractRoom room, out JigsawNativeSession? session)
    {
        session = JigsawNativeSession.Current;
        return room is CombatRoom { ParentEventId: not null } combat &&
            session?.Native.BaseRoom is EventRoom { CanonicalEvent: Colosseum or DenseVegetation } parent &&
            combat.ParentEventId == parent.CanonicalEvent.Id &&
            session.Map is { CurrentRoomHunted: true };
    }

    internal static async Task Enter(RunManager manager, CombatRoom room, bool fade, JigsawNativeSession session)
    {
        var parent = session.Native.CurrentRoom;
        long rootEntry = session.Map!.CreateSave().NextEntry;
        // The event callback returns before we wait for all owners' preceding
        // option work; never await the callback from inside itself.
        await Task.Yield();
        await manager.EventSynchronizer.AwaitPendingOptionTasks();
        if (JigsawNativeSession.Current != session || parent is not EventRoom ||
            !ReferenceEquals(session.Native.CurrentRoom, parent) || session.Map?.CreateSave().NextEntry != rootEntry)
            throw new InvalidOperationException("事件战斗所属的拼图会话已变化。");
        var snapshot = JigsawSaveSource.Copy(manager.ToSave(null));
        var initialRoom = new CombatRoom(room.Encounter, session.Native).ToSerializable();
        initialRoom.ParentEventId = room.ParentEventId;
        initialRoom.ShouldResumeParentEvent = room.ShouldResumeParentEventAfterCombat;
        foreach (var (owner, rewards) in room.ExtraRewards)
            initialRoom.ExtraRewards[owner.NetId] = rewards.Select(reward => reward.ToSerializable()).ToList();
        var point = new JigsawEventCombatPoint
        {
            Combat = initialRoom,
            Owners = manager.EventSynchronizer.Events.ToDictionary(model => model.Owner!.NetId, model => Capture(model.Rng))
        };
        var history = snapshot.MapPointHistory.Last().Last();
        history.Rooms.Add(new MapPointRoomHistoryEntry { RoomType = room.RoomType, ModelId = room.ModelId });
        if (!JigsawCheckpoint.Slot.TryGet(session.Native, out var data))
            throw new InvalidDataException("事件战前点缺少拼图根房。");
        data.EventCombat = point;
        JigsawSaveSource.CaptureDocument(snapshot, session.Native);
        sources.Remove(session.Native);
        sources.Add(session.Native, new(snapshot));
        await JigsawCaptureEmbeddedCombat.NativeEnter(manager, room, fade);
    }

    internal static async Task Load(JigsawNativeSession session)
    {
        if (!JigsawCheckpoint.Slot.TryGet(session.Native, out var data) || data.EventCombat is not { } point ||
            session.Map is not { CurrentRoomHunted: true, CurrentNode: not null } ||
            point.Combat == null || point.Owners == null ||
            point.Combat.IsPreFinished ||
            point.Combat.EncounterId == null || !point.Owners.Keys.ToHashSet().SetEquals(session.Native.Players.Select(player => player.NetId)) ||
            !ValidEncounter(point) || session.Native.CurrentMapPointHistoryEntry?.Rooms.LastOrDefault()?.ModelId != point.Combat.EncounterId)
            throw new InvalidDataException("事件战前点的父事件、初始遭遇或队员不匹配。");
        var manager = RunManager.Instance;
        var map = session.Map;
        long rootEntry = map.CreateSave().NextEntry;
        void RequireOwner(EventRoom? parentRoom = null)
        {
            if (!ReferenceEquals(JigsawNativeSession.Current, session) ||
                !ReferenceEquals(manager.DebugOnlyGetState(), session.Native) ||
                !ReferenceEquals(session.Map, map) || map.CreateSave().NextEntry != rootEntry ||
                (parentRoom != null && !ReferenceEquals(session.Native.CurrentRoom, parentRoom)))
                throw new InvalidOperationException("事件战斗恢复所属的拼图会话已变化。");
        }
        RequireOwner();
        var enter = AccessTools.Method(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)]);
        EventModel parent = point.Combat.ParentEventId == ModelDb.Event<Colosseum>().Id
            ? ModelDb.Event<Colosseum>() : ModelDb.Event<DenseVegetation>();
        var parentRoom = new EventRoom(parent);
        await (Task)enter.Invoke(manager, [parentRoom, true])!;
        RequireOwner(parentRoom);
        var synchronizer = manager.EventSynchronizer;
        await synchronizer.AwaitPendingOptionTasks();
        RequireOwner(parentRoom);
        if (!ReferenceEquals(manager.EventSynchronizer, synchronizer))
            throw new InvalidOperationException("事件战斗恢复的事件同步器已变化。");
        foreach (var model in synchronizer.Events)
        {
            if (model is Colosseum colosseum)
                colosseum.RestoreCombatPhase(point.Combat.ShouldResumeParentEvent);
            else
                NativeDenseVegetation.RestoreCombatPhase((DenseVegetation)model);
            AccessTools.Property(typeof(EventModel), nameof(EventModel.Rng)).SetValue(model, Restore(point.Owners[model.Owner!.NetId]));
        }
        var room = CombatRoom.FromSerializable(point.Combat, session.Native);
        await (Task)enter.Invoke(manager, [room, false])!;
    }

    private static bool ValidEncounter(JigsawEventCombatPoint point) => point.Combat.ParentEventId == ModelDb.Event<DenseVegetation>().Id
        ? !point.Combat.ShouldResumeParentEvent && point.Combat.EncounterId == ModelDb.Encounter<DenseVegetationEventEncounter>().Id
        : point.Combat.ParentEventId == ModelDb.Event<Colosseum>().Id && (point.Combat.ShouldResumeParentEvent
        ? point.Combat.EncounterId == ModelDb.Encounter<ColosseumFirstEncounter>().Id
        : point.Combat.EncounterId == ModelDb.Encounter<DecimillipedeElite>().Id ||
          point.Combat.EncounterId == ModelDb.Encounter<EntomancerElite>().Id ||
          point.Combat.EncounterId == ModelDb.Encounter<InfestedPrismsElite>().Id);

    private static JigsawEventCombatRng Capture(Rng rng) => new()
    {
#if STS2_PUBLIC
        Seed = rng.Seed, Counter = rng.Counter
#else
        State = rng.ToSerializable()
#endif
    };
    private static Rng Restore(JigsawEventCombatRng rng) =>
#if STS2_PUBLIC
        new(rng.Seed, rng.Counter);
#else
        new(rng.State);
#endif
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
internal static class JigsawCaptureEmbeddedCombat
{
    private static bool Prefix(RunManager __instance, AbstractRoom room, bool fadeToBlack, ref Task __result)
    {
        if (!JigsawEventCombatCheckpoint.CanCapture(room, out var session))
            return true;
        __result = JigsawEventCombatCheckpoint.Enter(__instance, (CombatRoom)room, fadeToBlack, session!);
        return false;
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Task NativeEnter(RunManager instance, AbstractRoom room, bool fadeToBlack) =>
        throw new NotSupportedException("Harmony must bind the native embedded-room entry.");
}
