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

internal static class JigsawHuntingPhrogRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Phrog fresh restores living original after partial worms then five new required worms nonempty rewards and Proceed once", Fresh)];

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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingPhrogRecoveryTests), nameof(BeforeRoomLoaded)));
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingPhrogRecoveryTests), nameof(AfterRoomLoaded)));
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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingPhrogRecoveryTests), nameof(CaptureStartup)));
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
            Check(Native.CurrentActIndex == 0 && Native.Act is Overgrowth,
                "The saved three-act run retains Overgrowth at act zero and its source-selected native elite queue without selecting or changing an encounter during restoration.");
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
        var patch = new Harmony("AllMatters.Tests.PhrogFresh");
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
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(CaptureOpening)));
#if STS2_PUBLIC
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(ObserveStartup)));
#endif
            var nativeStart = AccessTools.Method(typeof(CombatRoom), "StartCombat");
            var machine = nativeStart.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(machine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(ObserveRoomLoadedCaller)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingPhrogRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING84_PHROG_FRESH");
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("phrog-fresh-memory-only");
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
            SelectSourceEncounter(run.Act);
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var (map, target) = BeforeElite();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 0,
                "The source logical elite root belongs to native Overgrowth at act zero in the legal three-act run.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 0 && run.Act is Overgrowth && run.CurrentRoom is CombatRoom { Encounter: PhrogParasiteElite, ParentEventId: null, RoomType: RoomType.Elite } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target && accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter selects the punished native Phrog elite encounter and independently samples its actual before-roll source.");
            Check(run.Act.ToSave().SerializableRooms.EliteEncountersVisited == 1,
                "The actual original native elite entry advances its persisted encounter-consumption count exactly once.");
            var originalRoom = (CombatRoom)run.CurrentRoom!;
            var originalPhrog = PhrogIn(originalRoom);
            var originalEnemies = originalRoom.CombatState.Enemies.ToArray();
            CheckOpening(originalRoom);
            var opening = Opening(run, originalRoom);
            int steps = originalSession.Map!.Steps;
            int remaining = originalSession.Map.RemainingSteps;
            long entry = originalSession.Map.CreateSave().NextEntry;
            int completed = originalSession.Map.Completed.Count;

            await Infect(originalPhrog);
            Creature[] originalWorms;
            await using (var nodes = await JigsawHuntingPhrogNodeTests.CreateFreshDeathNodes(originalPhrog.Creature))
                originalWorms = await nodes.KillAndReveal();
            Check(originalPhrog.Creature.IsDead && !combatManager.IsEnding && !await combatManager.CheckWinCondition() &&
                !originalRoom.IsPreFinished && victoryBoundary == null,
                "Original nonzero Phrog death summons its five ordinary worms without accepting victory or saving a finished room.");
            await SpawnTurns(originalRoom, originalWorms);
            await Wriggle(originalWorms[1]);
            await CreatureCmd.Kill(originalWorms[0]);
            await CreatureCmd.Kill(originalWorms[2]);
            Check(originalWorms[0].IsDead && originalWorms[2].IsDead && originalRoom.CombatState.Enemies.Count == 3 &&
                originalWorms.Where(enemy => enemy.IsAlive).All(enemy => enemy.IsPrimaryEnemy && !enemy.HasPower<MinionPower>()) &&
                !combatManager.IsEnding && !await combatManager.CheckWinCondition(),
                "Actual source worm deaths leave only three required ordinary worms, including the Strength-two worm that really acted.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), originalWorms[1], 5, ValueProp.Unpowered, run.Players[0].Creature);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 7, ValueProp.Unpowered, originalWorms[1]);
            await PlayerCmd.GainGold(19, run.Players[0]);
            var movedCard = PileType.Hand.GetPile(run.Players[0]).Cards.First();
            await CardPileCmd.Add(movedCard, PileType.Discard);
            Check(!PileType.Hand.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Discard.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                !JsonNode.DeepEquals(Opening(run, originalRoom)["CombatPlayers"], opening["CombatPlayers"]),
                "Actual hand-to-discard movement and Infection change ordered combat piles before save; permanent-deck-only restoration cannot satisfy this oracle.");
            run.Rng.Niche.NextInt(100);
            run.Players[0].PlayerRng.Shops.NextInt(100);
            originalWorms[1].Monster!.Rng.NextInt(100);
            Check(!JsonNode.DeepEquals(Opening(run, originalRoom), opening),
                "The actual save source differs in dead Phrog, partial worms, Infection, Strength, state logs, player resources, ordered piles and RNG before rollback.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active Phrog save contains the native whole before-roll point, not the changed battle.");
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
            var phrog = PhrogIn(restoredRoom);
            Check(!ReferenceEquals(restored, run) && !ReferenceEquals(restoredSession, originalSession) &&
                !ReferenceEquals(restoredRoom, originalRoom) && !ReferenceEquals(phrog, originalPhrog) &&
                restoredRoom.CombatState.Enemies.All(enemy => originalEnemies.Concat(originalWorms).All(original => !ReferenceEquals(enemy, original) &&
                    !ReferenceEquals(enemy.Monster, original.Monster))),
                "Fresh lobby restoration creates new run, session, room and the living Phrog identity, with none of the old dead Phrog or worm identities.");
            CheckOpening(restoredRoom);
            Check(restored.Act.ToSave().SerializableRooms.EliteEncountersVisited == 1,
                "Fresh saved-run loading reconstructs the original single native elite exactly once, without consuming another queue entry.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening exactly restores original complete players, ordered five piles, energy/block/powers, run/player/monster RNG, acts, full map/history, slots, HP, powers, intents, complete StateLog, round/side and checkpoint act, with zero old worms.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same punished elite entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            Equal(Opening(restored, restoredRoom), opening, "Duplicate restored destination input cannot reopen combat or charge another logical entry.");

            await Infect(phrog);
            int rewardsBeforeVictory = state.Rewards.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), restored.Players[0].Creature, 9, ValueProp.Unpowered, phrog.Creature);
            await PlayerCmd.GainGold(23, restored.Players[0]);
            restored.Rng.Niche.NextInt(100);
            restored.Players[0].PlayerRng.Shops.NextInt(100);
            Creature[] newWorms;
            await using (var nodes = await JigsawHuntingPhrogNodeTests.CreateFreshDeathNodes(phrog.Creature))
                newWorms = await nodes.KillAndReveal();
            Check(phrog.Creature.IsDead && newWorms.All(enemy => originalWorms.All(old =>
                !ReferenceEquals(enemy, old) && !ReferenceEquals(enemy.Monster, old.Monster))) &&
                !combatManager.IsEnding && !await combatManager.CheckWinCondition() && !restoredRoom.IsPreFinished && victoryBoundary == null,
                "The restored living Phrog really dies again and creates five entirely new required worms; neither the old dead Phrog nor its partial worm roster survives fresh loading.");
            await SpawnTurns(restoredRoom, newWorms);
            foreach (var worm in newWorms.Take(4)) await CreatureCmd.Kill(worm);
            var last = newWorms[4];
            Check(newWorms.Take(4).All(enemy => enemy.IsDead) && last.IsAlive && last.IsPrimaryEnemy && !last.IsSecondaryEnemy &&
                !last.HasPower<MinionPower>() && restoredRoom.CombatState.Enemies.Count == 1 &&
                ReferenceEquals(restoredRoom.CombatState.Enemies.Single(), last) && combatManager.IsInProgress &&
                !combatManager.IsEnding && !await combatManager.CheckWinCondition() && !restoredRoom.IsPreFinished &&
                victoryBoundary == null && state.Rewards.Count == rewardsBeforeVictory,
                "The last ordinary fifth worm alone blocks the original victory check, with no finished save or reward even after Phrog and the four native worms died.");
            await CreatureCmd.Kill(last);
            Check(last.IsDead && restoredRoom.CombatState.Enemies.Count == 0 && combatManager.IsEnding,
                "Only actual death of the final required fifth worm empties the enemy roster and permits natural Ending.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns the native BurningBlood that will produce the independent victory healing effect.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts only actual Phrog and all five required worm deaths and finalizes victory.");
            await Until(() => restoredRoom.IsPreFinished && victoryBoundary != null,
                "Actual enemy deaths and the original victory coordinator naturally save the prefinished room.");
            await restoredSession.WaitForSave();
            Check(restored.Players[0].Creature.CurrentHp == expectedVictoryHealth && restored.Players[0].Creature.Powers.Count == 0 &&
                restored.Players[0].Creature.Block == 0,
                "Original Player.AfterCombatEnd removes the settled combat powers and block, then native BurningBlood heals exactly six before the victory save.");
            Check(state.Rewards.Count == rewardsBeforeVictory && restoredRoom.CombatState.Enemies.All(enemy => enemy.IsDead),
                "The native elite victory saves before generating any reward.");
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: null } &&
                won.SaveData.PreFinishedRoom.EncounterId == originalRoom.Encounter.Id,
                "The original victory flow saves a genuine top-level Phrog reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original non-final-act Phrog elite room generates and offers its real nonempty native reward set.");
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
                "Native prefinished room loading regenerates the saved elite encounter's reward offer.");
            var rewardRun = rewardListener.Native!;
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var secondRewards = state.Rewards.Last();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRoom is { IsPreFinished: true, ParentEventId: null } &&
                rewardRoom.CombatState.Enemies.Count == 0 && state.Openings.Count == openingCount && state.Startups.Count == startupCount &&
                !ReferenceEquals(rewardRun, restored) && !ReferenceEquals(secondRewards, firstRewards),
                "The second fresh lobby restores one top-level prefinished room with no combat opening or rebuilt enemies.");
            CheckRewards(secondRewards);
            Check(rewardRun.Act.ToSave().SerializableRooms.EliteEncountersVisited == 1,
                "Native prefinished reward loading preserves the original consumed elite count; asset preloading is not another elite entry.");
            // Native prefinished loading still generates encounter models to preload
            // assets; zero live enemies and unchanged coordinator/opening counts
            // prove that this did not start a second battle.
            Equal(JsonSerializer.SerializeToNode(rewardRoom.ToSerializable(), CompleteJson)!, victoryRoom!, "Reward loading preserves the original saved native room descriptor.");
            Equal(Map(rewardRun), victoryMap!, "Reward loading preserves the independently sampled victory root, full map, steps, budget and entry identity.");
            Equal(Map(rewardRun), generatedMap, "Reward generation itself never changes exploration state.");
            Equal(Boundary(rewardRun), generatedBoundary, "Fresh reward generation retains the original complete players, all RNG, acts and history at the matching post-generation stage.");
            Equal(RewardContents(secondRewards), rewardContents, "Fresh native elite rewards exactly reproduce the original complete gold/card/relic/potion contents.");
            await ClaimAndProceed(rewardRun, secondRewards, state, target, steps, remaining, entry, completed);
            Console.WriteLine($"PHROG_FRESH scenario=single original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} reward-opening=0 rewards={secondRewards.Rewards.Count} reward-sync=claimed duplicate=rejected proceed=original steps={steps} remaining={remaining} entry={entry}");
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
                throw new AggregateException("Phrog fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
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
                Console.WriteLine($"PHROG_FRESH public-startup-completed-cache callers={previous}/{index} same-Task.CompletedTask=true completed-successfully=true");
#else
                Fail($"beta callers {previous}/{index} reused the same long-running native turn-loop Task");
#endif
            }
        }
    }

    private static void SelectSourceEncounter(ActModel act)
    {
        var rooms = (RoomSet)AccessTools.Field(typeof(ActModel), "_rooms").GetValue(act)!;
        Check(rooms.eliteEncountersVisited == 0 && rooms.eliteEncounters.Count > 0,
            "Source fixture uses the actual unconsumed generated elite queue.");
        RoomSet.SwapToOrCreateAtIndex<EncounterModel, PhrogParasiteElite>(rooms.eliteEncounters, 0);
        Check(act.PullNextEncounter(RoomType.Elite) is PhrogParasiteElite && act.ToSave().SerializableRooms.EliteEncountersVisited == 0,
            "Native selection and actual saved queue agree on Phrog before its first entry, without consuming an elite.");
    }
    private static async Task Perform(MonsterModel monster) { await monster.PerformMove(); monster.RollMove(monster.CombatState.PlayerCreatures); }
    private static PhrogParasite PhrogIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<PhrogParasite>().Single();
    private static async Task Infect(PhrogParasite phrog)
    {
        var player = phrog.CombatState.Players.Single();
        var discard = PileType.Discard.GetPile(player).Cards.ToArray();
        int hp = player.Creature.CurrentHp;
        Check(phrog.NextMove.Id == "INFECT_MOVE", "The rebuilt living Phrog starts at the original Infect action.");
        await Perform(phrog);
        Check(player.Creature.CurrentHp == hp && phrog.NextMove.Id == "LASH_MOVE" &&
            phrog.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { "INFECT_MOVE", "LASH_MOVE" }) &&
            PileType.Discard.GetPile(player).Cards.Take(discard.Length).SequenceEqual(discard) &&
            PileType.Discard.GetPile(player).Cards.Skip(discard.Length).Count() == 3 &&
            PileType.Discard.GetPile(player).Cards.Skip(discard.Length).All(card => card is Infection),
            "Actual native Infect appends exactly three Infections after the unchanged original discard prefix and advances to Lash without damage.");
    }
    private static async Task SpawnTurns(CombatRoom room, Creature[] worms)
    {
        Check(worms.Length == 5 && worms.All(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy && !enemy.IsSecondaryEnemy &&
            !enemy.HasPower<MinionPower>() && enemy.Monster is Wriggler { StartStunned: true } &&
            enemy.Monster.NextMove.Id == "SPAWNED_MOVE" && enemy.Powers.Count == 0),
            "Every summoned fresh worm begins as a required ordinary enemy at the original empty spawn action, with no old Strength.");
        for (int index = 0; index < worms.Length; index++)
        {
            var worm = worms[index].Monster!;
            var run = (RunState)room.CombatState.RunState;
            var before = PlayerActionState(run, room);
            await Perform(worm);
            Equal(PlayerActionState(run, room), before,
                "The actual SPAWNED command leaves complete player HP/save state, ordered five piles, energy, block and powers unchanged.");
            Check(worm.NextMove.Id == (index % 2 == 0 ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE") &&
                worm.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { "SPAWNED_MOVE", worm.NextMove.Id }) &&
                worms[index].SlotName == "wriggler" + (index + 1),
                "Actual native spawn turns preserve numbered slots and Bite 1/3/5 versus Wriggle 2/4, with only fresh state-log entries.");
        }
    }
    private static JsonNode PlayerActionState(RunState run, CombatRoom room) => JsonSerializer.SerializeToNode(new
    {
        Players = Boundary(run)["Players"]!.DeepClone(), CombatPlayers = Opening(run, room)["CombatPlayers"]!.DeepClone()
    }, CompleteJson)!;
    private static async Task Wriggle(Creature creature)
    {
        var worm = creature.Monster!;
        var player = worm.CombatState.Players.Single();
        var discard = PileType.Discard.GetPile(player).Cards.ToArray();
        int hp = player.Creature.CurrentHp;
        Check(creature.SlotName == "wriggler2" && worm.NextMove.Id == "WRIGGLE_MOVE" && creature.GetPowerAmount<StrengthPower>() == 0,
            "The second source worm reaches its unbuffed original Wriggle after truly consuming SPAWNED.");
        await Perform(worm);
        Check(player.Creature.CurrentHp == hp && creature.GetPowerAmount<StrengthPower>() == 2 && worm.NextMove.Id == "NASTY_BITE_MOVE" &&
            PileType.Discard.GetPile(player).Cards.Take(discard.Length).SequenceEqual(discard) &&
            PileType.Discard.GetPile(player).Cards.Skip(discard.Length).Count() == 1 && PileType.Discard.GetPile(player).Cards.Last() is Infection,
            "Actual Wriggle appends one Infection and grants the source worm Strength two before advancing to Bite.");
    }
    private static void CheckOpening(CombatRoom room)
    {
        var phrog = PhrogIn(room);
        var creature = phrog.Creature;
        Check(room.Encounter is PhrogParasiteElite && room.Encounter.HasScene && room.CombatState.Enemies.Count == 1 &&
            creature.IsAlive && creature.IsPrimaryEnemy && !creature.IsSecondaryEnemy && !creature.HasPower<MinionPower>() &&
            creature.SlotName == "phrog" && creature.CurrentHp == creature.MaxHp && creature.Block == 0 && creature.Powers.Count == 1 &&
            creature.GetPowerAmount<InfestedPower>() == 4 && phrog.NextMove.Id == "INFECT_MOVE" &&
            phrog.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { "INFECT_MOVE" }) &&
            !room.CombatState.Enemies.Any(enemy => enemy.Monster is Wriggler),
            "Whole opening contains exactly the living original Phrog with Infested four once, its authored slot and original Infect; zero old worms, Strength or state logs.");
        Check(phrog.MinInitialHp == 61 && phrog.MaxInitialHp == 64 &&
            Enumerable.Range(61, 4).Any(hp => creature.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, room.Encounter, room.CombatState.Players.Count, 0)) &&
            room.CombatState.Players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 && player.Creature.Block == 0 && player.Creature.Powers.Count == 0 &&
                !PileType.Discard.GetPile(player).Cards.Any(card => card is Infection)),
            "Original A0 Phrog HP is sampled and scaled once, each owner has one hunted mask and zero old Infection, block or powers.");
        var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().ToArray();
        Check(grants.Length == 1 && grants[0].Power is InfestedPower && ReferenceEquals(grants[0].Actor, creature) && grants[0].Amount == 4,
            "Opening native history contains exactly the single original Infested-four grant and no duplicate enhancement.");
    }

    // Serializable run/player/act/RNG/history fields are save authority.
    // Logical map is compared separately; live piles/energy/block/powers and
    // monster slots/HP/intents/RNG/state logs and StartStunned are combat-session
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
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount }).ToArray(),
            Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }.Select(pile => new
            {
                Type = pile, Cards = pile.GetPile(player).Cards.Select(card => card.ToSerializable()).ToArray()
            }).ToArray()
        }).ToArray(),
        Enemies = room.CombatState.Enemies.Select(enemy => new
        {
            Monster = enemy.Monster!.Id, enemy.SlotName, enemy.MaxHp, enemy.CurrentHp, enemy.Block,
            Powers = enemy.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount }).ToArray(),
            Move = enemy.Monster.NextMove.Id, Rng = RngState(enemy.Monster.Rng),
            StateLog = enemy.Monster.MoveStateMachine!.StateLog.Select(move => move.Id).ToArray(),
            StartStunned = enemy.Monster is Wriggler worm ? worm.StartStunned : (bool?)null,
            enemy.Monster.ShouldDisappearFromDoom
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
    private static void CheckRewards(RewardsSet set) => Check(set.Player.NetId == 1 && set.Player.RunState.Act is Overgrowth && set.Player.RunState.CurrentActIndex < set.Player.RunState.Acts.Count - 1 &&
        set.Room is CombatRoom { Encounter: PhrogParasiteElite, IsPreFinished: true, ParentEventId: null } && set.Rewards.All(reward => reward.IsPopulated && !reward.SuccessfullySelected) &&
        set.Rewards.OfType<GoldReward>().Count() == 1 && set.Rewards.OfType<CardReward>().Count() == 1 &&
        set.Rewards.OfType<RelicReward>().Count() == 1 && !RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(set),
        "The real non-final-act elite offers populated, unclaimed nonempty gold/card/relic and any rolled potion rewards through an unfinished native stack.");

    private static async Task ClaimAndProceed(RunState run, RewardsSet rewards, Observation state,
        int target, int steps, int remaining, long entry, int completed)
    {
        var manager = RunManager.Instance;
        var player = run.Players.Single();
        var map = JigsawNativeSession.TryGet(run)!.Map!;
        int gold = player.Gold;
        var deck = player.Deck.Cards.Select(card => card.ToSerializable()).ToArray();
        var cards = rewards.Rewards.OfType<CardReward>().Select(reward => new { reward.Cards.First().Id, reward.Cards.First().CurrentUpgradeLevel }).ToArray();
        var relics = player.Relics.Select(relic => relic.Id).ToArray();
        var relicReward = rewards.Rewards.OfType<RelicReward>().Single();
        var awardedRelic = relicReward.Relic!.Id;
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
        foreach (var reward in rewards.Rewards.Where(reward => reward is not RelicReward).ToArray())
            Check(await manager.RewardsSetSynchronizer.SelectLocalReward(reward), "The actual synchronized local reward selection succeeds.");
        Check(!manager.RewardsSetSynchronizer.IsRewardsSetCompleted(rewards) && !relicReward.SuccessfullySelected &&
            player.Gold == gold + goldReward && player.Deck.Cards.Count == deck.Length + cards.Length,
            "The original non-relic rewards pay exact gold/card increments before any relic pickup can add its own native effects.");
        Equal(JsonSerializer.SerializeToNode(player.Deck.Cards.Take(deck.Length).Select(card => card.ToSerializable()).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(deck, CompleteJson)!, "Actual claims preserve the complete ordered preclaim deck.");
        Equal(JsonSerializer.SerializeToNode(player.Deck.Cards.Skip(deck.Length).Select(card => new { card.Id, card.CurrentUpgradeLevel }).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(cards, CompleteJson)!, "The selector's independently sampled exact card identity and upgrade level are appended once.");
        Equal(JsonSerializer.SerializeToNode(player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id).ToArray(), CompleteJson)!,
            JsonSerializer.SerializeToNode(potions.Concat(potionRewards).ToArray(), CompleteJson)!, "The complete ordered potion inventory gains exactly the offered potions.");
        Equal(JsonSerializer.SerializeToNode(player.ToSerializable(), CompleteJson)!["discovered_potions"]!, discovery,
            "Ordered potion discovery equals the independent preclaim local-profile prediction.");
        Check(await manager.RewardsSetSynchronizer.SelectLocalReward(relicReward), "The actual synchronized elite relic selection succeeds.");
        await FinishOffers(state);
        Check(manager.RewardsSetSynchronizer.IsRewardsSetCompleted(rewards) && rewards.Rewards.All(reward => reward.SuccessfullySelected) &&
            player.Relics.Select(relic => relic.Id).SequenceEqual(relics.Append(awardedRelic)),
            "The original elite relic is appended once after the unchanged ordered preclaim relic inventory; its native pickup effects remain active.");
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
            "Actual Proceed completes this elite root exactly once without another battle, reward set, entry identity or exploration charge.");
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
    private static (JigsawRun Map, int Target) BeforeElite()
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
                if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) && previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Elite && previous.ContainsKey(node.Id)).Id;
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
