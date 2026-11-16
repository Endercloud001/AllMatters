using AllMatters;
using AllMatters.Encounters;
using AllMatters.Events;
using AllMatters.Jigsaw;
using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

internal static class JigsawHuntingHolesCheckpointTests
{
    private static readonly JsonSerializerOptions CompleteStateJson = new() { IncludeFields = true };
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw hunting Hive real Holes AM_FIGHT battle reload restores its own complete opening", BattleReload),
        ("Jigsaw hunting Hive real Holes victory rewards reload and Proceed finish once", VictoryReload),
        ("Jigsaw hunting Holes pending option cannot enter combat after same-attempt map restore", PendingMapRestore),
        ("Jigsaw hunting Holes pending option normal release captures complete owner effects once", PendingNormalRelease),
        ("Jigsaw hunting Holes safe Unknown reaching zero enters real AM_FIGHT without punishment or checkpoint", SafeRoot),
        ("Jigsaw hunting Holes real saved descriptors reject mismatched parent encounter and resume before entering rooms", InvalidDescriptors),
        ("Jigsaw hunting Holes repeated real battle reload restores the same complete opening twice", RepeatedBattleReload)
    ];

    private sealed class StartupObservation
    {
        internal Task? Opening;
        internal Task? Startup;
        internal int OpeningCalls;
    }
    private static StartupObservation? observedStartup;

    private sealed class PendingEntryObservation(EventOption option)
    {
        internal readonly EventOption Option = option;
        internal readonly TaskCompletionSource ChoiceReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseChoice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task? OriginalChoice;
        internal Task? WrappedChoice;
        internal Task? Entry;
        internal Task? Pending;
        internal Func<Task>? CompletedEffects;
    }
    private static PendingEntryObservation? pendingEntry;

    private static void HoldChosenTask(EventOption __instance, ref Task __result)
    {
        if (pendingEntry is not { } pending || !ReferenceEquals(__instance, pending.Option)) return;
        pending.OriginalChoice = __result;
        __result = pending.WrappedChoice = Hold(__result, pending);
        static async Task Hold(Task original, PendingEntryObservation pending)
        {
            await original;
            pending.ChoiceReached.TrySetResult();
            await pending.ReleaseChoice.Task;
            if (pending.CompletedEffects != null) await pending.CompletedEffects();
        }
    }

    private static void ObserveEntryTask(Task __result)
    {
        if (pendingEntry is { } pending) pending.Entry = __result;
    }

    private static void ObservePendingTasks(Task __result)
    {
        if (pendingEntry is { Entry: not null } pending && !__result.IsCompleted) pending.Pending = __result;
    }

    private static void CaptureOpening(Task task)
    {
        if (observedStartup is not { } observation) return;
        observation.Opening = task;
        observation.OpeningCalls++;
    }

    private static void CaptureStartup(Task task)
    {
        if (observedStartup is { } observation) observation.Startup = task;
    }

    private static IEnumerable<CodeInstruction> ObserveOpening(IEnumerable<CodeInstruction> instructions)
    {
        var hook = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart));
        var capture = AccessTools.Method(typeof(JigsawHuntingHolesCheckpointTests), nameof(CaptureOpening));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(hook)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the exact native coordinator opening Task without replacing or repeating the Hook.");
    }

    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
#if STS2_PUBLIC
            "StartCombatInternal");
#else
            "RunTurnLoopAfter");
