using AllMatters.Jigsaw;
using AllMatters.Relics;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Saves.Validation;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class JigsawHuntingWaterfallRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Waterfall fresh rolls back revived explosion state then naturally revives once and restores nonempty victory rewards and Proceed", Fresh)];

    private static readonly JsonSerializerOptions CompleteJson = new() { IncludeFields = true };
    private static Observation? observation;

    private sealed class Observation
    {
        internal readonly List<Task> Startups = [];
        internal readonly List<Task> Openings = [];
        internal readonly List<Task> Offers = [];
        internal readonly List<RewardsSet> Rewards = [];
        internal int ReadyRound;
        internal readonly List<StartupCaller> Callers = [];
        internal StartupCaller? PendingCaller;
    }

    private sealed class StartupCaller(CombatManager manager, RunState run, CombatRoom room, Task? previousTask, int captureCount)
    {
        internal readonly CombatManager Manager = manager;
        internal readonly RunState Run = run;
        internal readonly CombatRoom Room = room;
        internal readonly CombatState Combat = room.CombatState;
        internal readonly Task? PreviousTask = previousTask;
        internal readonly int CaptureCount = captureCount;
        internal Task? Task;
        internal bool Returned;
    }

    private static void BeforeRoomLoaded(CombatManager manager, CombatRoom room)
    {
        var state = observation!;
        var run = RunManager.Instance.DebugOnlyGetState()!;
        Check(state.PendingCaller == null && ReferenceEquals(manager, CombatManager.Instance) &&
            ReferenceEquals(run.CurrentRoom, room) && ReferenceEquals(room.CombatState.RunState, run),
            "The original room-loaded call belongs to this exact live run, room and combat, without an overlapping old caller.");
        Task? previousTask = null;
#if !STS2_PUBLIC
        previousTask = (Task?)AccessTools.Field(typeof(CombatManager), "_turnLoopTask").GetValue(manager);
#endif
        var caller = new StartupCaller(manager, run, room, previousTask, state.Startups.Count);
        state.Callers.Add(caller);
        state.PendingCaller = caller;
    }
    private static void AfterRoomLoaded(CombatRoom room)
    {
        var state = observation!;
        var caller = state.PendingCaller ?? throw new InvalidOperationException("The original room-loaded return has no matching observed caller.");
        Check(ReferenceEquals(caller.Manager, CombatManager.Instance) &&
            ReferenceEquals(caller.Run, RunManager.Instance.DebugOnlyGetState()) && ReferenceEquals(caller.Run.CurrentRoom, room) &&
            ReferenceEquals(caller.Room, room) && ReferenceEquals(caller.Combat, room.CombatState) &&
            ReferenceEquals(caller.Combat.RunState, caller.Run),
            "The returning original room-loaded call retains its exact run, room, combat and manager owner.");
#if STS2_PUBLIC
        Check(state.Startups.Count == caller.CaptureCount + 1 && caller.Task != null && ReferenceEquals(state.Startups.Last(), caller.Task),
            "The one original public room-loaded call captures exactly the same single StartCombatInternal Task passed to RunSafely.");
#else
        // This is beta's sole startup observer, not a missing-capture fallback.
        // Native AfterCombatRoomLoaded stores its original RunTurnLoopAfter Task
        // before returning. Read that exact field at the real room caller.
        var task = (Task?)AccessTools.Field(typeof(CombatManager), "_turnLoopTask").GetValue(caller.Manager);
        Check(state.Startups.Count == caller.CaptureCount && task != null && !ReferenceEquals(task, caller.PreviousTask),
            "The single original beta room-loaded call replaces its previous loop with one new native Task and has no second observation source.");
        caller.Task = task;
        state.Startups.Add(task!);
        Check(ReferenceEquals(caller.Task, AccessTools.Field(typeof(CombatManager), "_turnLoopTask").GetValue(caller.Manager)),
            "The retained beta startup is the actual native field Task of this caller, not a task wrapper or a restarted coordinator.");
#endif
        caller.Returned = true;
        state.PendingCaller = null;
    }
    private static IEnumerable<CodeInstruction> ObserveRoomLoadedCaller(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatRoom), "StartCombat");
        var machine = start.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var owner = machine.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(CombatRoom));
        var loaded = AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(loaded)) { yield return instruction; continue; }
            matches++;
            // Duplicate only the existing receiver, then observe its actual
            // enclosing room. The original call executes once, unchanged.
            var duplicate = new CodeInstruction(OpCodes.Dup);
            duplicate.labels.AddRange(instruction.labels);
            instruction.labels.Clear();
            yield return duplicate;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(BeforeRoomLoaded)));
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(AfterRoomLoaded)));
        }
        Check(matches == 1, "The real combat-room state machine contains exactly one original room-loaded call to observe.");
    }
#if STS2_PUBLIC
    private static void CaptureStartup(Task task)
    {
        var state = observation!;
        var caller = state.PendingCaller ?? throw new InvalidOperationException("The public startup Task has no matching original room-loaded caller.");
        Check(caller.Task == null && ReferenceEquals(caller.Run, RunManager.Instance.DebugOnlyGetState()) &&
            ReferenceEquals(caller.Run.CurrentRoom, caller.Room) && ReferenceEquals(caller.Room.CombatState, caller.Combat),
            "Public captures only the single original Task inside the exact active native room caller.");
        caller.Task = task;
        state.Startups.Add(task);
    }
#endif
    private static void CaptureOpening(Task __result) => observation?.Openings.Add(__result);
    private static void CaptureOffer(Task __result) => observation?.Offers.Add(__result);
    private static Task ObserveRewards(RewardsSet set)
    {
        set.ThrowInTestIfRewardsNotTaken = false;
        observation!.Rewards.Add(set);
        return Task.CompletedTask;
    }
    private static bool SkipProfile() => false;
    private static bool SeenFtue(ref bool __result) { __result = true; return false; }
