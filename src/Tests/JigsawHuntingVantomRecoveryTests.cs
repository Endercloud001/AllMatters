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
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class JigsawHuntingVantomRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Vantom fresh restores both required Inklets and whole opening then refuses early victory preserves nonempty rewards and Proceed once", Fresh)];

    private static readonly JsonSerializerOptions CompleteJson = new() { IncludeFields = true };
    private static Observation? observation;

    private sealed class Observation
    {
        internal readonly List<Task> Startups = [];
        internal readonly List<Task> Openings = [];
        internal readonly List<Task> Offers = [];
        internal readonly List<RewardsSet> Rewards = [];
        internal int ReadyRound;
    }

    private static void CaptureStartup(Task task) => observation?.Startups.Add(task);
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
    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
#if STS2_PUBLIC
            "StartCombatInternal");
#else
            "RunTurnLoopAfter");
#endif
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingVantomRecoveryTests), nameof(CaptureStartup)));
        }
        Check(matches == 1, "Observe the single original coordinator Task; never invoke a second combat startup.");
    }

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
            Check(Native.CurrentActIndex == 0 && Native.Act is Overgrowth && Native.Act.BossEncounter is VantomBoss && !Native.Act.HasSecondBoss,
                "The saved three-act run retains its original single Overgrowth Vantom selection at act zero without setting a boss during restoration.");
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
        var patch = new Harmony("AllMatters.Tests.VantomFresh");
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
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingVantomRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingVantomRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingVantomRecoveryTests), nameof(CaptureOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingVantomRecoveryTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingVantomRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING84_VANTOM_FRESH");
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("vantom-fresh-memory-only");
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
            run.Act.SetBossEncounter(ModelDb.Encounter<VantomBoss>());
            run.Act.SetSecondBossEncounter(null);
            Check(run.Act.ToSave().SerializableRooms.BossEncountersVisited == 0 &&
                run.Act.PullNextEncounter(RoomType.Boss) is VantomBoss && !run.Act.HasSecondBoss,
                "The selected single native Vantom and independently declared unconsumed Boss queue agree before actual entry.");
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var (map, target) = BeforeBoss();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 0,
                "The source logical Boss root belongs to native Overgrowth at act zero in the legal three-act run.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 0 && run.Act is Overgrowth && run.CurrentRoom is CombatRoom { Encounter: VantomBoss, ParentEventId: null, RoomType: RoomType.Boss } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target && accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter selects the punished native Vantom Boss encounter and independently samples its actual before-roll source.");
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

            await VantomActions(originalRoom);
            var originalInklets = originalEnemies.Where(enemy => enemy.Monster is Inklet).ToArray();
            var actingInklet = (Inklet)originalInklets[0].Monster!;
            var aiBefore = RngState(run.Rng.MonsterAi);
            int healthBeforeJab = run.Players[0].Creature.CurrentHp;
            int logBeforeJab = actingInklet.MoveStateMachine!.StateLog.Count;
            await Perform(actingInklet);
            Check(run.Players[0].Creature.CurrentHp == healthBeforeJab - 3 &&
                actingInklet.NextMove.Id is "PIERCING_GAZE_MOVE" or "WHIRLWIND_MOVE" &&
                actingInklet.MoveStateMachine.StateLog.Count > logBeforeJab &&
                !JsonNode.DeepEquals(RngState(run.Rng.MonsterAi), aiBefore),
                "The original default Inklet really deals Jab three, logs its legal random successor and consumes the shared run MonsterAi stream; this is no distribution claim.");
            await CreatureCmd.Kill(originalInklets[1]);
            Check(originalBoss.Creature.IsAlive && originalInklets[1].IsDead && originalRoom.CombatState.Enemies.Count == 2 &&
                originalRoom.CombatState.Enemies.All(enemy => !enemy.HasPower<MinionPower>() && enemy.IsPrimaryEnemy),
                "A real source Inklet death removes one required ordinary enemy while Vantom and the other independent Inklet keep combat active.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), originalBoss.Creature, 5, ValueProp.Unpowered, run.Players[0].Creature);
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
            Check(!JsonNode.DeepEquals(Opening(run, originalRoom), opening), "Real four-move Boss history, Strength two, three Wounds, random Inklet continuation, dead required Inklet, player/pile and RNG changes exist before the whole battle rollback save.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active Vantom save contains the native whole before-roll point, not the changed battle.");
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
                "Fresh lobby restoration creates new run, session, room and all three enemy/model identities, including the dead old companion.");
            CheckOpening(restoredRoom);
            Check(restored.Act.ToSave().SerializableRooms.BossEncountersVisited == 1,
                "Fresh saved-run loading reconstructs the original single native Boss exactly once, without consuming another queue entry.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening exactly restores original complete players, ordered five piles, energy/block/powers, run/player/monster RNG, acts, full map/history, slots, HP, powers, intents and complete StateLog, MiddleInklet, round/side and checkpoint act.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same punished Boss entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            Equal(Opening(restored, restoredRoom), opening, "Duplicate restored destination input cannot reopen combat or charge another logical entry.");

            int restoredHealth = restored.Players[0].Creature.CurrentHp;
            await Perform(boss);
            Check(restored.Players[0].Creature.CurrentHp == restoredHealth - 7 && boss.NextMove.Id == "INKY_LANCE_MOVE" &&
                boss.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { "INK_BLOT_MOVE", "INKY_LANCE_MOVE" }),
                "The rebuilt Boss really performs its unbuffed first seven-damage move and advances to Inky Lance with only the fresh two-entry state log.");
            int rewardsBeforeVictory = state.Rewards.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), restored.Players[0].Creature, 9, ValueProp.Unpowered, boss.Creature);
            await PlayerCmd.GainGold(23, restored.Players[0]);
            restored.Rng.Niche.NextInt(100);
            restored.Players[0].PlayerRng.Shops.NextInt(100);
            var restoredInklets = restoredRoom.CombatState.Enemies.Where(enemy => enemy.Monster is Inklet).ToArray();
            Check(restoredInklets.Length == 2 && restoredInklets.All(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy && !enemy.HasPower<MinionPower>()) &&
                boss.Creature.IsAlive && combatManager.IsInProgress && !combatManager.IsEnding,
                "Both restored Inklets are living required primary enemies before the Boss's native death, including the one killed in the discarded old battle.");
            await CreatureCmd.Kill(boss.Creature);
            Check(boss.Creature.IsDead && restoredInklets.All(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy) &&
                restoredRoom.CombatState.Enemies.Count == 2 && !combatManager.IsEnding && !await combatManager.CheckWinCondition() &&
                !restoredRoom.IsPreFinished && victoryBoundary == null && state.Rewards.Count == rewardsBeforeVictory,
                "Killing the Boss cannot clear ordinary Inklets or win: the original check refuses while both required enemies live, with no victory save or rewards.");
            await CreatureCmd.Kill(restoredInklets[0]);
            Check(restoredInklets[0].IsDead && restoredInklets[1].IsAlive && restoredInklets[1].IsPrimaryEnemy &&
                restoredRoom.CombatState.Enemies.Count == 1 && !combatManager.IsEnding && !await combatManager.CheckWinCondition() &&
                !restoredRoom.IsPreFinished && victoryBoundary == null && state.Rewards.Count == rewardsBeforeVictory,
                "One remaining ordinary Inklet still blocks the original victory check; killing the Boss and one companion is insufficient.");
            await CreatureCmd.Kill(restoredInklets[1]);
            Check(restoredInklets.All(enemy => enemy.IsDead) && restoredRoom.CombatState.Enemies.Count == 0 && combatManager.IsEnding,
                "Only the final required Inklet's actual native death empties the enemy roster and permits natural Ending.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns the native BurningBlood that will produce the independent victory healing effect.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts only the actual Boss and both required Inklet deaths and finalizes victory.");
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
                "The original victory flow saves a genuine top-level Vantom reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original non-final-act Vantom Boss room generates and offers its real nonempty native reward set.");
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
            Console.WriteLine($"VANTOM_FRESH scenario=single original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} reward-opening=0 rewards={secondRewards.Rewards.Count} reward-sync=claimed duplicate=rejected proceed=original steps={steps} remaining={remaining} entry={entry}");
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
                throw new AggregateException("Vantom fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
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
        Check(state.Startups.Count == expected, "Each active room naturally starts exactly one original combat coordinator.");
    }

    private static async Task VantomActions(CombatRoom room)
    {
        var boss = BossIn(room);
        var player = room.CombatState.Players.Single();
        int hp = player.Creature.CurrentHp;
        var discard = PileType.Discard.GetPile(player).Cards.ToArray();
        int moves = CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Count(entry => ReferenceEquals(entry.Monster, boss));
        Check(boss.Creature.GetPowerAmount<StrengthPower>() == 0 && player.Creature.Block == 0,
            "The native four-command sample begins with zero Strength and zero player block.");
        await Perform(boss);
        Check(player.Creature.CurrentHp == hp - 7 && boss.NextMove.Id == "INKY_LANCE_MOVE",
            "Original Ink Blot deals exactly seven before the real state machine advances to Inky Lance.");
        await Perform(boss);
        Check(player.Creature.CurrentHp == hp - 19 && boss.NextMove.Id == "DISMEMBER_MOVE",
            "Original Inky Lance executes both six-damage hits before advancing to Dismember.");
        await Perform(boss);
        Check(player.Creature.CurrentHp == hp - 45 && boss.NextMove.Id == "PREPARE_MOVE" &&
            PileType.Discard.GetPile(player).Cards.Take(discard.Length).SequenceEqual(discard) &&
            PileType.Discard.GetPile(player).Cards.Skip(discard.Length).Count() == 3 &&
            PileType.Discard.GetPile(player).Cards.Skip(discard.Length).All(card => card is Wound),
            "Original Dismember deals twenty-six and appends exactly three actual Wounds after the intact original discard prefix.");
        await Perform(boss);
        var cycle = new[] { "INK_BLOT_MOVE", "INKY_LANCE_MOVE", "DISMEMBER_MOVE", "PREPARE_MOVE" };
        Check(player.Creature.IsAlive && player.Creature.CurrentHp == hp - 45 && boss.Creature.GetPowerAmount<StrengthPower>() == 2 &&
            boss.NextMove.Id == "INK_BLOT_MOVE" && boss.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(cycle.Append("INK_BLOT_MOVE")) &&
            CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Where(entry => ReferenceEquals(entry.Monster, boss))
                .Skip(moves).Select(entry => entry.Move.Id).SequenceEqual(cycle),
            "Original Prepare adds Strength two and completes the recorded four-command loop: intent returns to Ink Blot, but Wounds, Strength and five-entry StateLog expose the old battle.");
    }
    private static async Task Perform(MonsterModel monster) { await monster.PerformMove(); monster.RollMove(monster.CombatState.PlayerCreatures); }
    private static Vantom BossIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<Vantom>().Single();
    private static void CheckOpening(CombatRoom room)
    {
        var enemies = room.CombatState.Enemies;
        var players = room.CombatState.Players;
        Check(enemies.Count == 3 && enemies[0].Monster is Inklet { MiddleInklet: false } &&
            enemies[1].Monster is Vantom && enemies[2].Monster is Inklet { MiddleInklet: false } &&
            !ReferenceEquals(enemies[0].Monster, enemies[2].Monster) &&
            enemies.All(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy && !enemy.IsSecondaryEnemy && !enemy.HasPower<MinionPower>() &&
                enemy.Block == 0 && enemy.Powers.Count == 1 && enemy.SlotName == null &&
                enemy.Monster!.ShouldDisappearFromDoom == (enemy.Monster is Inklet)) &&
            enemies.Select(enemy => enemy.Monster!.NextMove.Id).SequenceEqual(new[] { "JAB_MOVE", "INK_BLOT_MOVE", "JAB_MOVE" }) &&
            enemies.All(enemy => enemy.Monster!.MoveStateMachine!.StateLog.Select(move => move.Id).SequenceEqual(new[] { enemy.Monster.NextMove.Id })),
            "The complete opening contains independent default Inklet, original Vantom and independent default Inklet: three required primary enemies, zero old Block/Strength and one initial state-log entry each.");
        Check(!room.Encounter.HasScene && room.Encounter.MonstersWithSlots.All(pair => pair.Item2 == null) &&
            players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 && player.Creature.Block == 0 && player.Creature.Powers.Count == 0),
            "The original unslotted row and one hunted mask per owner are reconstructed with zero old player block or powers.");
        foreach (var (enemy, minimum, maximum, slippery) in new[] { (enemies[0], 11, 17, 1), (enemies[1], 173, 173, 8), (enemies[2], 11, 17, 1) })
        {
            int amount = players.Count == 1 ? slippery : (int)ModelDb.Power<SlipperyPower>().GetScaledAmountForMultiplayer(room.CombatState, enemy, slippery, enemy, null);
            Check(enemy.CurrentHp == enemy.MaxHp && enemy.Monster!.MinInitialHp == minimum && enemy.Monster.MaxInitialHp == maximum &&
                Enumerable.Range(minimum, maximum - minimum + 1).Any(hp => enemy.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, room.Encounter, players.Count, 0)) &&
                enemy.GetPowerAmount<SlipperyPower>() == amount,
                "Every original A0 HP range is scaled once and its only native Slippery power retains the correct role-scaled amount.");
        }
        var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().ToArray();
        Check(grants.Length == 3 && enemies.All(enemy => grants.Count(entry => ReferenceEquals(entry.Actor, enemy) && entry.Power is SlipperyPower &&
            entry.Amount == (enemy.Monster is Vantom ? 8 : 1)) == 1),
            "Original opening history proves exactly one Slippery grant per actual creature and no duplicate hunting or hidden Minion grants.");
    }

    private static void CheckVictoryHunting(RunState run)
    {
        var map = JigsawNativeSession.TryGet(run)!.Map!;
        Check(!map.HuntingActive && !map.CurrentRoomHunted && run.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()),
            "The map-final single Boss victory clears hunting and every mask before non-final-act native rewards, including after fresh restoration.");
    }

    // Serializable run/player/act/RNG/history fields are save authority.
    // Logical map is compared separately; live piles/energy/block/powers and
    // monster slots/HP/intents/RNG/state logs and MiddleInklet are combat-session
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
            MiddleInklet = enemy.Monster is Inklet inklet ? inklet.MiddleInklet : (bool?)null,
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
        set.Room is CombatRoom { Encounter: VantomBoss, IsPreFinished: true, ParentEventId: null } && set.Rewards.All(reward => reward.IsPopulated && !reward.SuccessfullySelected) &&
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
