using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

internal static class JigsawMapDrawingSaveTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw map drawing save retains new annotations over an old gameplay checkpoint", Checkpoint),
        ("Jigsaw map drawing save recovery preserves same act and clears persist after a frame", Recovery),
        ("Jigsaw map drawing save and exit captures the latest stroke without entering a room", ExitSave)
    ];

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);

    private static bool ExitToTestMenu(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    private static Task ExitSave() => JigsawHuntingTreasureTests.WithTreasure(true, async (session, _) =>
    {
        using var host = new DrawingHost();
        host.AddLine(false, new(32, 48));
        var menu = new MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu.NPauseMenu();
        var exit = AccessTools.Method(menu.GetType(), "CloseToMenu");
        var harmony = new Harmony("AllMatters.Tests.DrawingExit");
        var previous = JigsawNativeSaveHost.Save;
        int? lines = null;
        try
        {
            harmony.Patch(exit, prefix: new HarmonyMethod(typeof(JigsawMapDrawingSaveTests), nameof(ExitToTestMenu)) { priority = Priority.Last });
            JigsawNativeSaveHost.Save = room =>
            {
                lines = RunManager.Instance.ToSave(room).MapDrawings!.drawings.Single().lines.Count;
                return Task.CompletedTask;
            };
            await ((Task)exit.Invoke(menu, null)!).WaitAsync(TimeSpan.FromSeconds(10));
            Check(lines == 1, "The native exit route persists a stroke made after the last room checkpoint.");
        }
        finally
        {
            JigsawNativeSaveHost.Save = previous;
            harmony.UnpatchAll(harmony.Id);
            menu.Free();
        }
    }, shouldSave: true);

    private static Task Checkpoint() => JigsawHuntingTreasureTests.WithTreasure(true, (session, _) =>
    {
        using var host = new DrawingHost();
        var old = RunManager.Instance.ToSave(null);
        AccessTools.Property(typeof(JigsawNativeSession), nameof(JigsawNativeSession.BeforeAncient)).SetValue(session, old);
        host.AddLine(false, new(32, 48));
        host.AddLine(true, new(40, 56));
        var saved = RunManager.Instance.ToSave(null);
        var copied = JigsawSaveSource.Copy(saved);
        var lines = copied.MapDrawings!.drawings.Single().lines;
        Check(lines.Count == 2 && !lines[0].isEraser && lines[1].isEraser,
            "The actual ToSave checkpoint bypass retains newer drawing and eraser strokes in native order after JSON copy.");
        Check(old.MapDrawings!.drawings.Single().lines.Count == 0,
            "Capturing current annotations does not mutate the retained before-room point.");
        Check(lines[0].mapPoints.Count == 2 && lines[0].mapPoints[0] != lines[1].mapPoints[0],
            "Native point coordinates survive the production save serializer.");
        return Task.CompletedTask;
    });

    private static Task Recovery() => JigsawHuntingTreasureTests.WithTreasure(true, async (session, _) =>
    {
        using var host = new DrawingHost();
        var checkpoint = RunManager.Instance.ToSave(null);
        host.AddLine(false, new(80, 100));
        JigsawMapDrawingSave.PreserveForRecovery(checkpoint, session.Native);
        Check(checkpoint.MapDrawings!.drawings.Single().lines.Count == 1,
            "Recovery receives the current same-act annotations before scene teardown.");
        var otherAct = JigsawSaveSource.Copy(checkpoint);
        otherAct.CurrentActIndex++;
        host.AddLine(true, new(90, 120));
        JigsawMapDrawingSave.PreserveForRecovery(otherAct, session.Native);
        Check(otherAct.MapDrawings!.drawings.Single().lines.Count == 1,
            "A different act cannot inherit annotations from the old canvas.");
        host.Drawings.ClearAllLines();
        Check(RunManager.Instance.ToSave(null).MapDrawings!.drawings.Single().lines.Count == 0,
            "A clear immediately followed by saving cannot resurrect deferred native line nodes.");
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var cleared = RunManager.Instance.ToSave(null);
        Check(JigsawSaveSource.Copy(cleared).MapDrawings!.drawings.Single().lines.Count == 0,
            "Native deferred line deletion is empty in the subsequent saved and copied point.");
    });

    // The absent rendered scene is replaced with native objects; serialization and clearing stay native.
    private sealed class DrawingHost : IDisposable
    {
        private readonly NGame? previous = NGame.Instance;
        private readonly NGame game = new();
        private readonly NSceneContainer scenes = new();
        private readonly NRun run = new();
        private readonly NGlobalUi global = new();
        private readonly NMapScreen map = new();
        private readonly SubViewport viewport = new();
        private readonly TextureRect texture = new();
        private readonly ShaderMaterial eraser = new();
        internal NMapDrawings Drawings { get; } = new() { Size = new(960, 1200) };

        internal DrawingHost()
        {
            AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, game);
            AccessTools.Property(typeof(NGame), nameof(NGame.RootSceneContainer)).SetValue(game, scenes);
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(scenes, run);
            AccessTools.Property(typeof(NRun), nameof(NRun.GlobalUi)).SetValue(run, global);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(global, map);
            AccessTools.Property(typeof(NMapScreen), nameof(NMapScreen.Drawings)).SetValue(map, Drawings);
            AccessTools.Field(typeof(NMapDrawings), "_eraserMaterial").SetValue(Drawings, eraser);
            var stateType = typeof(NMapDrawings).GetNestedType("DrawingState", System.Reflection.BindingFlags.NonPublic)!;
            object state = Activator.CreateInstance(stateType, nonPublic: true)!;
            AccessTools.Field(stateType, "playerId").SetValue(state, 1UL);
            AccessTools.Field(stateType, "drawViewport").SetValue(state, viewport);
            AccessTools.Field(stateType, "drawingTexture").SetValue(state, texture);
            ((System.Collections.IList)AccessTools.Field(typeof(NMapDrawings), "_drawingStates").GetValue(Drawings)!).Add(state);
        }

        internal void AddLine(bool erase, Vector2 point)
        {
            var line = new Line2D { Material = erase ? eraser : null };
            line.AddPoint(point); line.AddPoint(point + Vector2.One);
            viewport.AddChild(line);
        }

        public void Dispose()
        {
            AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, previous);
            viewport.Free(); texture.Free(); Drawings.Free(); map.Free(); global.Free(); run.Free(); scenes.Free(); game.Free(); eraser.Dispose();
        }
    }
}