#if STS2_PUBLIC
    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
            "StartCombatInternal");
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(CaptureStartup)));
        }
        Check(matches == 1, "Observe the single original coordinator Task; never invoke a second combat startup.");
    }

#endif
    private sealed class LoadListener(JsonNode expectedBoundary) : ILoadRunLobbyListener
    {
        internal LoadRunLobby Lobby = null!;
        internal Task? Loading;
        internal RunState? Native;
        public void BeginRun() => Loading = Load();
        private async Task Load()
        {
            Native = RunState.FromSerializable(Lobby.Run);
            await RunManager.Instance.SetUpSavedMultiplayer(Native, Lobby);
            // FromSerializable leaves this derived field at zero. Native room
            // loading computes it later in EnterMapPointInternal, not setup.
            Check(Native.ActFloor == 0, "Fresh native saved-run setup starts with zero ActFloor before actual map-room loading.");
            var expectedSetup = expectedBoundary.DeepClone();
            expectedSetup[nameof(RunState.ActFloor)] = 0;
            Equal(Boundary(Native), expectedSetup, "New lobby setup restores every independently sampled save field at its native pre-room phase.");
            Check(Native.CurrentActIndex == 0 && Native.Act is Underdocks && Native.Act.BossEncounter is WaterfallGiantBoss && !Native.Act.HasSecondBoss,
                "The saved three-act run retains its original single Underdocks WaterfallGiant selection at act zero without setting a boss during restoration.");
            RunManager.Instance.CombatReplayWriter.IsEnabled = false;
            RunManager.Instance.Launch();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(Lobby.Run.PreFinishedRoom is { IsPreFinished: true } finished
                ? CombatRoom.FromSerializable(finished, Native) : null);
            Check(Native.ActFloor == expectedBoundary[nameof(RunState.ActFloor)]!.GetValue<int>(),
                "Actual native map-room loading reconstructs ActFloor to the independently sampled original boundary value.");
        }
#if STS2_PUBLIC
        public void PlayerConnected(ulong playerId) { }
#else
        public void PlayerConnected(LoadRunLobbyPlayer player) { }
#endif
        public void PlayerReadyChanged(ulong playerId) { }
        public void RemotePlayerDisconnected(ulong playerId) { }
        public void LocalPlayerDisconnected(NetErrorInfo info) { }
        public Task<bool> ShouldAllowRunToBegin() => Task.FromResult(true);
    }

    private static async Task Fresh()
    {
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        var state = new Observation();
        var patch = new Harmony("AllMatters.Tests.WaterfallGiantFresh");
        var host = new Harmony("AllMatters.TestHost");
        var previousSave = JigsawNativeSaveHost.Save;
        var previousSelector = RewardsSet.testSelector;
        var previousProgress = SaveManager.Instance.Progress;
        var previousProgressState = JsonSerializer.SerializeToNode(previousProgress.ToSerializable(), CompleteJson)!;
        ulong? previousNetId = LocalContext.NetId;
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var previousSize = viewport.Size;
        IDisposable? atlases = null;
        LoadRunLobby? lobby = null;
        Task? loading = null;
        bool hostTouched = false;
        Exception? bodyFailure = null;
        var cleanupFailures = new List<Exception>();
        void Cleanup(Action action) { try { action(); } catch (Exception error) { cleanupFailures.Add(error); } }
        try
        {
            // ToSerializable contains mutable statistic objects. A complete JSON
            // round-trip separates them before native profile reconstruction.
            var progressData = JsonSerializer.Deserialize<SerializableProgress>(previousProgressState.ToJsonString(), CompleteJson)!;
            var progressContext = new DeserializationContext();
            var isolatedProgress = ProgressState.FromSerializable(progressData, progressContext);
            Check(!ReferenceEquals(isolatedProgress, previousProgress) && !progressContext.Errors.Any(),
                "The native profile clone is independent and has no deserialization errors.");
            Equal(JsonSerializer.SerializeToNode(isolatedProgress.ToSerializable(), CompleteJson)!, previousProgressState,
                "The isolated profile begins with every original serialized field unchanged.");
            SaveManager.Instance.Progress = isolatedProgress;
            // One local run owner in one process; each restore builds a fresh
            // lobby/run/session. No remote replica or process isolation claim.
            // Save IO is memory-only. Profile disk/replay/combat-win bookkeeping
            // and first-time UI are isolated; reward/discovery commands remain
            // original. All shared mutations are covered by this finally.
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            observation = state;
            RewardsSet.testSelector = ObserveRewards;
            viewport.Size = new Vector2I(1920, 1080);
            LocalContext.NetId = 1;
            foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(CaptureOpening)));
