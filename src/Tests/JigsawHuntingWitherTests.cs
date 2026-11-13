using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingWitherTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting Wither native two intensity cycles strengthen existing and generated cards", NativeIntensityCycles),
      ("Jigsaw hunting Wither strengthened twelfth native play preserves five statuses through hand overflow", StrengthenedTwelfthPlay),
      ("Jigsaw hunting Wither independent generation previews and native turn end preserve two upgrades", IndependentGenerationAndTurnEnd),
      ("Jigsaw hunting Wither one run owner and one to four combat roles retain native A8 A9 generation thresholds", CombatRoleAscensionBoundaries) ];

    // Native monster moves and generation hooks; this does not cover live turn
    // scheduling, connected multiplayer, presentation or whole-combat recovery.
    private static async Task NativeIntensityCycles()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player],
            acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
            gameMode: GameMode.Standard, ascensionLevel: 0, seed: "HUNTING_87_WITHER_CHAIN");
        run.CurrentActIndex = 2;
        var encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
        run.PushRoom(new CombatRoom(encounter, run));
        JigsawCheckpoint.Capture(run, HuntedBossRoot(player.NetId));
        JigsawNativeSession.Restore(run);
        try
        {
            NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted &&
                run.Players.Count == 1 && battle.Combat.PlayerCreatures.Count == 1,
                "The accepted hunted Boss root has one real run owner and one combat player.");
            await battle.Monster.AfterAddedToRoom();
            NativeEventTests.Check(battle.Monster.Creature.Powers.OfType<WitheringPresencePower>().Single().DisplayAmount == 12,
                "The actual hunted Aeonglass initialization supplies its twelve-card presence.");
            player.Creature.SetMaxHpInternal(5000);
            player.Creature.SetCurrentHpInternal(5000);
            var aeonglass = (Aeonglass)battle.Monster;
            NativeEventTests.Check(aeonglass.WitherUpgradeCount == 0,
                "The fresh native monster starts before any intensity upgrade.");
            NativeEventTests.Check(CombatManager.Instance.IsInProgress &&
                ReferenceEquals(player.Creature.CombatState, battle.Combat),
                "Native generated-card commands require active combat and the player's actual combat state.");

            var existing = new Dictionary<PileType, Wither>();
            foreach (var pile in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
            {
                var card = battle.Combat.CreateCard<Wither>(player);
                await CardPileCmd.AddGeneratedCardToCombat(card, pile, player);
                existing.Add(pile, card);
                NativeEventTests.Check(pile.GetPile(player).Cards.Contains(card) && card.DynamicVars.Damage.BaseValue == 3,
                    $"The native generation path puts an unstrengthened Wither in {pile} before the first intensity.");
            }

            // SetUpForCombat builds the state machine; the first native roll
            // publishes its initial move before any monster action is performed.
            aeonglass.RollMove(battle.Combat.PlayerCreatures);
            var generated = new List<Wither>();
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                foreach (string move in new[] { "EBB_MOVE", "EYE_LASERS_MOVE", "INCREASING_INTENSITY_MOVE" })
                {
                    NativeEventTests.Check(aeonglass.NextMove.Id == move,
                        $"Cycle {cycle} naturally reaches {move} without replacing the move or upgrade counter: actual={aeonglass.NextMove.Id}.");
                    await aeonglass.PerformMove();
                    aeonglass.RollMove(battle.Combat.PlayerCreatures);
                }

                decimal expectedDamage = 3 + 3 * cycle;
                NativeEventTests.Check(aeonglass.WitherUpgradeCount == cycle,
                    $"Cycle {cycle} increments the native generation upgrade count exactly once.");
                foreach (var (pile, card) in existing)
                    NativeEventTests.Check(pile.GetPile(player).Cards.Contains(card) && card.DynamicVars.Damage.BaseValue == expectedDamage,
                        $"Cycle {cycle} strengthens the same {pile} Wither exactly once to damage {expectedDamage}.");
                foreach (var card in generated)
                    NativeEventTests.Check(PileType.Discard.GetPile(player).Cards.Contains(card) && card.DynamicVars.Damage.BaseValue == expectedDamage,
                        $"Cycle {cycle} strengthens an earlier native-generated Wither once to damage {expectedDamage}.");

                var fresh = player.PlayerCombatState!.AllCards.OfType<Wither>()
                    .Where(card => !existing.Values.Contains(card) && !generated.Contains(card)).ToArray();
                NativeEventTests.Check(fresh.Length == 1 &&
                    PileType.Discard.GetPile(player).Cards.Contains(fresh[0]) &&
                    fresh[0].DynamicVars.Damage.BaseValue == expectedDamage,
                    $"A0 Standard cycle {cycle} natively generates one discarded Wither matching damage {expectedDamage} through the generation hook.");
                generated.AddRange(fresh);
                NativeEventTests.Check(player.PlayerCombatState.AllCards.OfType<Wither>().Count() == 4 + cycle &&
                    aeonglass.NextMove.Id == "EBB_MOVE",
                    $"Cycle {cycle} preserves all existing Withers and returns to the original Ebb move: actual={aeonglass.NextMove.Id}.");
            }
        }
        finally { JigsawNativeSession.Current?.Close(); }
    }

    private static async Task StrengthenedTwelfthPlay()
    {
        foreach (bool overflow in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                gameMode: GameMode.Standard, ascensionLevel: 0, seed: "HUNTING_87_WITHER_TWELFTH");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            run.PushRoom(new CombatRoom(encounter, run));
            JigsawCheckpoint.Capture(run, HuntedBossRoot(player.NetId));
            JigsawNativeSession.Restore(run);
            try
            {
                NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted &&
                    run.Players.Count == 1 && battle.Combat.PlayerCreatures.Count == 1 &&
                    CombatManager.Instance.IsInProgress && ReferenceEquals(player.Creature.CombatState, battle.Combat),
                    "The twelfth-play case uses one hunted owner in active Standard A0 combat.");
                await battle.Monster.AfterAddedToRoom();
                var aeonglass = (Aeonglass)battle.Monster;
                var presence = aeonglass.Creature.Powers.OfType<WitheringPresencePower>().Single();
                NativeEventTests.Check(presence.Target.Player == player && presence.DisplayAmount == 12,
                    "The real presence starts with twelve cards for this actual owner.");
                player.Creature.SetMaxHpInternal(5000);
                player.Creature.SetCurrentHpInternal(5000);
                aeonglass.RollMove(battle.Combat.PlayerCreatures);
                foreach (string move in new[] { "EBB_MOVE", "EYE_LASERS_MOVE", "INCREASING_INTENSITY_MOVE" })
                {
                    NativeEventTests.Check(aeonglass.NextMove.Id == move,
                        $"The native pre-trigger intensity cycle reaches {move}: actual={aeonglass.NextMove.Id}.");
                    await aeonglass.PerformMove();
                    aeonglass.RollMove(battle.Combat.PlayerCreatures);
                }
                var intensityWither = player.PlayerCombatState!.AllCards.OfType<Wither>().Single();
                NativeEventTests.Check(aeonglass.WitherUpgradeCount == 1 &&
                    intensityWither.DynamicVars.Damage.BaseValue == 6 &&
                    PileType.Discard.GetPile(player).Cards.Contains(intensityWither),
                    "The real first intensity generates one damage-six Wither before the twelfth-play trigger.");

                var held = new List<CardModel>();
                if (overflow)
                    for (int index = 0; index < 9; index++)
                    {
                        var card = battle.Combat.CreateCard<DefendIronclad>(player);
                        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                        held.Add(card);
                    }
                for (int play = 0; play < 11; play++) await Play();
                NativeEventTests.Check(presence.DisplayAmount == 1 && presence.DynamicVars["CardsLeft"].IntValue == 1 &&
                    player.PlayerCombatState.AllCards.OfType<Wither>().SequenceEqual(new[] { intensityWither }) &&
                    !player.PlayerCombatState.AllCards.Any(card => card is Wound or Burn or MegaCrit.Sts2.Core.Models.Cards.Void or Dazed),
                    $"overflow={overflow}: eleven actual plays retain only the earlier intensity Wither and leave one card before the trigger.");
                var beforeTrigger = player.PlayerCombatState.AllCards.ToHashSet();
                await Play();

                var statuses = PileType.Hand.GetPile(player).Cards.Concat(PileType.Discard.GetPile(player).Cards)
                    .Where(card => !beforeTrigger.Contains(card) &&
                        (card is Wound or Burn or MegaCrit.Sts2.Core.Models.Cards.Void or Dazed or Wither)).ToArray();
                NativeEventTests.Check(statuses.Length == 5 && statuses[0] is Wound && statuses[1] is Burn &&
                    statuses[2] is MegaCrit.Sts2.Core.Models.Cards.Void && statuses[3] is Dazed && statuses[4] is Wither &&
                    statuses.All(card => card.Owner == player),
                    $"overflow={overflow}: the owner's actual twelfth play preserves the five native status identities, order and ownership.");
                NativeEventTests.Check(((Wither)statuses[4]).DynamicVars.Damage.BaseValue == 6 &&
                    intensityWither.DynamicVars.Damage.BaseValue == 6 && aeonglass.WitherUpgradeCount == 1 &&
                    PileType.Discard.GetPile(player).Cards.Contains(intensityWither),
                    $"overflow={overflow}: the twelfth-play Wither matches the native upgrade count once and preserves the earlier damage-six Wither.");
                var hand = PileType.Hand.GetPile(player).Cards;
                NativeEventTests.Check(overflow ? hand.Count == 10 && held.All(card => hand.Contains(card)) &&
                    statuses[0].Pile == PileType.Hand.GetPile(player) &&
                    statuses.Skip(1).All(card => card.Pile == PileType.Discard.GetPile(player)) :
                    hand.Count == 5 && statuses.All(card => card.Pile == PileType.Hand.GetPile(player)),
                    $"overflow={overflow}: native insertion retains all five in an empty hand or spills only the last four from the nine-card hand.");
                NativeEventTests.Check(presence.DisplayAmount == 12 && presence.DynamicVars["CardsLeft"].IntValue == 12 &&
                    player.PlayerCombatState.AllCards.OfType<Wither>().Count() == 2,
                    $"overflow={overflow}: the twelfth-play reset preserves both independently generated Withers.");

                async Task Play()
                {
                    var card = battle.Combat.CreateCard<DefendIronclad>(player);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player);
                    await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
                }
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static async Task IndependentGenerationAndTurnEnd()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player],
            acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
            gameMode: GameMode.Standard, ascensionLevel: 0, seed: "HUNTING_87_WITHER_PREVIEW");
        run.CurrentActIndex = 2;
        var encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
        run.PushRoom(new CombatRoom(encounter, run));
        JigsawCheckpoint.Capture(run, HuntedBossRoot(player.NetId));
        JigsawNativeSession.Restore(run);
        try
        {
            NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted && run.Players.Count == 1 &&
                battle.Combat.PlayerCreatures.Count == 1 && CombatManager.Instance.IsInProgress,
                "Independent generation uses one actual hunted owner in active Standard A0 combat.");
            await battle.Monster.AfterAddedToRoom();
            player.Creature.SetMaxHpInternal(5000);
            player.Creature.SetCurrentHpInternal(5000);
            var aeonglass = (Aeonglass)battle.Monster;
            var presence = aeonglass.Creature.Powers.OfType<WitheringPresencePower>().Single();
            aeonglass.RollMove(battle.Combat.PlayerCreatures);
            for (int cycle = 1; cycle <= 2; cycle++)
                foreach (string move in new[] { "EBB_MOVE", "EYE_LASERS_MOVE", "INCREASING_INTENSITY_MOVE" })
                {
                    NativeEventTests.Check(aeonglass.NextMove.Id == move,
                        $"Independent-source cycle {cycle} reaches native {move}: actual={aeonglass.NextMove.Id}.");
                    await aeonglass.PerformMove();
                    aeonglass.RollMove(battle.Combat.PlayerCreatures);
                }
            var prior = player.PlayerCombatState!.AllCards.OfType<Wither>().ToArray();
            NativeEventTests.Check(aeonglass.WitherUpgradeCount == 2 && prior.Length == 2 &&
                prior.All(card => card.DynamicVars.Damage.BaseValue == 9),
                "Two actual intensity cycles strengthen both native-generated Withers to damage nine.");

            await CardPileCmd.AddToCombatAndPreview<Wither>(player.Creature, PileType.Hand, 1, player);
            var independent = player.PlayerCombatState.AllCards.OfType<Wither>().Single(card => !prior.Contains(card));
            NativeEventTests.Check(independent.Owner == player && PileType.Hand.GetPile(player).Cards.Contains(independent) &&
                independent.DynamicVars.Damage.BaseValue == 9 && aeonglass.WitherUpgradeCount == 2,
                "An independent native generation command matches the current two upgrades through the real hook.");

            var cardsBefore = player.PlayerCombatState.AllCards.ToArray();
            var pilesBefore = cardsBefore.Select(card => card.Pile).ToArray();
            var damageBefore = cardsBefore.OfType<Wither>().Select(card => card.DynamicVars.Damage.BaseValue).ToArray();
            var canonical = ModelDb.Card<Wither>();
            NativeEventTests.Check(!canonical.IsMutable && canonical.DynamicVars.Damage.BaseValue == 3,
                "The canonical Wither remains unstrengthened before reading any presence preview.");
            var json = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            string cardsContentsBefore = System.Text.Json.JsonSerializer.Serialize(cardsBefore.Select(card => card.ToSerializable()).ToArray(), json);
            string runRngBefore = System.Text.Json.JsonSerializer.Serialize(run.Rng.ToSerializable(), json);
            string playerBefore = System.Text.Json.JsonSerializer.Serialize(player.ToSerializable(), json);
            string monsterRngBefore = MonsterRng();
            var previews = new List<CardModel>();
            for (int read = 0; read < 3; read++)
            {
                var preview = presence.HoverTips.OfType<CardHoverTip>().Select(tip => tip.Card).OfType<Wither>().Single();
                NativeEventTests.Check(preview.IsMutable && !ReferenceEquals(preview, canonical) &&
                    !cardsBefore.Contains(preview) && !previews.Contains(preview) &&
                    preview.DynamicVars.Damage.BaseValue == 9,
                    $"Presence preview read {read + 1} creates a separate mutable damage-nine Wither.");
                previews.Add(preview);
                NativeEventTests.Check(aeonglass.WitherUpgradeCount == 2 && presence.DisplayAmount == 12 &&
                    canonical.DynamicVars.Damage.BaseValue == 3 &&
                    cardsBefore.SequenceEqual(player.PlayerCombatState.AllCards) &&
                    cardsBefore.Select(card => card.Pile).SequenceEqual(pilesBefore) &&
                    cardsBefore.OfType<Wither>().Select(card => card.DynamicVars.Damage.BaseValue).SequenceEqual(damageBefore) &&
                    System.Text.Json.JsonSerializer.Serialize(player.PlayerCombatState.AllCards.Select(card => card.ToSerializable()).ToArray(), json) == cardsContentsBefore &&
                    System.Text.Json.JsonSerializer.Serialize(run.Rng.ToSerializable(), json) == runRngBefore &&
                    System.Text.Json.JsonSerializer.Serialize(player.ToSerializable(), json) == playerBefore && MonsterRng() == monsterRngBefore,
                    $"Presence preview read {read + 1} preserves monster count, canonical, combat cards and complete serialized run/player/monster RNG state.");
            }

            NativeEventTests.Check(independent.HasTurnEndInHandEffect && player.Creature.Block == 0,
                "The independent damage-nine Wither has its native turn-end effect and no player block masks the damage.");
            decimal hpBefore = player.Creature.CurrentHp;
            // Use the same public card-effect wrapper invoked by each version's
            // CombatManager; this checks native damage, not the full turn loop.
            await independent.OnTurnEndInHandWrapper(new ThrowingPlayerChoiceContext());
            NativeEventTests.Check(hpBefore - player.Creature.CurrentHp == 9 &&
                independent.DynamicVars.Damage.BaseValue == 9 && aeonglass.WitherUpgradeCount == 2,
                "The real native Wither turn-end effect deals exactly nine HP damage without another upgrade.");

            string MonsterRng() =>
