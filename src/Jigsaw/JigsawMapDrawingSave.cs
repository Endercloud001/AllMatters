using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AllMatters.Jigsaw;

// Room checkpoints roll gameplay back, but annotations belong to the current act.
internal static class JigsawMapDrawingSave
{
    internal static NMapDrawings? CurrentDrawings()
    {
        var drawings = NRun.Instance?.GlobalUi?.MapScreen?.Drawings;
        return GodotObject.IsInstanceValid(drawings) ? drawings : null;
    }

    internal static void Capture(SerializableRun save, NMapDrawings drawings) =>
        save.MapDrawings = drawings.GetSerializableMapDrawings();

    // Called before recovery tears down the old NRun. A new act retains native clearing.
    internal static void PreserveForRecovery(SerializableRun checkpoint, RunState restored)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null } session ||
            checkpoint.CurrentActIndex != session.Native.CurrentActIndex ||
            restored.CurrentActIndex != session.Native.CurrentActIndex)
            return;
        if (CurrentDrawings() is { } drawings)
            Capture(checkpoint, drawings);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class JigsawCaptureMapDrawings
{
    // Must run after JigsawCaptureNativePoint selects a gameplay checkpoint.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SerializableRun __result)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null } session ||
            __result.CurrentActIndex != session.Native.CurrentActIndex)
            return;
        if (JigsawMapDrawingSave.CurrentDrawings() is { } drawings)
            JigsawMapDrawingSave.Capture(__result, drawings);
    }
}

[HarmonyPatch(typeof(NMapDrawings), "ClearAllLinesForPlayer")]
internal static class JigsawDetachClearedDrawingLines
{
    private static void Postfix(NMapDrawings __instance, object state)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null })
            return;
        Detach(state);
    }

    internal static void Detach(object state)
    {
        var viewport = (SubViewport)AccessTools.Field(state.GetType(), "drawViewport").GetValue(state)!;
        // Native QueueFree is deferred. A save in this frame must see the clear.
        foreach (var line in viewport.GetChildren().OfType<Line2D>())
            viewport.RemoveChild(line);
    }
}

[HarmonyPatch(typeof(NMapDrawings), nameof(NMapDrawings.ClearAllLines))]
internal static class JigsawDetachAllClearedDrawingLines
{
    private static void Postfix(NMapDrawings __instance)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null })
            return;
        foreach (var state in (System.Collections.IEnumerable)AccessTools.Field(typeof(NMapDrawings), "_drawingStates").GetValue(__instance)!)
            JigsawDetachClearedDrawingLines.Detach(state);
    }
}
