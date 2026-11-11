using System.Text.Json;
using AllMatters;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class CombatCaptureTests
{
    internal static async Task Run()
    {
#if ALLMATTERS_PERF_CAPTURE
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        string output = AllMatters.Diagnostics.CombatPerfCapture.OutputDirectory;
        var previous = Directory.Exists(output) ? Directory.GetFiles(output).ToHashSet() : [];
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "CAPTURE_NATIVE_COMBAT");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var point = native.Map.GetAllMapPoints().First(point => point.PointType == MegaCrit.Sts2.Core.Map.MapPointType.Monster);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await RunManager.Instance.EnterMapCoord(point.coord);
            NativeEventTests.Check(native.CurrentRoom is CombatRoom, "The patched native entry must actually mount CombatRoom.");
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while ((!Directory.Exists(output) || !Directory.GetFiles(output).Any(file => !previous.Contains(file))) && timeout.Elapsed.TotalSeconds < 8)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await AllMatters.Diagnostics.CombatPerfCapture.PendingWrites;
            NativeEventTests.Check(timeout.Elapsed.TotalSeconds < 8, "The collector automatically closes and writes its window after entry without manual Flush.");
            string file = Directory.GetFiles(output).Single(file => !previous.Contains(file));
            using var report = JsonDocument.Parse(File.ReadAllText(file));
            var root = report.RootElement;
            NativeEventTests.Check(root.GetProperty("room").GetString() == "CombatRoom" && root.GetProperty("outcome").GetString() == "completed",
                "Capture reports the actual native room and original task outcome.");
            NativeEventTests.Check(root.GetProperty("frames").EnumerateArray().Any(frame => frame.GetProperty("FrameGapMs").GetDouble() > 0),
                "Real ProcessFrame callbacks must produce measured frame intervals.");
            NativeEventTests.Check(root.GetProperty("stages").EnumerateArray().Any(stage => stage.GetProperty("Name").GetString() == "entry-total"),
                "The actual EnterMapCoord patch must record its completion.");
            var cancelled = Task.FromCanceled(new CancellationToken(true));
            bool cancellationPreserved = false;
            try { await AllMatters.Diagnostics.CombatPerfCapture.ObserveStage(cancelled, System.Diagnostics.Stopwatch.GetTimestamp(), "cancel-test"); }
            catch (OperationCanceledException) { cancellationPreserved = true; }
            NativeEventTests.Check(cancellationPreserved, "The observer preserves original cancellation.");
            var originalError = new InvalidOperationException("original entry failure");
            bool failurePreserved = false;
            try { await AllMatters.Diagnostics.CombatPerfCapture.ObserveStage(Task.FromException(originalError), System.Diagnostics.Stopwatch.GetTimestamp(), "fault-test"); }
            catch (InvalidOperationException error) { failurePreserved = ReferenceEquals(error, originalError); }
            NativeEventTests.Check(failurePreserved, "The observer preserves the original exception identity.");
            string blockedDirectory = Path.Combine(output, "blocked-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blockedDirectory, "A file cannot be used as a capture directory.");
            await AllMatters.Diagnostics.CombatPerfCapture.WriteReport(new { test = true }, blockedDirectory);
            File.Delete(blockedDirectory);
            NativeEventTests.Check(native.CurrentRoom is CombatRoom, "A failed background capture write leaves the native room usable.");
        }
        finally { JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }
#else
        await Task.CompletedTask;
        NativeEventTests.Check(typeof(ModEntry).Assembly.GetType("AllMatters.Diagnostics.CombatPerfCapture") == null,
            "Normal builds do not contain capture types or Harmony observers.");
#endif
    }
}