#if STS2_PUBLIC
                System.Text.Json.JsonSerializer.Serialize(new { aeonglass.Rng.Seed, aeonglass.Rng.Counter }, json);
#else
                System.Text.Json.JsonSerializer.Serialize(aeonglass.Rng.ToSerializable(), json);
#endif
        }
        finally { JigsawNativeSession.Current?.Close(); }
    }

    private static async Task CombatRoleAscensionBoundaries()
    {
        for (int count = 1; count <= 4; count++)
        foreach (int ascension in new[] { 8, 9 })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            // One run owner plus actual native combat roles; no remote clients
            // or additional run owners are represented by these inputs.
            var run = RunState.CreateForTest([players[0]],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                gameMode: GameMode.Standard, ascensionLevel: ascension, seed: "HUNTING_87_WITHER_ROLES");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            foreach (var player in players.Skip(1))
            {
                player.RunState = run;
                battle.Combat.AddPlayer(player);
                player.ResetCombatState();
            }
            run.PushRoom(new CombatRoom(encounter, run));
            JigsawCheckpoint.Capture(run, HuntedBossRoot(players[0].NetId));
            JigsawNativeSession.Restore(run);
            try
            {
                NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted &&
                    run.Players.Count == 1 && ReferenceEquals(run.Players[0], players[0]) &&
                    battle.Combat.PlayerCreatures.Count == count &&
                    battle.Combat.PlayerCreatures.Select(creature => creature.Player.NetId).ToHashSet()
                        .SetEquals(players.Select(player => player.NetId)) && CombatManager.Instance.IsInProgress,
                    $"A{ascension}: one run owner has exactly {count} native combat roles in the accepted hunted Boss room.");
                await battle.Monster.AfterAddedToRoom();
                var aeonglass = (Aeonglass)battle.Monster;
                var presences = aeonglass.Creature.Powers.OfType<WitheringPresencePower>().ToArray();
                NativeEventTests.Check(presences.Length == count && presences.All(power => power.DisplayAmount == 12) &&
                    presences.Select(power => power.Target.Player.NetId).ToHashSet().SetEquals(players.Select(player => player.NetId)),
                    $"A{ascension}, roles={count}: each native role has its own targeted twelve-card presence.");
                var existing = new Dictionary<Player, Wither>();
                foreach (var player in players)
                {
                    player.Creature.SetMaxHpInternal(5000);
                    player.Creature.SetCurrentHpInternal(5000);
                    var card = battle.Combat.CreateCard<Wither>(player);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                    existing.Add(player, card);
                    NativeEventTests.Check(ReferenceEquals(player.RunState, run) &&
                        ReferenceEquals(player.Creature.CombatState, battle.Combat) && card.Owner == player &&
                        PileType.Hand.GetPile(player).Cards.Contains(card) && card.DynamicVars.Damage.BaseValue == 3,
                        $"A{ascension}, roles={count}, owner={player.NetId}: the real generation command creates that role's own initial damage-three Hand Wither.");
                }
                aeonglass.RollMove(battle.Combat.PlayerCreatures);
                foreach (string move in new[] { "EBB_MOVE", "EYE_LASERS_MOVE", "INCREASING_INTENSITY_MOVE" })
                {
                    NativeEventTests.Check(aeonglass.NextMove.Id == move,
                        $"A{ascension}, roles={count}: the native cycle reaches {move}: actual={aeonglass.NextMove.Id}.");
                    await aeonglass.PerformMove();
                    aeonglass.RollMove(battle.Combat.PlayerCreatures);
                }

                // Both current targets use DeadlyEnemies=A9: Standard has no
                // beta Dread extra card or Draw-pile branch.
                int expectedGenerated = ascension == 9 ? 2 : 1;
                foreach (var player in players)
                {
                    var cards = player.PlayerCombatState!.AllCards.OfType<Wither>().ToArray();
                    var generated = cards.Where(card => !ReferenceEquals(card, existing[player])).ToArray();
                    NativeEventTests.Check(cards.Length == expectedGenerated + 1 && cards.All(card => card.Owner == player) &&
                        existing[player].DynamicVars.Damage.BaseValue == 6 &&
                        PileType.Hand.GetPile(player).Cards.Contains(existing[player]) && generated.Length == expectedGenerated &&
                        generated.All(card => card.DynamicVars.Damage.BaseValue == 6 &&
                            PileType.Discard.GetPile(player).Cards.Contains(card) && !existing.Values.Contains(card)),
                        $"A{ascension}, roles={count}, owner={player.NetId}: the existing Wither upgrades once and exactly {expectedGenerated} newly generated owner-matched Discard Withers have damage six.");
                }
                NativeEventTests.Check(aeonglass.WitherUpgradeCount == 1 && presences.All(power => power.DisplayAmount == 12 &&
                    power.DynamicVars["CardsLeft"].IntValue == 12) && aeonglass.NextMove.Id == "EBB_MOVE" &&
                    players.SelectMany(player => player.PlayerCombatState!.AllCards.OfType<Wither>()).Distinct().Count() ==
                        count * (expectedGenerated + 1),
                    $"A{ascension}, roles={count}: one native intensity preserves distinct role-owned cards, independent untouched counters and the original Ebb cycle.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static JigsawRun HuntedBossRoot(ulong player)
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
        Ok(map.SetRemainingSteps(0));
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
