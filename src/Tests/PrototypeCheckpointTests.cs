// THROWAWAY: native checkpoint experiment for the coop-stage prototype branch only.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using STS2RitsuLib.RunData;

internal static class PrototypeCheckpointTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("PrototypeCheckpoint native main and backup experiment", Experiment)];

    public sealed class State
    {
        public string Phase { get; set; } = "missing";
        public int Budget { get; set; } = -1;
        public bool Penalized { get; set; }
    }

    private sealed record Source(string Path, string Json);
    private static readonly ConditionalWeakTable<SerializableRun, Source> Sources = new();
    private static readonly Type Registry = typeof(RunSavedDataStore).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry", true)!;

    // Observe the actual native successful parse, not file existence or an independent JSON load.
    private static void Observe(string filePath, string content, ReadSaveResult<SerializableRun> __result)
    {
        if (__result.Success && __result.SaveData is { } save)
        {
            Sources.Remove(save);
            Sources.Add(save, new Source(filePath, content));
        }
        Console.WriteLine($"CHECKPOINT_PARSE path={filePath} status={__result.Status} success={__result.Success}");
    }

    // Minimal scoped adaptation: consume the path recorded by native's own successful parser result.
    private static async Task Experiment()
    {
        var harmony = new Harmony("AllMatters.Prototype68.Checkpoint");
        var parse = typeof(MigrationManager).GetMethod("LoadWithAggressiveRecovery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(SerializableRun));
        harmony.Patch(parse, postfix: new HarmonyMethod(typeof(PrototypeCheckpointTests), nameof(Observe)));
        Console.WriteLine($"CHECKPOINT_VERSION game={typeof(RunState).Assembly.FullName} ritsu={typeof(RunSavedDataStore).Assembly.FullName}");
        try
        {
            var slot = RunSavedDataStore.For("allmatters-prototype68-checkpoint").Register<State>("state");
            foreach (bool multiplayer in new[] { false, true })
            foreach (bool synchronous in new[] { false, true })
            foreach (bool adapted in new[] { false, true })
            foreach (string scenario in new[] { "normal", "missing-main", "native-invalid", "native-invalid-rename-fails", "write-fails" })
            {
                var io = new MockGodotFileIo("PROTOTYPE68-memory-wipe-me");
                var migration = new MigrationManager(io);
                var manager = new RunSaveManager(68, io, migration, forceSynchronous: synchronous);
                string path = RunSaveManager.GetRunSavePath(68, multiplayer ? "current_run_mp.save" : "current_run.save");
                var run = HeadlessTestSetup.PlayerWithRun("PROTOTYPE68_NATIVE_CHECKPOINT").RunState;
                RunManager.Instance.SetUpTest((RunState)run, new NetSingleplayerGameService());
                SerializableRun Capture(int gold, bool relic, string phase, int budget, bool penalized)
                {
                    run.Players[0].Gold = gold;
                    if (relic) run.Players[0].AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
                    slot.Set((RunState)run, new State { Phase = phase, Budget = budget, Penalized = penalized });
                    return RunManager.Instance.ToSave(null);
                }
                // Two deliberately distinct complete native+Mod snapshots.
                var first = Capture(111, false, "editing", 7, false);
                await manager.SaveRun(first, multiplayer);
                var second = Capture(222, true, "exploring", 0, true);
                // Mutate memory after ToSave: persisted B must retain the captured value.
                slot.Set((RunState)run, new State { Phase = "unsaved-memory", Budget = 99, Penalized = false });
                await manager.SaveRun(second, multiplayer);
                string savedMain = io.ReadFile(path)!;
                string savedBackup = io.ReadFile(path + ".backup")!;
                int savedEvents = 0;
                manager.Saved += () => savedEvents++;
                string latest = "B";
                string? writeError = null;
                if (scenario == "missing-main") io.DeleteFile(path);
                if (scenario.StartsWith("native-invalid"))
                {
                    var invalid = JsonNode.Parse(savedMain)!.AsObject();
                    invalid["schema_version"] = new JsonObject { ["PROTOTYPE"] = "native-type-error" };
                    io.SetFileContent(path, invalid.ToJsonString());
                    if (scenario.EndsWith("rename-fails"))
                        io.RenameFileAction = (_, _) => throw new IOException("PROTOTYPE cannot preserve corrupt primary");
                }
                if (scenario == "write-fails")
                {
                    var third = Capture(333, false, "unsaved-C", 3, false);
                    io.ShouldFailWrites = true;
                    try { await manager.SaveRun(third, multiplayer); latest = "C"; }
                    catch (Exception error) { writeError = error.GetType().Name + ": " + error.Message; }
                    finally { io.ShouldFailWrites = false; }
                    Require(writeError != null && latest == "B" && savedEvents == 0, "Failed save advanced checkpoint or signalled Saved");
                    Require(io.ReadFile(path) == savedMain && io.ReadFile(path + ".backup") == savedBackup, "Failed save changed stored bytes");
                }
                var loaded = multiplayer ? manager.LoadMultiplayerRunSave() : manager.LoadRunSave();
                Require(loaded.Success && loaded.SaveData != null, "Native load did not succeed");
                var source = Sources.GetValue(loaded.SaveData!, _ => throw new Exception("Native source not observed"));
                string ritsuCurrentPath = path;
                if (adapted)
                {
                    Registry.GetMethod("AttachDocumentFromJson")!.Invoke(null, [loaded.SaveData, source.Json]);
                }
                var restored = RunState.FromSerializable(loaded.SaveData!);
                bool hasMod = slot.TryGet(restored, out var state);
                bool backup = scenario is "missing-main" or "native-invalid" or "native-invalid-rename-fails";
                bool nativeMatches = restored.Players[0].Gold == (backup ? 111 : 222) &&
                    restored.Players[0].Relics.Any(relic => relic is RedMask) == !backup;
                bool modMatches = hasMod && state.Phase == (backup ? "editing" : "exploring") &&
                    state.Budget == (backup ? 7 : 0) && state.Penalized == !backup;
                Require(nativeMatches && source.Path.EndsWith(".backup") == backup, "Native chose unexpected checkpoint");
                Require(!adapted || modMatches, "Adapted recovery mixed/lost extension");
                Require(backup || modMatches, "Normal/native write path lost captured extension");
                Require(adapted || !backup || !modMatches, "Expected original backup counterexample was not reproduced");
                if (!adapted && scenario == "native-invalid-rename-fails")
                    Require(hasMod && state.Phase == "exploring" && state.Budget == 0 && state.Penalized,
                        "Expected primary Mod B / native backup A mismatch was not reproduced");
                var record = new { scenario, multiplayer, synchronous, adapted, nativeStatus = loaded.Status.ToString(),
                    source = source.Path, ritsuPath = ritsuCurrentPath, nativeGold = restored.Players[0].Gold,
                    nativeRedMask = restored.Players[0].Relics.Count(relic => relic is RedMask),
                    hasMod, state, nativeMatches, modMatches, primaryStillExists = io.FileExists(path),
                    writeError, savedEvents, latest,
                    calls = io.Calls.Select(call => new { method = call.Method, path = call.Args.FirstOrDefault()?.ToString() }).ToArray() };
                Console.WriteLine("CHECKPOINT_RESULT " + JsonSerializer.Serialize(record));
                RunManager.Instance.CleanUp(false);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            RunManager.Instance.CleanUp(false);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
