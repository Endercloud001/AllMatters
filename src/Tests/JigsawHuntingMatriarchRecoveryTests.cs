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

internal static class JigsawHuntingMatriarchRecoveryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Matriarch fresh lobbies reset real awakened shell and sleeping minion counters to the whole opening then preserve terminal victory without reopening", Fresh)];

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
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(CaptureStartup)));
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
            Check(Native.CurrentActIndex == 0 && Native.Act is Underdocks && Native.Act.BossEncounter is LagavulinMatriarchBoss,
                "The saved native act retains its Matriarch selection without setting a boss again during restoration.");
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
        var patch = new Harmony("AllMatters.Tests.MatriarchFresh");
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
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(SkipProfile)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(SeenFtue)));
            patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(CaptureOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(RewardsSet), nameof(RewardsSet.Offer)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingMatriarchRecoveryTests), nameof(CaptureOffer)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            combatManager.TurnStarted += Ready;

            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_MATRIARCH_FRESH");
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            var io = new MockGodotFileIo("matriarch-fresh-memory-only");
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
            // Select the scenario through the native mutable act. Room selection,
            // entry, serialization and both restored acts remain original consumers.
            run.Act.SetBossEncounter(ModelDb.Encounter<LagavulinMatriarchBoss>());
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var (map, target) = BeforeBoss();
            int stepsBefore = map.Steps;
            JigsawCheckpoint.Capture(run, map);
            Check(JigsawCheckpoint.Slot.TryGet(run, out var sourceCheckpoint) && sourceCheckpoint.Act == 0,
                "The source logical Boss root belongs to native single-act Underdocks at index zero.");
            JigsawNativeSession.Restore(run);
            var originalSession = JigsawNativeSession.Current!;
            await originalSession.Enter(target);
            originalEntry = false;
            await OpeningReady(state, 1);
            Check(beforeRoll != null && run.CurrentActIndex == 0 && run.Act is Underdocks && run.Act.BossEncounter is LagavulinMatriarchBoss &&
                run.CurrentRoom is CombatRoom { Encounter: LagavulinMatriarchBoss, ParentEventId: null, RoomType: RoomType.Boss } &&
                originalSession.Map is { CurrentRoomHunted: true } accepted && accepted.CurrentNode == target &&
                accepted.Steps == stepsBefore + 1 && accepted.RemainingSteps == 0,
                "Production session.Enter consumes the selected native final-Underdocks Matriarch Boss once and captures its actual before-roll source.");
            var originalRoom = (CombatRoom)run.CurrentRoom!;
            var originalMatriarch = MatriarchIn(originalRoom);
            var originalMinion = MinionIn(originalRoom);
            CheckOpening(originalRoom);
            var opening = Opening(run, originalRoom);
            var openingPlayerRng = JsonSerializer.SerializeToNode(run.Players[0].PlayerRng.ToSerializable(), CompleteJson)!;
            int steps = originalSession.Map!.Steps;
            int remaining = originalSession.Map.RemainingSteps;
            long entry = originalSession.Map.CreateSave().NextEntry;
            await NextPlayerTurn(originalRoom, state);
            Check(originalMinion.SleepTurnCount == 1 && originalMinion.DebuffTurnCount == 0 && !originalMinion.IsAwake &&
                originalMinion.Creature.GetPowerAmount<AsleepLagavulinPower>() == 2,
                "A complete original enemy phase really performs the left minion's Sleep once and decrements its native sleep power to two.");
            int threshold = originalMatriarch.Creature.MaxHp / 2;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), originalMatriarch.Creature,
                originalMatriarch.Creature.CurrentHp - threshold, ValueProp.Unblockable | ValueProp.Unpowered, run.Players[0].Creature);
            Check(originalMatriarch.Creature.IsAlive && originalMatriarch.Creature.CurrentHp == threshold &&
                originalMatriarch.IsAwake && originalMatriarch.IsShellAwake &&
                !originalMatriarch.Creature.HasPower<AsleepPower>() && !originalMatriarch.Creature.HasPower<PlatingPower>(),
                "Actual unblocked damage reaches the halved Boss's own half-health threshold, wakes body/shell and removes its original sleep/plating without killing it.");
            await NextPlayerTurn(originalRoom, state);
            Check(originalMatriarch.IsAwake && originalMatriarch.IsShellAwake &&
                originalMinion is { StartsAwake: false, IsAwake: false, SleepTurnCount: 2, DebuffTurnCount: 0 } &&
                originalMinion.Creature.GetPowerAmount<AsleepLagavulinPower>() == 1 &&
                originalMinion.NextMove.Id == AllMatters.Monsters.Lagavulin.SLEEP,
                "The real following enemy phase performs the Matriarch's stunned wake continuation while the untouched Minion sleeps a second time and retains its last sleep turn.");
            var generated = originalRoom.CombatState.CreateCard<DefendIronclad>(run.Players[0]);
            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, run.Players[0]);
            await CreatureCmd.GainBlock(run.Players[0].Creature, 11, ValueProp.Unpowered, null);
            await CreatureCmd.GainBlock(originalMatriarch.Creature, 13, ValueProp.Unpowered, null);
            await CreatureCmd.GainBlock(originalMinion.Creature, 17, ValueProp.Unpowered, null);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 2, run.Players[0].Creature, null);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), originalMatriarch.Creature, 3, originalMatriarch.Creature, null);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), originalMinion.Creature, 4, originalMinion.Creature, null);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), run.Players[0].Creature, 7,
                ValueProp.Unblockable | ValueProp.Unpowered, originalMatriarch.Creature);
            await PlayerCmd.GainGold(19, run.Players[0]);
            var movedCard = PileType.Hand.GetPile(run.Players[0]).Cards.First(card => !ReferenceEquals(card, generated));
            await CardPileCmd.Add(movedCard, PileType.Exhaust);
            Check(!PileType.Hand.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Exhaust.GetPile(run.Players[0]).Cards.Contains(movedCard) &&
                PileType.Hand.GetPile(run.Players[0]).Cards.Contains(generated) &&
                !JsonNode.DeepEquals(Opening(run, originalRoom)["CombatPlayers"], opening["CombatPlayers"]) &&
                originalMatriarch.Creature.Block >= 13 && originalMinion.Creature.Block >= 17 &&
                run.Players[0].Creature.GetPowerAmount<StrengthPower>() == 2 &&
                originalMatriarch.Creature.GetPowerAmount<StrengthPower>() == 3 && originalMinion.Creature.GetPowerAmount<StrengthPower>() == 4,
                "Actual commands leave changed HP/gold, block and powers on both sides, an exhausted original card and a generated combat-only card before save.");
            run.Rng.Niche.NextInt(100);
            run.Players[0].PlayerRng.Shops.NextInt(100);
            originalMatriarch.Rng.NextInt(100);
            originalMinion.Rng.NextInt(100);
            var disturbed = Opening(run, originalRoom);
            Check(!JsonNode.DeepEquals(disturbed["Boundary"]!["RunRng"], opening["Boundary"]!["RunRng"]) &&
                !JsonNode.DeepEquals(disturbed["Boundary"]!["Players"], opening["Boundary"]!["Players"]) &&
                !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(run.Players[0].PlayerRng.ToSerializable(), CompleteJson), openingPlayerRng) &&
                !JsonNode.DeepEquals(disturbed["Enemies"]![0]!["Rng"], opening["Enemies"]![0]!["Rng"]) &&
                !JsonNode.DeepEquals(disturbed["Enemies"]![1]!["Rng"], opening["Enemies"]![1]!["Rng"]),
                "Independent samples prove the old run RNG, player state/RNG and both distinct monster RNGs differ before native whole-battle rollback save.");
            await JigsawNativeSaveHost.Save(null);
            await originalSession.WaitForSave();
            var active = saves.LoadRunSave();
            Check(active.Success && active.SaveData != null && active.SaveData.PreFinishedRoom == null,
                "An active Matriarch save contains the native whole before-roll point, not the old awakened shell, sleep counts or changed combat piles.");
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
            var matriarch = MatriarchIn(restoredRoom);
            var minion = MinionIn(restoredRoom);
            Check(!ReferenceEquals(restored, run) && !ReferenceEquals(restoredSession, originalSession) &&
                !ReferenceEquals(restoredRoom, originalRoom) && !ReferenceEquals(matriarch, originalMatriarch) &&
                !ReferenceEquals(matriarch.Creature, originalMatriarch.Creature) && !ReferenceEquals(minion, originalMinion) &&
                !ReferenceEquals(minion.Creature, originalMinion.Creature),
                "Fresh lobby restoration creates new run, session, room and both Matriarch/Minion model and creature identities.");
            CheckOpening(restoredRoom);
            Check(restored.CurrentActIndex == 0 && JigsawCheckpoint.Slot.TryGet(restored, out var restoredCheckpoint) && restoredCheckpoint.Act == 0 &&
                new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }
                    .All(pile => !pile.GetPile(restored.Players[0]).Cards.Contains(generated)),
                "The restored native/logical first-act owners agree and no old generated combat-only card survives among all five new piles.");
            Equal(Opening(restored, restoredRoom), opening,
                "The fresh native opening restores complete players, ordered five piles, energy, both sides' block/powers/display amounts, run/player/both monster RNGs, acts, complete map/history, round/side/checkpoint act, enemy order/slots/HP and all original sleeping counters.");
            Check(restoredSession.Map is { CurrentRoomHunted: true } recovered && recovered.CurrentNode == target &&
                recovered.Steps == steps && recovered.RemainingSteps == remaining && recovered.CreateSave().NextEntry == entry,
                "Fresh reconstruction confirms the same hunted entry once without another budget or exploration charge.");
            await restoredSession.Enter(target);
            CheckOpening(restoredRoom);
            Equal(Opening(restored, restoredRoom), opening,
                "Duplicate restored destination input cannot repeat HP halving, Minion/power grants, combat startup or logical entry charges.");

            int rewardsBeforeVictory = state.Rewards.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), restored.Players[0].Creature, 9,
                ValueProp.Unblockable | ValueProp.Unpowered, matriarch.Creature);
            await PlayerCmd.GainGold(23, restored.Players[0]);
            restored.Rng.Niche.NextInt(100);
            restored.Players[0].PlayerRng.Shops.NextInt(100);
            Check(matriarch.Creature.IsAlive && minion.Creature.IsAlive && !minion.IsAwake && minion.SleepTurnCount == 0 &&
                minion.Creature.GetPowerAmount<MinionPower>() == 1 && combatManager.IsInProgress && !combatManager.IsEnding,
                "The fresh sleeping required Boss and exactly one untouched Minion remain alive immediately before real lethal damage.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), matriarch.Creature, matriarch.Creature.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, restored.Players[0].Creature);
            Check(matriarch.Creature.IsDead && minion.Creature.IsDead && restoredRoom.CombatState.Enemies.Count == 0 &&
                !restoredRoom.CombatState.ContainsCreature(matriarch.Creature) && !restoredRoom.CombatState.ContainsCreature(minion.Creature) && combatManager.IsEnding,
                "Real lethal Matriarch damage runs the original primary-enemy death chain and naturally removes its still-sleeping Minion without killing it separately.");
            int healthBeforeVictory = restored.Players[0].Creature.CurrentHp;
            int expectedVictoryHealth = Math.Min(restored.Players[0].Creature.MaxHp, healthBeforeVictory + 6);
            Check(restored.Players[0].GetRelic<BurningBlood>() != null,
                "The original Ironclad still owns native BurningBlood for the independent six-health victory prediction.");
            Check(await combatManager.CheckWinCondition(), "The original native win check accepts actual Matriarch and natural Minion death and finalizes victory.");
            await Until(() => restoredRoom.IsPreFinished && victoryBoundary != null,
                "Actual Boss death and the original victory coordinator naturally save the prefinished terminal room.");
            await restoredSession.WaitForSave();
            Check(restored.Players[0].Creature.CurrentHp == expectedVictoryHealth && restored.Players[0].Creature.Block == 0 &&
                restored.Players[0].Creature.Powers.Count == 0 && !restoredSession.Map!.HuntingActive &&
                restored.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()) && state.Rewards.Count == rewardsBeforeVictory,
                "Native victory cleanup heals exactly six, clears combat block/powers and releases final-Boss hunting/masks before any terminal reward is generated.");
            var won = saves.LoadRunSave();
            Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: null } &&
                won.SaveData.PreFinishedRoom.EncounterId == ModelDb.Encounter<LagavulinMatriarchBoss>().Id,
                "The original victory flow saves a genuine top-level Matriarch reward checkpoint, not the older active rollback source.");
            Equal(Boundary(restored), victoryBoundary!, "Victory finalization retains the independent state sampled immediately before actual serialization.");
            await restoredRoom.OfferRoomEndRewards();
            await Until(() => state.Rewards.Count == rewardsBeforeVictory + 1,
                "The original single-act Underdocks Matriarch room really generates and offers one native empty terminal reward set.");
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
                "Native prefinished room loading regenerates the saved Matriarch's reward offer.");
            var rewardRun = second.Native!;
            var rewardRoom = (CombatRoom)rewardRun.CurrentRoom!;
            var secondRewards = state.Rewards.Last();
            Check(rewardRun.CurrentRoomCount == 1 && rewardRoom is { IsPreFinished: true, Encounter: LagavulinMatriarchBoss, ParentEventId: null } &&
                rewardRoom.CombatState.Enemies.Count == 0 && state.Openings.Count == openingCount && state.Startups.Count == startupCount &&
                !ReferenceEquals(rewardRun, restored) && !ReferenceEquals(secondRewards, firstRewards),
                "The second fresh lobby restores one top-level prefinished room with no combat opening, revived Matriarch/Minion or running sleep counters.");
            CheckRewards(secondRewards);
            await FinishOffers(state);
            Check(JigsawCheckpoint.Slot.TryGet(rewardRun, out var terminalCheckpoint) && terminalCheckpoint.Act == 0 &&
                JigsawNativeSession.TryGet(rewardRun)!.Map is { HuntingActive: false } &&
                rewardRun.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()),
                "The new terminal lobby retains the original released first-act root and cannot restore a cleared hunting mask.");
            Equal(JsonSerializer.SerializeToNode(rewardRoom.ToSerializable(), CompleteJson)!, victoryRoom!, "Reward loading preserves the original saved native room descriptor.");
            Equal(Map(rewardRun), victoryMap!, "Reward loading preserves the independently sampled victory root, full map, steps, budget and entry identity.");
            Equal(Map(rewardRun), generatedMap, "Reward generation itself never changes exploration state.");
            Equal(Boundary(rewardRun), generatedBoundary, "Fresh reward generation retains the original complete players, all RNG, acts and history at the matching post-generation stage.");
            Equal(RewardContents(secondRewards), rewardContents, "Fresh native single-act Underdocks Matriarch rewards exactly reproduce the original empty terminal contents.");
            Console.WriteLine($"MATRIARCH_FRESH act={rewardRun.CurrentActIndex} original=1 active=1 new-lobbies=2 process=same-process-new-runs opening={state.Openings.Count} old-awake=true old-shell=true old-minion-sleep=2 restored-awake=false restored-shell=false restored-minion-sleep=0 natural-minion-clear=1 reward-opening=0 terminal-rewards=0 reward-sync=completed steps={steps} remaining={remaining} entry={entry}");
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
                throw new AggregateException("Matriarch fresh-lobby cleanup failed.", bodyFailure == null ? cleanupFailures : new[] { bodyFailure }.Concat(cleanupFailures));
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

    private static LagavulinMatriarch MatriarchIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<LagavulinMatriarch>().Single();
    private static AllMatters.Monsters.Lagavulin MinionIn(CombatRoom room) => room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<AllMatters.Monsters.Lagavulin>().Single();
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
    private static void CheckOpening(CombatRoom room)
    {
        var matriarch = MatriarchIn(room);
        var minion = MinionIn(room);
        var boss = matriarch.Creature;
        var companion = minion.Creature;
        var players = room.CombatState.Players;
        Check(room is { Encounter: LagavulinMatriarchBoss, ParentEventId: null, RoomType: RoomType.Boss } && room.CombatState.Enemies.Count == 2 &&
            ReferenceEquals(room.CombatState.Enemies[0], companion) && ReferenceEquals(room.CombatState.Enemies[1], boss) &&
            room.Encounter.MonstersWithSlots.Select(pair => pair.Item1).SequenceEqual(new MonsterModel[] { minion, matriarch }) &&
            room.Encounter.MonstersWithSlots.All(pair => pair.Item2 == null) && !room.Encounter.HasScene &&
            boss.SlotName == null && boss.IsPrimaryEnemy && !boss.IsSecondaryEnemy && !boss.HasPower<MinionPower>() &&
            boss.CurrentHp == boss.MaxHp && matriarch.MinInitialHp == 222 && matriarch.MaxInitialHp == 222 &&
            boss.MaxHp == (int)Creature.ScaleHpForMultiplayer(222, room.Encounter, players.Count, 0) / 2 &&
            matriarch is { IsAwake: false, IsShellAwake: false } && matriarch.NextMove.Id == "SLEEP_MOVE" &&
            boss.Powers.Count == 2 && boss.GetPowerAmount<AsleepPower>() == 3 && boss.GetPowerAmount<PlatingPower>() == 12,
            "The original A0 Matriarch remains second and unslotted, full native scaled HP halved exactly once, body/shell asleep, original Sleep and only Asleep3/Plating12.");
        Check(companion.SlotName == null && companion.IsSecondaryEnemy && !companion.IsPrimaryEnemy &&
            companion.CurrentHp == companion.MaxHp && minion.MinInitialHp == 109 && minion.MaxInitialHp == 111 &&
            Enumerable.Range(109, 3).Any(hp => companion.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, room.Encounter, players.Count, 0)) &&
            minion is { StartsAwake: false, IsAwake: false, SleepTurnCount: 0, DebuffTurnCount: 0 } &&
            minion.NextMove.Id == AllMatters.Monsters.Lagavulin.SLEEP && companion.Powers.Count == 3 &&
            companion.GetPowerAmount<MinionPower>() == 1 && companion.GetPowerAmount<AsleepLagavulinPower>() == 3 &&
            companion.GetPowerAmount<AllMatters.Powers.MetallicizePower>() == 8 && companion.Block == 8 &&
            ReferenceEquals(matriarch.RunRng, room.CombatState.RunState.Rng) && ReferenceEquals(minion.RunRng, room.CombatState.RunState.Rng) &&
            !ReferenceEquals(matriarch.Rng, minion.Rng),
            "The one left native Mod Lagavulin preserves its scaled original HP, sleep/zero counters and original first-turn Metal8 block, plus exactly Minion1 and its distinct native RNG.");
        var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().ToArray();
        Check(grants.Length == 5 && grants.Count(entry => ReferenceEquals(entry.Actor, boss) && entry.Power is AsleepPower && entry.Amount == 3) == 1 &&
            grants.Count(entry => ReferenceEquals(entry.Actor, boss) && entry.Power is PlatingPower && entry.Amount == 12) == 1 &&
            grants.Count(entry => ReferenceEquals(entry.Actor, companion) && entry.Power is AsleepLagavulinPower && entry.Amount == 3) == 1 &&
            grants.Count(entry => ReferenceEquals(entry.Actor, companion) && entry.Power is AllMatters.Powers.MetallicizePower && entry.Amount == 8) == 1 &&
            grants.Count(entry => ReferenceEquals(entry.Actor, companion) && entry.Power is MinionPower && entry.Amount == 1) == 1 &&
            !CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Any() &&
            players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1 && player.Creature.Block == 0 && player.Creature.Powers.Count == 0),
            "Actual first-turn history proves five exact owner grants once, no damage/wake shortcut and exactly one mask per owner before taking the complete opening sample.");
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
            Matriarch = enemy.Monster is LagavulinMatriarch matriarch ? new { matriarch.IsAwake, matriarch.IsShellAwake } : null,
            Minion = enemy.Monster is AllMatters.Monsters.Lagavulin minion ? new
            { minion.StartsAwake, minion.IsAwake, minion.SleepTurnCount, minion.DebuffTurnCount } : null,
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
    private static void CheckRewards(RewardsSet set)
    {
        var run = set.Player.RunState;
        bool completed = RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(set);
        string items = string.Join(',', set.Rewards.Select(reward =>
            $"{reward.GetType().Name}:populated={reward.IsPopulated}:selected={reward.SuccessfullySelected}"));
        // Both current native versions omit default rewards for a final-act
        // Boss. The synchronizer immediately completes that exact empty set.
        Check(set.Player.NetId == 1 && run.Act is Underdocks && run.CurrentActIndex == run.Acts.Count - 1 &&
            set.Room is CombatRoom { Encounter: LagavulinMatriarchBoss, IsPreFinished: true, ParentEventId: null, RoomType: RoomType.Boss } room && room.ExtraRewards.Count == 0 &&
            set.Rewards.Count == 0 && completed,
            $"The native single-act Underdocks Matriarch offer is an empty completed terminal set: owner={set.Player.NetId}, room={set.Room?.GetType().Name}, act={run.CurrentActIndex}/{run.Acts.Count}, count={set.Rewards.Count}, completed={completed}, items=[{items}].");
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
