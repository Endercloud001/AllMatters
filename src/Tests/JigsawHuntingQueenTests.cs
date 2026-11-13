using AllMatters.Jigsaw;
using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingQueenTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Queen real amalgam death replaces every supported Standard intent with Rekindle", RealDeathIntent),
     ("Jigsaw Queen native death ignores prevention foreign amalgams duplicates and excluded roots", DeathBoundaries),
     ("Jigsaw Queen native Rekindle creates two independent scaled Minions and Enrage never refills them", SummonAndEnrage),
     ("Jigsaw Queen native coordinator skips birth attacks and attacks directly in the next enemy phase", NativeTurns),
     ("Jigsaw Queen awaited actions reject stale combat identities and preserve dependency failures", AwaitedWork),
     ("Jigsaw Queen real native boss entry keeps original creation and preloads Rekindle assets", NativeEntry),
     ("Jigsaw Queen actual TorchHead factory Spine bilingual copy and declared scene slots load", Presentation)];

    private static async Task RealDeathIntent()
    {
        // The current Jigsaw entry supports Standard; beta Dread is outside this slice.
        foreach (string move in new[] { "PUPPET_STRINGS_MOVE", "YOU_ARE_MINE_MOVE", "BURN_BRIGHT_FOR_ME_MOVE",
            "OFF_WITH_YOUR_HEAD_MOVE", "EXECUTION_MOVE", "ENRAGE_MOVE" })
        {
            using var battle = new QueenBattle();
            await battle.Queen.AfterAddedToRoom();
            CheckAssetEnumeration(battle);
            Check(battle.Combat.Enemies.Count == 2 && battle.Queen.NextMove.Id == "PUPPET_STRINGS_MOVE" &&
                battle.Amalgam.Creature.IsAlive, "The unmodified QueenBoss creates its original Queen and living amalgam with native opening intent.");
            Force(battle.Queen, move);
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            Check(battle.Amalgam.Creature.IsDead && battle.Queen.Creature.IsAlive && battle.Queen.NextMove.Id == "REKINDLE",
                $"A real native amalgam death must replace every current Queen intent: before={move}, expected=REKINDLE, actual={battle.Queen.NextMove.Id}.");
            CheckAssetEnumeration(battle);
            battle.Queen.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Queen.NextMove.Id == "REKINDLE", "An unperformed forced Rekindle cannot be skipped by the next native intent roll.");
        }
    }

    public sealed class QueenDeathShield : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
        public override bool ShouldDie(Creature creature) => creature != Owner;
        public override async Task AfterPreventingDeath(Creature creature)
        {
            if (creature != Owner) return;
            await CreatureCmd.Heal(creature, 1);
            await PowerCmd.Remove(this);
        }
    }

    private static async Task DeathBoundaries()
    {
        ModelDb.Inject(typeof(QueenDeathShield));
        using (var battle = new QueenBattle())
        {
            await battle.Queen.AfterAddedToRoom();
            Force(battle.Queen, "BURN_BRIGHT_FOR_ME_MOVE");
            await PowerCmd.Apply<QueenDeathShield>(new ThrowingPlayerChoiceContext(), battle.Amalgam.Creature, 1, battle.Queen.Creature, null);
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            Check(battle.Amalgam.Creature.CurrentHp == 1 && battle.Queen.NextMove.Id == "BURN_BRIGHT_FOR_ME_MOVE" &&
                !(bool)AccessTools.Property(typeof(Queen), "HasAmalgamDied").GetValue(battle.Queen)!,
                "Real death prevention retains the living amalgam, current intent and native first-phase flag.");
            var foreign = await CreatureCmd.Add<TorchHeadAmalgam>(battle.Combat, "amalgam");
            await CreatureCmd.Kill(foreign);
            Check(battle.Queen.NextMove.Id == "BURN_BRIGHT_FOR_ME_MOVE", "A different actual amalgam's real death cannot hijack this Queen's phase.");
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            Check(battle.Queen.NextMove.Id == "REKINDLE", "The later actual death remains eligible after a prevented and foreign death.");
            await Perform(battle.Queen);
            CheckAssetEnumeration(battle);
            Force(battle.Queen, "BURN_BRIGHT_FOR_ME_MOVE");
            await NotifyDeath(battle);
            Check(battle.Queen.NextMove.Id == "BURN_BRIGHT_FOR_ME_MOVE" && Torches(battle).Length == 2,
                "A repeated native death notification neither replaces another intent nor creates replacement torches.");
        }
        foreach (string scope in new[] { "safe", "event", "non-jigsaw", "other-act", "ordinary", "no-acts", "closed" })
        {
            using var battle = new QueenBattle(scope: scope);
            await battle.Queen.AfterAddedToRoom();
            if (scope == "closed") JigsawNativeSession.TryGet(battle.Run)!.Close();
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            Check(battle.Queen.NextMove.Id == "PUPPET_STRINGS_MOVE" && Torches(battle).Length == 0 &&
                battle.Combat.Encounter!.Slots.SequenceEqual(new[] { "amalgam", "queen" }),
                $"Native Queen behavior remains unenhanced in excluded scope={scope}, including an actual no-acts run.");
            if (scope is "non-jigsaw" or "safe")
                Check(!battle.Combat.Encounter.GetAssetPaths(battle.Run).Contains(JigsawHuntingQueen.ScenePath),
                    "The real native asset consumer excludes the hunted scene in " + scope);
        }
    }

    private static async Task SummonAndEnrage()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new QueenBattle(count);
            await battle.Queen.AfterAddedToRoom();
            battle.Combat.CurrentSide = CombatSide.Enemy;
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            int health = battle.Players.Sum(player => player.Creature.CurrentHp);
            await Perform(battle.Queen);
            var torches = Torches(battle);
            int hp = (int)Creature.ScaleHpForMultiplayer(50, battle.Combat.Encounter, count, 2);
            Check(torches.Length == 2 && !ReferenceEquals(torches[0], torches[1]) && torches.All(torch =>
                torch.MaxHp == hp && torch.CurrentHp == hp && torch.HasPower<MinionPower>() && torch.IsSecondaryEnemy &&
                torch.Monster is TorchHead { MinInitialHp: 50, MaxInitialHp: 50 } && torch.Monster.NextMove.Id == "ATTACK") &&
                battle.Queen.NextMove.Id == "ENRAGE_MOVE" && battle.Players.Sum(player => player.Creature.CurrentHp) == health,
                $"Real Rekindle creates two distinct native-scaled HP{hp} Minions with the single attack and does no damage for roles={count}.");
            var grants = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Where(entry => entry.Power is MinionPower && torches.Contains(entry.Actor)).ToArray();
            Check(grants.Length == 2 && grants.All(entry => entry.Amount == 1), "Each actual torch receives exactly one native Minion grant.");
            var unrelated = await CreatureCmd.Add<TorchHead>(battle.Combat, JigsawHuntingQueen.LeftTorch);
            await Perform(battle.Queen);
            Check(battle.Queen.Creature.GetPowerAmount<StrengthPower>() == 2 && torches.All(torch => torch.GetPowerAmount<StrengthPower>() == 2) &&
                unrelated.GetPowerAmount<StrengthPower>() == 0 && battle.Queen.NextMove.Id == "OFF_WITH_YOUR_HEAD_MOVE",
                "The original Enrage grants Queen exactly two and appends two only to its owned torches, then enters the native attack loop.");
            var owner = battle.Players[0];
            int maxBefore = owner.Creature.MaxHp;
            torches[0].SetCurrentHpInternal(1);
            await FeedTarget(torches[0]);
            Check(torches[0].IsDead && owner.Creature.MaxHp == maxBefore,
                "A real native Feed lethally damages a TorchHead without triggering its Fatal Max HP reward.");
            var primary = await CreatureCmd.Add<FungusBeast>(battle.Combat);
            primary.SetCurrentHpInternal(1);
            await FeedTarget(primary);
            Check(primary.IsDead && owner.Creature.MaxHp == maxBefore + 3,
                "The same real Feed chain grants its native three Max HP when killing a primary enemy control.");
            Force(battle.Queen, "ENRAGE_MOVE");
            await Perform(battle.Queen);
            Force(battle.Queen, "ENRAGE_MOVE");
            await Perform(battle.Queen);
            Check(battle.Queen.Creature.GetPowerAmount<StrengthPower>() == 6 && torches[1].GetPowerAmount<StrengthPower>() == 6 &&
                torches[0].IsDead && unrelated.GetPowerAmount<StrengthPower>() == 0 && Torches(battle).Length == 2,
                "Repeated Enrage buffs the surviving owned torch without reviving the dead torch, refilling its slot or touching another source's torch.");
            using var ending = new JigsawHuntingCombatTests.NativeEndingScope();
            await CreatureCmd.Kill(battle.Queen.Creature);
            Check(torches[1].IsDead && unrelated.IsDead && battle.Combat.Enemies.Count == 0 && CombatManager.Instance.IsEnding,
                "Native primary-enemy death cleans up remaining secondary Minions and releases the real victory condition without extra player kills.");

            async Task FeedTarget(Creature target)
            {
                var feed = battle.Combat.CreateCard<Feed>(owner);
                await CardPileCmd.Add(feed, PileType.Discard);
                await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), feed, target, skipCardPileVisuals: true);
            }
        }
    }

    private sealed class AwaitProbe(Creature queen)
    {
        internal readonly Creature Queen = queen;
        internal readonly TaskCompletionSource Reached = new();
        internal readonly TaskCompletionSource Release = new();
        internal Exception? Failure;
    }
    private static AwaitProbe? pause;
    private static void PauseCast(Creature creature, string triggerName, ref Task __result)
    {
        if (pause is { } probe && ReferenceEquals(creature, probe.Queen) && triggerName == "Cast") __result = Hold(__result, probe);
        static async Task Hold(Task original, AwaitProbe probe)
        {
            await original;
            probe.Reached.TrySetResult();
            await probe.Release.Task;
            if (probe.Failure != null) throw probe.Failure;
        }
    }

    private static async Task AwaitedWork()
    {
        var patch = new Harmony("AllMatters.Tests.QueenAwait");
        patch.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingQueenTests), nameof(PauseCast)));
        try
        {
            foreach (string boundary in new[] { "session", "map", "room", "removed", "failure" })
            {
                using var battle = new QueenBattle();
                await battle.Queen.AfterAddedToRoom();
                await CreatureCmd.Kill(battle.Amalgam.Creature);
                var probe = new AwaitProbe(battle.Queen.Creature);
                pause = probe;
                var operation = battle.Queen.PerformMove();
                try
                {
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && Torches(battle).Length == 0, "Real Rekindle is awaiting its native animation before adding either torch.");
                    if (boundary == "session") JigsawNativeSession.TryGet(battle.Run)!.Close();
                    if (boundary == "map")
                    { JigsawCheckpoint.Capture(battle.Run, BossRoot(battle.Players[0].NetId)); JigsawNativeSession.Restore(battle.Run); }
                    if (boundary == "room") battle.Run.PushRoom(new CombatRoom(battle.Combat));
                    if (boundary == "removed") battle.Combat.RemoveCreature(battle.Queen.Creature);
                    if (boundary == "failure") probe.Failure = new InvalidOperationException("Injected native Queen animation failure");
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check((boundary == "failure" ? ReferenceEquals(failure, probe.Failure) : failure == null) && Torches(battle).Length == 0,
                        "Awaited Rekindle rejects stale identity or propagates the exact dependency failure: " + boundary);
                }
                finally { probe.Release.TrySetResult(); try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { } pause = null; }
            }
            foreach (bool fail in new[] { false, true })
            {
                using var battle = new QueenBattle();
                await battle.Queen.AfterAddedToRoom();
                await CreatureCmd.Kill(battle.Amalgam.Creature);
                await Perform(battle.Queen);
                var torches = Torches(battle);
                var probe = new AwaitProbe(battle.Queen.Creature);
                pause = probe;
                var operation = battle.Queen.PerformMove();
                try
                {
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    if (fail) probe.Failure = new InvalidOperationException("Injected original Enrage animation failure");
                    else JigsawNativeSession.TryGet(battle.Run)!.Close();
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check((fail ? ReferenceEquals(failure, probe.Failure) : failure == null) &&
                        torches.All(torch => torch.GetPowerAmount<StrengthPower>() == 0) &&
                        battle.Queen.Creature.GetPowerAmount<StrengthPower>() == (fail ? 0 : 2),
                        "The original Enrage keeps its own result or error while stale/failed work adds no torch Strength.");
                }
                finally { probe.Release.TrySetResult(); try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { } pause = null; }
            }
        }
        finally { pause = null; patch.UnpatchAll(patch.Id); }
    }

    private static async Task Perform(MonsterModel monster)
    { await monster.PerformMove(); monster.RollMove(monster.CombatState.PlayerCreatures); }
    private static void Force(MonsterModel monster, string move) => monster.SetMoveImmediate((MoveState)monster.MoveStateMachine!.States[move], true);
    private static Creature[] Torches(QueenBattle battle) => battle.Combat.Enemies.Where(enemy => enemy.Monster is TorchHead).ToArray();
    private static void CheckAssetEnumeration(QueenBattle battle)
    {
        var machine = battle.Queen.MoveStateMachine;
        var move = battle.Queen.NextMove;
        for (int pass = 0; pass < 2; pass++) _ = battle.Combat.Encounter!.GetAssetPaths(battle.Run).ToArray();
        Check(ReferenceEquals(machine, battle.Queen.MoveStateMachine) && ReferenceEquals(move, battle.Queen.NextMove),
            "Repeated real intent/asset enumeration leaves the live native machine and current action unchanged before, during and after the once-only Rekindle.");
    }
    // The original native Kill already dispatched Hook.AfterDeath. Public's
    // dispatcher cannot construct a new choice context after it has detached
    // the dead target, so deliver the explicit duplicate to the same public
    // model callback with a valid native choice context.
    private static Task NotifyDeath(QueenBattle battle) => battle.Queen.AfterDeath(new ThrowingPlayerChoiceContext(), battle.Amalgam.Creature,
#if !STS2_PUBLIC
        null,
#endif
        false, 0f);

    private static Task? startup;
    private static void CaptureStartup(Task task) => startup = task;
    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
