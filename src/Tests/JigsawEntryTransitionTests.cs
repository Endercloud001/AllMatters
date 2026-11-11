using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawEntryTransitionTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw entry transition waits before native save and enters once", () => Run("ordinary")),
     ("Jigsaw entry transition external room fade takes ownership without stale native entry", () => Run("stolen")),
     ("Jigsaw entry transition a closed session cannot enter or reveal a replacement", () => Run("closed")),
     ("Jigsaw entry transition invalidated attempt never changes native state", () => Run("attempt")),
     ("Jigsaw entry transition leaves an already active native fade untouched", () => Run("busy")),
     ("Jigsaw entry transition failed native entry restores map and engine input", () => Run("failure")),
     ("Jigsaw entry transition native ENet slow endpoint waits for shared readiness", () => Network("slow")),
     ("Jigsaw entry transition native ENet fade failure releases every endpoint", () => Network("failure")),
     ("Jigsaw entry transition native ENet early failure reaches endpoints still fading", () => Network("earlyfailure")),
#if STS2_PUBLIC
     ("Jigsaw entry transition public defers only the native ordinary reveal callsite", () => Run("public")),
     ("Jigsaw entry transition public keeps native participation after a committed visual takeover", () => Network("takeover")),
#endif
    ];
    private static async Task Network(string mode)
    {
        string? previous = System.Environment.GetEnvironmentVariable("JIGSAW_ENTRY_TRANSITION");
        try { System.Environment.SetEnvironmentVariable("JIGSAW_ENTRY_TRANSITION", mode); await JigsawNetworkTests.Run(); }
        finally { System.Environment.SetEnvironmentVariable("JIGSAW_ENTRY_TRANSITION", previous); }
    }
    private static TaskCompletionSource<bool>? fade;
    private static int outs, ins, nativeIns, nativeSaves;
    private static void ObserveSave() => nativeSaves++;
    private static void ObserveNativeIn() => nativeIns++;
    private static bool HoldOut(ref Task __result)
    { outs++; __result = fade!.Task; return false; }
    private static bool ObserveIn(ref Task __result)
    { ins++; __result = Task.CompletedTask; return false; }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private static async Task Run(string mode)
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        HeadlessUiHost.EnsureInputActions();
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "ENTRY_TRANSITION");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        var io = new MockGodotFileIo("entry-transition-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), false);
        var harmony = new Harmony("AllMatters.Tests.EntryTransition");
        using var host = new JigsawNativeMapUiHost();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size; tree.Root.Size = new(1280, 720);
        var transition = new NTransition();
        transition.AddChild(new Control { Name = "GradientTransition" });
        transition.AddChild(new Control { Name = "SimpleTransition" });
        try
        {
            native.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed)).First(run => run.Pool[0].Nodes[0].Room == JigsawRoom.Shop);
            Check(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success &&
                map.ChooseEnd(1, EndChoice.Manual).Success && map.Inspect().Success && map.CompleteAncient(1).Success && map.Continue(1).Success, "Prepare a validated ordinary shop route.");
            JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            var view = await host.Open(session);
            AccessTools.Property(typeof(NGame), nameof(NGame.Transition)).SetValue(NGame.Instance, transition);
            // Native TestMode omits NRun's room UI. Keep the real map mounted while
            // letting native room callbacks use their existing headless path.
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(NGame.Instance!.RootSceneContainer, null);
            await HeadlessUiHost.Mount(transition);
            if (mode == "busy") await transition.RoomFadeOut(); // Real native TestMode sets InTransition.
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeOut)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionTests), nameof(HoldOut)));
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeIn)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionTests), nameof(ObserveIn)));
            harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.ToSave)), postfix: new HarmonyMethod(typeof(JigsawEntryTransitionTests), nameof(ObserveSave)));
            if (mode == "public")
                harmony.Patch(AccessTools.Method(typeof(RunManager), "FadeIn", [typeof(bool)]), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionTests), nameof(ObserveNativeIn)));
            fade = new(TaskCreationOptions.RunContinuationsAsynchronously); outs = ins = nativeIns = nativeSaves = 0;
            int visited = native.VisitedMapCoords.Count;
            if (mode == "busy")
            {
                bool rejected = false;
                try { await session.Enter(1); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && transition.InTransition && outs == 0 && ins == 0 && native.VisitedMapCoords.Count == visited && view.CanProcess(),
                    "An already active native fade must keep its ownership and input state without another native call or reveal.");
                return;
            }
            var point = view.MapControl.GlobalPosition + view.GridToCanvas(session.Map!.Nodes.Single(n => n.Id == 1).Center);
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
            RunManager.Instance.ActionExecutor.Unpause();
            for (int i = 0; i < 6; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(outs == 1, $"Engine input on the actual map must wait for fade-out before entering (fades={outs}, visited={native.VisitedMapCoords.Count}, before={visited}, room={native.CurrentRoom?.GetType().Name}).");
            var entering = session.Enter(1);
            Check(outs == 1 && !entering.IsCompleted && nativeSaves == 0 && native.VisitedMapCoords.Count == visited && session.Map!.Steps == 0,
                "Ordinary production entry must await room fade-out before native save/projection/entry.");
            Check(ReferenceEquals(session.Enter(1), entering), "Repeated entry during the fade shares the one native operation.");
            if (mode is "closed" or "attempt")
            {
                if (mode == "closed")
                {
                    RunManager.Instance.CleanUp(false);
                    var replacement = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)], acts: [ModelDb.Act<Overgrowth>()], seed: "REPLACEMENT");
                    RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                }
                else session.Map!.AbandonEntry(session.Map.PendingEntry!.Id);
                fade.SetResult(true);
                bool cancelled = false;
                try { await entering; } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && native.VisitedMapCoords.Count == visited && ins == (mode == "closed" ? 0 : 1),
                    "Stale fade completion must not enter native state or reveal over a replacement session.");
                return;
            }
            if (mode == "stolen")
            {
                await transition.RoomFadeIn();
                for (int i = 0; i < 6; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(entering.IsCompleted, "An external call on the shared native transition must cancel the old entry wait, even when its tween completion never arrives.");
                bool cancelled = false;
                try { await entering; } catch (OperationCanceledException) { cancelled = true; }
                fade.SetResult(true);
                Check(cancelled && native.VisitedMapCoords.Count == visited && ins == 1,
                    "The displaced entry must propagate cancellation, never enter, and never reveal over the new owner.");
                return;
            }
            if (mode == "failure")
            {
                JigsawEntryFailureHost.Early = true;
                fade.SetResult(true);
                bool failed = false;
                try { await entering; } catch (IOException) { failed = true; }
                Check(failed && view.Visible && view.IsProcessingInput() && view.CanProcess() && session.Map!.Steps == 0 && ins == 1,
                    "Native entry failure must reveal the restored uncharged map with input enabled.");
                var retry = view.DecisionControl.GetChildren().OfType<Button>().Single(button => button.Text == "重试进入");
                Check(retry.IsVisibleInTree() && !retry.Disabled, "The failed entry exposes the production retry button.");
                view.SwitchZone(1); view.SwitchZone(1); retry.GrabFocus();
                Input.ParseInputEvent(new InputEventAction { Action = MegaCrit.Sts2.Core.ControllerInput.MegaInput.select, Pressed = true });
                for (int i = 0; i < 30 && JigsawNativeSession.Current == session; i++)
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(JigsawNativeSession.Current != session, "Engine confirmation on the restored retry button starts actual whole-checkpoint recovery.");
                return;
            }
            fade.SetResult(true); await entering;
            Check(native.CurrentRoom is MerchantRoom && session.Map!.Steps == 1 && nativeSaves > 0 && native.VisitedMapCoords.Count == visited + 1 && outs == 1 && ins == 1,
                "The real native room and RoomEntered confirmation enter once and reveal once.");
            Check(!view.Visible, "Successful readiness hides the production map.");
            if (mode == "public")
            {
                Check(nativeIns == 0, "Public ordinary native entry must defer its private FadeIn call until the outer owner reveals.");
                await (Task)AccessTools.Method(typeof(RunManager), "FadeIn", [typeof(bool)]).Invoke(RunManager.Instance, [true])!;
                Check(nativeIns == 1, "The same private FadeIn outside this ordinary entry must still execute unchanged.");
            }
        }
        finally
        {
            fade?.TrySetResult(true); JigsawEntryFailureHost.Reset(); harmony.UnpatchAll(harmony.Id); transition.Free(); tree.Root.Size = originalSize; RunManager.Instance.CleanUp(false);
        }
    }
}