#endif
        var capture = AccessTools.Method(typeof(JigsawHuntingHolesCheckpointTests), nameof(CaptureStartup));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the same native startup or turn-loop Task passed to RunSafely.");
    }

    private static bool SkipCombatFtue(ref bool __result) { __result = true; return false; }
    private static bool SkipProfile() => false;

    private static Task BattleReload() => BattleCheckpoint(false);
    private static Task VictoryReload() => BattleCheckpoint(true);
    private static Task PendingMapRestore() => BattleCheckpoint(false, invalidateMap: true);
    private static Task PendingNormalRelease() => BattleCheckpoint(false, releasePending: true);
    private static Task SafeRoot() => BattleCheckpoint(false, safeRoot: true);
    private static Task InvalidDescriptors() => BattleCheckpoint(false, rejectDescriptors: true);
    private static Task RepeatedBattleReload() => BattleCheckpoint(false, repeatReload: true);

    private static async Task BattleCheckpoint(bool victory, bool invalidateMap = false, bool releasePending = false,
        bool safeRoot = false, bool rejectDescriptors = false, bool repeatReload = false)
    {
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        var previousNetId = LocalContext.NetId;
        var previousSave = JigsawNativeSaveHost.Save;
        RunState? activeRun = null;
        Harmony? patch = null;
        Harmony? host = null;
        IDisposable? atlases = null;
        bool hostTouched = false;
        Exception? bodyFailure = null;
        var cleanupFailures = new List<Exception>();
        var observation = new StartupObservation();
        int readyRound = 0;
        void PlayerTurnReady(CombatState state)
        {
            if (ReferenceEquals(state.RunState, activeRun) && state.CurrentSide == CombatSide.Player)
                readyRound = state.RoundNumber;
        }
        void RecordCleanupFailure(string step, Exception error)
        {
            var failure = new InvalidOperationException($"Holes checkpoint cleanup failed during {step}.", error);
            cleanupFailures.Add(failure);
            Console.Error.WriteLine(failure);
        }
        void Cleanup(string step, Action action)
        {
            try { action(); } catch (Exception error) { RecordCleanupFailure(step, error); }
        }
        try
        {
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            patch = new Harmony("AllMatters.Tests.HolesCheckpoint.NativeStartup");
            var start = AccessTools.Method(typeof(CombatManager), "StartCombatInternal");
            var stateMachine = start.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(stateMachine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(ObserveOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(SkipCombatFtue)));
            foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(SkipProfile)));
            observedStartup = observation;
            activeRun = SharedHolesRun();
            var originalRun = activeRun;
            var player = originalRun.Players[0];
            manager.SetUpTest(originalRun, new NetSingleplayerGameService());
            combatManager.Reset(true);
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = player.NetId;
            var io = new MockGodotFileIo("hunting85-holes-checkpoint-memory-only");
            var saves = new RunSaveManager(85, io, new MigrationManager(io), forceSynchronous: true);
            JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
            combatManager.TurnStarted += PlayerTurnReady;
            player.Creature.SetMaxHpInternal(500);
            player.Creature.SetCurrentHpInternal(500);
            for (int card = 0; card < 20; card++) player.Deck.AddInternal(originalRun.CreateCard<DefendIronclad>(player));
            EventModel model;
            var parent = ModelDb.Event<FieldOfManSizedHoles>();
            using (HeadlessCombatInProgress.EnterEvent())
            {
                manager.GenerateRooms();
                await manager.GenerateMap();
                originalRun.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
                originalRun.AddVisitedMapCoord(originalRun.Map.StartingMapPoint.coord);
                Check(originalRun.MapPointHistory.Sum(points => points.Count) == 1,
                    "The real first native event history matches the prepared shared Holes seed's floor.");
                JigsawCheckpoint.Capture(originalRun, AcceptedRoot(player.NetId, safeRoot));
                JigsawNativeSession.Restore(originalRun);
                await JigsawHuntingNative.SynchronizeMasks(JigsawNativeSession.TryGet(originalRun)!);
                Check(originalRun.Players.All(owner => safeRoot ? !owner.Relics.OfType<ArchitectMask>().Any() :
                    owner.Relics.OfType<ArchitectMask>().Count() == 1 && !owner.Relics.OfType<ArchitectMask>().Single().IsTradable),
                    "The real native entry mask synchronization matches this accepted safe or hunted root.");
                await manager.EnterRoom(new EventRoom(parent));
                model = manager.EventSynchronizer.GetLocalEvent();
                Check(model is FieldOfManSizedHoles && model.IsShared && manager.EventSynchronizer.IsShared &&
                    model.CurrentOptions.Count(option => option.TextKey.EndsWith(".AM_FIGHT")) == 1,
                    "The actual native shared Holes page offers its original mod AM_FIGHT option.");
            }
            var map = JigsawNativeSession.TryGet(originalRun)!.Map!;
            Check(map.CurrentNode != null && map.CurrentRoomHunted == !safeRoot &&
                map.Nodes.Single(node => node.Id == map.CurrentNode).Room == JigsawRoom.Unknown,
                "The actual source event is governed by the intended accepted Unknown root.");
            host = new Harmony("AllMatters.TestHost");
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            int fight = model.CurrentOptions.ToList().FindIndex(option => option.TextKey.EndsWith(".AM_FIGHT"));
            if (invalidateMap || releasePending)
            {
                await PendingOptionBoundary(originalRun, model, fight, patch, observation, saves, invalidateMap,
                    battle => readyRound == battle.CombatState.RoundNumber && readyRound >= 1 && PileType.Hand.GetPile(player).Cards.Count == 5);
                return;
            }
            manager.EventSynchronizer.ChooseLocalOption(fight);
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            await AwaitOpening(observation);
            Check(originalRun.CurrentRoomCount == 2 && originalRun.BaseRoom is EventRoom eventRoom &&
                eventRoom.CanonicalEvent.Id == parent.Id && originalRun.CurrentRoom is CombatRoom
                { Encounter: HolesEncounter, ParentEventId: var parentId, ShouldResumeParentEventAfterCombat: true } && parentId == parent.Id,
                "The real AM_FIGHT synchronizer created the correct resumable parent and Holes child combat.");
            var room = (CombatRoom)originalRun.CurrentRoom!;
            if (!safeRoot) AssertOpening(player);
            await UntilNative(() => readyRound == room.CombatState.RoundNumber && readyRound >= 1 &&
                PileType.Hand.GetPile(player).Cards.Count == 5, observation,
                "The original native coordinator finished its complete first player setup before the rollback reference is observed.");
            if (safeRoot)
            {
                Check(map.RemainingSteps == 0 && !map.HuntingActive && !map.CurrentRoomHunted &&
                    player.Creature.GetPowerAmount<WeakPower>() == 0 && player.Creature.GetPowerAmount<FrailPower>() == 0 &&
                    player.Creature.GetPowerAmount<VulnerablePower>() == 0 &&
                    !combatManager.History.Entries.OfType<PowerReceivedEntry>().Any(entry => ReferenceEquals(entry.Actor, player.Creature) &&
                        entry.Power is WeakPower or FrailPower or VulnerablePower),
                    "This Unknown entry used its final available step and remains safe throughout its real Hive Holes opening.");
                Check(!JigsawEventCombatCheckpoint.HasPoint(originalRun) && JigsawEventCombatCheckpoint.Before(originalRun) == null &&
                    JigsawCheckpoint.Slot.Get(originalRun).EventCombat == null && !HolesPageState(originalRun).AcceptedFight &&
                    HolesPageState(originalRun).FightOwners.Count == 0 && observation.OpeningCalls == 1 &&
                    originalRun.CurrentRoomCount == 2 && ReferenceEquals(originalRun.CurrentRoom, room),
                    "Real safe AM_FIGHT enters once without a hunted combat checkpoint or an accepted hunted parent phase.");
                return;
            }
            if (victory)
            {
                await VictoryRewards(originalRun, saves, observation, run => activeRun = run);
                return;
            }
            string playerBefore = JsonSerializer.Serialize(player.ToSerializable(), CompleteStateJson);
            string runRngBefore = JsonSerializer.Serialize(originalRun.Rng.ToSerializable(), CompleteStateJson);
            string eventRngBefore = EventRng(model);
            string enemiesBefore = JigsawHuntingCombatTests.Opening(room);
            string monsterRngBefore = MonsterRngContents(room);
            int steps = map.Steps;
            int remaining = map.RemainingSteps;
            int? root = map.CurrentNode;
            // Native JSON omits empty event-choice Variables and reads them back as null.
            var historyType = JsonSerializationUtility.GetTypeInfo<MapPointHistoryEntry>();
            var expectedHistory = JsonSerializer.Deserialize(
                JsonSerializer.Serialize(originalRun.CurrentMapPointHistoryEntry!, historyType), historyType)!;
            string historyBefore = JsonSerializer.Serialize(expectedHistory, CompleteStateJson);
            var initialCombatState = room.CombatState;
            for (int reload = 0; reload < (repeatReload ? 2 : 1); reload++)
            {
                originalRun = activeRun!;
                player = originalRun.Players[0];
                room = (CombatRoom)originalRun.CurrentRoom!;
                model = manager.EventSynchronizer.GetLocalEvent();
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 5, ValueProp.Unpowered,
                    room.CombatState.Enemies.Single());
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), room.CombatState.Enemies.Single(), 7,
                    ValueProp.Unpowered, player.Creature);
                await PlayerCmd.GainGold(23, player);
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), player.Creature, 4,
                    room.CombatState.Enemies.Single(), null);
                originalRun.Rng.Niche.NextInt(100);
                model.Rng.NextInt(100);
                foreach (var enemy in room.CombatState.Enemies) enemy.Monster!.Rng.NextInt(100);
                Check(JsonSerializer.Serialize(player.ToSerializable(), CompleteStateJson) != playerBefore &&
                    JsonSerializer.Serialize(originalRun.Rng.ToSerializable(), CompleteStateJson) != runRngBefore &&
                    EventRng(model) != eventRngBefore && JigsawHuntingCombatTests.Opening(room) != enemiesBefore &&
                    MonsterRngContents(room) != monsterRngBefore &&
                    player.Creature.GetPowerAmount<WeakPower>() == 5,
                    "Real HP, gold, monster, opening-stack, run, owner event and complete monster RNG mutations happened before the actual combat save.");
                await JigsawNativeSaveHost.Save(null);
                var saved = saves.LoadRunSave();
                Check(saved.Success && saved.SaveData != null,
                    "The actual Holes battle save was written through the isolated native RunSaveManager and parses.");
                if (rejectDescriptors)
                {
                    await RejectInvalidDescriptors(originalRun, io, saves, observation, run => activeRun = run);
                    return;
                }
                combatManager.Reset(true);
                await AwaitStopped(observation);
                JigsawNativeSession.TryGet(originalRun)?.Close();
                manager.CleanUp(false);
                activeRun = RunState.FromSerializable(saved.SaveData!);
                observation = new StartupObservation();
                observedStartup = observation;
                readyRound = 0;
                await manager.SetUpSavedSingleplayer(activeRun, saved.SaveData!);
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = player.NetId;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(null);
                await manager.EventSynchronizer.AwaitPendingOptionTasks();
                Check(activeRun.CurrentRoomCount == 2 && activeRun.BaseRoom is EventRoom restoredParent &&
                    restoredParent.CanonicalEvent.Id == parent.Id && activeRun.CurrentRoom is CombatRoom
                    { Encounter: HolesEncounter, ParentEventId: var restoredParentId, ShouldResumeParentEventAfterCombat: true } &&
                    restoredParentId == parent.Id,
                    $"Native combat reload must rebuild the same Holes parent and resumable child, not its initial event page; " +
                    $"actual stack={activeRun.CurrentRoomCount}, base={activeRun.BaseRoom?.GetType().Name}, current={activeRun.CurrentRoom?.GetType().Name}.");
                await AwaitOpening(observation);
                var restoredRoom = (CombatRoom)activeRun.CurrentRoom!;
                var restoredPlayer = activeRun.Players[0];
                await UntilNative(() => readyRound == restoredRoom.CombatState.RoundNumber && readyRound >= 1 &&
                    PileType.Hand.GetPile(restoredPlayer).Cards.Count == 5, observation,
                    "The reloaded battle completes its one native opening and complete first player setup.");
                Check(!ReferenceEquals(restoredRoom.CombatState, room.CombatState) && !ReferenceEquals(restoredRoom.CombatState, initialCombatState) &&
                    JsonSerializer.Serialize(restoredPlayer.ToSerializable(), CompleteStateJson) == playerBefore &&
                    JsonSerializer.Serialize(activeRun.Rng.ToSerializable(), CompleteStateJson) == runRngBefore &&
                    EventRng(manager.EventSynchronizer.GetLocalEvent()) == eventRngBefore &&
                    JigsawHuntingCombatTests.Opening(restoredRoom) == enemiesBefore &&
                    MonsterRngContents(restoredRoom) == monsterRngBefore,
                    $"Actual reload {reload + 1} independently restores the entire original player, run RNG, owner event RNG and initial enemy state.");
                AssertOpening(restoredPlayer);
                var restoredMap = JigsawNativeSession.TryGet(activeRun)!.Map!;
                Check(restoredMap.CurrentRoomHunted && restoredMap.CurrentNode == root && restoredMap.Steps == steps &&
                    restoredMap.RemainingSteps == remaining && JsonSerializer.Serialize(activeRun.CurrentMapPointHistoryEntry, CompleteStateJson) == historyBefore,
                    "Whole-battle rollback preserves the accepted root, exploration charge and entire native root history exactly.");
                var restoredModel = manager.EventSynchronizer.GetLocalEvent();
                Check(restoredModel is FieldOfManSizedHoles && restoredModel.IsShared && !restoredModel.IsFinished &&
                    restoredModel.CurrentOptions.All(option => option.IsLocked && !option.WasChosen),
                    "The reconstructed parent retains the unfinished shared fight and has no executable initial choice.");
                string parentBefore = ParentContents(restoredModel);
                Check(JigsawCheckpoint.Slot.TryGet(activeRun, out var checkpoint) && checkpoint.EventCombat != null,
                    "The restored accepted battle still owns its complete native event-combat point.");
                string checkpointBefore = JsonSerializer.Serialize(checkpoint, CompleteStateJson);
                foreach (var option in restoredModel.CurrentOptions.Where(option => option.TextKey.EndsWith(".AM_FIGHT") ||
                    option.TextKey.EndsWith(".RESIST") || option.TextKey.EndsWith(".ENTER_YOUR_HOLE")).ToArray())
                {
                    Check(option.IsLocked, "Any retained initial action is locked by the native null-callback contract.");
                    await option.Chosen();
                    Check(!option.WasChosen, "A locked old native action does not even record itself as chosen.");
                }
                Check(JsonSerializer.Serialize(restoredPlayer.ToSerializable(), CompleteStateJson) == playerBefore &&
                    JsonSerializer.Serialize(activeRun.Rng.ToSerializable(), CompleteStateJson) == runRngBefore &&
                    EventRng(restoredModel) == eventRngBefore && ReferenceEquals(activeRun.CurrentRoom, restoredRoom) &&
                    JsonSerializer.Serialize(checkpoint, CompleteStateJson) == checkpointBefore && observation.OpeningCalls == 1 &&
                    ParentContents(restoredModel) == parentBefore,
                    "Invoking any retained old choice cannot replay combat, removal or enchantment, or change the parent, player, full RNG states or rollback point.");
            }
        }
        catch (Exception error)
        {
            bodyFailure = error;
            Console.Error.WriteLine("Holes checkpoint body failure (before cleanup):");
            Console.Error.WriteLine(error);
        }
        finally
        {
            Cleanup("native Reset", () => combatManager.Reset(true));
            try { await AwaitStopped(observation); }
            catch (Exception error) { RecordCleanupFailure("awaiting native startup after Reset", error); }
            Cleanup("TurnStarted unsubscribe", () => combatManager.TurnStarted -= PlayerTurnReady);
            Cleanup("session close", () => { if (activeRun != null) JigsawNativeSession.TryGet(activeRun)?.Close(); });
            Cleanup("native run cleanup", () => manager.CleanUp(false));
            Cleanup("save delegate restore", () => JigsawNativeSaveHost.Save = previousSave);
            Cleanup("LocalContext restore", () => LocalContext.NetId = previousNetId);
            observedStartup = null;
            if (hostTouched)
            {
                Cleanup("IsInProgress host restore", () => host!.Patch(
                    AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
                Cleanup("IsEnding host restore", () => host!.Patch(
                    AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            }
            Cleanup("test patch removal", () => { if (patch != null) patch.UnpatchAll(patch.Id); });
            Cleanup("atlas disposal", () => atlases?.Dispose());
        }
        if (bodyFailure != null)
        {
            if (cleanupFailures.Count > 0) bodyFailure.Data["HolesCheckpointCleanupFailures"] = new AggregateException(cleanupFailures);
            ExceptionDispatchInfo.Capture(bodyFailure).Throw();
        }
        if (cleanupFailures.Count > 0) throw new AggregateException("Holes checkpoint cleanup failed.", cleanupFailures);
    }

    private static async Task RejectInvalidDescriptors(RunState originalRun, MockGodotFileIo io, RunSaveManager saves,
        StartupObservation startup, Action<RunState> setActiveRun)
    {
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        string path = RunSaveManager.GetRunSavePath(85, "current_run.save");
        var original = JsonNode.Parse(io.ReadFile(path)!)!;
        (string Name, string Property, object Value)[] mutations =
        [
            ("Colosseum parent with Holes encounter", nameof(SerializableRoom.ParentEventId), ModelDb.Event<AllMatters.Events.Colosseum>().Id),
            ("DenseVegetation parent with Holes encounter", nameof(SerializableRoom.ParentEventId), ModelDb.Event<DenseVegetation>().Id),
            ("Holes parent with foreign encounter", nameof(SerializableRoom.EncounterId), ModelDb.Encounter<DenseVegetationEventEncounter>().Id),
            ("Holes child without parent resume", nameof(SerializableRoom.ShouldResumeParentEvent), false)
        ];
        combatManager.Reset(true);
        await AwaitStopped(startup);
        JigsawNativeSession.TryGet(originalRun)?.Close();
        manager.CleanUp(false);
        try
        {
            foreach (var mutation in mutations)
            {
                var malformed = original.DeepClone();
                var combat = malformed["_ritsulib"]!["run_saved_data"]![ModEntry.ModId]!["jigsaw"]!["data"]!["EventCombat"]!["Combat"]!.AsObject();
                var property = typeof(SerializableRoom).GetProperty(mutation.Property)!;
                string jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                Check(combat.ContainsKey(jsonName), $"The real saved descriptor contains the native {mutation.Property} property before mutation.");
                var previous = combat[jsonName]?.DeepClone();
                combat[jsonName] = JsonSerializer.SerializeToNode(mutation.Value, mutation.Value.GetType(), CompleteStateJson);
                Check(!JsonNode.DeepEquals(previous, combat[jsonName]), "The actual persisted native descriptor field changes.");
                io.SetFileContent(path, malformed.ToJsonString());
                var parsed = saves.LoadRunSave();
                Check(parsed.Success && parsed.SaveData != null, "The native reader accepts the well-formed outer save with one invalid encounter combination.");
                var rejectedRun = RunState.FromSerializable(parsed.SaveData!);
                setActiveRun(rejectedRun);
                Check(JigsawCheckpoint.Slot.TryGet(rejectedRun, out var point) && point.EventCombat != null &&
                    Equals(property.GetValue(point.EventCombat.Combat), mutation.Value),
                    "The actual saved descriptor imports the intended changed field without dropping or replacing its combat point.");
                await manager.SetUpSavedSingleplayer(rejectedRun, parsed.SaveData!);
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = rejectedRun.Players[0].NetId;
                await manager.GenerateMap();
                var session = JigsawNativeSession.TryGet(rejectedRun)!;
                var map = session.Map!;
                var previousRoom = rejectedRun.CurrentRoom;
                int roomCount = rejectedRun.CurrentRoomCount;
                int openingCalls = startup.OpeningCalls;
                string players = JsonSerializer.Serialize(rejectedRun.Players.Select(player => player.ToSerializable()).ToArray(), CompleteStateJson);
                string rng = JsonSerializer.Serialize(rejectedRun.Rng.ToSerializable(), CompleteStateJson);
                string history = JsonSerializer.Serialize(rejectedRun.CurrentMapPointHistoryEntry, CompleteStateJson);
                string mapState = JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson);
                string descriptor = JsonSerializer.Serialize(point, CompleteStateJson);
                string page = HolesPageContents(rejectedRun);
                Exception? rejection = null;
                try { await manager.LoadIntoLatestMapCoord(null); }
                catch (InvalidDataException error) when (error.Message == "事件战前点的父事件、初始遭遇或队员不匹配。") { rejection = error; }
                Check(rejection != null && ReferenceEquals(JigsawNativeSession.Current, session) && ReferenceEquals(session.Map, map) &&
                    ReferenceEquals(rejectedRun.CurrentRoom, previousRoom) && rejectedRun.CurrentRoomCount == roomCount &&
                    manager.EventSynchronizer.Events.Count == 0 && startup.OpeningCalls == openingCalls &&
                    JsonSerializer.Serialize(rejectedRun.Players.Select(player => player.ToSerializable()).ToArray(), CompleteStateJson) == players &&
                    JsonSerializer.Serialize(rejectedRun.Rng.ToSerializable(), CompleteStateJson) == rng &&
                    JsonSerializer.Serialize(rejectedRun.CurrentMapPointHistoryEntry, CompleteStateJson) == history &&
                    JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson) == mapState &&
                    JsonSerializer.Serialize(JigsawCheckpoint.Slot.Get(rejectedRun), CompleteStateJson) == descriptor &&
                    HolesPageContents(rejectedRun) == page,
                    $"Native load rejects {mutation.Name} before creating any parent/child, calling the opening or mutating complete players/RNG/history/map/descriptor/holesPage.");
                combatManager.Reset(true);
                JigsawNativeSession.TryGet(rejectedRun)?.Close();
                manager.CleanUp(false);
            }
        }
        finally { io.SetFileContent(path, original.ToJsonString()); }
    }

    private static async Task PendingOptionBoundary(RunState run, EventModel model, int fight,
        Harmony patch, StartupObservation startup, RunSaveManager saves, bool replaceMap, Func<CombatRoom, bool> playerReady)
    {
        var manager = RunManager.Instance;
        var pending = pendingEntry = new PendingEntryObservation(model.CurrentOptions[fight]);
        patch.Patch(AccessTools.Method(typeof(EventOption), nameof(EventOption.Chosen)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(HoldChosenTask)));
        patch.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(ObserveEntryTask)));
        patch.Patch(AccessTools.Method(manager.EventSynchronizer.GetType(), nameof(manager.EventSynchronizer.AwaitPendingOptionTasks)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingHolesCheckpointTests), nameof(ObservePendingTasks)));
        Exception? failure = null;
        Exception? entryRejection = null;
        string? completedPlayers = null;
        string? completedRunRng = null;
        string? completedEventRng = null;
        int goldBefore = run.Players[0].Gold;
        if (!replaceMap)
            pending.CompletedEffects = async () =>
            {
                await PlayerCmd.GainGold(37, model.Owner!);
                model.Rng.NextInt(100);
                run.Rng.Niche.NextInt(100);
                completedPlayers = JsonSerializer.Serialize(run.Players.Select(player => player.ToSerializable()).ToArray(), CompleteStateJson);
                completedRunRng = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
                completedEventRng = EventRng(model);
            };
        try
        {
            // Controlled pending-option fault injection: the real AM_FIGHT callback
            // runs unchanged; its returned task remains pending in the native collection.
            manager.EventSynchronizer.ChooseLocalOption(fight);
            await UntilNative(() => pending.ChoiceReached.Task.IsCompletedSuccessfully && pending.Entry != null &&
                pending.Pending != null && !pending.Pending.IsCompleted, startup,
                "The actual child-entry coordinator awaits the real synchronizer's pending AM_FIGHT task.");
            var tree = Godot.Engine.GetMainLoop();
            for (int frame = 0; frame < 3; frame++)
                await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
            Check(pending.OriginalChoice!.IsCompletedSuccessfully && pending.WrappedChoice is { IsCompleted: false } &&
                pending.Entry is { IsCompleted: false } && run.CurrentRoom is EventRoom && run.CurrentRoomCount == 1 &&
                startup.OpeningCalls == 0 && !JigsawEventCombatCheckpoint.HasPoint(run) && !HolesPageState(run).AcceptedFight,
                "The original choice already ran, but its controlled pending task prevents checkpoint capture and child startup.");
            var session = JigsawNativeSession.TryGet(run)!;
            var oldMap = session.Map!;
            int? root = oldMap.CurrentNode;
            long entry = oldMap.CreateSave().NextEntry;
            if (!replaceMap)
            {
                var releasedParent = run.CurrentRoom;
                string releasedMapBefore = JsonSerializer.Serialize(oldMap.CreateSave(), CompleteStateJson);
                string releasedRunBefore = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
                string releasedEventBefore = EventRng(model);
                int historyRooms = run.CurrentMapPointHistoryEntry!.Rooms.Count;
                pending.ReleaseChoice.TrySetResult();
                await pending.WrappedChoice!.WaitAsync(TimeSpan.FromSeconds(5));
                await pending.Pending!.WaitAsync(TimeSpan.FromSeconds(5));
                await pending.Entry!.WaitAsync(TimeSpan.FromSeconds(5));
                await AwaitOpening(startup);
                var child = (CombatRoom)run.CurrentRoom!;
                await UntilNative(() => playerReady(child), startup, "The released child reaches its complete first native player setup.");
                Check(completedPlayers != null && completedRunRng != null && completedEventRng != null &&
                    completedRunRng != releasedRunBefore && completedEventRng != releasedEventBefore && run.Players[0].Gold == goldBefore + 37,
                    "The genuinely tracked delayed owner task completed real gold and both RNG effects before child-entry acceptance.");
                Check(ReferenceEquals(session.Map, oldMap) && ReferenceEquals(run.BaseRoom, releasedParent) && run.CurrentRoomCount == 2 &&
                    child is { Encounter: HolesEncounter, ShouldResumeParentEventAfterCombat: true } && child.ParentEventId == model.Id &&
                    startup.OpeningCalls == 1 && pending.Entry!.IsCompletedSuccessfully &&
                    JsonSerializer.Serialize(oldMap.CreateSave(), CompleteStateJson) == releasedMapBefore &&
                    oldMap.CurrentNode == root && oldMap.CreateSave().NextEntry == entry &&
                    run.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms + 1,
                    "Normal release on the same map accepts exactly one resumable Holes child and one opening, without changing the logical root or budget.");
                AssertOpening(run.Players[0]);
                Check(JigsawCheckpoint.Slot.TryGet(run, out var point) && point.EventCombat != null &&
                    point.EventCombat.Owners.Keys.ToHashSet().SetEquals(run.Players.Select(player => player.NetId)),
                    "The accepted complete point contains every actual owner after its pending task has completed.");
                Check(CheckpointOwnerRng(point.EventCombat!.Owners[model.Owner!.NetId]) == completedEventRng &&
                    HolesPageState(run).AcceptedFight && RngContents(HolesPageState(run).FightOwners[model.Owner!.NetId].Restore()) == completedEventRng,
                    "Both accepted Holes parent states capture the completed owner's full event RNG rather than its held state.");
                await JigsawNativeSaveHost.Save(null);
                var saved = saves.LoadRunSave();
                Check(saved.Success && saved.SaveData != null &&
                    JsonSerializer.Serialize(saved.SaveData.Players.ToArray(), CompleteStateJson) == completedPlayers &&
                    JsonSerializer.Serialize(saved.SaveData.SerializableRng, CompleteStateJson) == completedRunRng,
                    "The actual native checkpoint JSON contains the complete delayed-owner player state and all run RNG streams, without post-startup RNG leakage.");
                pending.ReleaseChoice.TrySetResult();
                Check(startup.OpeningCalls == 1 && run.CurrentRoomCount == 2 && ReferenceEquals(run.CurrentRoom, child),
                    "Completing an already released pending source cannot enter or apply the opening twice.");
                return;
            }
            JigsawCheckpoint.Capture(run, oldMap);
            JigsawNativeSession.Restore(run);
            var restoredSession = JigsawNativeSession.TryGet(run)!;
            var restoredMap = restoredSession.Map!;
            Check(ReferenceEquals(session, restoredSession) && !ReferenceEquals(oldMap, restoredMap) &&
                restoredMap.CurrentNode == root && restoredMap.CreateSave().NextEntry == entry &&
                ReferenceEquals(JigsawNativeSession.Current, session),
                "The public restore entry replaces the map within this same live session and native run while retaining the same accepted attempt.");
            var parentRoom = run.CurrentRoom;
            string mapBefore = JsonSerializer.Serialize(restoredMap.CreateSave(), CompleteStateJson);
            string playersBefore = JsonSerializer.Serialize(run.Players.Select(player => player.ToSerializable()).ToArray(), CompleteStateJson);
            string rngBefore = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
            string eventRngBefore = EventRng(model);
            string historyBefore = JsonSerializer.Serialize(run.CurrentMapPointHistoryEntry, CompleteStateJson);
            string parentBefore = ParentContents(model);
            string descriptorBefore = JsonSerializer.Serialize(JigsawCheckpoint.Slot.Get(run), CompleteStateJson);
            string pageBefore = HolesPageContents(run);
            pending.ReleaseChoice.TrySetResult();
            await pending.WrappedChoice!.WaitAsync(TimeSpan.FromSeconds(5));
            await pending.Pending!.WaitAsync(TimeSpan.FromSeconds(5));
            try { await pending.Entry!.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (InvalidOperationException error) when (error.Message == "事件战斗所属的拼图会话已变化。") { entryRejection = error; }
            Check(entryRejection != null,
                "The actual stale child-entry task explicitly rejects the replaced map with the existing ownership exception.");
            Check(ReferenceEquals(session.Map, restoredMap) && ReferenceEquals(run.CurrentRoom, parentRoom) &&
                run.CurrentRoomCount == 1 && run.CurrentRoom is EventRoom && startup.OpeningCalls == 0 &&
                startup.Opening == null && startup.Startup == null && !JigsawEventCombatCheckpoint.HasPoint(run) &&
                JsonSerializer.Serialize(restoredMap.CreateSave(), CompleteStateJson) == mapBefore &&
                JsonSerializer.Serialize(run.Players.Select(player => player.ToSerializable()).ToArray(), CompleteStateJson) == playersBefore &&
                JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson) == rngBefore && EventRng(model) == eventRngBefore &&
                JsonSerializer.Serialize(run.CurrentMapPointHistoryEntry, CompleteStateJson) == historyBefore &&
                ParentContents(model) == parentBefore && JsonSerializer.Serialize(JigsawCheckpoint.Slot.Get(run), CompleteStateJson) == descriptorBefore &&
                HolesPageContents(run) == pageBefore,
                "An old child-entry request cannot change the replacement map, complete players/RNG/history, parent, rollback descriptor or holesPage, or start combat after the same-attempt map was restored. " +
                $"Actual room={run.CurrentRoom?.GetType().Name}, stack={run.CurrentRoomCount}, openingCalls={startup.OpeningCalls}, " +
                $"point={JigsawEventCombatCheckpoint.HasPoint(run)}, acceptedFight={HolesPageState(run).AcceptedFight}, sameMap={ReferenceEquals(session.Map, restoredMap)}.");
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            pending.ReleaseChoice.TrySetResult();
            var observed = new[] { pending.OriginalChoice, pending.WrappedChoice, pending.Pending, pending.Entry }
                .OfType<Task>().Distinct().ToArray();
            var cleanupErrors = new List<Exception>();
            foreach (var task in observed)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) when (ReferenceEquals(task, pending.Entry) && ReferenceEquals(error, entryRejection)) { }
                catch (Exception error) { cleanupErrors.Add(error); }
            }
            pendingEntry = null;
            if (cleanupErrors.Count > 0)
            {
                var error = new AggregateException(cleanupErrors);
                Console.Error.WriteLine($"Holes pending-option task cleanup failure: {error}");
                if (failure == null) throw error;
                failure.Data["HolesPendingOptionCleanupFailure"] = error;
            }
        }
    }

    private static HolesSharedPage.PageState HolesPageState(RunState run)
    {
        var slot = (STS2RitsuLib.RunData.RunSavedData<HolesSharedPage.PageState>)AccessTools.Field(typeof(HolesSharedPage), "Sharing").GetValue(null)!;
        return slot.Get(run);
    }
    private static string HolesPageContents(RunState run) => JsonSerializer.Serialize(HolesPageState(run), CompleteStateJson);

    private static string CheckpointOwnerRng(JigsawEventCombatRng rng) =>
