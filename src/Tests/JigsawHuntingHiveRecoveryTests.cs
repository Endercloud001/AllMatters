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

internal static class JigsawHuntingHiveRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Hive fresh normal entry restores complete opening and three debuffs once then nonempty rewards and Proceed once", Fresh)];

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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingHiveRecoveryTests), nameof(BeforeRoomLoaded)));
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingHiveRecoveryTests), nameof(AfterRoomLoaded)));
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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingHiveRecoveryTests), nameof(CaptureStartup)));
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
            Check(Native.CurrentActIndex == 1 && Native.Act is Hive && Native.Act.Index == 1,
                "The saved native second act remains Hive at native index one without selecting a new encounter during restoration.");
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
        var patch = new Harmony("AllMatters.Tests.HiveFresh");
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
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(CaptureOpening)));
#if STS2_PUBLIC
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(ObserveStartup)));
#endif
            // Recompile the actual caller as well: a warmed caller may already
            // inline the short room-loaded method before this fixture patches it.
            var nativeStart = AccessTools.Method(typeof(CombatRoom), "StartCombat");
            var machine = nativeStart.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(machine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(ObserveRoomLoadedCaller)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingHiveRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING85_HIVE_FRESH");
            run.CurrentActIndex = 1;
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("hive-fresh-memory-only");
            var saves = new RunSaveManager(85, io, new MigrationManager(io), forceSynchronous: true);
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
            var (map, target) = BeforeCombat();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 1,
                "The source logical root is bound to native act one, not a Hive fixture mislabelled as first act.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 1 && run.Act is Hive && run.Act.Index == 1 &&
                run.CurrentRoom is CombatRoom { Encounter: SpinyToadNormal, ParentEventId: null, RoomType: RoomType.Monster } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target && accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter selects the punished native second-act Hive SpinyToad normal encounter and independently samples its actual before-roll source.");
            var originalRoom = (CombatRoom)run.CurrentRoom!;
            var originalToad = originalRoom.CombatState.Enemies.Single();
            var originalEnemies = originalRoom.CombatState.Enemies.ToArray();
            CheckOpening(originalRoom);
            Check(run.Act.ToSave().SerializableRooms.NormalEncountersVisited == 1,
                "Actual native room entry consumes exactly the one independently prepared source encounter.");
            var opening = Opening(run, originalRoom);
            int steps = originalSession.Map!.Steps;
            int remaining = originalSession.Map.RemainingSteps;
            long entry = originalSession.Map.CreateSave().NextEntry;
            int completed = originalSession.Map.Completed.Count;

            await Perform(originalToad.Monster!);
            Check(originalToad.Monster is SpinyToad { IsSpiny: true } &&
                originalToad.Monster.NextMove.Id == "SPIKE_EXPLOSION_MOVE" && originalToad.GetPowerAmount<ThornsPower>() == 5,
                "The original native Spikes move leaves its real spiny state, five Thorns and old Explosion continuation before rollback save.");
            decimal oldWeak = run.Players[0].Creature.GetPowerAmount<WeakPower>();
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 4, originalToad, null);
            Check(run.Players[0].Creature.GetPowerAmount<WeakPower>() == oldWeak + 4,
                "A separate real native source adds four old Weak stacks without being clamped by the opening-only Hive rule.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), originalToad, 5, ValueProp.Unpowered, run.Players[0].Creature);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 7, ValueProp.Unpowered, originalToad);
            await PlayerCmd.GainGold(19, run.Players[0]);
            var movedCard = PileType.Hand.GetPile(run.Players[0]).Cards.First();
            await CardPileCmd.Add(movedCard, PileType.Discard);
            Check(!PileType.Hand.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Discard.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                !JsonNode.DeepEquals(Opening(run, originalRoom)["CombatPlayers"], opening["CombatPlayers"]),
                "Actual hand-to-discard movement changes ordered combat piles before save; a permanent-deck-only restore cannot satisfy this oracle.");
            run.Rng.Niche.NextInt(100);
            run.Players[0].PlayerRng.Shops.NextInt(100);
            originalToad.Monster!.Rng.NextInt(100);
            Check(!JsonNode.DeepEquals(Opening(run, originalRoom), opening), "Real player, enemy, pile, old move, powers and RNG changes exist before the whole battle rollback save.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active Hive save contains the native whole before-roll point, not the changed battle.");
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
            var toad = restoredRoom.CombatState.Enemies.Single();
            Check(!ReferenceEquals(restored, run) && !ReferenceEquals(restoredSession, originalSession) &&
                !ReferenceEquals(restoredRoom, originalRoom) && restoredRoom.CombatState.Enemies.All(enemy =>
                    originalEnemies.All(original => !ReferenceEquals(enemy, original) && !ReferenceEquals(enemy.Monster, original.Monster))),
                "Fresh lobby restoration creates new run, session, room and a new native enemy/model identity.");
            CheckOpening(restoredRoom);
            Check(restored.CurrentActIndex == 1 && JigsawCheckpoint.Slot.TryGet(restored, out var restoredCheckpoint) && restoredCheckpoint.Act == 1,
                "The fresh run and its restored logical root retain the same native second-act ownership.");
            Check(restored.Act.ToSave().SerializableRooms.NormalEncountersVisited == 1,
                "Fresh actual saved-run loading consumes the same one source encounter, without advancing to a second ordinary room.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening exactly restores original complete players, ordered five piles, energy/block/powers, run/player/monster RNG, acts, full map/history, slots, HP, powers, intents and the original nonspiny no-Thorns enemy state.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same punished normal entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            CheckOpening(restoredRoom);
            Equal(Opening(restored, restoredRoom), opening, "Duplicate restored destination input cannot apply the three opening debuffs again, reopen combat or charge another logical entry.");

            int rewardsBeforeVictory = state.Rewards.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), restored.Players[0].Creature, 9, ValueProp.Unpowered, toad);
            await PlayerCmd.GainGold(23, restored.Players[0]);
            restored.Rng.Niche.NextInt(100);
            restored.Players[0].PlayerRng.Shops.NextInt(100);
            Check(toad.IsAlive && restoredRoom.CombatState.Enemies.Count == 1 && toad.IsPrimaryEnemy &&
                !toad.IsSecondaryEnemy && !toad.HasPower<MinionPower>() && combatManager.IsInProgress && !combatManager.IsEnding,
                "The one native normal SpinyToad remains the live required primary enemy before actual lethal damage.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), toad, toad.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, restored.Players[0].Creature);
            Check(toad.IsDead && restoredRoom.CombatState.Enemies.Count == 0 && combatManager.IsEnding,
                "Actual lethal damage to the original required SpinyToad releases native combat completion.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns the native BurningBlood that will produce the independent victory healing effect.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts the actual normal encounter deaths and finalizes victory.");
            await Until(() => restoredRoom.IsPreFinished && victoryBoundary != null,
                "Actual enemy deaths and the original victory coordinator naturally save the prefinished room.");
            await restoredSession.WaitForSave();
            Check(restored.Players[0].Creature.CurrentHp == expectedVictoryHealth && restored.Players[0].Creature.Powers.Count == 0 &&
                restored.Players[0].Creature.Block == 0,
                "Original Player.AfterCombatEnd removes the settled combat powers and block, then native BurningBlood heals exactly six before the victory save.");
            Check(state.Rewards.Count == rewardsBeforeVictory && restoredRoom.CombatState.Enemies.All(enemy => enemy.IsDead),
                "The native normal victory saves before generating any reward.");
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: null } &&
                won.SaveData.PreFinishedRoom.EncounterId == originalRoom.Encounter.Id,
                "The original victory flow saves a genuine top-level normal reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original normal second-act Hive room generates and offers its real nonempty native reward set.");
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

            var second = new LoadListener(victoryBoundary!);
            lobby = new LoadRunLobby(new NetSingleplayerGameService(), second, won.SaveData!);
            second.Lobby = lobby;
            lobby.AddLocalHostPlayer();
            lobby.SetReady(true);
            loading = second.Loading;
            Check(loading != null, "A second new lobby ready flow owns native reward-point restoration.");
            await loading!.WaitAsync(TimeSpan.FromSeconds(15));
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 2,
                "Native prefinished room loading regenerates the saved normal encounter's reward offer.");
            var rewardRun = second.Native!;
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var secondRewards = state.Rewards.Last();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRoom is { IsPreFinished: true, ParentEventId: null } &&
                rewardRoom.CombatState.Enemies.Count == 0 && state.Openings.Count == openingCount && state.Startups.Count == startupCount &&
                !ReferenceEquals(rewardRun, restored) && !ReferenceEquals(secondRewards, firstRewards),
                "The second fresh lobby restores one top-level prefinished room with no combat opening or rebuilt enemies.");
            CheckRewards(secondRewards);
            Check(rewardRun.Act.ToSave().SerializableRooms.NormalEncountersVisited == 1,
                "Prefinished native loading preserves the consumed source encounter count despite preloading encounter models.");
            // Prefinished loading still generates models to preload assets;
            // no live enemies and no additional original coordinator/opening
            // prove that generation did not reopen a second battle.
            Equal(JsonSerializer.SerializeToNode(rewardRoom.ToSerializable(), CompleteJson)!, victoryRoom!, "Reward loading preserves the original saved native room descriptor.");
            Equal(Map(rewardRun), victoryMap!, "Reward loading preserves the independently sampled victory root, full map, steps, budget and entry identity.");
            Equal(Map(rewardRun), generatedMap, "Reward generation itself never changes exploration state.");
            Equal(Boundary(rewardRun), generatedBoundary, "Fresh reward generation retains the original complete players, all RNG, acts and history at the matching post-generation stage.");
            Equal(RewardContents(secondRewards), rewardContents, "Fresh native normal rewards exactly reproduce the original complete gold/card/potion contents.");
            await ClaimAndProceed(rewardRun, secondRewards, state, target, steps, remaining, entry, completed);
            Console.WriteLine($"HIVE_FRESH encounter=SpinyToadNormal act={rewardRun.CurrentActIndex} original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} opening-applications=Weak1/Frail1/Vulnerable1 reward-opening=0 rewards={secondRewards.Rewards.Count} reward-sync=claimed duplicate=rejected proceed=original steps={steps} remaining={remaining} entry={entry}");
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
                throw new AggregateException("Hive fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
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
        Check(state.Startups.Count == expected, $"Each active room naturally starts exactly one original combat coordinator. expected={expected} actual={state.Startups.Count}; startups=[{string.Join(",", state.Startups.Select(task => $"{task.Id}:{task.Status}"))}]; openings=[{string.Join(",", state.Openings.Select(task => $"{task.Id}:{task.Status}"))}]; liveRoom={RunManager.Instance.DebugOnlyGetState()?.CurrentRoom?.GetType().Name}");
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
                Console.WriteLine($"HIVE_FRESH public-startup-completed-cache callers={previous}/{index} same-Task.CompletedTask=true completed-successfully=true");
#else
                Fail($"beta callers {previous}/{index} reused the same long-running native turn-loop Task");
#endif
            }
        }
    }

    private static void SelectSourceEncounter(ActModel act)
    {
        // Both target versions declare this protected field. Read the actual
        // mutable RoomSet once, then use the original native queue helper.
        // Loading never selects a replacement encounter or reseeds the queue.
        var rooms = (RoomSet)AccessTools.Field(typeof(ActModel), "_rooms").GetValue(act)!;
        Check(act is Hive && act.Index == 1 && rooms.normalEncountersVisited == 0 && rooms.normalEncounters.Count > 0,
            "The native second-act Hive source starts at its real generated ordinary queue before any normal encounter is consumed.");
        RoomSet.SwapToOrCreateAtIndex<EncounterModel, SpinyToadNormal>(rooms.normalEncounters, 0);
        Check(act.PullNextEncounter(RoomType.Monster) is SpinyToadNormal && act.ToSave().SerializableRooms.NormalEncountersVisited == 0,
            "Source-only selection and the actual serialized act agree on SpinyToadNormal without consuming an encounter.");
    }
    private static async Task Perform(MonsterModel monster) { await monster.PerformMove(); monster.RollMove(monster.CombatState.PlayerCreatures); }
    private static void CheckOpening(CombatRoom room)
    {
        var enemies = room.CombatState.Enemies;
        Check(room is { Encounter: SpinyToadNormal, ParentEventId: null, RoomType: RoomType.Monster } &&
            enemies.Count == 1 && enemies[0].Monster is SpinyToad && enemies[0].SlotName == null && !room.Encounter.HasScene,
            "The actual original native normal roster remains exactly one slotless SpinyToad, with no unrelated appended monster.");
        var enemy = enemies.Single();
        var monster = (SpinyToad)enemy.Monster!;
        Check(enemy.IsPrimaryEnemy && !enemy.IsSecondaryEnemy && !enemy.HasPower<MinionPower>() &&
            enemy.CurrentHp == enemy.MaxHp && enemy.Powers.Count == 0 && enemy.Block == 0 && !monster.IsSpiny &&
            monster.NextMove.Id == "PROTRUDING_SPIKES_MOVE" && monster.MinInitialHp == 116 && monster.MaxInitialHp == 119 &&
            Enumerable.Range(116, 4).Any(hp => enemy.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, room.Encounter, room.CombatState.Players.Count, 0)),
            "The original A0 HP116..119 scales once; the required enemy begins nonspiny with original Spikes, no old Thorns, powers or block.");
        var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();
        Check(received.Length == room.CombatState.Players.Count * 3 && received.All(entry => entry.Amount == 1 &&
                room.CombatState.Players.Any(player => ReferenceEquals(entry.Actor, player.Creature))) &&
            room.CombatState.Players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 &&
                received.Count(entry => ReferenceEquals(entry.Actor, player.Creature) && entry.Power is WeakPower) == 1 &&
                received.Count(entry => ReferenceEquals(entry.Actor, player.Creature) && entry.Power is FrailPower) == 1 &&
                received.Count(entry => ReferenceEquals(entry.Actor, player.Creature) && entry.Power is VulnerablePower) == 1),
            "Actual native power-command history independently proves exactly one initial Weak, Frail and Vulnerable stack per owner and one actual mask, with no duplicate opening application.");
        // Native first-turn power ticking differs between target versions.
        // History proves the fixed applications above; complete current powers
        // are sampled after the same real opening/draw phase for fresh equality.
    }

    // Serializable run/player/act/RNG/history fields are save authority.
    // Logical map is compared separately; live piles/energy/block/powers and
    // monster slots/HP/intents/RNG are combat-session
    // state rebuilt on opening.
    // Profile potion discovery is sampled separately before reward claims.
    private static JsonNode Boundary(RunState run) => JsonSerializer.SerializeToNode(new
    {
        Players = run.Players.OrderBy(player => player.NetId).Select(player => player.ToSerializable()).ToArray(),
        RunRng = run.Rng.ToSerializable(), Acts = run.Acts.Select(act => act.ToSave()).ToArray(),
        run.CurrentActIndex, run.ActFloor, run.VisitedMapCoords, History = History(run)
    }, CompleteJson)!;
    private static JsonNode Map(RunState run) => JsonSerializer.SerializeToNode(JigsawNativeSession.TryGet(run)!.Map!.CreateSave(), CompleteJson)!;
    private static JsonNode Opening(RunState run, CombatRoom room) => JsonSerializer.SerializeToNode(new
    {
        Boundary = Boundary(run), Map = Map(run), Room = room.ToSerializable(),
        CheckpointAct = JigsawCheckpoint.Slot.TryGet(run, out var checkpoint) ? checkpoint.Act : -1,
        CombatPlayers = room.CombatState.Players.OrderBy(player => player.NetId).Select(player => new
        {
            player.NetId, player.PlayerCombatState!.Energy, player.Creature.Block,
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
            Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }.Select(pile => new
            {
                Type = pile, Cards = pile.GetPile(player).Cards.Select(card => card.ToSerializable()).ToArray()
            }).ToArray()
        }).ToArray(),
        Enemies = room.CombatState.Enemies.Select(enemy => new
        {
            Monster = enemy.Monster!.Id, enemy.SlotName, enemy.MaxHp, enemy.CurrentHp, enemy.Block,
            Powers = enemy.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
            Move = enemy.Monster.NextMove.Id, Rng = RngState(enemy.Monster.Rng), Spiny = ((SpinyToad)enemy.Monster).IsSpiny
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
    private static void CheckRewards(RewardsSet set) => Check(set.Player.NetId == 1 && set.Player.RunState.CurrentActIndex == 1 && set.Player.RunState.Act is Hive &&
        set.Room is CombatRoom { IsPreFinished: true, ParentEventId: null, RoomType: RoomType.Monster, Encounter: SpinyToadNormal } && set.Rewards.All(reward => reward.IsPopulated && !reward.SuccessfullySelected) &&
        set.Rewards.OfType<GoldReward>().Count() == 1 && set.Rewards.OfType<GoldReward>().Single().Amount > 0 &&
        set.Rewards.OfType<CardReward>().Count() == 1 && set.Rewards.OfType<CardReward>().Single().Cards.Any() &&
        !set.Rewards.OfType<RelicReward>().Any() && !RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(set),
        "The real native second-act Hive ordinary encounter offers populated, unclaimed nonempty gold/card and any rolled potion rewards through an unfinished native stack.");

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
            "Actual Proceed completes this normal root exactly once without another battle, reward set, entry identity or exploration charge.");
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
    private static (JigsawRun Map, int Target) BeforeCombat()
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
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Combat && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path.SkipLast(1))
        {
            var enter = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(enter.Id));
            Ok(map.CompleteRoom(enter.Id));
        }
        Ok(map.SetRemainingSteps(0));
        return (map, target);
    }
    private static void Equal(JsonNode actual, JsonNode expected, string reason) => Check(JsonNode.DeepEquals(actual, expected),
        reason + " expected=" + expected.ToJsonString() + " actual=" + actual.ToJsonString());
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
}