#if STS2_PUBLIC
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(ObserveStartup)));
#endif
            var nativeStart = AccessTools.Method(typeof(CombatRoom), "StartCombat");
            var machine = nativeStart.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(machine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(ObserveRoomLoadedCaller)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingWaterfallRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            ModelDb.Inject(typeof(JigsawHuntingWaterfallTests.HiddenWaterfallState));
            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Underdocks>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING84_WATERFALL_FRESH");
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("waterfall-fresh-memory-only");
            var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
            JsonNode? beforeRoll = null;
            JsonNode? victoryBoundary = null;
            JsonNode? victoryMap = null;
            JsonNode? victoryRoom = null;
            bool originalEntry = true;
            JigsawNativeSaveHost.Save = async room =>
            {
                var current = manager.DebugOnlyGetState()!;
                if (originalEntry && current.CurrentRoomCount == 0 && JigsawNativeSession.Current?.Map?.PendingEntry != null)
                    beforeRoll = Boundary(current);
                if (room is CombatRoom { IsPreFinished: true } won)
                {
                    victoryBoundary = Boundary(current);
                    victoryMap = Map(current);
                    victoryRoom = JsonSerializer.SerializeToNode(won.ToSerializable(), CompleteJson);
                }
                await saves.SaveRun(manager.ToSave(room), false);
            };
            manager.GenerateRooms();
            run.Act.SetBossEncounter(ModelDb.Encounter<WaterfallGiantBoss>());
            run.Act.SetSecondBossEncounter(null);
            Check(run.Act.ToSave().SerializableRooms.BossEncountersVisited == 0 &&
                run.Act.PullNextEncounter(RoomType.Boss) is WaterfallGiantBoss && !run.Act.HasSecondBoss,
                "The selected single native WaterfallGiant and independently declared unconsumed Boss queue agree before actual entry.");
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var (map, target) = BeforeBoss();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 0,
                "The source logical Boss root belongs to native Underdocks at act zero in the legal three-act run.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 0 && run.Act is Underdocks && run.CurrentRoom is CombatRoom { Encounter: WaterfallGiantBoss, ParentEventId: null, RoomType: RoomType.Boss } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target && accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter selects the punished native WaterfallGiant Boss encounter and independently samples its actual before-roll source.");
            Check(run.Act.ToSave().SerializableRooms.BossEncountersVisited == 1,
                "The actual original native Boss entry advances its persisted encounter-consumption count exactly once.");
            var originalRoom = (CombatRoom)run.CurrentRoom!;
            var originalBoss = BossIn(originalRoom);
            var originalEnemies = originalRoom.CombatState.Enemies.ToArray();
            CheckOpening(originalRoom);
            var opening = Opening(run, originalRoom);
            int steps = originalSession.Map!.Steps;
            int remaining = originalSession.Map.RemainingSteps;
            long entry = originalSession.Map.CreateSave().NextEntry;
            int completed = originalSession.Map.Completed.Count;

            await FirstRevival(originalRoom);
            var sourceRegen = originalBoss.Creature.GetPower<RegenPower>()!;
            int sourceRegenAmount = sourceRegen.Amount;
            var sourceHidden = originalBoss.Creature.GetPower<JigsawHuntingWaterfallTests.HiddenWaterfallState>()!;
            int sourceHiddenAmount = sourceHidden.Amount;
            Check(!await combatManager.CheckWinCondition() && !originalRoom.IsPreFinished && victoryBoundary == null && state.Rewards.Count == 0,
                "The first fully returned lethal damage revives the living required giant; the original win check refuses an early victory and no reward save exists.");
            await CaptureExplosion(originalBoss);
            Check(originalBoss.Creature.GetPowerAmount<ArtifactPower>() == 1,
                "Source save deliberately retains its one usable hunting Artifact, alongside old Regen and hidden test state.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 7, ValueProp.Unpowered, originalBoss.Creature);
            await PlayerCmd.GainGold(19, run.Players[0]);
            var movedCard = PileType.Hand.GetPile(run.Players[0]).Cards.First();
            await CardPileCmd.Add(movedCard, PileType.Discard);
            Check(!PileType.Hand.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Discard.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                !JsonNode.DeepEquals(Opening(run, originalRoom)["CombatPlayers"], opening["CombatPlayers"]),
                "Actual hand-to-discard movement changes ordered combat piles before save; a permanent-deck-only restore cannot satisfy this oracle.");
            run.Rng.Niche.NextInt(100);
            run.Players[0].PlayerRng.Shops.NextInt(100);
            originalBoss.Rng.NextInt(100);
            Check(!JsonNode.DeepEquals(Opening(run, originalRoom), opening),
                "Infinite HP, old explosion flag, captured damage fifteen, pressure index six, retained powers and real player/pile/RNG changes differ from the independently sampled original opening.");
            Check(ReferenceEquals(originalBoss.Creature.GetPower<RegenPower>(), sourceRegen) && sourceRegen.Amount == sourceRegenAmount &&
                ReferenceEquals(originalBoss.Creature.GetPower<JigsawHuntingWaterfallTests.HiddenWaterfallState>(), sourceHidden) && sourceHidden.Amount == sourceHiddenAmount &&
                originalBoss.Creature.GetPowerAmount<ArtifactPower>() == 1,
                "Immediately before actual active serialization, AboutToBlow and all player/pile/RNG changes retain the exact independently sampled revived Regen and hidden instances and amounts, plus Artifact one.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active WaterfallGiant save contains the native whole before-roll point, not the changed battle.");
            await StopCombat(state);
            originalSession.Close();
            manager.CleanUp(false);
            state.ReadyRound = 0;

            var first = new LoadListener(beforeRoll!);
            lobby = new LoadRunLobby(new NetSingleplayerGameService(), first, active.SaveData!);
            first.Lobby = lobby;
            lobby.AddLocalHostPlayer();
            lobby.SetReady(true);
            loading = first.Loading;
            Check(loading != null, "Only the fresh lobby ready flow starts actual saved-run setup.");
            await loading!.WaitAsync(TimeSpan.FromSeconds(15));
            await OpeningReady(state, 2);
            var restored = first.Native!;
            var restoredSession = JigsawNativeSession.Current!;
            var restoredRoom = (CombatRoom)restored.CurrentRoom!;
            var boss = BossIn(restoredRoom);
            Check(!ReferenceEquals(restored, run) && !ReferenceEquals(restoredSession, originalSession) &&
                !ReferenceEquals(restoredRoom, originalRoom) && !ReferenceEquals(boss, originalBoss) &&
                restoredRoom.CombatState.Enemies.All(enemy => originalEnemies.All(original => !ReferenceEquals(enemy, original) &&
                    !ReferenceEquals(enemy.Monster, original.Monster))),
                "Fresh lobby restoration creates new run, session, room and the giant creature/model identities.");
            CheckOpening(restoredRoom);
            Check(restored.Act.ToSave().SerializableRooms.BossEncountersVisited == 1,
                "Fresh saved-run loading reconstructs the original single native Boss exactly once, without consuming another queue entry.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening exactly restores original complete players, ordered five piles, energy/block/powers, run/player/monster RNG, acts, full map/history, slots, HP, powers, intents and complete StateLog, HP display, revival fields, round/side and checkpoint act.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same punished Boss entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            Equal(Opening(restored, restoredRoom), opening, "Duplicate restored destination input cannot reopen combat or charge another logical entry.");

            int rewardsBeforeVictory = state.Rewards.Count;
            await FirstRevival(restoredRoom);
            Check(!await combatManager.CheckWinCondition() && !combatManager.IsEnding && !restoredRoom.IsPreFinished &&
                victoryBoundary == null && state.Rewards.Count == rewardsBeforeVictory,
                "Fresh first lethal damage completes its own revival and still cannot prematurely save victory or offer rewards.");
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), boss.Creature, 2, restored.Players[0].Creature, null);
            Check(!boss.Creature.HasPower<WeakPower>() && !boss.Creature.HasPower<ArtifactPower>(),
                "The single newly granted Artifact actually blocks one new Weak and is consumed; no old Artifact was stitched into the fresh phase.");
            await CaptureExplosion(boss);
            int capturedDamage = Field<int>(boss, "_steamEruptionDamage");
            int healthBeforeExplosion = restored.Players[0].Creature.CurrentHp;
            Check(restored.Players[0].Creature.Block == 0 && restored.Players[0].Creature.Powers.Count == 0 && capturedDamage == 15,
                "The original final explosion begins against the unblocked unpowered player with independently sampled native damage fifteen.");
            await boss.PerformMove();
            Check(restored.Players[0].Creature.CurrentHp == healthBeforeExplosion - capturedDamage &&
                boss.Creature.IsDead && !restoredRoom.CombatState.ContainsCreature(boss.Creature) &&
                !boss.Creature.HasPower<ArtifactPower>() && !boss.Creature.HasPower<RegenPower>() &&
                !boss.Creature.HasPower<JigsawHuntingWaterfallTests.HiddenWaterfallState>() &&
                restoredRoom.CombatState.Enemies.Count == 0 && combatManager.IsEnding,
                "Original Explode deals the exact captured damage and its own native Kill removes the giant and all retained revival powers; only this final death permits Ending.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns the native BurningBlood that will produce the independent victory healing effect.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts the genuine final explosion death and finalizes victory.");
            await Until(() => restoredRoom.IsPreFinished && victoryBoundary != null,
                "Actual enemy deaths and the original victory coordinator naturally save the prefinished room.");
            await restoredSession.WaitForSave();
            Check(restored.Players[0].Creature.CurrentHp == expectedVictoryHealth && restored.Players[0].Creature.Powers.Count == 0 &&
                restored.Players[0].Creature.Block == 0,
                "Original Player.AfterCombatEnd removes the settled combat powers and block, then native BurningBlood heals exactly six before the victory save.");
            CheckVictoryHunting(restored);
            Check(state.Rewards.Count == rewardsBeforeVictory && restoredRoom.CombatState.Enemies.All(enemy => enemy.IsDead),
                "The native Boss victory saves before generating any reward.");
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: null } &&
                won.SaveData.PreFinishedRoom.EncounterId == originalRoom.Encounter.Id,
                "The original victory flow saves a genuine top-level WaterfallGiant reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original non-final-act WaterfallGiant Boss room generates and offers its real nonempty native reward set.");
            var firstRewards = state.Rewards.Last();
            CheckRewards(firstRewards);
            var rewardContents = RewardContents(firstRewards);
            var generatedBoundary = Boundary(restored);
            var generatedMap = Map(restored);
            var sourcePlayer = JsonSerializer.SerializeToNode(restored.Players[0].ToSerializable(), CompleteJson)!;
            manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
            await FinishOffers(state);
            Check(firstRewards.Rewards.All(reward => !reward.SuccessfullySelected), "The original nonempty set is genuinely skipped before reward-point restoration.");
            Equal(JsonSerializer.SerializeToNode(restored.Players[0].ToSerializable(), CompleteJson)!, sourcePlayer,
                "Source skip pays no inventory; native skipped-card history is allowed only after the independently sampled generation boundary.");
            await StopCombat(state);
            restoredSession.Close();
            lobby.CleanUp(disconnectSession: false);
            lobby = null;
            manager.CleanUp(false);
            int openingCount = state.Openings.Count;
            int startupCount = state.Startups.Count;

            var rewardListener = new LoadListener(victoryBoundary!);
            lobby = new LoadRunLobby(new NetSingleplayerGameService(), rewardListener, won.SaveData!);
            rewardListener.Lobby = lobby;
            lobby.AddLocalHostPlayer();
            lobby.SetReady(true);
            loading = rewardListener.Loading;
            Check(loading != null, "A second new lobby ready flow owns native reward-point restoration.");
            await loading!.WaitAsync(TimeSpan.FromSeconds(15));
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 2,
                "Native prefinished room loading regenerates the saved Boss encounter's reward offer.");
            var rewardRun = rewardListener.Native!;
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var secondRewards = state.Rewards.Last();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRoom is { IsPreFinished: true, ParentEventId: null } &&
                rewardRoom.CombatState.Enemies.Count == 0 && state.Openings.Count == openingCount && state.Startups.Count == startupCount &&
                !ReferenceEquals(rewardRun, restored) && !ReferenceEquals(secondRewards, firstRewards),
                "The second fresh lobby restores one top-level prefinished room with no combat opening or rebuilt enemies.");
            CheckRewards(secondRewards);
            CheckVictoryHunting(rewardRun);
            Check(rewardRun.Act.ToSave().SerializableRooms.BossEncountersVisited == 1,
                "Native prefinished reward loading preserves the original consumed Boss count; asset preloading is not another Boss entry.");
            // Native prefinished loading still generates encounter models to preload
            // assets; zero live enemies and unchanged coordinator/opening counts
            // prove that this did not start a second battle.
            Equal(JsonSerializer.SerializeToNode(rewardRoom.ToSerializable(), CompleteJson)!, victoryRoom!, "Reward loading preserves the original saved native room descriptor.");
            Equal(Map(rewardRun), victoryMap!, "Reward loading preserves the independently sampled victory root, full map, steps, budget and entry identity.");
            Equal(Map(rewardRun), generatedMap, "Reward generation itself never changes exploration state.");
            Equal(Boundary(rewardRun), generatedBoundary, "Fresh reward generation retains the original complete players, all RNG, acts and history at the matching post-generation stage.");
            Equal(RewardContents(secondRewards), rewardContents, "Fresh native Boss rewards exactly reproduce the original complete gold/card/potion contents.");
            await ClaimAndProceed(rewardRun, secondRewards, state, target, steps, remaining, entry, completed);
            Console.WriteLine($"WATERFALL_FRESH scenario=single original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} reward-opening=0 rewards={secondRewards.Rewards.Count} reward-sync=claimed duplicate=rejected proceed=original steps={steps} remaining={remaining} entry={entry}");
        }
        catch (Exception error) { bodyFailure = error; throw; }
        finally
        {
            try { await FinishOffers(state); } catch (Exception error) { cleanupFailures.Add(error); }
            try { await StopCombat(state); } catch (Exception error) { cleanupFailures.Add(error); }
            if (loading != null && !loading.IsCompleted)
                try { await loading.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception error) { cleanupFailures.Add(error); }
            Cleanup(() => combatManager.TurnStarted -= Ready);
            Cleanup(() => lobby?.CleanUp(disconnectSession: false));
            Cleanup(() => JigsawNativeSession.Current?.Close());
            Cleanup(() => manager.CleanUp(false));
            if (hostTouched)
            {
                Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
                Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            }
            Cleanup(() => patch.UnpatchAll(patch.Id));
            observation = null;
            JigsawNativeSaveHost.Save = previousSave;
            RewardsSet.testSelector = previousSelector;
            LocalContext.NetId = previousNetId;
            Cleanup(() => viewport.Size = previousSize);
            Cleanup(() => atlases?.Dispose());
            Cleanup(() => SaveManager.Instance.Progress = previousProgress);
            Cleanup(() => Check(ReferenceEquals(SaveManager.Instance.Progress, previousProgress),
                "Cleanup restores the exact original shared profile reference after actual reward discovery."));
            Cleanup(() => Equal(JsonSerializer.SerializeToNode(previousProgress.ToSerializable(), CompleteJson)!, previousProgressState,
                "The original profile's complete serialized content is untouched by this test's native rewards and cleanup."));
            if (cleanupFailures.Count > 0)
                throw new AggregateException("WaterfallGiant fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
        }
        void Ready(CombatState combat)
        {
            if (ReferenceEquals(combat.RunState, manager.DebugOnlyGetState()) && combat.CurrentSide == CombatSide.Player)
                state.ReadyRound = combat.RoundNumber;
        }
    }

    private static async Task OpeningReady(Observation state, int expected)
    {
        await Until(() => state.Openings.Count == expected && state.Openings.Last().IsCompleted && state.ReadyRound > 0 &&
            RunManager.Instance.DebugOnlyGetState()!.Players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5),
            "The single native opening completes actual first-turn setup and drawing before state sampling.");
        await state.Openings.Last();
        if (state.Startups.Count != expected)
        {
            string nativeTask = "public caller task capture";
#if !STS2_PUBLIC
            // Read only on failure so normal execution retains its original
            // observation path and timing. Do not fill a missing capture.
            var task = (Task?)AccessTools.Field(typeof(CombatManager), "_turnLoopTask").GetValue(CombatManager.Instance);
            nativeTask = task == null ? "null" : $"id={task.Id},status={task.Status}";
#endif
            Check(false, $"Each active room naturally starts exactly one original combat coordinator. expected={expected}, actual={state.Startups.Count}, nativeTask=[{nativeTask}]");
        }
        // A public startup finishes after first-turn setup; it is not beta's
        // long-running turn loop. Observe each real call and exact returned Task,
        // allowing only the explicit successful completion cache to be shared.
        void Fail(string part) => Check(false,
            $"Native startup observation failed: {part}. expected={expected}, callers={state.Callers.Count}, captured={state.Startups.Count}, " +
            "tasks=[" + string.Join(";", state.Callers.Select((caller, index) => caller.Task == null
                ? $"caller={index},task=null,returned={caller.Returned}"
                : $"caller={index},id={caller.Task.Id},status={caller.Task.Status},returned={caller.Returned}")) + "]");
        if (state.Callers.Count != expected) Fail("original room-loaded caller count");
        if (state.PendingCaller != null) Fail("original room-loaded caller has not returned");
        for (int index = 0; index < state.Callers.Count; index++)
        {
            var caller = state.Callers[index];
            var task = caller.Task;
            if (!caller.Returned || task == null) Fail($"caller {index} did not retain its returned original Task");
            if (caller.CaptureCount != index || !ReferenceEquals(task, state.Startups[index]))
                Fail($"caller {index} did not observe exactly its own single original Task at the matching capture position");
            for (int previous = 0; previous < index; previous++)
            {
                var prior = state.Callers[previous];
                if (ReferenceEquals(caller.Run, prior.Run)) Fail($"callers {previous}/{index} reused a native run");
                if (ReferenceEquals(caller.Room, prior.Room)) Fail($"callers {previous}/{index} reused a native room");
                if (ReferenceEquals(caller.Combat, prior.Combat)) Fail($"callers {previous}/{index} reused a native combat");
                if (!ReferenceEquals(task, prior.Task)) continue;
#if STS2_PUBLIC
                if (!ReferenceEquals(task, Task.CompletedTask) || !ReferenceEquals(prior.Task, Task.CompletedTask) ||
                    !task!.IsCompletedSuccessfully || !prior.Task!.IsCompletedSuccessfully)
                    Fail($"public callers {previous}/{index} shared a Task other than the successful Task.CompletedTask cache");
                Console.WriteLine($"WATERFALL_FRESH public-startup-completed-cache callers={previous}/{index} same-Task.CompletedTask=true completed-successfully=true");
#else
                Fail($"beta callers {previous}/{index} reused the same long-running native turn-loop Task");
#endif
            }
        }
    }

    private static async Task Perform(MonsterModel monster) { await monster.PerformMove(); monster.RollMove(monster.CombatState.PlayerCreatures); }
    private static WaterfallGiant BossIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<WaterfallGiant>().Single();
    private static T Field<T>(WaterfallGiant giant, string name) => (T)AccessTools.Field(typeof(WaterfallGiant), name).GetValue(giant)!;

    private static async Task FirstRevival(CombatRoom room)
    {
        var giant = BossIn(room);
        var target = giant.Creature;
        var player = room.CombatState.Players.Single();
        var context = new ThrowingPlayerChoiceContext();
        Check(giant.NextMove.Id == "PRESSURIZE_MOVE" && target.IsAlive && !Field<bool>(giant, "_isAboutToBlow"),
            "Each real first revival begins at the original living Pressurize phase, never a direct Trigger call.");
        await Perform(giant);
        Check(target.GetPowerAmount<SteamEruptionPower>() == 15 && giant.NextMove.Id == "STOMP_MOVE" && !target.HasPower<ArtifactPower>(),
            "Original Pressurize grants Steam fifteen and advances the actual state machine to Stomp before lethal damage.");
        await PowerCmd.Apply<WeakPower>(context, target, 2, player.Creature, null);
        await PowerCmd.Apply<RegenPower>(context, target, 2, target, null);
        await PowerCmd.Apply<JigsawHuntingWaterfallTests.HiddenWaterfallState>(context, target, 6, target, null);
        var regen = target.GetPower<RegenPower>()!;
        int regenAmount = regen.Amount;
        var hidden = target.GetPower<JigsawHuntingWaterfallTests.HiddenWaterfallState>()!;
        Check(target.GetPowerAmount<WeakPower>() == 2 && regenAmount > 0 && hidden.Amount == 6 &&
            !hidden.IsVisible && hidden.TypeForCurrentAmount == PowerType.Debuff,
            "A visible Weak, positive native Regen and exact hidden synthetic debuff provide independent filtering counterexamples before death.");
        int history = CombatManager.Instance.History.Entries.Count();
        // Await the complete Damage/Kill/revival/death-cleanup chain. The
        // intermediate Trigger task cannot prove its powers survive Kill.
        await CreatureCmd.Damage(context, target, target.CurrentHp, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        Check(target.IsAlive && room.CombatState.ContainsCreature(target) && target.CurrentHp == 999999999 &&
            target.MaxHp == 999999999 && target.HpDisplay == HpDisplay.InfiniteWithoutNumbers &&
            Field<bool>(giant, "_isAboutToBlow") && giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE" &&
            giant.NextMove.Intents.Single() is StunIntent && target.GetPowerAmount<SteamEruptionPower>() == 15 &&
            !target.Powers.Any(power => power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff),
            "The fully returned real lethal hit leaves the living infinite-HP native about-to-blow phase, Steam fifteen and zero visible debuffs.");
        Check(ReferenceEquals(target.GetPower<RegenPower>(), regen) && regen.Amount == regenAmount &&
            ReferenceEquals(target.GetPower<JigsawHuntingWaterfallTests.HiddenWaterfallState>(), hidden) && hidden.Amount == 6,
            "Complete native death cleanup preserves the exact positive Regen and hidden synthetic debuff instances and amounts in the hunted revival.");
        var grants = CombatManager.Instance.History.Entries.Skip(history).OfType<PowerReceivedEntry>()
            .Where(entry => ReferenceEquals(entry.Actor, target) && entry.Power is ArtifactPower).ToArray();
        Check(target.GetPowerAmount<ArtifactPower>() == 1 && grants.Length == 1 && grants[0].Amount == 1,
            "Each actual first revival returns with exactly one native Artifact grant of one, with no reused old grant.");
    }

    private static async Task CaptureExplosion(WaterfallGiant giant)
    {
        int steam = giant.Creature.GetPowerAmount<SteamEruptionPower>();
        Check(steam == 15 && giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE", "Actual AboutToBlow consumes this phase's independently sampled nonzero Steam.");
        await Perform(giant);
        Check(!giant.Creature.HasPower<SteamEruptionPower>() && giant.NextMove.Id == "EXPLODE_MOVE" &&
            Field<int>(giant, "_steamEruptionDamage") == steam && Field<int>(giant, "_pressureBuildupIdx") == 6 &&
            Field<bool>(giant, "_isAboutToBlow") && giant.Creature.HpDisplay == HpDisplay.InfiniteWithoutNumbers &&
            giant.ShouldDisappearFromDoom && !giant.ShouldFadeAfterDeath,
            "Original AboutToBlow captures damage fifteen, removes Steam and directly sets backend pressure six before the original Roll reaches Explode.");
    }

    private static void CheckOpening(CombatRoom room)
    {
        var giant = BossIn(room);
        var target = giant.Creature;
        Check(room.Encounter is WaterfallGiantBoss && room.CombatState.Enemies.Count == 1 && target.IsAlive &&
            target.IsPrimaryEnemy && !target.IsSecondaryEnemy && !target.HasPower<MinionPower>() &&
            target.CurrentHp == target.MaxHp && giant.MinInitialHp == 240 && giant.MaxInitialHp == 240 &&
            target.MaxHp == (int)Creature.ScaleHpForMultiplayer(240, room.Encounter, room.CombatState.Players.Count, 0) &&
            target.HpDisplay != HpDisplay.InfiniteWithoutNumbers && target.Block == 0 && target.Powers.Count == 0 &&
            giant.NextMove.Id == "PRESSURIZE_MOVE" &&
            giant.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { "PRESSURIZE_MOVE" }) &&
            Field<int>(giant, "_currentPressureGunDamage") == 20 && Field<int>(giant, "_steamEruptionDamage") == 0 &&
            !Field<bool>(giant, "_isAboutToBlow") && Field<int>(giant, "_pressureBuildupIdx") == 0 &&
            giant.ShouldDisappearFromDoom && giant.ShouldFadeAfterDeath,
            "The sole original A0 giant has finite scaled HP240, native HP display, no powers, original Pressurize and no old explosion capture, flag or pressure index.");
        Check(room.CombatState.Players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 &&
            player.Creature.Block == 0 && player.Creature.Powers.Count == 0) &&
            !CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Any(),
            "Each owner has one hunted mask and the complete original opening has zero old player powers or native power-grant history.");
    }

    private static void CheckVictoryHunting(RunState run)
    {
        var map = JigsawNativeSession.TryGet(run)!.Map!;
        Check(!map.HuntingActive && !map.CurrentRoomHunted && run.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()),
            "The map-final single Boss victory clears hunting and every mask before non-final-act native rewards, including after fresh restoration.");
    }

    // Serializable run/player/act/RNG/history fields are save authority.
    // Logical map is compared separately; live piles/energy/block/powers and
    // monster slots/HP/display/intents/RNG/state logs and revival fields are combat-session
    // state rebuilt on opening.
    // Profile potion discovery is sampled separately before reward claims.
    private static JsonNode Boundary(RunState run) => JsonSerializer.SerializeToNode(new
    {
        Players = run.Players.OrderBy(player => player.NetId).Select(player => player.ToSerializable()).ToArray(),
        RunRng = run.Rng.ToSerializable(), RunExtraFields = run.ExtraFields.ToSerializable(), Acts = run.Acts.Select(act => act.ToSave()).ToArray(),
        run.CurrentActIndex, run.ActFloor, run.VisitedMapCoords, History = History(run)
    }, CompleteJson)!;
    private static JsonNode Map(RunState run) => JsonSerializer.SerializeToNode(JigsawNativeSession.TryGet(run)!.Map!.CreateSave(), CompleteJson)!;
    private static JsonNode Opening(RunState run, CombatRoom room) => JsonSerializer.SerializeToNode(new
    {
        Boundary = Boundary(run), Map = Map(run), Room = room.ToSerializable(),
        room.CombatState.RoundNumber, room.CombatState.CurrentSide,
        CheckpointAct = JigsawCheckpoint.Slot.TryGet(run, out var checkpoint) ? checkpoint.Act : -1,
        CombatPlayers = room.CombatState.Players.OrderBy(player => player.NetId).Select(player => new
        {
            player.NetId, player.PlayerCombatState!.Energy, player.Creature.Block,
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount, power.IsVisible, power.TypeForCurrentAmount }).ToArray(),
            Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }.Select(pile => new
            {
                Type = pile, Cards = pile.GetPile(player).Cards.Select(card => card.ToSerializable()).ToArray()
            }).ToArray()
        }).ToArray(),
        Enemies = room.CombatState.Enemies.Select(enemy => new
        {
            Monster = enemy.Monster!.Id, enemy.SlotName, enemy.MaxHp, enemy.CurrentHp, enemy.HpDisplay, enemy.Block,
            Powers = enemy.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount, power.IsVisible, power.TypeForCurrentAmount }).ToArray(),
            Move = enemy.Monster.NextMove.Id, Rng = RngState(enemy.Monster.Rng),
            StateLog = enemy.Monster.MoveStateMachine!.StateLog.Select(move => move.Id).ToArray(),
            PressureGunDamage = Field<int>((WaterfallGiant)enemy.Monster, "_currentPressureGunDamage"),
            ExplosionDamage = Field<int>((WaterfallGiant)enemy.Monster, "_steamEruptionDamage"),
            AboutToBlow = Field<bool>((WaterfallGiant)enemy.Monster, "_isAboutToBlow"),
            PressureIndex = Field<int>((WaterfallGiant)enemy.Monster, "_pressureBuildupIdx"),
            Intents = enemy.Monster.NextMove.Intents.Select(intent => intent.GetType().FullName).ToArray(),
            enemy.Monster.ShouldDisappearFromDoom, enemy.Monster.ShouldFadeAfterDeath
        }).ToArray()
    }, CompleteJson)!;
    private static JsonNode History(RunState run)
    {
        var type = JsonSerializationUtility.GetTypeInfo<MapPointHistoryEntry>();
        return JsonSerializer.SerializeToNode(run.MapPointHistory.Select(act => act.Select(entry =>
            JsonSerializer.Deserialize(JsonSerializer.Serialize(entry, type), type)).ToArray()).ToArray(), CompleteJson)!;
    }
    private static JsonNode RngState(Rng rng) =>
