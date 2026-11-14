using AllMatters.Jigsaw;
using AllMatters.Powers;
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
using MegaCrit.Sts2.Core.Models.Cards;
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

internal static class JigsawHuntingTestSubjectRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw TestSubject fresh lobbies reset real third phase Scrutiny clock to the whole first opening then preserve terminal victory without reopening", Fresh)];

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
    private static Task ObserveTerminalRewards(RewardsSet set)
    {
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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(CaptureStartup)));
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
            Check(Native.CurrentActIndex == 2 && Native.Act is Glory && Native.Act.BossEncounter is TestSubjectBoss,
                "The saved native act retains its TestSubject selection without setting a boss again during restoration.");
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
        var patch = new Harmony("AllMatters.Tests.TestSubjectFresh");
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
            RewardsSet.testSelector = ObserveTerminalRewards;
            viewport.Size = new Vector2I(1920, 1080);
            LocalContext.NetId = 1;
            foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(CaptureOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingTestSubjectRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_TESTSUBJECT_FRESH");
            run.CurrentActIndex = 2;
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("testsubject-fresh-memory-only");
            var saves = new RunSaveManager(87, io, new MigrationManager(io), forceSynchronous: true);
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
            // Select the scenario through the native mutable act. Room selection,
            // entry, serialization and both restored acts remain original consumers.
            run.Act.SetBossEncounter(ModelDb.Encounter<TestSubjectBoss>());
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var (map, target) = BeforeBoss();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 2,
                "The source logical Boss root belongs to native third-act Glory at index two.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 2 && run.Act is Glory && run.Act.BossEncounter is TestSubjectBoss &&
                run.CurrentRoom is CombatRoom { Encounter: TestSubjectBoss, ParentEventId: null, RoomType: RoomType.Boss } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target &&
                accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter consumes the selected native final-Glory TestSubject Boss once and captures its actual before-roll source.");
            var originalRoom = (CombatRoom)run.CurrentRoom!;
            var originalSubject = SubjectIn(originalRoom);
            CheckOpening(originalRoom);
            var opening = Opening(run, originalRoom);
            int steps = originalSession.Map!.Steps;
            int remaining = originalSession.Map.RemainingSteps;
            long entry = originalSession.Map.CreateSave().NextEntry;
            int originalKills = run.ExtraFields.TestSubjectKills;

            await ReachThirdForm(originalRoom, state);
            Check(FullTurns(originalSubject) == 1 && !originalSubject.Creature.HasPower<ScrutinyPower>(),
                "The real second revival completes before the first full third-form player turn arms the fresh shared clock at one.");
            await NextPlayerTurn(originalRoom, state);
            Check(FullTurns(originalSubject) == 2 && !originalSubject.Creature.HasPower<ScrutinyPower>(),
                "The next complete native player turn advances the source clock to two without premature Scrutiny.");
            await NextPlayerTurn(originalRoom, state);
            Check(FullTurns(originalSubject) == 3 && originalSubject.Creature.GetPowerAmount<ScrutinyPower>() == 1 &&
                run.Players.All(player => !player.Creature.HasPower<ScrutinyPower>()) && run.ExtraFields.TestSubjectKills == originalKills + 2,
                "Two actual native prevented deaths and three complete third-form turns leave real monster-owned Scrutiny, clock three and exactly two old native kill increments.");
            int oldHand = PileType.Hand.GetPile(run.Players[0]).Cards.Count;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, run.Players[0], fromHandDraw: false);
            Check(PileType.Hand.GetPile(run.Players[0]).Cards.Count == oldHand,
                "The old third-turn Scrutiny actually refuses a native extra draw while retaining the original five-card opening hand.");
            var generated = originalRoom.CombatState.CreateCard<DefendIronclad>(run.Players[0]);
            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, run.Players[0]);
            Check(PileType.Hand.GetPile(run.Players[0]).Cards.Count == oldHand + 1,
                "A real generated non-draw card still enters during old Scrutiny, leaving an old battle-only card to remove on restoration.");
            await CreatureCmd.GainBlock(run.Players[0].Creature, 11, ValueProp.Unpowered, null);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), originalSubject.Creature, 5,
                ValueProp.Unblockable | ValueProp.Unpowered, run.Players[0].Creature);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 7,
                ValueProp.Unblockable | ValueProp.Unpowered, originalSubject.Creature);
            await PlayerCmd.GainGold(19, run.Players[0]);
            var movedCard = PileType.Hand.GetPile(run.Players[0]).Cards.First(card => !ReferenceEquals(card, generated));
            await CardPileCmd.Add(movedCard, PileType.Exhaust);
            Check(!PileType.Hand.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Exhaust.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                !JsonNode.DeepEquals(Opening(run, originalRoom)["CombatPlayers"], opening["CombatPlayers"]),
                "A real hand-to-exhaust move, changed block/HP and generated card alter the independently sampled five-pile state before save.");
            run.Rng.Niche.NextInt(100);
            run.Players[0].PlayerRng.Shops.NextInt(100);
            originalSubject.Rng.NextInt(100);
            Check(!JsonNode.DeepEquals(Opening(run, originalRoom), opening),
                "Real old form, clock, Scrutiny, native kill fields, player piles/HP/block and all three RNG sources differ before native whole-battle rollback save.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active TestSubject save contains the native whole before-roll point, not the old third phase, Scrutiny or shared turn count.");
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
            var subject = SubjectIn(restoredRoom);
            Check(!ReferenceEquals(restored, run) && !ReferenceEquals(restoredSession, originalSession) &&
                !ReferenceEquals(restoredRoom, originalRoom) && !ReferenceEquals(subject, originalSubject) &&
                !ReferenceEquals(subject.Creature, originalSubject.Creature),
                "Fresh lobby restoration creates new run, session, room, TestSubject creature/model identities.");
            CheckOpening(restoredRoom);
            Check(restored.CurrentActIndex == 2 && JigsawCheckpoint.Slot.TryGet(restored, out var restoredCheckpoint) && restoredCheckpoint.Act == 2 &&
                FullTurns(subject) == null && restored.ExtraFields.TestSubjectKills == originalKills &&
                new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }
                    .All(pile => !pile.GetPile(restored.Players[0]).Cards.Contains(generated)),
                "The restored native/logical third-act owners agree; old clock, native death increments and battle-only generated card do not survive in the new first form.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening exactly restores complete players, all ordered five piles, energy/block/powers, run/player/monster RNG, acts, complete map/history, slots, HP, intents and original first-form native counters and absent third-phase clock.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same hunted entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            CheckOpening(restoredRoom);
            Equal(Opening(restored, restoredRoom), opening, "Duplicate restored destination input cannot repeat initial grants, attach the old phase clock, reopen combat or charge another logical entry.");

            await ReachThirdForm(restoredRoom, state);
            Check(FullTurns(subject) == 1 && !subject.Creature.HasPower<ScrutinyPower>() &&
                restored.ExtraFields.TestSubjectKills == originalKills + 2,
                "The fresh battle genuinely repeats both revivals and starts its new clock at one rather than carrying source turn three or double-counting old deaths.");
            int freshHand = PileType.Hand.GetPile(restored.Players[0]).Cards.Count;
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, restored.Players[0], fromHandDraw: false);
            Check(PileType.Hand.GetPile(restored.Players[0]).Cards.Count == freshHand + 1,
                "The new first complete third-form turn actually allows native extra draw, unlike the saved old Scrutiny turn.");
            int rewardsBeforeVictory = state.Rewards.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), restored.Players[0].Creature, 9, ValueProp.Unblockable | ValueProp.Unpowered, subject.Creature);
            await PlayerCmd.GainGold(23, restored.Players[0]);
            restored.Rng.Niche.NextInt(100);
            restored.Players[0].PlayerRng.Shops.NextInt(100);
            Check(subject.Creature.IsAlive && !subject.Creature.HasPower<AdaptablePower>() && combatManager.IsInProgress && !combatManager.IsEnding,
                "The genuinely completed third form has no remaining Adaptable revival and remains the required enemy before actual final native death.");
            await CreatureCmd.Kill(subject.Creature);
            Check(subject.Creature.IsDead && restoredRoom.CombatState.Enemies.Count == 0 && combatManager.IsEnding,
                "Actual native third-form death removes the required Boss and releases combat completion without another revival.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns native BurningBlood for the independent six-health victory prediction.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts actual TestSubject death and finalizes victory.");
            await Until(() => restoredRoom.IsPreFinished && victoryBoundary != null,
                "Actual Boss death and the original victory coordinator naturally save the prefinished terminal room.");
            await restoredSession.WaitForSave();
            Check(restored.Players[0].Creature.CurrentHp == expectedVictoryHealth && restored.Players[0].Creature.Block == 0 &&
                restored.Players[0].Creature.Powers.Count == 0 && !restoredSession.Map!.HuntingActive &&
                restored.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()) && state.Rewards.Count == rewardsBeforeVictory,
                "Native victory cleanup heals exactly six, clears combat block/powers and releases final-Boss hunting/masks before any terminal reward is generated.");
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: null } &&
                won.SaveData.PreFinishedRoom.EncounterId == ModelDb.Encounter<TestSubjectBoss>().Id,
                "The original victory flow saves a genuine top-level TestSubject reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original final-Glory TestSubject room really generates and offers one native empty terminal reward set.");
            var firstRewards = state.Rewards.Last();
            CheckRewards(firstRewards);
            var rewardContents = RewardContents(firstRewards);
            var generatedBoundary = Boundary(restored);
            var generatedMap = Map(restored);
            await FinishOffers(state);
            Equal(Boundary(restored), generatedBoundary, "Source terminal reward completion pays nothing and changes no complete player, RNG or history field.");
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
                "Native prefinished room loading regenerates the saved TestSubject's reward offer.");
            var rewardRun = second.Native!;
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var secondRewards = state.Rewards.Last();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRoom is { IsPreFinished: true, Encounter: TestSubjectBoss, ParentEventId: null } &&
                rewardRoom.CombatState.Enemies.Count == 0 && state.Openings.Count == openingCount && state.Startups.Count == startupCount &&
                !ReferenceEquals(rewardRun, restored) && !ReferenceEquals(secondRewards, firstRewards),
                "The second fresh lobby restores one top-level prefinished room with no combat opening, revived TestSubject, Scrutiny or running phase clock.");
            CheckRewards(secondRewards);
            await FinishOffers(state);
            Check(JigsawCheckpoint.Slot.TryGet(rewardRun, out var terminalCheckpoint) && terminalCheckpoint.Act == 2 &&
                JigsawNativeSession.TryGet(rewardRun)!.Map is { HuntingActive: false } &&
                rewardRun.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()),
                "The new terminal lobby retains the original released third-act root and cannot restore a cleared hunting mask.");
            Equal(JsonSerializer.SerializeToNode(rewardRoom.ToSerializable(), CompleteJson)!, victoryRoom!, "Reward loading preserves the original saved native room descriptor.");
            Equal(Map(rewardRun), victoryMap!, "Reward loading preserves the independently sampled victory root, full map, steps, budget and entry identity.");
            Equal(Map(rewardRun), generatedMap, "Reward generation itself never changes exploration state.");
            Equal(Boundary(rewardRun), generatedBoundary, "Fresh reward generation retains the original complete players, all RNG, acts and history at the matching post-generation stage.");
            Equal(RewardContents(secondRewards), rewardContents, "Fresh native final-Glory TestSubject rewards exactly reproduce the original empty terminal contents.");
            Console.WriteLine($"TESTSUBJECT_FRESH act={rewardRun.CurrentActIndex} original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} old-respawns=2 old-full-turns=3 old-scrutiny=1 restored-respawns=0 restored-clock=absent new-third-clock=1 reward-opening=0 terminal-rewards=0 reward-sync=completed steps={steps} remaining={remaining} entry={entry}");
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
                throw new AggregateException("TestSubject fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
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

    private static TestSubject SubjectIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<TestSubject>().Single();
    private static int NativeCount(TestSubject subject, string field) => (int)AccessTools.Field(typeof(TestSubject), field).GetValue(subject)!;
    private static int? FullTurns(TestSubject subject)
    {
        // Read only: native callbacks own both phase counters and the keyed
        // production clock. Never manufacture a clock to make this check pass.
        var clocks = AccessTools.Field(typeof(JigsawHuntingBosses), "clocks").GetValue(null)!;
        object?[] arguments = [subject, null];
        if (!(bool)AccessTools.Method(clocks.GetType(), "TryGetValue").Invoke(clocks, arguments)!) return null;
        return (int)AccessTools.Field(arguments[1]!.GetType(), "FullTurns").GetValue(arguments[1])!;
    }
    private static async Task NextPlayerTurn(CombatRoom room, Observation state)
    {
        int previous = room.CombatState.RoundNumber;
        var player = room.CombatState.Players.Single();
        // A native block command protects this one test owner through the
        // necessary real enemy actions without changing saved starting MaxHp.
        await CreatureCmd.GainBlock(player.Creature, 100, ValueProp.Unpowered, null);
        CombatManager.Instance.SetReadyToEndTurn(player, false);
        await Until(() => state.ReadyRound > previous && state.ReadyRound == room.CombatState.RoundNumber &&
            room.CombatState.CurrentSide == CombatSide.Player && PileType.Hand.GetPile(player).Cards.Count == 5,
            "Actual native readiness completes exactly one enemy phase and the next full five-card player start.");
    }
    private static async Task ReachThirdForm(CombatRoom room, Observation state)
    {
        var subject = SubjectIn(room);
        for (int revival = 1; revival <= 2; revival++)
        {
            await CreatureCmd.Kill(subject.Creature);
            Check(subject.NextMove.Id == "RESPAWN_MOVE" && room.CombatState.Enemies.Contains(subject.Creature) && !CombatManager.Instance.IsEnding,
                "Real native Adaptable death retains this subject and selects Respawn instead of completing the Boss.");
            await NextPlayerTurn(room, state);
            int hp = (int)Creature.ScaleHpForMultiplayer(revival == 1 ? 200 : 300, room.Encounter, room.CombatState.Players.Count, 2);
            Check(NativeCount(subject, "_respawns") == revival && subject.Creature.IsAlive && subject.Creature.CurrentHp == hp && subject.Creature.MaxHp == hp,
                "The original coordinator completes one real revival with the correct independently known A0 second/third-form HP scaled once.");
            if (revival == 1)
            {
                var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Where(entry =>
                    entry.Power is SkittishPower or SuckPower or PainfulStabsPower).ToArray();
                Check(subject.Creature.HasPower<AdaptablePower>() && subject.NextMove.Id == "MULTI_CLAW_MOVE" &&
                    subject.Creature.GetPowerAmount<SkittishPower>() == 10 && subject.Creature.GetPowerAmount<SuckPower>() == 3 &&
                    !subject.Creature.HasPower<PainfulStabsPower>() && FullTurns(subject) == null && grants.Length == 2 &&
                    grants.Count(entry => entry.Power is SkittishPower && entry.Amount == 10) == 1 &&
                    grants.Count(entry => entry.Power is SuckPower && entry.Amount == 3) == 1 && grants.All(entry => ReferenceEquals(entry.Actor, subject.Creature)),
                    "The actual first revival applies fixed Skittish10/Suck3 once, never grants Painful Stabs and has no third-phase clock yet.");
            }
            else Check(subject.Creature.HasPower<NemesisPower>() && !subject.Creature.HasPower<AdaptablePower>() &&
                    !subject.Creature.HasPower<SkittishPower>() && !subject.Creature.HasPower<SuckPower>() &&
                    !subject.Creature.HasPower<PainfulStabsPower>() && subject.NextMove.Id == "PHASE3_LACERATE_MOVE" &&
                    FullTurns(subject) == 1 && !subject.Creature.HasPower<ScrutinyPower>(),
                "The actual completed second revival retains native Nemesis, removes all phase-two additions and reaches only its new first full third-form turn.");
        }
    }
    private static void CheckOpening(CombatRoom room)
    {
        var subject = SubjectIn(room);
        var enemy = subject.Creature;
        var players = room.CombatState.Players;
        Check(room is { Encounter: TestSubjectBoss, ParentEventId: null, RoomType: RoomType.Boss } && room.CombatState.Enemies.Count == 1 &&
            enemy.SlotName == null && enemy.IsPrimaryEnemy && !enemy.IsSecondaryEnemy && !enemy.HasPower<MinionPower>() &&
            enemy.CurrentHp == enemy.MaxHp && subject.MinInitialHp == 100 && subject.MaxInitialHp == 100 &&
            enemy.MaxHp == (int)Creature.ScaleHpForMultiplayer(100, room.Encounter, players.Count, 2) && enemy.Block == 0 &&
            subject.NextMove.Id == "BITE_MOVE" && NativeCount(subject, "_respawns") == 0 && NativeCount(subject, "_extraMultiClawCount") == 0 &&
            !subject.ShouldDisappearFromDoom && FullTurns(subject) == null && enemy.Powers.Count == 2 &&
            enemy.GetPowerAmount<AdaptablePower>() == 1 && enemy.GetPowerAmount<EnragePower>() == 2 &&
            !enemy.HasPower<NemesisPower>() && !enemy.HasPower<SkittishPower>() && !enemy.HasPower<SuckPower>() &&
            !enemy.HasPower<PainfulStabsPower>() && !enemy.HasPower<ScrutinyPower>(),
            "The one original required A0 subject begins at base100 HP scaled once, original Bite, only Adaptable1/Enrage2 and no old form, MultiClaw count or armed clock.");
        var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().ToArray();
        Check(grants.Length == 2 && grants.Count(entry => entry.Power is AdaptablePower && entry.Amount == 1) == 1 &&
            grants.Count(entry => entry.Power is EnragePower && entry.Amount == 2) == 1 && grants.All(entry => ReferenceEquals(entry.Actor, enemy)) &&
            players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 && player.Creature.Block == 0 && !player.Creature.HasPower<ScrutinyPower>()),
            "Original native opening history proves each first-form grant exactly once with correct owner and one mask, without old phase-two or third-form applications.");
    }

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
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
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
            Respawns = NativeCount((TestSubject)enemy.Monster, "_respawns"),
            ExtraMultiClawCount = NativeCount((TestSubject)enemy.Monster, "_extraMultiClawCount"),
            PhaseThreeFullTurns = FullTurns((TestSubject)enemy.Monster), enemy.Monster.ShouldDisappearFromDoom
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
    private static void CheckRewards(RewardsSet set)
    {
        var run = set.Player.RunState;
        bool completed = RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(set);
        string items = string.Join(',', set.Rewards.Select(reward =>
            $"{reward.GetType().Name}:populated={reward.IsPopulated}:selected={reward.SuccessfullySelected}"));
        // Both current native versions omit default rewards for a final-act
        // Boss. The synchronizer immediately completes that exact empty set.
        Check(set.Player.NetId == 1 && run.Act is Glory && run.CurrentActIndex == run.Acts.Count - 1 &&
            set.Room is CombatRoom { Encounter: TestSubjectBoss, IsPreFinished: true, ParentEventId: null, RoomType: RoomType.Boss } room && room.ExtraRewards.Count == 0 &&
            set.Rewards.Count == 0 && completed,
            $"The native final-Glory TestSubject offer is an empty completed terminal set: owner={set.Player.NetId}, room={set.Room?.GetType().Name}, act={run.CurrentActIndex}/{run.Acts.Count}, count={set.Rewards.Count}, completed={completed}, items=[{items}].");
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
