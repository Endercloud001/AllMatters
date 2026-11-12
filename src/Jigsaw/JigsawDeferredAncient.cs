using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;

namespace AllMatters.Jigsaw;

// A returning original owner settles their Ancient before the native loader
// continues its existing room restoration. The complete source point remains
// the save result until every active owner's real native option has finished.
internal sealed class JigsawDeferredAncient(JigsawNativeSession session, SerializableRun point)
{
    private readonly SerializableRun source = JigsawSaveSource.Copy(point);
    private AbstractRoom? originalRoom;
    private Task? entry;
    private Task? resume;
    private bool running;
    internal bool IsResuming { get; private set; }
    internal bool IsRunning => running && !IsResuming;

    internal Task Enter(AbstractRoom? preFinishedRoom)
    {
        if (entry != null)
            return entry;
        originalRoom = preFinishedRoom;
        session.BeginDeferredAncient(source);
        running = true;
        // A restored Ancient skips room-visit counters and external entry effects.
        // BeginEvent below opens only the owing owners' real native options.
        var room = new EventRoom(session.Native.Act.Ancient);
        room.MarkPreFinished();
        return entry = RunManager.Instance.EnterRoom(room);
    }

    internal void RestoreSaveRoom(SerializableRun save) => save.PreFinishedRoom = source.PreFinishedRoom;

    internal Task Resume()
    {
        if (resume != null)
            return resume;
        if (!session.AncientReady || session.BeforeAncient != null)
            return Task.CompletedTask;
        return resume = ResumeCore();
    }

    private async Task ResumeCore()
    {
        await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
        if (JigsawNativeSession.Current != session)
            return;
        IsResuming = true;
        try
        {
            await RunManager.Instance.LoadIntoLatestMapCoord(originalRoom);
            session.FinishDeferredAncient();
            session.OnEdited();
        }
        catch { IsResuming = false; resume = null; throw; }
    }

    internal void RecordHistory(AncientEventModel ancient)
    {
        var history = session.Native.MapPointHistory[session.Native.CurrentActIndex]
            .FirstOrDefault(entry => entry.MapPointType == MapPointType.Ancient)
            ?? throw new InvalidDataException("补领 Ancient 的原生历史缺失。");
        var options = (IEnumerable<MegaCrit.Sts2.Core.Events.EventOption>)AccessTools.Property(typeof(AncientEventModel), "GeneratedOptions").GetValue(ancient)!;
        foreach (var option in options)
            history.GetEntry(ancient.Owner!.NetId).AncientChoices.Add(new AncientChoiceHistoryEntry(option.Title, option.WasChosen));
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.BeginEvent))]
internal static class JigsawDeferredAncientOwner
{
    private static void Prefix(EventModel __instance, Player player, ref bool isPreFinished)
    {
        if (__instance is AncientEventModel && JigsawNativeSession.Current is { DeferredAncient.IsRunning: true, Map: { } map } session &&
            ReferenceEquals(player.RunState, session.Native) && __instance.Id == session.Native.Act.Ancient.Id)
            isPreFinished = map.CreateSave().AncientCompleted.Contains(player.NetId);
    }
}

[HarmonyPatch(typeof(AncientEventModel), "UpdateRunHistory")]
internal static class JigsawDeferredAncientHistory
{
    private static bool Prefix(AncientEventModel __instance)
    {
        if (JigsawNativeSession.Current?.DeferredAncient is not { IsRunning: true } deferred)
            return true;
        deferred.RecordHistory(__instance);
        return false;
    }
}