#if STS2_PUBLIC
        JsonSerializer.SerializeToNode(new { rng.Seed, rng.Counter }, CompleteJson)!;
#else
        JsonSerializer.SerializeToNode(rng.ToSerializable(), CompleteJson)!;
#endif
    private static JsonNode RewardContents(RewardsSet set) => JsonSerializer.SerializeToNode(new
    {
        set.Player.NetId,
        Rewards = set.Rewards.Select(reward => new
        {
            Type = reward.GetType().FullName, Data = reward is CardReward ? null : reward.ToSerializable(),
            Potion = (reward as PotionReward)?.Potion?.Id,
            Cards = (reward as CardReward)?.Cards.Select(card => card.ToSerializable()).ToArray()
        }).ToArray()
    }, CompleteJson)!;
    private static void CheckRewards(RewardsSet set) => Check(set.Player.NetId == 1 && set.Player.RunState.Act is Underdocks && set.Player.RunState.CurrentActIndex < set.Player.RunState.Acts.Count - 1 &&
        set.Room is CombatRoom { Encounter: WaterfallGiantBoss, IsPreFinished: true, ParentEventId: null } && set.Rewards.All(reward => reward.IsPopulated && !reward.SuccessfullySelected) &&
        set.Rewards.OfType<GoldReward>().Count() == 1 && set.Rewards.OfType<CardReward>().Count() == 1 &&
        !set.Rewards.OfType<RelicReward>().Any() && !RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(set),
        "The real non-final-act Boss offers populated, unclaimed nonempty gold/card and any rolled potion rewards through an unfinished native stack.");

    private static async Task ClaimAndProceed(RunState run, RewardsSet rewards, Observation state,
        int target, int steps, int remaining, long entry, int completed)
    {
        var manager = RunManager.Instance;
        var player = run.Players.Single();
        var map = JigsawNativeSession.TryGet(run)!.Map!;
        int gold = player.Gold;
        var deck = player.Deck.Cards.Select(card => card.ToSerializable()).ToArray();
        var cards = rewards.Rewards.OfType<CardReward>().Select(reward => new { reward.Cards.First().Id, reward.Cards.First().CurrentUpgradeLevel }).ToArray();
        var potions = player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id).ToArray();
        var potionRewards = rewards.Rewards.OfType<PotionReward>().Select(reward => reward.Potion!.Id).ToArray();
        int goldReward = rewards.Rewards.OfType<GoldReward>().Sum(reward => reward.Amount);
        // Profile discovery is local session state, not a field that may be
        // copied from the restored run after claiming. Sample its preclaim set.
        var discovery = JsonSerializer.SerializeToNode(player.ToSerializable(), CompleteJson)!["discovered_potions"]!.DeepClone().AsArray();
        var seenPotions = SaveManager.Instance.Progress.DiscoveredPotions.ToHashSet();
        foreach (var potion in potionRewards)
            if (seenPotions.Add(potion)) discovery.Add(JsonSerializer.SerializeToNode(potion, CompleteJson));
        using var selector = HeadlessNativeApi.UseSelector(new JigsawHuntingCombatTests.TakeCardReward());
        foreach (var reward in rewards.Rewards.ToArray())
            Check(await manager.RewardsSetSynchronizer.SelectLocalReward(reward), "The actual synchronized local reward selection succeeds.");
        await FinishOffers(state);
        Check(manager.RewardsSetSynchronizer.IsRewardsSetCompleted(rewards) && rewards.Rewards.All(reward => reward.SuccessfullySelected) &&
            player.Gold == gold + goldReward && player.Deck.Cards.Count == deck.Length + cards.Length,
            "The native reward stack completes with every offered reward claimed and the exact gold/card increment.");
        Equal(JsonSerializer.SerializeToNode(player.Deck.Cards.Take(deck.Length).Select(card => card.ToSerializable()).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(deck, CompleteJson)!, "Actual claims preserve the complete ordered preclaim deck.");
        Equal(JsonSerializer.SerializeToNode(player.Deck.Cards.Skip(deck.Length).Select(card => new { card.Id, card.CurrentUpgradeLevel }).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(cards, CompleteJson)!, "The selector's independently sampled exact card identity and upgrade level are appended once.");
        Equal(JsonSerializer.SerializeToNode(player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(potions.Concat(potionRewards).ToArray(), CompleteJson)!, "The complete ordered potion inventory gains exactly the offered potions.");
        Equal(JsonSerializer.SerializeToNode(player.ToSerializable(), CompleteJson)!["discovered_potions"]!, discovery,
            "Ordered potion discovery equals the independent preclaim local-profile prediction.");
        var claimed = Boundary(run);
        var claimedMap = Map(run);
        bool rejected = false;
        try { await manager.RewardsSetSynchronizer.SelectLocalReward(rewards.Rewards[0]); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "A duplicate selection against the completed actual native reward stack is rejected.");
        Equal(Boundary(run), claimed, "Rejected duplicate reward input cannot pay again or alter complete players, RNG, acts or history.");
        Equal(Map(run), claimedMap, "Rejected duplicate reward input cannot alter exploration.");
        int openings = state.Openings.Count;
        int startups = state.Startups.Count;
        int offers = state.Rewards.Count;
        await HeadlessNativeApi.Proceed(manager);
        await Until(() => map.CurrentNode == null && map.Completed.Contains(target),
            "Actual terminal Proceed reaches the existing production room completion path without a test-issued completion or extra save.");
        Check(map.Completed.Count == completed + 1 && map.Steps == steps && map.RemainingSteps == remaining &&
            map.CreateSave().NextEntry == entry && state.Openings.Count == openings && state.Startups.Count == startups && state.Rewards.Count == offers,
            "Actual Proceed completes this Boss root exactly once without another battle, reward set, entry identity or exploration charge.");
        Equal(Boundary(run), claimed, "Actual Proceed retains all claimed players, RNG, acts and native history.");
    }

    private static async Task FinishOffers(Observation state)
    {
        var manager = RunManager.Instance;
        var run = manager.DebugOnlyGetState();
        if (run != null && state.Rewards.Any(set => ReferenceEquals(set.Player.RunState, run) && !manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)))
            manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
        foreach (var offer in state.Offers) await offer.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static async Task StopCombat(Observation state)
    {
        CombatManager.Instance.Reset(true);
        foreach (var task in state.Openings.Concat(state.Startups))
            try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { } // Native reset canceled this exact observed combat.
    }
    private static async Task Until(Func<bool> condition, string reason)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 300 && !condition(); frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(condition(), reason);
    }
    private static (JigsawRun Map, int Target) BeforeBoss()
    {
        var map = JigsawRun.Create(75, players: [1UL]);
        Ok(map.ChooseEnd(1, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(1));
        Ok(map.Continue(1));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss && previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(0));
        foreach (int node in path.SkipLast(1))
        {
            var enter = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(enter.Id));
            Ok(map.CompleteRoom(enter.Id));
        }
        return (map, target);
    }
    private static void Equal(JsonNode actual, JsonNode expected, string reason) => Check(JsonNode.DeepEquals(actual, expected),
        reason + " expected=" + expected.ToJsonString() + " actual=" + actual.ToJsonString());
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
}
