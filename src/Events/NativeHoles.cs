using HarmonyLib;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.RunData;

namespace AllMatters.Events;

[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class HolesSharedPage
{
    public sealed class PageState
    {
        public int Floor { get; set; } = -1;
        public bool Shared { get; set; }
        public bool AcceptedFight { get; set; }
        public int? FightRoot { get; set; }
        public long FightEntry { get; set; }
        public Dictionary<ulong, FightRng> FightOwners { get; set; } = [];
    }
    public sealed class FightRng
    {
        public int Counter { get; set; }
#if STS2_PUBLIC
        public uint Seed { get; set; }
        internal static FightRng Capture(Rng rng) => new() { Seed = rng.Seed, Counter = rng.Counter };
        internal Rng Restore() => new(Seed, Counter);
#else
        // Keep this event's saved state explicit instead of nesting native field-only SerializableRng.
        public ulong State0 { get; set; }
        public ulong State1 { get; set; }
        public ulong State2 { get; set; }
        public ulong State3 { get; set; }
        internal static FightRng Capture(Rng rng)
        {
            var state = rng.ToSerializable();
            return new()
            {
                Counter = state.counter,
                State0 = state.state0,
                State1 = state.state1,
                State2 = state.state2,
                State3 = state.state3
            };
        }
        internal Rng Restore() => new(new SerializableRng
        { counter = Counter, state0 = State0, state1 = State1, state2 = State2, state3 = State3 });
#endif
    }
    private static readonly RunSavedData<PageState> Sharing = RunSavedDataStore.For(ModEntry.ModId).Register<PageState>("holesPage");
    internal static void Initialize() => _ = Sharing;
    private static void Postfix(EventModel __instance, ref bool __result)
    {
        if (__instance is not FieldOfManSizedHoles || __instance.Owner == null)
            return;
        var run = (RunState)__instance.Owner.RunState;
        int floor = run.MapPointHistory.Sum(points => points.Count);
        var page = Sharing.Get(run);
        if (page.Floor != floor)
        {
            // This is queried before EventModel initializes its owner-dependent RNG.
            // A separate native RNG gives every peer the same floor roll without
            // consuming another event's or player's random stream.
            var rng = new Rng(run.Rng.Seed);
            for (int i = 0; i < floor; i++)
                rng.NextInt(100);
            bool shared = rng.NextInt(100) < 33;
            page = Sharing.Modify(run, state => { state.Floor = floor; state.Shared = shared; ClearFight(state); });
        }
        __result = page.Shared;
    }

    internal static void RecordAcceptedFight(JigsawNativeSession session, CombatRoom combat)
    {
        if (!IsCurrentFight(session, combat))
            return;
        SaveOwners(session, accepted: true);
    }

    internal static void CaptureFinishedFight(RunState run)
    {
        if (JigsawNativeSession.TryGet(run) is not { } session || run.CurrentRoom is not CombatRoom { IsPreFinished: true } combat ||
            !IsCurrentFight(session, combat) || !MatchesAcceptedFight(session, Sharing.Get(run)))
            return;
        SaveOwners(session, accepted: false);
    }

    private static bool IsCurrentFight(JigsawNativeSession session, CombatRoom combat) =>
        ReferenceEquals(JigsawNativeSession.Current, session) && !session.ClosedToken.IsCancellationRequested &&
        session.Map is { CurrentRoomHunted: true, CurrentNode: not null } &&
        session.Native.BaseRoom is EventRoom { CanonicalEvent: FieldOfManSizedHoles } parent &&
        combat is { Encounter: Encounters.HolesEncounter, ShouldResumeParentEventAfterCombat: true } &&
        combat.ParentEventId == parent.CanonicalEvent.Id;

    private static void SaveOwners(JigsawNativeSession session, bool accepted)
    {
        var run = session.Native;
        var models = RunManager.Instance.EventSynchronizer.Events;
        if (models.Any(model => model is not FieldOfManSizedHoles || model.Owner == null || !ReferenceEquals(model.Owner.RunState, run)) ||
            !models.Select(model => model.Owner!.NetId).ToHashSet().SetEquals(run.Players.Select(player => player.NetId)))
            throw new InvalidDataException("洞穴战斗的父事件所有者不完整。");
        var owners = models.ToDictionary(model => model.Owner!.NetId, model => FightRng.Capture(model.Rng));
        Sharing.Modify(run, page =>
        {
            if (accepted)
            {
                page.Floor = run.MapPointHistory.Sum(points => points.Count);
                page.AcceptedFight = true;
                page.FightRoot = session.Map!.CurrentNode;
                // NextEntry is the last issued BeginEnter attempt, retained after ConfirmEnter.
                page.FightEntry = session.Map!.CreateSave().NextEntry;
            }
            page.FightOwners = owners;
        });
    }

    private static bool MatchesAcceptedFight(JigsawNativeSession session, PageState page) =>
        page.AcceptedFight && page.Floor == session.Native.MapPointHistory.Sum(points => points.Count) &&
        session.Map is { CurrentRoomHunted: true, CurrentNode: not null } map &&
        map.CurrentNode == page.FightRoot && map.CreateSave().NextEntry == page.FightEntry &&
        session.Native.CurrentMapPointHistoryEntry?.Rooms.LastOrDefault()?.ModelId == ModelDb.Encounter<Encounters.HolesEncounter>().Id;

    internal static async Task RestoreAfterBegin(Task original, FieldOfManSizedHoles model)
    {
        var run = model.Owner?.RunState as RunState;
        var session = run == null ? null : JigsawNativeSession.TryGet(run);
        var map = session?.Map;
        var parent = run?.CurrentRoom;
        int? root = map?.CurrentNode;
        long? entry = map?.CreateSave().NextEntry;
        await original;
        if (run == null || session == null || map == null ||
            !ReferenceEquals(JigsawNativeSession.Current, session) || session.ClosedToken.IsCancellationRequested ||
            !ReferenceEquals(session.Native, run) ||
            !ReferenceEquals(session.Map, map) || !ReferenceEquals(run.CurrentRoom, parent) ||
            parent is not EventRoom { CanonicalEvent: FieldOfManSizedHoles } ||
            map.CurrentNode != root || map.CreateSave().NextEntry != entry ||
            !RunManager.Instance.EventSynchronizer.Events.Any(current => ReferenceEquals(current, model)))
            return;
        var page = Sharing.Get(run);
        if (!MatchesAcceptedFight(session, page))
            return;
        if (!page.FightOwners.Keys.ToHashSet().SetEquals(run.Players.Select(player => player.NetId)) ||
            !page.FightOwners.TryGetValue(model.Owner!.NetId, out var rng))
            throw new InvalidDataException("洞穴奖励点缺少父事件所有者随机状态。");
        HolesFightReturn.RestoreCombatPhase(model);
        AccessTools.Property(typeof(EventModel), nameof(EventModel.Rng)).SetValue(model, rng.Restore());
    }

    internal static void FinishAcceptedFight(EventModel model, CombatRoom combat)
    {
        if (model.Owner?.RunState is RunState run && JigsawNativeSession.TryGet(run) is { } session &&
            model.IsFinished && combat.IsPreFinished && combat.ShouldResumeParentEventAfterCombat &&
            combat.ParentEventId == model.Id && run.CurrentRoom is EventRoom { CanonicalEvent: FieldOfManSizedHoles } &&
            ReferenceEquals(JigsawNativeSession.Current, session) && !session.ClosedToken.IsCancellationRequested &&
            RunManager.Instance.EventSynchronizer.Events.Any(current => ReferenceEquals(current, model)) &&
            MatchesAcceptedFight(session, Sharing.Get(run)))
            Sharing.Modify(run, ClearFight);
    }

    private static void ClearFight(PageState page)
    {
        page.AcceptedFight = false;
        page.FightRoot = null;
        page.FightEntry = 0;
        page.FightOwners = [];
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.BeginEvent))]
internal static class HolesFightParentReload
{
    private static void Postfix(EventModel __instance, ref Task __result)
    {
        if (__instance is FieldOfManSizedHoles holes)
            __result = HolesSharedPage.RestoreAfterBegin(__result, holes);
    }
}

