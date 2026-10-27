using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Runs;

internal static class TrashHeapTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Trash heap grab grants 50 gold and a retained card once without display RNG", Grab),
        ("Trash heap migrated cards appear in native shops and generation with original artwork", CardPools),
        ("Trash heap migrated relics enter the common native reward bag", RelicPools),
        ("Trash heap Distraction upgrade generates upgraded skill free only this turn and still exhausts", DistractionUpgrade),
        ("Trash heap Outmaneuver retains Sly after upgrade and grants energy when discarded", Sly),
        ("Trash heap dive retains three native relic outcomes and owner damage once", Dive),
        ("Trash heap bilingual descriptions and upgrade previews match effects", Presentation),
    ];

    private static Task Presentation()
    {
        var loc = LocManager.Instance;
        try
        {
            foreach (var (language, added, sly) in new[] { ("eng", "Upgraded", "Sly"), ("zhs", "升级过的", "奇巧") })
            {
                loc.SetLanguage(language);
                var card = ModelDb.Card<Distraction>().ToMutable();
                var ordinary = card.GetDescriptionForPile(PileType.None);
                var preview = card.GetDescriptionForUpgradePreview();
                NativeEventTests.Check(!ordinary.Contains(added) && preview.Contains(added), "Only the upgraded form generates upgraded skills.");
                var highlights = System.Text.RegularExpressions.Regex.Matches(preview, @"\[green\](.*?)\[/green\]", System.Text.RegularExpressions.RegexOptions.Singleline);
                NativeEventTests.Check(highlights.Count == 1 && System.Text.RegularExpressions.Regex.Replace(highlights[0].Groups[1].Value, @"\[[^\]]+\]", "").Trim() == added,
                    "Only the newly added upgrade phrase is highlighted.");
                CardCmd.Upgrade(card);
                NativeEventTests.Check(card.GetDescriptionForUpgradePreview() == preview && card.GetDescriptionForPile(PileType.None).Contains(added)
                    && !card.GetDescriptionForPile(PileType.None).Contains("[green]"), "An already upgraded card has the same preview and ordinary display.");
                var outmaneuver = ModelDb.Card<Outmaneuver>().ToMutable();
                NativeEventTests.Check(outmaneuver.GetDescriptionForPile(PileType.None).Contains(sly), "Native keyword text shows Sly in each language.");
                NativeEventTests.Check(card.HoverTips.Any() && outmaneuver.HoverTips.Any(), "Native skill and keyword hover tips remain available.");
            }
        }
        finally { loc.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    private static async Task Dive()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var seen = new HashSet<Type>();
        for (int i = 0; i < 128; i++)
        {
            var run = HeadlessTestSetup.Run("TRASH_DIVE_" + i, ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>());
            var player = run.Players[0];
            var other = run.Players[1];
            var model = await NativeEventTests.Begin<TrashHeap>(player);
            var option = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".DIVE_IN"));
            int hp = player.Creature.CurrentHp, gold = player.Gold, otherHp = other.Creature.CurrentHp;
            int otherRelics = other.Relics.Count, relics = player.Relics.Count;
            await option.Chosen();
            var awarded = player.Relics.Last();
            seen.Add(awarded.GetType());
            NativeEventTests.Check(awarded is DarkstonePeriapt or DreamCatcher or HandDrill && player.Relics.Count == relics + 1
                && player.Creature.CurrentHp == hp - 8 && player.Gold == gold && model.IsFinished, "Dive loses eight HP and awards one retained relic, then finishes.");
            var rng = HeadlessTestSetup.RngPosition(model.Rng);
            await option.Chosen();
            NativeEventTests.Check(player.Creature.CurrentHp == hp - 8 && player.Relics.Count == relics + 1 && HeadlessTestSetup.RngPosition(model.Rng) == rng
                && other.Creature.CurrentHp == otherHp && other.Relics.Count == otherRelics, "Stale choices do not repeat damage/rewards or affect teammates.");
        }
        NativeEventTests.Check(seen.SetEquals(new[] { typeof(DarkstonePeriapt), typeof(DreamCatcher), typeof(HandDrill) }), "All three retained relics can be awarded.");
    }

    private static async Task DistractionUpgrade()
    {
        foreach (bool upgraded in new[] { false, true })
            foreach (bool fullHand in new[] { false, true })
            {
                var (player, combat) = NativeEventTests.Combat();
                var card = combat.CreateCard<Distraction>(player);
                if (upgraded) CardCmd.Upgrade(card);
                NativeEventTests.Check(card.EnergyCost.Canonical == 1 && card.Keywords.Contains(CardKeyword.Exhaust),
                    "Both forms cost one and retain Exhaust.");
                if (fullHand)
                    for (int i = 0; i < 10; i++)
                        await CardPileCmd.Add(combat.CreateCard<DefendIronclad>(player), PileType.Hand);
                await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
                var generated = (fullHand ? player.PlayerCombatState!.DiscardPile : player.PlayerCombatState!.Hand).Cards.Single();
                NativeEventTests.Check(generated.Type == CardType.Skill && generated.Owner == player && generated.IsUpgraded == upgraded
                    && generated.EnergyCost.GetAmountToSpend() == 0 && player.PlayerCombatState.ExhaustPile.Cards.Contains(card),
                    $"Generated {generated.Id}: source upgraded={upgraded}, full={fullHand}, upgraded={generated.IsUpgraded}, cost={generated.EnergyCost.GetAmountToSpend()}, exhausted={player.PlayerCombatState.ExhaustPile.Cards.Contains(card)}.");
                NativeEventTests.Check(!ModelDb.GetById<CardModel>(generated.Id).IsUpgraded, "The canonical template is untouched.");
                generated.EndOfTurnCleanup();
                NativeEventTests.Check(generated.EnergyCost.GetAmountToSpend() == generated.EnergyCost.Canonical,
                    "The free cost expires at turn end.");
            }
    }

    private static async Task Sly()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var (player, combat) = NativeEventTests.Combat();
            var card = combat.CreateCard<Outmaneuver>(player);
            if (upgraded) CardCmd.Upgrade(card);
            NativeEventTests.Check(card.IsSlyThisTurn, "Outmaneuver has permanent native Sly in both forms.");
            await CardPileCmd.Add(card, PileType.Hand);
            await CardCmd.Discard(new ThrowingPlayerChoiceContext(), card);
            NativeEventTests.Check(player.Creature.GetPower<EnergyNextTurnPower>()?.Amount == (upgraded ? 3 : 2),
                "Discard triggers native Sly auto play and grants only the owner's next-turn energy.");
        }
    }

    private static Task CardPools()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas");
        foreach (var entry in new (CardModel Card, CharacterModel Character, CardRarity Rarity)[]
        {
            (ModelDb.Card<Rebound>(), ModelDb.Character<Defect>(), CardRarity.Uncommon),
            (ModelDb.Card<Stack>(), ModelDb.Character<Defect>(), CardRarity.Common),
            (ModelDb.Card<RipAndTear>(), ModelDb.Character<Defect>(), CardRarity.Uncommon),
            (ModelDb.Card<DualWield>(), ModelDb.Character<Ironclad>(), CardRarity.Uncommon),
            (ModelDb.Card<Entrench>(), ModelDb.Character<Ironclad>(), CardRarity.Uncommon),
        })
        {
            var player = HeadlessTestSetup.PlayerWithRun("TRASH_POOL", entry.Character);
            var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToArray();
            NativeEventTests.Check(pool.Any(c => c.Id == entry.Card.Id) && entry.Card.Pool == player.Character.CardPool
                && entry.Card.Rarity == entry.Rarity && !ModelDb.CardPool<EventCardPool>().AllCardIds.Contains(entry.Card.Id),
                $"{entry.Card.Id} belongs to its normal character pool at the specified rarity.");
            var legal = pool.Where(c => c.Id == entry.Card.Id);
            var shop = CardFactory.CreateForMerchant(player, legal, entry.Rarity).Card;
            HeadlessTestSetup.Combat(player.RunState);
            var generated = CardFactory.GetDistinctForCombat(player, legal, 1, player.RunState.Rng.CombatCardGeneration).Single();
            NativeEventTests.Check(shop.Id == entry.Card.Id && shop.Owner == player && generated.Id == entry.Card.Id,
                "Native shop and combat generation can actually create the migrated card.");
            NativeEventTests.Check(entry.Card.PortraitPath.Contains("/event/") && entry.Card.Portrait != null
                && (AtlasManager.HasSprite("card_atlas", "event/" + entry.Card.Id.Entry.ToLowerInvariant())
                    || entry.Card.HasPortrait || entry.Card.HasBetaPortrait),
                "Migration loads the original portrait, including native beta artwork where no atlas sprite exists.");
        }
        return Task.CompletedTask;
    }

    private static Task RelicPools()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        foreach (var relic in new RelicModel[] { ModelDb.Relic<MawBank>(), ModelDb.Relic<TheBoot>() })
        {
            var player = NativeEventTests.Player();
            player.RelicGrabBag.Populate(player, player.RunState.Rng.UpFront);
            NativeEventTests.Check(ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(player.UnlockState).Any(r => r.Id == relic.Id)
                && !ModelDb.RelicPool<EventRelicPool>().AllRelicIds.Contains(relic.Id) && relic.Rarity == RelicRarity.Common,
                "Both relics move into the normal common shared pool.");
            var reward = RelicFactory.PullNextRelicFromFront(player, RelicRarity.Common, r => r.Id == relic.Id);
            NativeEventTests.Check(reward.Id == relic.Id && relic.Icon != null && relic.IconOutline != null && relic.BigIcon != null,
                "The real reward bag yields the migrated relic and its original artwork loads.");
        }
        return Task.CompletedTask;
    }

    private static async Task Grab()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var seen = new HashSet<Type>();
        for (int i = 0; i < 128; i++)
        {
            var player = NativeEventTests.Player("TRASH_" + i);
            var model = await NativeEventTests.Begin<TrashHeap>(player);
            var option = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".GRAB"));
            option.Description.Add("Gold", model.DynamicVars.Gold.IntValue);
            int gold = player.Gold, count = player.Deck.Cards.Count;
            var rng = HeadlessTestSetup.RngPosition(model.Rng);
            NativeEventTests.Check(option.Description.GetFormattedText().Contains("50"), "The option displays 50 gold.");
            _ = option.Description.GetFormattedText();
            NativeEventTests.Check(HeadlessTestSetup.RngPosition(model.Rng) == rng, "Formatting consumes no event RNG.");
            await option.Chosen();
            NativeEventTests.Check(player.Gold == gold + 50 && player.Deck.Cards.Count == count + 1 && model.IsFinished,
                "Grab grants exactly 50 gold and one permanent card, then finishes.");
            NativeEventTests.Check(AllMatters.Events.TrashHeapCandidates.Cards(player).Any(c => c.Id == player.Deck.Cards.Last().Id),
                "Grab awards a member of the actual incremental candidate pool.");
            seen.Add(player.Deck.Cards.Last().GetType());
            rng = HeadlessTestSetup.RngPosition(model.Rng);
            await option.Chosen();
            NativeEventTests.Check(player.Gold == gold + 50 && player.Deck.Cards.Count == count + 1 && HeadlessTestSetup.RngPosition(model.Rng) == rng,
                "A stale option cannot repeat rewards or reroll.");
        }
        NativeEventTests.Check(seen.IsSupersetOf(new[] { typeof(Caltrops), typeof(Clash), typeof(Distraction), typeof(MegaCrit.Sts2.Core.Models.Cards.HelloWorld), typeof(Outmaneuver) }),
            "Every retained card, including Hello World, remains obtainable from Grab.");
    }
}
