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
      ("Jigsaw compatibility Flight rejects marked save and real lobby validation without rewriting files", Restore),
      ("Jigsaw compatibility native daily sampling retains Flight before during and after jigsaw", DailyCandidates) ];

    private static Task DailyCandidates()
    {
        bool previous = JigsawPreferences.Enabled;
        string[]? reference = null;
        bool sampledFlight = false;
        var originalCandidates = ModelDb.GoodModifiers.Select(modifier => modifier.Id).ToArray();
        try
        {
            foreach (bool enabled in new[] { false, true, false })
            {
                JigsawPreferences.Enabled = enabled;
                var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
                var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "DAILY_COMPATIBILITY_88");
                RunManager.Instance.SetUpNewSingleplayer(native, false);
#if STS2_PUBLIC
                var screen = new MegaCrit.Sts2.Core.Nodes.Screens.DailyRun.NDailyRunScreen();
                var lobby = new MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby(GameMode.Daily,
                    new NetSingleplayerGameService(), screen, new MegaCrit.Sts2.Core.Daily.TimeServerResult
                    {
                        serverTime = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                        localReceivedTime = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero)
                    }, 4);
                HarmonyLib.AccessTools.Field(screen.GetType(), "_lobby").SetValue(screen, lobby);
#endif
                try
                {
                    NativeEventTests.Check((JigsawNativeSession.TryGet(native) != null) == enabled,
                        "The daily sampler is observed before, during and after a real jigsaw activation.");
                    var samples = new List<string>();
                    for (uint seed = 0; seed < 64; seed++)
                    {
                        var rng = new MegaCrit.Sts2.Core.Random.Rng(seed);
#if STS2_PUBLIC
                        var modifiers = (IReadOnlyCollection<ModifierModel>)HarmonyLib.AccessTools.Method(screen.GetType(), "RollModifiers").Invoke(screen, [rng])!;
#else
                        var modifiers = ModifierModel.Pick2Good1Bad(rng, [ModelDb.Character<Ironclad>()]);
#endif
                        sampledFlight |= modifiers.Any(modifier => modifier is Flight);
                        samples.Add(System.Text.Json.JsonSerializer.Serialize(modifiers.Select(modifier => modifier.ToSerializable()).ToArray(),
                            new System.Text.Json.JsonSerializerOptions { IncludeFields = true }) +
                            ":" + HeadlessTestSetup.RngPosition(rng));
                    }
                    if (reference == null) reference = samples.ToArray();
                    else NativeEventTests.Check(reference.SequenceEqual(samples),
                        "Jigsaw cannot change any sampled modifier, generated modifier property or native RNG position for the same daily input.");
                    NativeEventTests.Check(originalCandidates.SequenceEqual(ModelDb.GoodModifiers.Select(modifier => modifier.Id)) &&
                        ModelDb.GoodModifiers.Any(modifier => modifier is Flight),
                        "The original daily good-modifier candidates retain Flight and their order.");
                }
                finally
                {
#if STS2_PUBLIC
                    lobby.CleanUp(false);
                    lobby.InputSynchronizer.Dispose();
                    screen.Free();
#endif
                    RunManager.Instance.CleanUp(false);
                }
            }
            NativeEventTests.Check(sampledFlight, "The actual native daily samples include Flight; preservation is not vacuous.");
        }
        finally { JigsawPreferences.Enabled = previous; }
        return Task.CompletedTask;
    }

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
