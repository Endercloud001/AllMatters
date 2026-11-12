using AllMatters.Jigsaw;
using AllMatters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Map;
using AllMatters.Encounters;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;

// Independent tests for the shared complete combat source. No hunting enemy
// implementation or new art is required to run this slice on integration.
internal static class JigsawCombatCheckpointTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("JigsawCombatCheckpoint active save restores the whole before roll point without appending history", () => BeforeBattle(false)),
        ("JigsawCombatCheckpoint genuine victory reloads post battle rewards without reopening combat", () => BeforeBattle(true)),
        ("JigsawCombatCheckpoint Colosseum second battle restores its own complete source and finite parent", () => EmbeddedBefore(true, false)),
        ("JigsawCombatCheckpoint Colosseum genuine second victory restores rewards and terminal parent", () => EmbeddedBefore(true, true)),
        ("JigsawCombatCheckpoint DenseVegetation preserves finished healing before battle and blocks repeated rest", () => EmbeddedBefore(false, false)),
        ("JigsawCombatCheckpoint DenseVegetation genuine victory cannot repeat completed rest", () => EmbeddedBefore(false, true)),
        ("JigsawCombatCheckpoint suspended event load cannot modify a replacement run", () => EmbeddedBefore(true, false, replaceDuringLoad: true)),
        ("JigsawCombatCheckpoint missing native owner RNG fields reject actual save restoration", () => EmbeddedBefore(true, false, malformedRng: true))
    ];

    private static async Task BeforeBattle(bool victory)
    {
        using var selector = CardSelectCmd.UseSelector(new TakeCardReward());
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var native = RunState.CreateForTest(
            [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_FOREST_ENTRY");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        manager.CombatReplayWriter.IsEnabled = false;
        var io = new MockGodotFileIo("combat-checkpoint-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        var previousSave = JigsawNativeSaveHost.Save;
        ulong? previousNetId = LocalContext.NetId;
        LocalContext.NetId = 1;
        JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var originalSize = viewport.Size;
        viewport.Size = new Vector2I(1920, 1080);
        var profileHost = new Harmony("AllMatters.Tests.CombatCheckpointProfile");
        profileHost.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
            prefix: new HarmonyMethod(typeof(JigsawCombatCheckpointTests), nameof(SkipProfile)));
        profileHost.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
            prefix: new HarmonyMethod(typeof(JigsawCombatCheckpointTests), nameof(SkipProfile)));
        profileHost.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)),
            prefix: new HarmonyMethod(typeof(JigsawCombatCheckpointTests), nameof(SkipProfile)));
        try
        {
            manager.GenerateRooms();
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var map = JigsawRun.Create(75, players: [1UL]);
            Ok(map.ChooseEnd(1, EndChoice.Automatic));
            Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
            Ok(map.CompleteAncient(1));
            Ok(map.Continue(1));
            var previous = new Dictionary<int, int> { [0] = -1 };
            var pending = new Queue<int>();
            pending.Enqueue(0);
            while (pending.TryDequeue(out int current))
                foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
                {
                    int next = edge.First == current ? edge.Second : edge.First;
                    if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) &&
                        previous.TryAdd(next, current)) pending.Enqueue(next);
                }
            int target = map.Nodes.First(node => node.Room == JigsawRoom.Combat && previous.ContainsKey(node.Id)).Id;
            var path = new List<int>();
            for (int node = target; node != 0; node = previous[node]) path.Add(node);
            path.Reverse();
            foreach (int node in path.SkipLast(1))
            {
                var entry = map.BeginEnter(node)!;
                Ok(map.ConfirmEnter(entry.Id));
                Ok(map.CompleteRoom(entry.Id));
            }
            int stepsBefore = map.Steps;
            Ok(map.SetRemainingSteps(0));
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            await session.Enter(target);
            Check(native.CurrentRoom is CombatRoom && session.Map!.Steps == stepsBefore + 1 && session.Map.CurrentRoomHunted,
                "The production native room readiness confirms one hunted logical entry.");
            var room = (CombatRoom)native.CurrentRoom!;
            Check(room.CombatState.Enemies.Count > 0, "The real native encounter creates its initial enemies.");
            var enemy = room.CombatState.Enemies[0];
            int historyRooms = native.CurrentMapPointHistoryEntry!.Rooms.Count;
            if (victory)
            {
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                    7, ValueProp.Unpowered, enemy);
                await PlayerCmd.GainGold(19, native.Players[0]);
                native.Rng.Niche.NextInt(100);
                var endCombat = AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes);
                await (Task)endCombat.Invoke(CombatManager.Instance, null)!;
                Check(room.IsPreFinished, "The actual native EndCombat flow marks the current room prefinished.");
                var saved = saves.LoadRunSave();
                Check(saved.Success && saved.SaveData?.PreFinishedRoom is { IsPreFinished: true },
                    "The real awaited victory flow saves its native reward checkpoint instead of the before-roll point.");
                string postBattlePlayer = JsonSerializer.Serialize(native.Players[0].ToSerializable());
                string postBattleRng = JsonSerializer.Serialize(native.Rng.ToSerializable());
                CombatManager.Instance.Reset(true); manager.CleanUp(false);
                var restored = RunState.FromSerializable(saved.SaveData!);
                Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == postBattlePlayer &&
                    JsonSerializer.Serialize(restored.Rng.ToSerializable()) == postBattleRng,
                    "The saved victory point retains actual post-battle player effects and RNG together.");
                await manager.SetUpSavedSingleplayer(restored, saved.SaveData!);
                LocalContext.NetId = restored.Players[0].NetId;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(CombatRoom.FromSerializable(saved.SaveData!.PreFinishedRoom!, restored));
                Check(restored.CurrentRoom is CombatRoom { IsPreFinished: true } && JigsawNativeSession.Current!.Map is { } resumedMap &&
                    resumedMap.Steps == stepsBefore + 1 && resumedMap.CurrentNode == target,
                    "The real native reward-room reload does not start another combat or charge a second entry.");
                Check(restored.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms,
                    "Reward restore appends no duplicate combat history entry.");
                return;
            }
            else
            {
                string originalPlayer = JsonSerializer.Serialize(native.Players[0].ToSerializable());
                string originalEnemies = Opening(room);
                string originalRng = JsonSerializer.Serialize(native.Rng.ToSerializable());
                long originalEntry = session.Map.CreateSave().NextEntry;
                int originalRemaining = session.Map.RemainingSteps;
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                    7, ValueProp.Unpowered, enemy);
                await PlayerCmd.GainGold(19, native.Players[0]);
                native.Rng.Niche.NextInt(100);
                enemy.Monster!.Rng.NextInt(100);
                await JigsawNativeSaveHost.Save(null);
                var loaded = saves.LoadRunSave();
                Check(loaded.Success && loaded.SaveData != null, "A real active battle save parses through the native memory IO.");
                CombatManager.Instance.Reset(true);
                manager.CleanUp(false);
                var restored = RunState.FromSerializable(loaded.SaveData!);
                await manager.SetUpSavedSingleplayer(restored, loaded.SaveData!);
                LocalContext.NetId = restored.Players[0].NetId;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(null);
                var resumed = JigsawNativeSession.Current!;
                Console.WriteLine($"COMBAT_CHECKPOINT room={restored.CurrentRoom?.GetType().Name}; expected={originalEnemies}; actual={(restored.CurrentRoom is CombatRoom actualRoom ? Opening(actualRoom) : "no-combat")}");
                Check(restored.CurrentRoom is CombatRoom restoredRoom && Opening(restoredRoom) == originalEnemies,
                    "Native reload rebuilds the exact same enemies, initial HP, powers, intents and monster RNG once.");
                Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == originalPlayer &&
                    JsonSerializer.Serialize(restored.Rng.ToSerializable()) == originalRng,
                    "Native player effects and run RNG roll back together to the original battle opening.");
                Check(resumed.Map is { CurrentRoomHunted: true } restoredMap && restoredMap.CurrentNode == target &&
                    restoredMap.Steps == stepsBefore + 1 && restoredMap.RemainingSteps == originalRemaining &&
                    restoredMap.CreateSave().NextEntry == originalEntry,
                    "The complete native restore preserves the same hunted root and charges the logical entry exactly once.");
                Check(restored.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms,
                    "Active battle restore appends no duplicate combat history entry.");
                await resumed.Enter(target);
                Check(resumed.Map!.Steps == stepsBefore + 1, "A duplicate restored destination cannot charge again.");
            }
        }
        finally
        {
            profileHost.UnpatchAll(profileHost.Id);
            JigsawNativeSaveHost.Save = previousSave;
            LocalContext.NetId = previousNetId;
            CombatManager.Instance.Reset(true);
            manager.CleanUp(false);
            viewport.Size = originalSize;
        }
    }

    private static async Task EmbeddedBefore(bool colosseum, bool victory, bool replaceDuringLoad = false, bool malformedRng = false)
    {
        using var selector = CardSelectCmd.UseSelector(new TakeCardReward());
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var events = HeadlessCombatInProgress.EnterEvent();
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: colosseum ? "HUNTING84_EVENT_SECOND" : "HUNTING84_VEGETATION");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        manager.CombatReplayWriter.IsEnabled = false;
        ulong? previousNetId = LocalContext.NetId;
        LocalContext.NetId = 1;
        var io = new MockGodotFileIo("combat-checkpoint-event-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        var previousSave = JigsawNativeSaveHost.Save;
        JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
        var profile = new Harmony("AllMatters.Tests.CombatCheckpointEventProfile");
        foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
            profile.Patch(method, prefix: new HarmonyMethod(typeof(JigsawCombatCheckpointTests), nameof(SkipProfile)));
        try
        {
            manager.GenerateRooms(); await manager.GenerateMap();
            EventModel parent = colosseum ? ModelDb.Event<AllMatters.Events.Colosseum>() :
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.DenseVegetation>();
            // Force the known finite event into this hunted logical root. Its
            // eligibility and ecological enhancements are outside this slice.
            native.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            var map = ReadyEventRoot();
            JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            int steps = map.Steps;
            if (!colosseum) native.Players[0].Creature.SetCurrentHpInternal(10);
            await manager.EnterRoom(new EventRoom(parent));
            var model = manager.EventSynchronizer.GetLocalEvent();
            int healed = 0;
            if (colosseum)
            {
                await model.CurrentOptions.Single().Chosen();
                await model.CurrentOptions.Single().Chosen();
                await Until(() => native.CurrentRoom is CombatRoom);
                var first = (CombatRoom)native.CurrentRoom!;
                Check(first.Encounter is ColosseumFirstEncounter, "The actual first event battle begins.");
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                    9, ValueProp.Unpowered, first.CombatState.Enemies[0]);
                AccessTools.Property(native.Players[0].PlayerCombatState!.GetType(), "TurnNumber")
                    .SetValue(native.Players[0].PlayerCombatState, 1);
                await (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes)
                    .Invoke(CombatManager.Instance, null)!;
                await manager.ProceedFromTerminalRewardsScreen();
                await manager.EventSynchronizer.AwaitPendingOptionTasks();
                Check(native.CurrentRoom is EventRoom && model.CurrentOptions.Count == 2,
                    "Actual native victory and Resume return the same parent to intermission.");
            }
            else
            {
                await model.CurrentOptions.Single(option => option.TextKey.EndsWith("_HEAL")).Chosen();
                healed = native.Players[0].Creature.CurrentHp;
                Check(healed > 10, "The real event rest action heals before combat.");
            }
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".FIGHT")).Chosen();
            await Until(() => native.CurrentRoom is CombatRoom);
            var battle = (CombatRoom)native.CurrentRoom!;
            Check(!colosseum || battle.Encounter is not ColosseumFirstEncounter,
                "The accepted second event battle uses its newly selected native encounter.");
            string encounter = battle.Encounter.Id.Entry;
            string opening = Opening(battle);
            string playerBefore = JsonSerializer.Serialize(native.Players[0].ToSerializable());
            string rngBefore = JsonSerializer.Serialize(native.Rng.ToSerializable());
            string ownerRng = EventRng(model);
            int historyRooms = native.CurrentMapPointHistoryEntry!.Rooms.Count;
            long entry = map.CreateSave().NextEntry;
            int remaining = map.RemainingSteps;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                5, ValueProp.Unpowered, battle.CombatState.Enemies[0]);
            await PlayerCmd.GainGold(23, native.Players[0]); native.Rng.Niche.NextInt(100);
            battle.CombatState.Enemies[0].Monster!.Rng.NextInt(100);
            await JigsawNativeSaveHost.Save(null);
            if (malformedRng)
            {
                RejectMissingOwnerRng(io, saves);
                return;
            }
            var saved = saves.LoadRunSave();
            Check(saved.Success && saved.SaveData != null, "The actual embedded battle save parses through native IO.");
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
            var restored = RunState.FromSerializable(saved.SaveData!);
            await manager.SetUpSavedSingleplayer(restored, saved.SaveData!);
            LocalContext.NetId = 1; await manager.GenerateMap();
            if (replaceDuringLoad)
            {
                await ReplaceSuspendedLoad(restored, manager);
                return;
            }
            await manager.LoadIntoLatestMapCoord(null);
            Check(restored.CurrentRoomCount == 2 && restored.BaseRoom is EventRoom &&
                restored.CurrentRoom is CombatRoom restoredBattle && restoredBattle.Encounter.Id.Entry == encounter,
                "Native load rebuilds the accepted child and its parent stack without restarting the root.");
            Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == playerBefore &&
                JsonSerializer.Serialize(restored.Rng.ToSerializable()) == rngBefore &&
                (colosseum || restored.Players[0].Creature.CurrentHp == healed),
                "The whole point retains preceding victory losses or completed healing while rolling back only this battle.");
            var restoredMap = JigsawNativeSession.Current!.Map!;
            Check(restored.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms && restoredMap.Steps == steps &&
                restoredMap.RemainingSteps == remaining && restoredMap.CreateSave().NextEntry == entry,
                "Embedded battle restore preserves the same entry, budget and history without charging or appending again.");
            var resumedBattle = (CombatRoom)restored.CurrentRoom!;
            Check(Opening(resumedBattle) == opening && EventRng(manager.EventSynchronizer.GetLocalEvent()) == ownerRng,
                "All native initial enemy values and monster RNG, plus owner event RNG, rebuild at the same point.");
            var resumedParent = manager.EventSynchronizer.GetLocalEvent();
            foreach (var stale in resumedParent.CurrentOptions.Where(option =>
                option.TextKey.EndsWith(".FLEE") || option.TextKey.Contains(".AM_REST_")).ToArray())
                await stale.Chosen();
            Check(!resumedParent.IsFinished && ReferenceEquals(restored.CurrentRoom, resumedBattle) &&
                JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == playerBefore,
                "Finite parent callbacks cannot flee or replay an already completed rest behind its accepted child.");
            if (!victory) return;
            await PlayerCmd.GainGold(31, restored.Players[0]);
            await (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes)
                .Invoke(CombatManager.Instance, null)!;
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: not null },
                "Actual embedded victory saves the genuine native reward room with its parent identity.");
            string postPlayer = JsonSerializer.Serialize(restored.Players[0].ToSerializable());
            string postRng = JsonSerializer.Serialize(restored.Rng.ToSerializable());
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
            var rewardRun = RunState.FromSerializable(won.SaveData!);
            Check(JsonSerializer.Serialize(rewardRun.Players[0].ToSerializable()) == postPlayer &&
                JsonSerializer.Serialize(rewardRun.Rng.ToSerializable()) == postRng,
                "The genuine saved victory point contains the complete postbattle player and RNG before reward delivery resumes.");
            Check(JigsawCheckpoint.Slot.TryGet(rewardRun, out var point) && point.EventCombat == null,
                "Victory clears only the active child battle rollback descriptor.");
            await manager.SetUpSavedSingleplayer(rewardRun, won.SaveData!);
            LocalContext.NetId = 1; await manager.GenerateMap();
            await manager.LoadIntoLatestMapCoord(CombatRoom.FromSerializable(won.SaveData!.PreFinishedRoom!, rewardRun));
            Check(rewardRun.CurrentRoomCount == 2 && rewardRun.CurrentRoom is CombatRoom { IsPreFinished: true } &&
                rewardRun.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms &&
                JigsawNativeSession.Current!.Map!.Steps == steps,
                "Reward load resumes the parent and reward stack without reopening or duplicating entry/history.");
            var rewardParent = manager.EventSynchronizer.GetLocalEvent();
            string beforeStaleReward = JsonSerializer.Serialize(rewardRun.Players[0].ToSerializable());
            foreach (var stale in rewardParent.CurrentOptions.Where(option =>
                option.TextKey.EndsWith(".FLEE") || option.TextKey.Contains(".AM_REST_")).ToArray())
                await stale.Chosen();
            Check(!rewardParent.IsFinished && JsonSerializer.Serialize(rewardRun.Players[0].ToSerializable()) == beforeStaleReward,
                "The terminal parent behind native victory rewards cannot flee or repeat its completed rest.");
        }
        finally
        {
            profile.UnpatchAll(profile.Id); JigsawNativeSaveHost.Save = previousSave;
            CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousNetId;
        }
    }

    private static void RejectMissingOwnerRng(MockGodotFileIo io, RunSaveManager saves)
    {
        string path = RunSaveManager.GetRunSavePath(84, "current_run.save");
        var original = JsonNode.Parse(io.ReadFile(path)!)!;
#if STS2_PUBLIC
        string[] fields = ["Seed", "Counter"];
#else
        string[] fields = ["State"];
#endif
        foreach (string field in fields)
        {
            var malformed = original.DeepClone();
            var rng = malformed["_ritsulib"]!["run_saved_data"]![ModEntry.ModId]!["jigsaw"]!["data"]!
                ["EventCombat"]!["Owners"]!["1"]!.AsObject();
            Check(rng.Remove(field), "Preparation removes an actual persisted native owner RNG field.");
            io.SetFileContent(path, malformed.ToJsonString());
            var parsed = saves.LoadRunSave();
            Check(parsed.Success && parsed.SaveData != null, "The native reader still accepts the structurally valid outer save.");
            bool rejected = false;
            try { RunState.FromSerializable(parsed.SaveData!); }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or JsonException) { rejected = true; }
            Check(rejected, $"A native owner RNG missing {field} must reject restoration instead of substituting zero/default state.");
        }
    }

    private static async Task ReplaceSuspendedLoad(RunState oldRun, RunManager manager)
    {
        var host = new Harmony("AllMatters.Tests.SuspendedCombatCheckpointLoad");
        var enter = AccessTools.Method(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)]);
        var suspended = new SuspendedParentEntry();
        SuspendedParentEntry.Current = suspended;
        host.Patch(enter, postfix: new HarmonyMethod(typeof(SuspendedParentEntry), nameof(SuspendedParentEntry.Postfix)));
        Task? load = null;
        try
        {
            load = JigsawEventCombatCheckpoint.Load(JigsawNativeSession.Current!);
            await Until(() => suspended.Arrived.Task.IsCompleted);
            Check(oldRun.CurrentRoom is EventRoom, "The real native parent entry completed before its load continuation is suspended.");
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
            var replacement = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>()], seed: "COMBAT_CHECKPOINT_REPLACEMENT");
            manager.SetUpTest(replacement, new NetSingleplayerGameService());
            LocalContext.NetId = 1;
            manager.GenerateRooms(); await manager.GenerateMap(); await manager.EnterRoom(new MapRoom());
            var replacementMap = ReadyEventRoot();
            JigsawCheckpoint.Capture(replacement, replacementMap); JigsawNativeSession.Restore(replacement);
            string player = JsonSerializer.Serialize(replacement.Players[0].ToSerializable());
            string rng = JsonSerializer.Serialize(replacement.Rng.ToSerializable());
            var replacementSession = JigsawNativeSession.Current;
            suspended.Release.TrySetResult(true);
            bool rejected = false;
            try { await load; }
            catch (Exception error) when (error is InvalidOperationException or OperationCanceledException) { rejected = true; }
            Check(rejected && ReferenceEquals(JigsawNativeSession.Current, replacementSession) && replacement.CurrentRoom is MapRoom &&
                replacement.CurrentRoomCount == 1 && JsonSerializer.Serialize(replacement.Players[0].ToSerializable()) == player &&
                JsonSerializer.Serialize(replacement.Rng.ToSerializable()) == rng,
                "An obsolete suspended load rejects before touching the replacement synchronizer, room stack, player or RNG.");
        }
        finally
        {
            suspended.Release.TrySetResult(true);
            if (load != null) try { await load; } catch { }
            SuspendedParentEntry.Current = null; host.UnpatchAll(host.Id);
        }
    }

    private sealed class SuspendedParentEntry
    {
        internal static SuspendedParentEntry? Current;
        internal readonly TaskCompletionSource<bool> Arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool captured;
        internal static void Postfix(AbstractRoom room, bool isRestoringRoomStackBase, ref Task __result)
        {
            if (Current is not { captured: false } hold || !isRestoringRoomStackBase || room is not EventRoom) return;
            hold.captured = true; __result = hold.Wait(__result);
        }
        private async Task Wait(Task original)
        {
            await original; Arrived.TrySetResult(true); await Release.Task;
        }
    }

    private static string EventRng(EventModel model) => MonsterRng(model.Rng);

    private static async Task Until(Func<bool> condition)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 120 && !condition(); frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(condition(), "The actual asynchronous embedded battle entry finishes within the bounded host frame budget.");
    }
    private static JigsawRun ReadyEventRoot(ulong[]? owners = null)
    {
        owners ??= [1];
        var map = JigsawRun.Create(75, players: owners);
        foreach (ulong owner in owners) Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong owner in owners) Ok(map.CompleteAncient(owner));
        foreach (ulong owner in owners) Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Unknown && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse(); Ok(map.SetRemainingSteps(0));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        return map;
    }

    private static TaskCompletionSource? ownerRelease;
    private static bool ownerWaiting;
    private static string? ownerCompletedRunRng;
    private static void DelayOwnerFight(MegaCrit.Sts2.Core.Models.Events.DenseVegetation __instance, ref Task __result)
    {
        if (__instance.Owner!.NetId == 3 && ownerRelease != null) __result = Complete(__result, __instance);
        static async Task Complete(Task original, EventModel model)
        {
            await original;
            ownerWaiting = true;
            await ownerRelease!.Task;
            // These are real effects of the final owner's actual chosen task,
            // which the native synchronizer itself tracks in its pending list.
            await PlayerCmd.GainGold(37, model.Owner!);
            model.Rng.NextInt(100);
            model.Owner!.RunState.Rng.Niche.NextInt(100);
            ownerCompletedRunRng = JsonSerializer.Serialize(model.Owner.RunState.Rng.ToSerializable());
        }
    }

    internal static async Task NetworkOwners(RunState native, RunSaveManager saves, bool reloaded,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        var manager = RunManager.Instance;
        string[] peers = ["host", "2", "3"];
        string State() => JsonSerializer.Serialize(new
        {
            Players = native.Players.OrderBy(player => player.NetId).Select(player => player.ToSerializable()).ToArray(),
            Rng = native.Rng.ToSerializable(),
            Owners = manager.EventSynchronizer.Events.OrderBy(model => model.Owner!.NetId)
                .Select(model => new { model.Owner!.NetId, Rng = EventRng(model) }).ToArray(),
            Map = JigsawNativeSession.Current!.Map!.CreateSave() with
                { Players = JigsawNativeSession.Current.Map.ActivePlayers.Order().ToArray() },
            Rooms = native.CurrentMapPointHistoryEntry!.Rooms,
            Opening = native.CurrentRoom is CombatRoom battle ? Opening(battle) : null
        });
        if (reloaded)
        {
            File.WriteAllText(Path.Combine(directory, role + ".restored.json"), State());
            Check(native.CurrentRoomCount == 2 && native.BaseRoom is EventRoom && native.CurrentRoom is CombatRoom,
                "The real three-owner load lobby rebuilds the parent and child together.");
            Check(JsonNode.DeepEquals(JsonNode.Parse(State()), JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "event-opening.json")))),
                "A fresh three-process native load preserves every player's complete state, every run and owner RNG, opening enemies, logical entry, budget and history.");
            Console.WriteLine($"COMBAT_CHECKPOINT_ENET role={role} original=3 active=3 restored=parent+child");
            return;
        }
        var harmony = new Harmony("AllMatters.Tests.CombatCheckpointOwners");
        ownerRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ownerWaiting = false;
        ownerCompletedRunRng = null;
        // Both supported native Fight implementations return the actual chosen
        // task after requesting combat; only its final owner's completion waits.
        harmony.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.Events.DenseVegetation), "Fight"),
            postfix: new HarmonyMethod(typeof(JigsawCombatCheckpointTests), nameof(DelayOwnerFight)));
        try
        {
            var map = ReadyEventRoot(native.Players.Select(player => player.NetId).ToArray());
            native.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event,
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.DenseVegetation>().Id);
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            foreach (var player in native.Players) player.Creature.SetCurrentHpInternal(10);
            await manager.EnterRoom(new EventRoom(ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.DenseVegetation>()));
            manager.ActionExecutor.Unpause();
            signal("event-ready");
            await until(() => peers.All(peer => signaled(peer, "event-ready")), "all three actual endpoints enter the shared event");
            var sync = manager.EventSynchronizer;
            int heal = sync.GetLocalEvent().CurrentOptions.ToList().FindIndex(option => option.TextKey.EndsWith("_HEAL"));
            Check(heal >= 0, "The native event offers a real common healing action.");
            sync.ChooseLocalOption(heal);
            await until(() => sync.Events.All(model => model.CurrentOptions.Any(option => option.TextKey.EndsWith(".FIGHT"))),
                "the genuine shared vote completes healing for all three owner models");
            await sync.AwaitPendingOptionTasks();
            int steps = map.Steps, remaining = map.RemainingSteps;
            long entry = map.CreateSave().NextEntry;
            int history = native.CurrentMapPointHistoryEntry!.Rooms.Count;
            int gold = native.Players.Single(player => player.NetId == 3).Gold;
            signal("healed");
            await until(() => peers.All(peer => signaled(peer, "healed")), "all endpoints complete native owner tasks before the fight vote");
            sync.ChooseLocalOption(sync.GetLocalEvent().CurrentOptions.ToList().FindIndex(option => option.TextKey.EndsWith(".FIGHT")));
            await until(() => ownerWaiting, "the real final owner's chosen task is suspended on each endpoint");
            int frames = 0;
            await until(() => ++frames >= 8, "transport and frames continue while the actual final owner task remains held");
            var actualTasks = (IEnumerable<Task>)AccessTools.Field(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer), "_pendingOptionTasks").GetValue(sync)!;
            Check(actualTasks.Count() == 3 && actualTasks.Count(task => !task.IsCompleted) == 1,
                "The native synchronizer tracks three actual owner option tasks and exactly the deliberately delayed final task remains pending.");
            Check(native.CurrentRoom is EventRoom && native.CurrentRoomCount == 1 &&
                !JigsawEventCombatCheckpoint.HasPoint(native) && native.CurrentMapPointHistoryEntry.Rooms.Count == history &&
                map.Steps == steps && map.RemainingSteps == remaining && map.CreateSave().NextEntry == entry,
                "No half checkpoint, child history or extra logical charge appears while the final actual owner task is pending.");
            signal("owner-held");
            await until(() => peers.All(peer => signaled(peer, "owner-held")), "all three endpoints prove the last native owner callback is still held");
            ownerRelease.TrySetResult();
            await until(() => native.CurrentRoom is CombatRoom, "the complete native child enters only after every genuine owner task finishes");
            Check(native.CurrentRoomCount == 2 && native.Players.Single(player => player.NetId == 3).Gold == gold + 37 &&
                native.Players.All(player => player.Creature.CurrentHp > 10) && native.CurrentMapPointHistoryEntry.Rooms.Count == history + 1 &&
                map.Steps == steps && map.RemainingSteps == remaining && map.CreateSave().NextEntry == entry,
                "The accepted child includes all three real healing effects and the final owner's delayed effect with exactly one history row and no extra steps.");
            await JigsawNativeSaveHost.Save!(null);
            var loaded = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            File.WriteAllText(Path.Combine(directory, role + ".actual.json"), State());
            File.WriteAllText(Path.Combine(directory, role + ".saved.json"), JsonSerializer.Serialize(loaded.SaveData));
            Check(loaded.Success && loaded.SaveData != null && loaded.SaveData.Players.All(saved =>
                JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(saved)), JsonNode.Parse(JsonSerializer.Serialize(native.Players.Single(player => player.NetId == saved.NetId).ToSerializable())))) &&
                JsonSerializer.Serialize(loaded.SaveData.SerializableRng) == ownerCompletedRunRng,
                "The actual saved complete point includes all delayed owner player and run RNG effects.");
            if (role == "host")
            {
                File.WriteAllText(Path.Combine(directory, "combat-checkpoint.json"), JigsawSaveSource.Serialize(loaded.SaveData!));
                File.WriteAllText(Path.Combine(directory, "event-opening.json"), State());
            }
            signal("event-saved");
            await until(() => peers.All(peer => signaled(peer, "event-saved")), "all three endpoints complete the native save before fresh-process restoration");
            Console.WriteLine($"COMBAT_CHECKPOINT_ENET role={role} original=3 active=3 actual-owner-tasks=3 delayed-owner=3");
        }
        finally
        {
            ownerRelease?.TrySetResult(); ownerRelease = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }
    private static bool SkipProfile() => false;

    private sealed class TakeCardReward : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult(options.Take(minSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => new() { card = options[0].Card };
    }

    private static string Opening(CombatRoom room) => JsonSerializer.Serialize(room.CombatState.Enemies.Select(creature => new
    {
        Monster = creature.Monster!.Id.Entry,
        creature.MaxHp,
        creature.CurrentHp,
        Powers = creature.Powers.Select(power => new { power.Id.Entry, power.Amount }).ToArray(),
        Move = creature.Monster.NextMove.Id,
        Rng = MonsterRng(creature.Monster.Rng)
    }).ToArray());

    private static string MonsterRng(MegaCrit.Sts2.Core.Random.Rng rng) =>
#if STS2_PUBLIC
        JsonSerializer.Serialize(new { rng.Seed, rng.Counter });
#else
        JsonSerializer.Serialize(rng.ToSerializable());
#endif

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