[HarmonyPatch(typeof(FieldOfManSizedHoles), "GenerateInitialOptions")]
internal static class HolesFightOption
{
    private static void Postfix(FieldOfManSizedHoles __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (!__instance.IsShared)
            return;
        var model = __instance;
        __result = __result.Append(NativeEventActions.Option(model, "FIELD_OF_MAN_SIZED_HOLES.pages.INITIAL.options.AM_FIGHT", () =>
        {
            EncounterModel encounter = ModelDb.Encounter<Encounters.HolesEncounter>();
#if STS2_PUBLIC
            encounter = encounter.ToMutable();
#endif
            NativeEventActions.EnterCombat(model, encounter, [], true);
            return Task.CompletedTask;
        })).ToArray();
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class HolesFightReturn
{
    internal static void RestoreCombatPhase(FieldOfManSizedHoles model) =>
        // An empty native page is finished immediately. Keep the accepted fight
        // visible but locked until the real child combat resumes this parent.
        NativeEventActions.State(model, "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description",
            [new EventOption(model, null, "FIELD_OF_MAN_SIZED_HOLES.pages.INITIAL.options.AM_FIGHT")]);

    private static void Postfix(EventModel __instance, AbstractRoom exitedRoom)
    {
        if (__instance is FieldOfManSizedHoles && exitedRoom is CombatRoom
            { Encounter: Encounters.HolesEncounter, ParentEventId: var parent } combat && parent == __instance.Id)
        {
            NativeEventActions.Finish(__instance, "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description");
            HolesSharedPage.FinishAcceptedFight(__instance, combat);
        }
    }
}
