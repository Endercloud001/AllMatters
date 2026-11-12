using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;

internal static class JigsawFlightCompatibilityTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw compatibility Flight rejects activation without a partial session and preserves other modes", NewRuns),
      ("Jigsaw compatibility Flight rejects marked save and real lobby validation without rewriting files", Restore) ];

    private static Task NewRuns()
    {
        bool previous = JigsawPreferences.Enabled;
        try
        {
            foreach (bool enabled in new[] { false, true })
            foreach (var mode in new[] { GameMode.Standard, GameMode.Custom, GameMode.Daily })
            {
                JigsawPreferences.Enabled = enabled;
                var flight = ModelDb.Modifier<Flight>().ToMutable();
                var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
                var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()],
                    modifiers: [flight], gameMode: mode, seed: "FLIGHT_COMPATIBILITY_88");
                try
                {
                    bool rejected = false;
                    try
                    {
                        RunManager.Instance.SetUpNewSingleplayer(native, false,
                            mode == GameMode.Daily ? new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero) : null);
                    }
                    catch (InvalidOperationException error) when (error.Message.Contains("Flight", StringComparison.Ordinal))
                    { rejected = true; }
                    NativeEventTests.Check(rejected == (enabled && mode == GameMode.Standard),
                        "Only an actual attempt to activate jigsaw with Flight is rejected; ordinary Custom/Daily setup stays available.");
                    NativeEventTests.Check(JigsawNativeSession.TryGet(native) == null && native.Modifiers.Count == 1 &&
                        ReferenceEquals(native.Modifiers[0], flight) && flight.ShouldAllowFreeTravel(),
                        "Rejected activation cannot leave a partial session or remove/disable the native Flight modifier.");

                    rejected = false;
                    try { JigsawNativeSession.Enable(native); }
                    catch (InvalidOperationException error) when (error.Message.Contains("Flight", StringComparison.Ordinal))
                    { rejected = true; }
                    NativeEventTests.Check(rejected && JigsawNativeSession.TryGet(native) == null,
                        "Direct activation also rejects Flight before creating any session, regardless of preferences or game mode.");
                }
                finally { RunManager.Instance.CleanUp(false); }
            }
        }
        finally { JigsawPreferences.Enabled = previous; }
        return Task.CompletedTask;
    }

    private static async Task Restore()
    {
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()],
            modifiers: [ModelDb.Modifier<Flight>().ToMutable()], seed: "FLIGHT_RESTORE_88");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        var io = new MockGodotFileIo("flight-compatibility-88");
        var saves = new RunSaveManager(88, io, new MigrationManager(io), forceSynchronous: true);
        try
        {
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var ordinary = RunManager.Instance.ToSave(null);
            await saves.SaveRun(ordinary, true);
            var ordinaryLobby = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            NativeEventTests.Check(ordinaryLobby.Success && ordinaryLobby.SaveData != null,
                "An ordinary Flight document remains loadable through the real native lobby validator.");
            var restored = RunState.FromSerializable(ordinaryLobby.SaveData!);
            NativeEventTests.Check(JigsawNativeSession.TryGet(restored) == null && restored.Modifiers.OfType<Flight>().Single().ShouldAllowFreeTravel(),
                "An ordinary Flight save stays ordinary and retains native free travel.");

            JigsawCheckpoint.Capture(native, JigsawRun.Create(88));
            var marked = RunManager.Instance.ToSave(null);
            marked.Modifiers.Add(ModelDb.Modifier<AllMattersJigsawMode>().ToMutable().ToSerializable());
            JigsawSaveSource.CaptureDocument(marked, native);
            await saves.SaveRun(marked, true);
            string path = RunSaveManager.GetRunSavePath(88, "current_run_mp.save");
            string original = io.ReadFile(path);
            string originalDocument = System.Text.Json.JsonSerializer.Serialize(marked);
            var rejectedLobby = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            NativeEventTests.Check(!rejectedLobby.Success && io.ReadFile(path) == original,
                "Lobby validation rejects the Flight/jigsaw combination before its validating early return and never rewrites the source file.");

            bool rejected = false;
            try { RunState.FromSerializable(marked); }
            catch (InvalidDataException error) when (error.Message.Contains("Flight", StringComparison.Ordinal))
            { rejected = true; }
            NativeEventTests.Check(rejected && System.Text.Json.JsonSerializer.Serialize(marked) == originalDocument &&
                io.ReadFile(path) == original && JigsawNativeSession.TryGet(native) == null,
                "Direct restoration reports unsupported Flight explicitly, restores the original modifier list and leaves input/file/session untouched.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }
}
