using AllMatters.Jigsaw;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingMultiClawTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting MultiClaw native phase two intent and attack use nine damage", PhaseTwoAttack),
      ("Jigsaw hunting MultiClaw native setup leaves a non Jigsaw run with no acts untouched", NonJigsawSetup),
      ("Jigsaw hunting MultiClaw native consecutive attacks preserve ascension targets Strength and phase three", ConsecutiveAttacks),
      ("Jigsaw hunting MultiClaw native attacks retain every target for one to four combat players", CombatTargets) ];

    private static async Task CombatTargets()
    {
        for (int count = 1; count <= 4; count++)
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            // One real run owner plus native combat roles, not remote clients.
            var run = RunState.CreateForTest([players[0]],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                ascensionLevel: 9, seed: "HUNTING_87_MULTI_CLAW_TARGETS");
            run.CurrentActIndex = 2;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<TestSubjectBoss>().ToMutable(), setUpMonster: false);
            foreach (var player in players.Skip(1))
            {
                player.RunState = run;
                battle.Combat.AddPlayer(player);
                player.ResetCombatState();
            }
            run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
            JigsawCheckpoint.Capture(run, BossRoot(players[0].NetId));
            JigsawNativeSession.Restore(run);
            try
            {
                battle.Monster.SetUpForCombat();
                await battle.Monster.AfterAddedToRoom();
                foreach (var player in players)
                {
                    player.Creature.SetMaxHpInternal(5000);
                    player.Creature.SetCurrentHpInternal(5000);
                }
                using var lifecycle = new NativeRevivalScope();
                await CreatureCmd.Kill(battle.Monster.Creature);
                await battle.Monster.PerformMove();
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                var creature = battle.Monster.Creature;
                int[] hitCounts = [3, 4];
                int[] perHitDamage = [10, 19];
                int[] strengthAfter = [9, 21];
                for (int attack = 0; attack < hitCounts.Length; attack++)
                {
                    NativeEventTests.Check(battle.Monster.NextMove.Id == "MULTI_CLAW_MOVE",
                        "The native phase-two state machine keeps the next claw attack.");
                    var before = players.Select(player => player.Creature.CurrentHp).ToArray();
                    int historyStart = CombatManager.Instance.History.Entries.Count();
                    await battle.Monster.PerformMove();
                    var hits = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>().ToArray();
                    NativeEventTests.Check(hits.Length == count * hitCounts[attack] &&
                        hits.All(hit => ReferenceEquals(hit.Dealer, creature) && hit.Result.UnblockedDamage == perHitDamage[attack]),
                        $"A9 native claw {attack + 1} records {hitCounts[attack]} hits per original target at {perHitDamage[attack]} damage; combat players={count}.");
                    for (int index = 0; index < players.Length; index++)
                        NativeEventTests.Check(hits.Count(hit => ReferenceEquals(hit.Receiver, players[index].Creature)) == hitCounts[attack] &&
                            before[index] - players[index].Creature.CurrentHp == hitCounts[attack] * perHitDamage[attack],
                            "Each actual combat player remains an original native target with the same per-hit damage and total HP loss.");
                    NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == strengthAfter[attack],
                        "Native Suck counts damaging hits rather than multiplying Strength by the number of combat targets.");
                    battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                }
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static async Task ConsecutiveAttacks()
    {
        foreach (int ascension in new[] { 8, 9 })
        foreach (bool hunted in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                ascensionLevel: ascension, seed: "HUNTING_87_MULTI_CLAW_CONSECUTIVE");
            run.CurrentActIndex = 2;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<TestSubjectBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
            JigsawCheckpoint.Capture(run, BossRoot(player.NetId, hunted));
            JigsawNativeSession.Restore(run);
            try
            {
                battle.Monster.SetUpForCombat();
                await battle.Monster.AfterAddedToRoom();
                player.Creature.SetMaxHpInternal(5000);
                player.Creature.SetCurrentHpInternal(5000);
                using var lifecycle = new NativeRevivalScope();
                await CreatureCmd.Kill(battle.Monster.Creature);
                await battle.Monster.PerformMove();
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                var creature = battle.Monster.Creature;
                NativeEventTests.Check(creature.GetPowerAmount<SuckPower>() == (hunted ? 3 : 0) &&
                    creature.GetPowerAmount<PainfulStabsPower>() == (hunted ? 0 : 1),
                    "The real first revival retains the safe/native and hunted phase-two grants.");
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), creature, 2, creature, null);
                int baseDamage = hunted ? (ascension == 9 ? 10 : 9) : (ascension == 9 ? 11 : 10);
                // Suck3 grants three Strength per unblocked hit after each whole
                // attack; its real three/four/five-hit grants are nine/twelve/fifteen.
                int[] strengthBefore = hunted ? [2, 11, 23] : [2, 2, 2];
                int[] strengthAfter = hunted ? [11, 23, 38] : [2, 2, 2];
                int[] hitCounts = [3, 4, 5];
                for (int attack = 0; attack < hitCounts.Length; attack++)
                {
                    NativeEventTests.Check(battle.Monster.NextMove.Id == "MULTI_CLAW_MOVE" &&
                        creature.GetPowerAmount<StrengthPower>() == strengthBefore[attack],
                        $"Native consecutive claw {attack + 1} keeps its move and Strength; A{ascension}, hunted={hunted}.");
                    int expectedDamage = hitCounts[attack] * (baseDamage + strengthBefore[attack]);
                    var intent = battle.Monster.NextMove.Intents.OfType<MultiAttackIntent>().Single();
                    for (int preview = 0; preview < 5; preview++)
                        NativeEventTests.Check(intent.GetTotalDamage(battle.Combat.PlayerCreatures, creature) == expectedDamage,
                            $"Repeated native previews keep {hitCounts[attack]} hits without advancing N; A{ascension}, hunted={hunted}.");
                    int historyStart = CombatManager.Instance.History.Entries.Count();
                    decimal before = player.Creature.CurrentHp;
                    await battle.Monster.PerformMove();
                    var hits = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>().ToArray();
                    NativeEventTests.Check(before - player.Creature.CurrentHp == expectedDamage &&
                        hits.Length == hitCounts[attack] && hits.All(hit => ReferenceEquals(hit.Dealer, creature) &&
                            ReferenceEquals(hit.Receiver, player.Creature) && hit.Result.UnblockedDamage == baseDamage + strengthBefore[attack]),
                        $"Actual native claw {attack + 1} deals {hitCounts[attack]} hits and {expectedDamage} HP damage; A{ascension}, hunted={hunted}.");
                    NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == strengthAfter[attack],
                        "The whole native attack retains Suck Strength gain per unblocked hit and the independent Strength source.");
                    battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                }
                await CreatureCmd.Kill(creature);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "RESPAWN_MOVE",
                    "Actual phase-two death selects the native second revival.");
                await battle.Monster.PerformMove();
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "PHASE3_LACERATE_MOVE" &&
                    !creature.HasPower<SuckPower>() && !creature.HasPower<PainfulStabsPower>(),
                    "The completed native second revival selects Lacerate and removes phase-two attack powers.");
                int lacerateBase = ascension == 9 ? 11 : 10;
                decimal lacerateDamage = 3 * (lacerateBase + creature.GetPowerAmount<StrengthPower>());
                var lacerateIntent = battle.Monster.NextMove.Intents.OfType<MultiAttackIntent>().Single();
                NativeEventTests.Check(lacerateIntent.GetTotalDamage(battle.Combat.PlayerCreatures, creature) == lacerateDamage,
                    "Phase-three Lacerate keeps its original base damage and three-hit intent.");
                int lacerateHistory = CombatManager.Instance.History.Entries.Count();
                decimal beforeLacerate = player.Creature.CurrentHp;
                await battle.Monster.PerformMove();
                NativeEventTests.Check(beforeLacerate - player.Creature.CurrentHp == lacerateDamage &&
                    CombatManager.Instance.History.Entries.Skip(lacerateHistory).OfType<DamageReceivedEntry>().Count() == 3,
                    "The actual phase-three attack retains native Lacerate damage and hit count.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static Task NonJigsawSetup()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], ascensionLevel: 8, seed: "HUNTING_87_MULTI_CLAW_NO_ACTS");
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
            ModelDb.Encounter<TestSubjectBoss>().ToMutable(), setUpMonster: false);
        NativeEventTests.Check(JigsawNativeSession.TryGet(run) == null,
            "The native no-acts run has no Jigsaw session.");
        battle.Monster.SetUpForCombat();
        var move = (MoveState)battle.Monster.MoveStateMachine!.States["MULTI_CLAW_MOVE"];
        decimal intended = move.Intents.OfType<MultiAttackIntent>().Single()
            .GetTotalDamage(battle.Combat.PlayerCreatures, battle.Monster.Creature);
        NativeEventTests.Check(intended == 30,
            $"Native non-Jigsaw A8 setup retains its three hits of ten: expected=30, actual={intended}.");
        return Task.CompletedTask;
    }

    private static async Task PhaseTwoAttack()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player],
            acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
            ascensionLevel: 8, seed: "HUNTING_87_MULTI_CLAW");
        run.CurrentActIndex = 2;
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
            ModelDb.Encounter<TestSubjectBoss>().ToMutable(), setUpMonster: false);
        run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
        JigsawCheckpoint.Capture(run, BossRoot(player.NetId));
        JigsawNativeSession.Restore(run);
        try
        {
            battle.Monster.SetUpForCombat();
            await battle.Monster.AfterAddedToRoom();
            player.Creature.SetMaxHpInternal(2000);
            player.Creature.SetCurrentHpInternal(2000);
            using var lifecycle = new NativeRevivalScope();
            await CreatureCmd.Kill(battle.Monster.Creature);
            NativeEventTests.Check(battle.Monster.NextMove.Id == "RESPAWN_MOVE",
                "Actual phase-one death selects the native revival.");
            await battle.Monster.PerformMove();
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            NativeEventTests.Check(battle.Monster.NextMove.Id == "MULTI_CLAW_MOVE",
                "Actual first revival progresses into the native phase-two claw move.");
            var intent = battle.Monster.NextMove.Intents.OfType<MultiAttackIntent>().Single();
            decimal intended = intent.GetTotalDamage(battle.Combat.PlayerCreatures, battle.Monster.Creature);
            NativeEventTests.Check(intended == 27,
                $"The first hunted A8 claw intent is three hits of nine: expected=27, actual={intended}.");
            decimal before = player.Creature.CurrentHp;
            await battle.Monster.PerformMove();
            NativeEventTests.Check(before - player.Creature.CurrentHp == 27,
                "The real native phase-two attack deals three hits of nine.");
        }
        finally { JigsawNativeSession.Current?.Close(); }
    }

    // Reuse the native lifecycle boundary already exercised by the Boss tests.
    private sealed class NativeRevivalScope : IDisposable
    {
        private readonly Harmony host = new("AllMatters.TestHost");

        internal NativeRevivalScope()
        {
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
#if STS2_PUBLIC
            AccessTools.Property(typeof(CombatManager), nameof(CombatManager.IsInProgress)).SetValue(CombatManager.Instance, true);
#else
            var state = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(state.GetType(), "IsInProgress").SetValue(state, true);
#endif
        }

        public void Dispose()
        {
            host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix"));
        }
    }

    private static JigsawRun BossRoot(ulong player, bool hunted = true)
    {
        var map = JigsawRun.Create(75, budget: 13, players: [player]);
        Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(player));
        Ok(map.Continue(player));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss && previous.TryAdd(next, current))
                    pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Boss && previous.ContainsKey(node.Id)).Id;
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
        return map;
    }

    private static void Ok(JigsawResult result) => NativeEventTests.Check(result.Success, result.Reason);
}
