using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingActTwoTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting act two Knowledge Ponder heals twenty only in punished Boss action", Ponder),
      ("Jigsaw hunting act two Insatiable Liquify gives original target five sandpit and four in each pile", Liquify),
      ("Jigsaw hunting act two Knowledge attacks match native intents and A8 A9 damage", KnowledgeAttacks),
      ("Jigsaw hunting act two Knowledge first pair uses native player choice without adding display cards", KnowledgeFirstPair),
      ("Jigsaw hunting act two Knowledge second pair follows native cycle and applies Tender to its owner", KnowledgeSecondPair),
      ("Jigsaw hunting act two Knowledge third pair uses twenty one and finishes the native three curse cycle", KnowledgeThirdPair),
      ("Jigsaw hunting act two Hive normal opening uses native power commands and preserves unrelated Weak", HiveNormalOpening) ];

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);

    private static JigsawRun BossRoot(bool hunted, IEnumerable<ulong> players, JigsawRoom targetRoom = JigsawRoom.Boss)
    {
        var map = JigsawRun.Create(75, players: players);
        foreach (ulong player in map.ActivePlayers) Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong player in map.ActivePlayers) Ok(map.CompleteAncient(player));
        foreach (ulong player in map.ActivePlayers) Ok(map.Continue(player));
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        int boss = map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
        var queue = new Queue<int>();
        var previous = new Dictionary<int, int> { [0] = -1 };
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        if (targetRoom != JigsawRoom.Boss)
            boss = map.Nodes.Where(node => node.Room == targetRoom).OrderByDescending(node =>
            {
                int distance = 0;
                for (int current = node.Id; current != 0; current = previous[current]) distance++;
                return distance;
            }).First().Id;
        var path = new List<int>();
        for (int node = boss; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != boss) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "Accepted map entry establishes the actual root punishment.");
        return map;
    }

    private static async Task Ponder()
    {
        foreach (bool hunted in new[] { false, true })
        foreach (int count in new[] { 1 })
        {
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest(players, acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "HUNTING_85_PONDER");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable());
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, players.Select(player => player.NetId)));
            JigsawNativeSession.Restore(run);
            Check(JigsawHuntingNative.RootIsHunted(players[0]) == hunted && run.CurrentRoomCount == 1,
                "A bound native session and actual top-level Boss consume the authoritative root snapshot.");
            foreach (var player in players)
            { player.Creature.SetMaxHpInternal(2000); player.Creature.SetCurrentHpInternal(2000); }
            var creature = battle.Monster.Creature;
            creature.SetCurrentHpInternal(creature.MaxHp - 150);
            decimal before = creature.CurrentHp;
            battle.Monster.SetMoveImmediate((MoveState)battle.Monster.MoveStateMachine!.States["PONDER_MOVE"], true);
            await battle.Monster.PerformMove();
            Check(creature.CurrentHp - before == (hunted ? 20 : 30 * count),
                $"Actual Ponder healing is {(hunted ? 20 : 30 * count)} with players={count}, punished={hunted}; actual={creature.CurrentHp - before}.");
            Check(creature.GetPowerAmount<StrengthPower>() == 2 && players.All(player => player.Creature.CurrentHp < 2000),
                "The same action preserves its native attack and Strength gain.");
            before = creature.CurrentHp;
            await CreatureCmd.Heal(creature, 7);
            Check(creature.CurrentHp - before == 7, "A separate native healing source remains seven, even for the punished demon.");
        }
    }

    private sealed class KnowledgeSecondOption(bool firstOnFirst = false) : ICardSelector
    {
        internal int Calls;
        internal CardModel[] Offered = [];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Offered = options.ToArray();
            Check(Offered.Length == 2 && maxSelect == 1, "The real native command offers exactly two single-choice display cards.");
            Calls++;
            return Task.FromResult<IEnumerable<CardModel>>([Offered[firstOnFirst && Calls == 1 ? 0 : 1]]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException();
    }

    private static async Task KnowledgeFirstPair()
    {
        foreach (bool hunted in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()],
                seed: "HUNTING_85_KNOWLEDGE_PAIR_ONE");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId]));
            JigsawNativeSession.Restore(run);
            battle.Monster.SetUpForCombat();
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "CURSE_OF_KNOWLEDGE_MOVE", "The actual native initial action remains Curse of Knowledge.");
            int originalDeck = player.Deck.Cards.Count;
            var selector = new KnowledgeSecondOption();
            using (CardSelectCmd.UseSelector(selector)) await battle.Monster.PerformMove();
            Check(selector.Calls == 1 && selector.Offered.All(card => ReferenceEquals(card.Owner, player) && card is KnowledgeDemon.IChoosable),
                "The native action asks once for its original owner and both options can execute their native choice effect.");
            Check(hunted
                ? selector.Offered[0] is MindRot && selector.Offered[1].Title == ModelDb.Card<Calamity>().Title
                : selector.Offered[0] is Disintegration && selector.Offered[1] is MindRot,
                $"The first pair is {(hunted ? "MindRot/Calamity" : "original Disintegration/MindRot")}; actual={string.Join('/', selector.Offered.Select(card => card.Id.Entry))}.");
            Check(player.Creature.GetPowerAmount<CalamityPower>() == (hunted ? 1 : 0) &&
                player.Creature.GetPowerAmount<MindRotPower>() == (hunted ? 0 : 1) &&
                player.Creature.GetPowerAmount<DisintegrationPower>() == 0,
                "Only the selected native power is applied once; the unselected effect is absent.");
            Check(player.Deck.Cards.Count == originalDeck && selector.Offered.All(card => card.Pile == null && !player.Deck.Cards.Contains(card)),
                "Neither first-pair display card enters the deck or any combat pile.");
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "SLAP_MOVE" && selector.Calls == 1,
                "The completed outer action advances once to Slap without another player choice.");
        }
    }
    private static async Task KnowledgeSecondPair()
    {
        foreach (bool hunted in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "HUNTING_85_PAIR_TWO");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId]));
            JigsawNativeSession.Restore(run);
            battle.Monster.SetUpForCombat();
            player.Creature.SetMaxHpInternal(2000);
            player.Creature.SetCurrentHpInternal(2000);
            int originalDeck = player.Deck.Cards.Count;
            var selector = new KnowledgeSecondOption(firstOnFirst: true);
            using (CardSelectCmd.UseSelector(selector))
            {
                foreach (string move in new[] { "CURSE_OF_KNOWLEDGE_MOVE", "SLAP_MOVE", "KNOWLEDGE_OVERWHELMING_MOVE", "PONDER_MOVE", "CURSE_OF_KNOWLEDGE_MOVE" })
                {
                    battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                    Check(battle.Monster.NextMove.Id == move, $"The native cycle reaches {move} without overriding its counter or state.");
                    await battle.Monster.PerformMove();
                }
            }
            Check(selector.Calls == 2 && selector.Offered.All(card => ReferenceEquals(card.Owner, player) && card is KnowledgeDemon.IChoosable),
                "The actual outer action makes exactly two personal choices across the two cycles.");
            Check(hunted
                ? selector.Offered[0] is Sloth && selector.Offered[1].Title == ModelDb.Power<TenderPower>().Title.GetFormattedText()
                : selector.Offered[0] is Disintegration && selector.Offered[1] is Sloth,
                $"The actual second pair is {(hunted ? "Sloth/Tender" : "original Disintegration/Sloth")}; actual={string.Join('/', selector.Offered.Select(card => card.Id.Entry))}.");
            Check(player.Creature.GetPowerAmount<TenderPower>() == (hunted ? 1 : 0) && player.Creature.GetPowerAmount<SlothPower>() == (hunted ? 0 : 3),
                "Only the selected second-round native power is applied to its original owner.");
            Check(player.Deck.Cards.Count == originalDeck && selector.Offered.All(card => card.Pile == null && !player.Deck.Cards.Contains(card)),
                "Second-round display cards do not enter the deck or any combat pile.");
            Check(ModelDb.Card<Disintegration>().DynamicVars["DisintegrationPower"].BaseValue == 6,
                "Native per-instance round values do not mutate the canonical Disintegration template.");
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "SLAP_MOVE" && selector.Calls == 2, "The second choice advances once to native Slap.");
        }
    }
    private sealed class KnowledgeCycleSelector(bool hunted, int thirdOption) : ICardSelector
    {
        internal readonly List<CardModel[]> Rounds = [];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            Check(cards.Length == 2, "Each real cycle offers exactly two display cards.");
            Rounds.Add(cards);
            int selected = Rounds.Count == 3 ? thirdOption : (hunted ? 0 : 1);
            return Task.FromResult<IEnumerable<CardModel>>([cards[selected]]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException();
    }

    private static async Task KnowledgeThirdPair()
    {
        foreach (bool hunted in new[] { false, true })
        foreach (int selected in new[] { 0, 1 })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "HUNTING_85_PAIR_THREE");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId]));
            JigsawNativeSession.Restore(run);
            battle.Monster.SetUpForCombat();
            player.Creature.SetMaxHpInternal(4000);
            player.Creature.SetCurrentHpInternal(4000);
            int originalDeck = player.Deck.Cards.Count;
            var selector = new KnowledgeCycleSelector(hunted, selected);
            using (CardSelectCmd.UseSelector(selector))
                for (int round = 0; round < 3; round++)
                {
                    foreach (string move in new[] { "CURSE_OF_KNOWLEDGE_MOVE", "SLAP_MOVE", "KNOWLEDGE_OVERWHELMING_MOVE", "PONDER_MOVE" })
                    {
                        battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                        Check(battle.Monster.NextMove.Id == move, $"Round {round + 1} progresses through actual {move}.");
                        await battle.Monster.PerformMove();
                    }
                }
            Check(selector.Rounds.Count == 3, "Three complete native cycles ask exactly three times.");
            var third = selector.Rounds[2];
            Check(hunted ? third[0] is WasteAway && third[1] is Disintegration : third[0] is Disintegration && third[1] is WasteAway,
                $"The third pair keeps its specified order; punished={hunted}, actual={string.Join('/', third.Select(card => card.Id.Entry))}.");
            var disintegration = third.OfType<Disintegration>().Single();
            Check(disintegration.DynamicVars["DisintegrationPower"].BaseValue == (hunted ? 21 : 8),
                "Only the actual third-round display instance carries twenty-one; safe native remains eight.");
            bool choseDamage = hunted ? selected == 1 : selected == 0;
            Check(player.Creature.GetPowerAmount<DisintegrationPower>() == (choseDamage ? (hunted ? 21 : 8) : 0) &&
                player.Creature.GetPowerAmount<WasteAwayPower>() == (choseDamage ? 0 : 1), "The third selection applies exactly its chosen native power.");
            var history = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Power is DisintegrationPower or WasteAwayPower).ToArray();
            Check(history.Length == 1 && history[0].Amount == (choseDamage ? (hunted ? 21 : 8) : 1) &&
                ReferenceEquals(history[0].Actor, player.Creature) && ReferenceEquals(history[0].Applier, player.Creature),
                "Native PowerCmd records one correctly targeted and attributed third-choice application.");
            Check(player.Deck.Cards.Count == originalDeck && selector.Rounds.SelectMany(cards => cards).All(card =>
                ReferenceEquals(card.Owner, player) && card.Pile == null && !player.Deck.Cards.Contains(card)), "All three rounds keep display instances out of every pile and persistent deck.");
            Check(ModelDb.Card<Disintegration>().DynamicVars["DisintegrationPower"].BaseValue == 6, "The native canonical template stays six.");
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "SLAP_MOVE" && selector.Rounds.Count == 3,
                "After three curses, the actual native conditional branch returns to Slap without a fourth choice.");
        }
    }
    private static async Task HiveNormalOpening()
    {
        foreach (bool hunted in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "HUNTING_85_HIVE_OPENING");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<SpinyToadNormal>().ToMutable());
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId], JigsawRoom.Combat));
            JigsawNativeSession.Restore(run);
            Check(run.CurrentRoom is CombatRoom { ParentEventId: null } && JigsawHuntingNative.RootIsHunted(player) == hunted,
                "The real native normal encounter consumes a matching accepted normal-room root.");
            await Hook.BeforeCombatStart(run, battle.Combat);
            Check(player.Creature.GetPowerAmount<WeakPower>() == (hunted ? 1 : 0) &&
                player.Creature.GetPowerAmount<FrailPower>() == (hunted ? 1 : 0) &&
                player.Creature.GetPowerAmount<VulnerablePower>() == (hunted ? 1 : 0),
                "Hive normal's real initial hook gives exactly one Weak, Frail and Vulnerable per player only on a hunted root.");
            var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();
            Check(received.Length == (hunted ? 3 : 0) && received.All(entry => entry.Amount == 1 && ReferenceEquals(entry.Actor, player.Creature)),
                "The opening uses actual native power commands and history with its original player target.");
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), player.Creature, 4, battle.Monster.Creature, null);
            Check(player.Creature.GetPowerAmount<WeakPower>() == (hunted ? 5 : 4),
                "A separate later Weak source on the same target retains its requested four stacks.");
        }
    }
    private static async Task KnowledgeAttacks()
    {
        foreach (bool hunted in new[] { false, true })
        foreach (int ascension in new[] { 8, 9 })
        foreach (string moveId in new[] { "SLAP_MOVE", "KNOWLEDGE_OVERWHELMING_MOVE" })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()],
                ascensionLevel: ascension, seed: "HUNTING_85_KNOWLEDGE_ATTACKS");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId]));
            JigsawNativeSession.Restore(run);
            battle.Monster.SetUpForCombat();
            player.Creature.SetMaxHpInternal(2000);
            player.Creature.SetCurrentHpInternal(2000);
            bool slap = moveId == "SLAP_MOVE";
            // H resolution fixes these amounts and keeps the original three hits.
            int expected = slap ? (hunted ? (ascension == 9 ? 16 : 15) : (ascension == 9 ? 18 : 17))
                : 3 * (hunted ? (ascension == 9 ? 7 : 6) : (ascension == 9 ? 9 : 8));
            var move = (MoveState)battle.Monster.MoveStateMachine!.States[moveId];
            decimal intended = slap
                ? move.Intents.OfType<SingleAttackIntent>().Single().GetTotalDamage(battle.Combat.PlayerCreatures, battle.Monster.Creature)
                : move.Intents.OfType<MultiAttackIntent>().Single().GetTotalDamage(battle.Combat.PlayerCreatures, battle.Monster.Creature);
            Check(intended == expected,
                $"{moveId} native intent A{ascension}, punished={hunted}: expected={expected}, actual={intended}.");
            battle.Monster.SetMoveImmediate(move, true);
            decimal before = player.Creature.CurrentHp;
            await battle.Monster.PerformMove();
            Check(before - player.Creature.CurrentHp == expected,
                $"{moveId} real DamageCmd A{ascension}, punished={hunted}: expected={expected}, actual={before - player.Creature.CurrentHp}.");
        }
    }
    private static async Task Liquify()
    {
        foreach (bool hunted in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "HUNTING_85_LIQUIFY");
            run.CurrentActIndex = 1;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<TheInsatiableBoss>().ToMutable(), setUpMonster: false);
            run.PushRoom(new CombatRoom(battle.Combat));
            JigsawCheckpoint.Capture(run, BossRoot(hunted, [player.NetId]));
            JigsawNativeSession.Restore(run);
            // Native entry pushes the room before setting up monsters. Preserve that
            // ordering and initialize once after binding the authoritative root.
            battle.Monster.SetUpForCombat();
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "LIQUIFY_GROUND_MOVE", "The actual native initial move remains Liquify.");
            int originalDeck = player.Deck.Cards.Count;
            int beforeHand = PileType.Hand.GetPile(player).Cards.Count;
            await battle.Monster.PerformMove();
            var sandpit = battle.Monster.Creature.Powers.OfType<SandpitPower>().Single();
            Check(sandpit.Amount == (hunted ? 5 : 4) && ReferenceEquals(sandpit.Target, player.Creature),
                $"Liquify keeps its original target and gives {(hunted ? 5 : 4)} Sandpit; actual={sandpit.Amount}.");
            int expectedCards = hunted ? 4 : 3;
            Check(PileType.Draw.GetPile(player).Cards.Count(card => card is FranticEscape) == expectedCards &&
                PileType.Discard.GetPile(player).Cards.Count(card => card is FranticEscape) == expectedCards,
                "The same native action inserts exactly the requested cards into each original target pile.");
            var initial = (MoveState)battle.Monster.MoveStateMachine!.States["LIQUIFY_GROUND_MOVE"];
            Check(initial.Intents.OfType<StatusIntent>().Single().CardCount == (hunted ? 8 : 6),
                "The actual initial status intent agrees with the complete action's generated card count.");
            Check(player.Deck.Cards.Count == originalDeck && PileType.Hand.GetPile(player).Cards.Count == beforeHand,
                "Generated statuses do not enter the persistent deck or bypass the target piles into hand.");
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "THRASH_MOVE", "Liquify completes the original move progression without repeating its opening effects.");
        }
    }
}