#if STS2_PUBLIC
            "StartCombatInternal");
#else
            "RunTurnLoopAfter");
#endif
        var capture = AccessTools.Method(typeof(JigsawHuntingQueenTests), nameof(CaptureStartup));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Dup);
            yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the one native room-loaded coordinator Task without repeating its startup.");
    }
    private static bool SkipFtue(ref bool __result) { __result = true; return false; }
    private static async Task Until(Func<bool> condition, string reason)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 300 && !condition(); frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(condition(), reason);
    }

    private static async Task NativeTurns()
    {
        var patch = new Harmony("AllMatters.Tests.QueenTurns");
        patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingQueenTests), nameof(SkipFtue)));
        patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
            transpiler: new HarmonyMethod(typeof(JigsawHuntingQueenTests), nameof(ObserveStartup)));
        try
        {
            for (int count = 1; count <= 4; count++)
            {
                using var battle = new QueenBattle(count);
                foreach (var player in battle.Players)
                {
                    player.Creature.SetMaxHpInternal(5000);
                    player.Creature.SetCurrentHpInternal(5000);
                    for (int card = 0; card < 30; card++)
                        PileType.Draw.GetPile(player).AddInternal(battle.Combat.CreateCard<DefendIronclad>(player));
                }
                using var ending = new JigsawHuntingCombatTests.NativeEndingScope();
                var manager = CombatManager.Instance;
                int readyRound = 0;
                void Ready(CombatState state) { if (state.CurrentSide == CombatSide.Player) readyRound = state.RoundNumber; }
                manager.TurnStarted += Ready;
                Task? operation = null;
                try
                {
                    startup = null;
                    manager.AfterCombatRoomLoaded();
                    operation = startup ?? throw new InvalidOperationException("The original Queen coordinator Task was not captured.");
                    await Until(() => readyRound == battle.Combat.RoundNumber && battle.Players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5),
                        "The single real native coordinator completes opening hand setup for every combat role.");
                    await CreatureCmd.Kill(battle.Amalgam.Creature);
                    int[] health = battle.Players.Select(player => player.Creature.CurrentHp).ToArray();
                    int round = battle.Combat.RoundNumber;
                    manager.SetReadyToEndTurn(battle.Players[0], false);
                    await Until(() => readyRound > round && battle.Combat.CurrentSide == CombatSide.Player &&
                        battle.Players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5), "Readiness drives one whole native Rekindle enemy phase.");
                    var torches = Torches(battle);
                    Check(torches.Length == 2 && torches.All(torch => torch.Monster!.NextMove.Id == "ATTACK" &&
                        torch.GetPowerAmount<StrengthPower>() == 0) &&
                        battle.Players.Select(player => player.Creature.CurrentHp).SequenceEqual(health) &&
                        !CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Any(entry => entry.Monster is TorchHead),
                        "Real enemy snapshot and birth flags skip all torch attacks in the summon phase while preparing the direct attack intent.");
                    round = battle.Combat.RoundNumber;
                    manager.SetReadyToEndTurn(battle.Players[0], false);
                    await Until(() => readyRound > round && battle.Combat.CurrentSide == CombatSide.Player &&
                        battle.Players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5), "Readiness drives the next complete native enemy phase exactly once.");
                    var torchMoves = CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Where(entry => entry.Monster is TorchHead).ToArray();
                    Check(torchMoves.Length == 2 && torches.All(torch => torchMoves.Count(entry => ReferenceEquals(entry.Monster, torch.Monster) && entry.Move.Id == "ATTACK") == 1) &&
                        battle.Players.Select((player, index) => health[index] - player.Creature.CurrentHp).All(damage => damage == 20) &&
                        torches.All(torch => torch.GetPowerAmount<StrengthPower>() == 2) &&
                        battle.Queen.Creature.GetPowerAmount<StrengthPower>() == 2 && battle.Queen.NextMove.Id == "OFF_WITH_YOUR_HEAD_MOVE",
                        $"For {count} actual combat roles, next enemy phase attacks twice for fixed ten per player before native Enrage gives each owned torch and Queen two Strength.");
                }
                finally
                {
                    manager.Reset(true);
                    try { if (operation != null) try { await operation; } catch (OperationCanceledException) { } }
                    finally { manager.TurnStarted -= Ready; }
                }
            }
        }
        finally { startup = null; CombatManager.Instance.Reset(true); patch.UnpatchAll(patch.Id); }
    }

    private static bool SkipIo() => false;
    private static async Task NativeEntry()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_QUEEN_NATIVE_ENTRY");
        run.CurrentActIndex = 2;
        var manager = RunManager.Instance;
        ulong? previousNetId = LocalContext.NetId;
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var previousSize = viewport.Size;
        var patch = new Harmony("AllMatters.Tests.QueenEntryIo");
        try
        {
            manager.SetUpTest(run, new NetSingleplayerGameService());
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = player.NetId;
            viewport.Size = new Vector2I(1920, 1080);
            JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenTests), nameof(SkipIo)));
            patch.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenTests), nameof(SkipIo)));
            using (HeadlessCombatInProgress.EnterEvent())
            {
                manager.GenerateRooms();
                await manager.GenerateMap();
                await manager.EnterRoom(new MapRoom());
                JigsawCheckpoint.Capture(run, BossRoot(player.NetId));
                JigsawNativeSession.Restore(run);
                var projection = JigsawEntryProjection.Append(run.Map, 1, JigsawRoom.Boss);
                run.Map = projection.Map;
                Check(run.AddVisitedMapCoord(projection.Point.coord), "The selected boss input has a native coordinate before room entry.");
                run.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, ModelDb.Encounter<QueenBoss>().Id);
                // Select this encounter as the room-entry input; the native
                // room owns every generation, preload and creature operation.
                await manager.EnterRoom(new CombatRoom(ModelDb.Encounter<QueenBoss>().ToMutable(), run));
            }
            var room = (CombatRoom)run.CurrentRoom!;
            var queen = room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<Queen>().Single();
            var amalgam = room.CombatState.Enemies.Select(enemy => enemy.Monster).OfType<TorchHeadAmalgam>().Single();
            Check(room.CombatState.Enemies.Count == 2 && room.Encounter.MonstersWithSlots.Select(pair => pair.Item2).SequenceEqual(new[] { "amalgam", "queen" }) &&
                queen.MoveStateMachine!.States.ContainsKey("REKINDLE") && TorchesIn(room.CombatState).Length == 0,
                "Actual native EnterRoom preserves the original two-monster creation and installs Rekindle before any torch exists.");
            Check(room.Encounter.GetAssetPaths(run).Contains(JigsawHuntingQueen.ScenePath) &&
                room.Encounter.GetAssetPaths(run).Contains(ModelDb.Monster<TorchHead>().CustomVisualsPath!),
                "The real native preload consumer includes the formal encounter scene and independently registered TorchHead visual resource.");
            var scene = room.Encounter.CreateScene();
            try
            {
                Check(room.Encounter.Slots.All(slot => scene.GetNodeOrNull<Marker2D>(slot) != null), "The native scene consumer instantiates every declared summon slot.");
            }
            finally { scene.Free(); }
            await CreatureCmd.Kill(amalgam.Creature);
            Check(queen.NextMove.Id == "REKINDLE", "Real room-created Queen recognizes the actual original amalgam's native death.");
        }
        finally
        {
            patch.UnpatchAll(patch.Id);
            JigsawNativeSaveHost.Save = null;
            JigsawNativeSession.TryGet(run)?.Close();
            CombatManager.Instance.Reset(true);
            manager.CleanUp(false);
            LocalContext.NetId = previousNetId;
            viewport.Size = previousSize;
        }
    }

    private static Creature[] TorchesIn(CombatState combat) => combat.Enemies.Where(enemy => enemy.Monster is TorchHead).ToArray();

    private static async Task Presentation()
    {
        using var battle = new QueenBattle();
        await PreloadManager.LoadRoomCombatAssets(battle.Combat.Encounter!, battle.Run);
        var scene = battle.Combat.Encounter!.CreateScene();
        try
        {
            Check(scene.GetNode<Marker2D>("amalgam").Position == new Vector2(1207, 709) &&
                scene.GetNode<Marker2D>("queen").Position == new Vector2(1606, 695) &&
                scene.GetNode<Marker2D>(JigsawHuntingQueen.LeftTorch).Position.X < scene.GetNode<Marker2D>(JigsawHuntingQueen.RightTorch).Position.X &&
                scene.GetNode<Marker2D>(JigsawHuntingQueen.RightTorch).Position.X < scene.GetNode<Marker2D>("queen").Position.X,
                "The formal scene preserves both original native markers and orders the two declared TorchHead slots to Queen's left.");
        }
        finally { scene.Free(); }
        var model = ModelDb.Monster<TorchHead>();
        using var visuals = model.CreateVisuals();
        var tree = (SceneTree)Engine.GetMainLoop();
        await HeadlessUiHost.Mount(visuals);
        try
        {
            var sprite = visuals.SpineBody!;
            for (int frame = 0; frame < 30 && !sprite.IsAnimationStateReady(); frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(sprite.IsAnimationStateReady() && new[] { "idle", "halberd_slam" }.All(sprite.GetSkeleton()!.GetData()!.HasAnimation),
                "The actual registered factory loads the independent TorchHead skeleton's real idle and halberd animations.");
            var animator = model.GenerateAnimator(sprite);
            animator.SetTrigger("Attack");
            var state = sprite.GetAnimationState();
#if STS2_PUBLIC
            var track = state.GetCurrent(0);
#else
            using var track = state.GetCurrent(0);
#endif
            Check(track?.GetAnimationName() == "halberd_slam", "The gameplay Attack trigger starts the real halberd animation.");
            state.Update(track!.GetAnimationDuration() + 0.1f); state.Apply(sprite.GetSkeleton()!);
            state.Update(0.01f); state.Apply(sprite.GetSkeleton()!);
#if STS2_PUBLIC
            var next = state.GetCurrent(0);
#else
            using var next = state.GetCurrent(0);
#endif
            Check(next?.GetAnimationName() == "idle", "Spine action completion returns to the looping idle without an invented hurt action.");
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                foreach (string key in new[] { "ALL_MATTERS_MONSTER_TORCH_HEAD.name", "ALL_MATTERS_MONSTER_TORCH_HEAD.moves.ATTACK.title", "QUEEN.moves.REKINDLE.title" })
                {
                    string text = new LocString("monsters", key).GetRawText();
                    Check(!string.IsNullOrWhiteSpace(text) && !text.Contains("ALL_MATTERS") && !text.Contains(".moves."), "Real localized monster/action text resolves: " + language + "/" + key);
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); tree.Root.RemoveChild(visuals); visuals.Free(); }
    }

    private sealed class QueenBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal Queen Queen { get; }
        internal TorchHeadAmalgam Amalgam { get; }
        internal Player[] Players { get; }

        internal QueenBattle(int count = 1, string scope = "hunted")
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            var player = Players[0];
            Run = RunState.CreateForTest([player], acts: scope == "no-acts" ? [] : [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_QUEEN_DEATH");
            if (scope != "no-acts") Run.CurrentActIndex = scope == "other-act" ? 1 : 2;
            var encounter = ModelDb.Encounter<QueenBoss>().ToMutable();
            Combat = new CombatState(encounter, Run) { RoundNumber = 1 };
            try
            {
                Combat.AddPlayer(player);
                player.ResetCombatState();
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = player.NetId;
                CombatManager.Instance.Reset(true);
                CombatManager.Instance.SetUpCombat(Combat);
                foreach (var extra in Players.Skip(1)) { extra.RunState = Run; Combat.AddPlayer(extra); extra.ResetCombatState(); }
                if (scope == "event") Run.PushRoom(new EventRoom(ModelDb.Event<FieldOfManSizedHoles>()));
                Run.PushRoom(new CombatRoom(Combat) { ParentEventId = scope == "event" ? ModelDb.Event<FieldOfManSizedHoles>().Id : null });
                if (scope is not ("non-jigsaw" or "no-acts"))
                {
                    JigsawCheckpoint.Capture(Run, BossRoot(player.NetId, scope != "safe", scope == "event" ? JigsawRoom.Unknown : scope == "ordinary" ? JigsawRoom.Combat : JigsawRoom.Boss));
                    JigsawNativeSession.Restore(Run);
                }
                var projection = JigsawEntryProjection.Append(Run.Map, 1, JigsawRoom.Boss);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), "The native boss has a real map coordinate before creature RNG initialization.");
                encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature);
                    monster.RollMove(Combat.PlayerCreatures);
                }
                Queen = Combat.Enemies.Select(enemy => enemy.Monster).OfType<Queen>().Single();
                Amalgam = Combat.Enemies.Select(enemy => enemy.Monster).OfType<TorchHeadAmalgam>().Single();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
            LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun BossRoot(ulong owner, bool hunted = true, JigsawRoom root = JigsawRoom.Boss)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss && previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == root && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "Public entry transitions establish the requested safe or hunted root.");
        return map;
    }

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
}