#if STS2_PUBLIC
        RngContents(new Rng(rng.Seed, rng.Counter));
#else
        RngContents(new Rng(rng.State));
#endif

    private static async Task VictoryRewards(RunState originalRun, RunSaveManager saves,
        StartupObservation observation, Action<RunState> setActiveRun)
    {
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        var player = originalRun.Players[0];
        var room = (CombatRoom)originalRun.CurrentRoom!;
        var originalParent = manager.EventSynchronizer.GetLocalEvent();
        var originalMap = JigsawNativeSession.TryGet(originalRun)!.Map!;
        int root = originalMap.CurrentNode!.Value;
        int steps = originalMap.Steps;
        int remaining = originalMap.RemainingSteps;
        long entry = originalMap.CreateSave().NextEntry;
        int completed = originalMap.Completed.Count;
        var previousSelector = RewardsSet.testSelector;
        var offered = new List<RewardsSet>();
        RunState currentRun = originalRun;
        Exception? failure = null;
        RewardsSet.testSelector = set =>
        {
            // Keep the genuine native reward stack open for explicit sync selection.
            set.ThrowInTestIfRewardsNotTaken = false;
            offered.Add(set);
            return Task.CompletedTask;
        };
        using var cards = HeadlessNativeApi.UseSelector(new JigsawHuntingCombatTests.TakeCardReward());
        try
        {
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 11, ValueProp.Unpowered,
                room.CombatState.Enemies.Single());
            await PlayerCmd.GainGold(19, player);
            originalRun.Rng.Niche.NextInt(100);
            originalParent.Rng.NextInt(100);
            await CreatureCmd.Kill(room.CombatState.Enemies.ToArray(), force: true);
            // Use native victory finalization after actual enemy deaths; no room/save fields are forged.
            if (!room.IsPreFinished)
                await (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes)
                    .Invoke(combatManager, null)!;
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is
                { IsPreFinished: true, ParentEventId: var parentId, ShouldResumeParentEvent: true } &&
                parentId == originalParent.Id && room.IsPreFinished &&
                JigsawCheckpoint.Slot.TryGet(originalRun, out var wonPoint) && wonPoint.EventCombat == null,
                "Actual Holes victory saves its resumable native reward room and clears the active battle rollback descriptor.");
            string postPlayer = JsonSerializer.Serialize(player.ToSerializable(), CompleteStateJson);
            string postRng = JsonSerializer.Serialize(originalRun.Rng.ToSerializable(), CompleteStateJson);
            string postEventRng = EventRng(originalParent);
            var historyType = JsonSerializationUtility.GetTypeInfo<MapPointHistoryEntry>();
            string postHistory = JsonSerializer.Serialize(JsonSerializer.Deserialize(
                JsonSerializer.Serialize(originalRun.CurrentMapPointHistoryEntry!, historyType), historyType), CompleteStateJson);

            // Victory saves before reward generation. Observe the real first offer,
            // then reload that same pre-generation point for the second offer.
            await room.OfferRoomEndRewards();
            await UntilNative(() => offered.Count == 1, observation, "The original native victory reward set is offered.");
            var firstRewards = offered.Single();
            CheckHolesRewards(firstRewards);
            string rewardIdentity = RewardContents(firstRewards);
            string generatedRng = JsonSerializer.Serialize(originalRun.Rng.ToSerializable(), CompleteStateJson);
            string generatedPlayer = JsonSerializer.Serialize(player.ToSerializable(), CompleteStateJson);
            manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
            Check(manager.RewardsSetSynchronizer.IsRewardsSetCompleted(firstRewards) &&
                firstRewards.Rewards.All(reward => !reward.SuccessfullySelected) &&
                JsonSerializer.Serialize(player.ToSerializable(), CompleteStateJson) == generatedPlayer,
                "The original generated set is genuinely skipped without paying any reward before reload.");
            combatManager.Reset(true);
            await AwaitStopped(observation);
            JigsawNativeSession.TryGet(originalRun)?.Close();
            manager.CleanUp(false);
            var rewardRun = RunState.FromSerializable(won.SaveData!);
            currentRun = rewardRun;
            setActiveRun(rewardRun);
            Check(JigsawCheckpoint.Slot.TryGet(rewardRun, out var rewardPoint) && rewardPoint.EventCombat == null,
                "The saved reward point no longer owns an active combat rollback descriptor.");
            Check(JsonSerializer.Serialize(rewardRun.Players[0].ToSerializable(), CompleteStateJson) == postPlayer &&
                JsonSerializer.Serialize(rewardRun.Rng.ToSerializable(), CompleteStateJson) == postRng,
                "Native victory JSON preserves the full postbattle player and all RNG fields before reward generation.");
            await manager.SetUpSavedSingleplayer(rewardRun, won.SaveData!);
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = player.NetId;
            await manager.GenerateMap();
            await manager.LoadIntoLatestMapCoord(CombatRoom.FromSerializable(won.SaveData!.PreFinishedRoom!, rewardRun));
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            await UntilNative(() => offered.Count == 2, observation, "Native prefinished loading offers the restored rewards.");
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var rewardPlayer = rewardRun.Players[0];
            var rewardParent = manager.EventSynchronizer.GetLocalEvent();
            var rewardMap = JigsawNativeSession.TryGet(rewardRun)!.Map!;
            var restoredRewards = offered[1];
            CheckHolesRewards(restoredRewards);
            Check(rewardRun.CurrentRoomCount == 2 && rewardRun.BaseRoom is EventRoom &&
                rewardRoom is { Encounter: HolesEncounter, IsPreFinished: true, ShouldResumeParentEventAfterCombat: true } &&
                rewardRoom.ParentEventId == originalParent.Id && observation.OpeningCalls == 1 &&
                !ReferenceEquals(firstRewards, restoredRewards) && RewardContents(restoredRewards) == rewardIdentity,
                "Reward reload regenerates the same complete native reward contents in the same parent/child stack without another opening Hook.");
            Check(JsonSerializer.Serialize(rewardPlayer.ToSerializable(), CompleteStateJson) == generatedPlayer,
                "Reward reload regenerates the same complete player state, including player RNG and potion odds, before any restored reward is claimed.");
            Check(JsonSerializer.Serialize(rewardRun.Rng.ToSerializable(), CompleteStateJson) == generatedRng,
                "The original and restored native reward offers consume all run RNG streams identically from the same saved point.");
            Check(EventRng(rewardParent) == postEventRng,
                "The parent behind restored rewards retains its complete postbattle event RNG state.");
            Check(JsonSerializer.Serialize(rewardRun.CurrentMapPointHistoryEntry, CompleteStateJson) == postHistory &&
                rewardMap.CurrentNode == root && rewardMap.Steps == steps && rewardMap.RemainingSteps == remaining &&
                rewardMap.CreateSave().NextEntry == entry && rewardMap.Completed.Count == completed,
                "Reward reload preserves the entire native history, accepted root and exploration budget without completing or charging again.");
            Check(rewardParent is FieldOfManSizedHoles && rewardParent.IsShared && !rewardParent.IsFinished &&
                rewardParent.CurrentOptions.All(option => option.IsLocked && !option.WasChosen),
                "The accepted victory parent remains unfinished behind rewards and exposes no executable initial choice.");
            string parentBefore = ParentContents(rewardParent);
            string pointBefore = JsonSerializer.Serialize(rewardPoint, CompleteStateJson);
            foreach (var option in rewardParent.CurrentOptions.Where(IsOldHolesChoice).ToArray())
            {
                await option.Chosen();
                Check(option.IsLocked && !option.WasChosen, "Retained old reward-parent actions remain inert when actually invoked.");
            }
            Check(ReferenceEquals(rewardRun.CurrentRoom, rewardRoom) && ParentContents(rewardParent) == parentBefore &&
                JsonSerializer.Serialize(rewardPlayer.ToSerializable(), CompleteStateJson) == generatedPlayer &&
                JsonSerializer.Serialize(rewardRun.Rng.ToSerializable(), CompleteStateJson) == generatedRng &&
                EventRng(rewardParent) == postEventRng && JsonSerializer.Serialize(rewardPoint, CompleteStateJson) == pointBefore &&
                offered.Count == 2 && observation.OpeningCalls == 1,
                "Old reward-parent choices cannot summon again, remove or enchant cards, pay rewards or alter complete native state.");
            int gold = rewardPlayer.Gold;
            int deck = rewardPlayer.Deck.Cards.Count;
            int potions = rewardPlayer.PotionSlots.Count(potion => potion != null);
            int potionRewards = restoredRewards.Rewards.OfType<PotionReward>().Count();
            int goldReward = restoredRewards.Rewards.OfType<GoldReward>().Sum(reward => reward.Amount);
            foreach (var reward in restoredRewards.Rewards.ToArray())
                Check(await manager.RewardsSetSynchronizer.SelectLocalReward(reward), "The real synchronized reward is successfully claimed.");
            Check(manager.RewardsSetSynchronizer.IsRewardsSetCompleted(restoredRewards) &&
                restoredRewards.Rewards.All(reward => reward.SuccessfullySelected) &&
                rewardPlayer.Gold == gold + goldReward && rewardPlayer.Deck.Cards.Count == deck + 2 &&
                rewardPlayer.PotionSlots.Count(potion => potion != null) == potions + potionRewards &&
                restoredRewards.Rewards.OfType<PotionReward>().All(reward => reward.ClaimedPotion != null &&
                    reward.ClaimedPotion.Id == reward.Potion!.Id &&
                    rewardPlayer.PotionSlots.Any(potion => potion?.Id == reward.Potion!.Id)),
                "Native gold, card and any rolled potion rewards are all delivered exactly once.");
            string claimedPlayer = JsonSerializer.Serialize(rewardPlayer.ToSerializable(), CompleteStateJson);
            string claimedRng = JsonSerializer.Serialize(rewardRun.Rng.ToSerializable(), CompleteStateJson);
            await HeadlessNativeApi.Proceed(manager);
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRun.CurrentRoom is EventRoom &&
                ReferenceEquals(manager.EventSynchronizer.GetLocalEvent(), rewardParent) && rewardParent.IsFinished &&
                rewardParent.Description!.LocEntryKey == "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description" &&
                rewardParent.CurrentOptions.All(option => option.IsLocked) &&
                JsonSerializer.Serialize(rewardPlayer.ToSerializable(), CompleteStateJson) == claimedPlayer &&
                JsonSerializer.Serialize(rewardRun.Rng.ToSerializable(), CompleteStateJson) == claimedRng &&
                observation.OpeningCalls == 1 && offered.Count == 2 && rewardMap.Steps == steps && rewardMap.RemainingSteps == remaining,
                "Actual terminal Proceed resumes the same Holes parent into its dedicated finished narrative without another reward or entry charge.");
            // The headless host has no map UI exit. Submit the existing session
            // completion entry only after native Proceed/Resume has really finished.
            JigsawNativeSession.TryGet(rewardRun)!.CompleteCurrentRoom(true);
            await UntilNative(() => rewardMap.CurrentNode == null && rewardMap.Completed.Contains(root), observation,
                "The finished native event allows its existing session completion entry to finish the root.");
            await JigsawNativeSaveHost.Save(null);
            var finished = saves.LoadRunSave();
            Check(finished.Success && finished.SaveData != null && rewardMap.Completed.Count == completed + 1 &&
                rewardMap.Steps == steps && rewardMap.RemainingSteps == remaining,
                "The completed root is saved once without another exploration charge.");
            combatManager.Reset(true);
            await AwaitStopped(observation);
            JigsawNativeSession.TryGet(rewardRun)?.Close();
            manager.CleanUp(false);
            var finishedRun = RunState.FromSerializable(finished.SaveData!);
            currentRun = finishedRun;
            setActiveRun(finishedRun);
            await manager.SetUpSavedSingleplayer(finishedRun, finished.SaveData!);
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = player.NetId;
            await manager.GenerateMap();
            await manager.LoadIntoLatestMapCoord(null);
            var finishedMap = JigsawNativeSession.TryGet(finishedRun)!.Map!;
            Check(finishedRun.CurrentRoom is MapRoom && finishedRun.CurrentRoomCount == 1 &&
                finishedMap.CurrentNode == null && finishedMap.Completed.Contains(root) &&
                finishedMap.Completed.Count == completed + 1 && finishedMap.Steps == steps && finishedMap.RemainingSteps == remaining &&
                JsonSerializer.Serialize(finishedRun.Players[0].ToSerializable(), CompleteStateJson) == claimedPlayer &&
                JsonSerializer.Serialize(finishedRun.Rng.ToSerializable(), CompleteStateJson) == claimedRng &&
                observation.OpeningCalls == 1 && offered.Count == 2,
                "The actual completed-point reload opens the logical map with the exact claimed player and RNG, without a second fight or reward set.");
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            RewardsSet.testSelector = previousSelector;
            try
            {
                foreach (var set in offered.Where(set => ReferenceEquals(set.Player.RunState, currentRun)))
                    if (!manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)) manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Holes reward-stack cleanup failure: {error}");
                if (failure == null) throw;
                failure.Data["HolesRewardStackCleanupFailure"] = error;
            }
        }
    }

    private static bool IsOldHolesChoice(EventOption option) => option.TextKey.EndsWith(".AM_FIGHT") ||
        option.TextKey.EndsWith(".RESIST") || option.TextKey.EndsWith(".ENTER_YOUR_HOLE");

    private static string ParentContents(EventModel model) => JsonSerializer.Serialize(new
    {
        model.IsFinished, Description = model.Description?.GetRawText(),
        Options = model.CurrentOptions.Select(option => new { option.TextKey, option.IsLocked, option.WasChosen }).ToArray()
    }, CompleteStateJson);

    private static string RewardContents(RewardsSet set) => JsonSerializer.Serialize(set.Rewards.Select(reward => new
    {
        // Native CardReward.ToSerializable rejects beta IsFromCombat flags;
        // compare its actual populated card contents instead of that recipe.
        Type = reward.GetType().FullName, Data = reward is CardReward ? null : reward.ToSerializable(),
        Potion = (reward as PotionReward)?.Potion?.Id,
        Cards = (reward as CardReward)?.Cards.Select(card => card.ToSerializable()).ToArray()
    }).ToArray(), CompleteStateJson);

    private static void CheckHolesRewards(RewardsSet set) => Check(set.Rewards.All(reward => reward.IsPopulated) &&
        set.Rewards.OfType<GoldReward>().Count() == 2 && set.Rewards.OfType<CardReward>().Count() == 2 &&
        !set.Rewards.OfType<RelicReward>().Any(), "Actual Holes rewards retain doubled native gold/card rewards and no relic.");

    private static async Task AwaitStopped(StartupObservation observation)
    {
        if (observation.Startup == null) return;
        try { await observation.Startup.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { } // The observed combat was explicitly reset before awaiting teardown.
    }

    private static async Task AwaitOpening(StartupObservation observation)
    {
        await UntilNative(() => observation.Opening != null, observation, "The actual combat coordinator reached its original opening Hook.");
        await observation.Opening!.WaitAsync(TimeSpan.FromSeconds(5));
        Check(observation.Opening.IsCompletedSuccessfully && observation.OpeningCalls == 1 && observation.Startup != null,
            "The same native coordinator's observed opening Task completed once, without a test-issued extra Hook.");
    }

    private static async Task UntilNative(Func<bool> ready, StartupObservation observation, string reason)
    {
        var tree = Godot.Engine.GetMainLoop();
        for (int frame = 0; frame < 300; frame++)
        {
            if (observation.Startup is { IsFaulted: true } failed) await failed;
            if (ready()) return;
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        }
        Check(ready(), reason);
    }

    private static void AssertOpening(Player player)
    {
        var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => ReferenceEquals(entry.Actor, player.Creature) &&
                entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();
        Check(player.Creature.GetPowerAmount<WeakPower>() == 1 && player.Creature.GetPowerAmount<FrailPower>() == 1 &&
            player.Creature.GetPowerAmount<VulnerablePower>() == 1 && received.Length == 3 &&
            received.Select(entry => entry.Power.GetType()).SequenceEqual(new[] { typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower) }) &&
            received.All(entry => entry.Amount == 1 && entry.Applier == null),
            "This actual combat opening grants each ordinary Hive debuff once, without retained battle stacks or corrected duplicate commands.");
    }

    private static string EventRng(EventModel model) => RngContents(model.Rng);
    private static string MonsterRngContents(CombatRoom room) => JsonSerializer.Serialize(
        room.CombatState.Enemies.Select(enemy => RngContents(enemy.Monster!.Rng)).ToArray(), CompleteStateJson);
    private static string RngContents(Rng rng) =>
#if STS2_PUBLIC
        JsonSerializer.Serialize(new { rng.Seed, rng.Counter }, CompleteStateJson);
#else
        JsonSerializer.Serialize(rng.ToSerializable(), CompleteStateJson);
#endif

    private static RunState SharedHolesRun()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                seed: "HUNTING_85_HOLES_CHECKPOINT" + seed);
            run.CurrentActIndex = 1;
            var rng = new Rng(run.Rng.Seed);
            rng.NextInt(100);
            if (rng.NextInt(100) < 33) return run;
        }
        throw new InvalidOperationException("No deterministic native shared Holes seed was found.");
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool safeRoot = false)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss) continue;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Unknown && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(safeRoot ? path.Count : 0));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentNode == target && map.CurrentRoomHunted == !safeRoot && (!safeRoot || map.RemainingSteps == 0),
            "Public room-entry transitions establish the intended safe or hunted Unknown root.");
        return map;
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
}
