using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer;

internal static class NativeProbabilityTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native probabilities bridge returns free or finishes within three steps with legal Basic removal", Bridge),
        ("Native probabilities brain leech freezes and pays the displayed potion then restores inventory", Brain),
        ("Native probabilities seed requires two legal cards before charging its displayed potion", Seed),
        ("Native probabilities future refresh swaps two specific bottles and ends after one trade", Future),
        ("Native probabilities self help offers actual enchantments and locks unavailable types", Book),
        ("Native probabilities future card trade grants upgraded same-type same-rarity character cards", FutureCards),
    ];
    private static async Task Bridge()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        int falls = 0, removals = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var p = NativeEventTests.Player("BRIDGE" + seed);
            var original = p.Deck.Cards.ToArray();
            int hp = p.Creature.CurrentHp;
            var e = await NativeEventTests.Begin<SlipperyBridge>(p);
            var step = e.CurrentOptions.SingleOrDefault(o => o.TextKey.EndsWith(".AM_FORWARD"));
            NativeEventTests.Check(step != null, "Bridge must offer the new forward choice.");
            for (int i = 0; i < 3 && !e.IsFinished; i++)
            {
                var forward = e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_FORWARD"));
                if (i == 2) NativeEventTests.Check(forward.WillKillPlayer != null, "The last risk must retain a lethal damage warning.");
                await forward.Chosen();
            }
            NativeEventTests.Check(e.IsFinished, "Three steps must finish.");
            var removed = original.Except(p.Deck.Cards).ToArray();
            if (removed.Length == 1)
            {
                removals++;
                NativeEventTests.Check(removed[0].Rarity == MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Basic && p.Creature.CurrentHp == hp, "Successful removal prefers legal Basic and costs no HP.");
            }
            else
            {
                falls++;
                NativeEventTests.Check(removed.Length == 0 && p.Creature.CurrentHp == hp - 13, "Third failure costs 13 HP with no removal.");
            }
        }
        NativeEventTests.Check(falls > 0 && removals > 0, "Exercise success and falling outcomes.");
        var leavePlayer = NativeEventTests.Player();
        var leave = await NativeEventTests.Begin<SlipperyBridge>(leavePlayer);
        int count = leavePlayer.Deck.Cards.Count;
        await leave.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_RETURN")).Chosen();
        NativeEventTests.Check(leave.IsFinished && leavePlayer.Deck.Cards.Count == count, "Return must finish without removing a card.");
    }

    private static async Task Brain()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        await PotionCmd.TryToProcure<BlockPotion>(p);
        var potion = p.Potions.Single();
        var e = await NativeEventTests.Begin<BrainLeech>(p);
        var option = e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_STIMULATE"));
        NativeEventTests.Check(!CanRemove(p) && option.Description.GetFormattedText().Contains(potion.Title.GetFormattedText()), "Freeze and display the actual bottle on entry.");
        int block = p.Creature.Block;
        await potion.OnUseWrapper(new ThrowingPlayerChoiceContext(), p.Creature);
        NativeEventTests.Check(p.Creature.Block == block && p.Potions.Contains(potion), "Frozen potion cannot be drunk.");
        await option.Chosen();
        NativeEventTests.Check(e.IsFinished && !p.Potions.Any() && CanRemove(p), "Consume the displayed bottle once, then restore inventory.");
    }

    private static async Task Seed()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int count in new[] { 1, 2 })
        {
            var p = NativeEventTests.Player();
            await PotionCmd.TryToProcure<BlockPotion>(p);
            var selected = p.Deck.Cards.Take(count).ToArray();
            foreach (var card in p.Deck.Cards.Except(selected).ToArray()) p.Deck.RemoveInternal(card);
            var e = await NativeEventTests.Begin<SapphireSeed>(p);
            var option = e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_CATALYZE"));
            NativeEventTests.Check(option.IsLocked == (count == 1), "Payment requires two legal targets.");
            if (count == 1)
            {
                await option.Chosen();
                NativeEventTests.Check(p.Potions.Count() == 1, "Locked selection cannot consume payment.");
                e.EnsureCleanup();
            }
            else
            {
                using (CardSelectCmd.UseSelector(new NativeEventTests.Select(selected))) await option.Chosen();
                NativeEventTests.Check(selected.All(c => c.Enchantment is Sown) && !p.Potions.Any() && e.IsFinished, "Enchant the two selected cards after one payment.");
            }
            NativeEventTests.Check(CanRemove(p), "All completion paths restore inventory.");
        }
    }

    private static async Task Future()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        await PotionCmd.TryToProcure<FirePotion>(p);
        await PotionCmd.TryToProcure<BlockPotion>(p);
        p.Gold = 100;
        var e = await NativeEventTests.Begin<TheFutureOfPotions>(p);
        string first = e.CurrentOptions[0].Description.GetFormattedText();
        string second = e.CurrentOptions[1].Description.GetFormattedText();
        var bottles = p.Potions.ToArray();
        var cardBottle = bottles.Single(b => first.Contains(b.Title.GetFormattedText()));
        var relicBottle = bottles.Single(b => second.Contains(b.Title.GetFormattedText()));
        NativeEventTests.Check(cardBottle != relicBottle && !CanRemove(p), "Routes must bind distinct instances and freeze both.");
        await e.CurrentOptions[2].Chosen();
        NativeEventTests.Check(p.Gold == 50 && e.CurrentOptions[0].Description.GetFormattedText().Contains(relicBottle.Title.GetFormattedText())
            && e.CurrentOptions[1].Description.GetFormattedText().Contains(cardBottle.Title.GetFormattedText()), "Two-bottle refresh costs 50 and swaps positions.");
        await e.CurrentOptions[2].Chosen();
        NativeEventTests.Check(p.Gold == 0 && e.CurrentOptions[2].IsLocked, "Refresh can repeat until full payment is unavailable.");
        await e.CurrentOptions[1].Chosen();
        NativeEventTests.Check(e.IsFinished && p.Potions.Count() == 1 && CanRemove(p), "Relic trade ends and consumes only its current specific bottle.");
    }

    private static async Task Book()
    {
        var p = NativeEventTests.Player();
        var e = await NativeEventTests.Begin<SelfHelpBook>(p);
        var option = e.CurrentOptions[0];
        var attack = p.Deck.Cards.First(c => c.Type == CardType.Attack);
        NativeEventTests.Check(e.CurrentOptions[2].IsLocked, "Power option must stay locked without legal power cards.");
        using (CardSelectCmd.UseSelector(new NativeEventTests.Select(attack))) await option.Chosen();
        NativeEventTests.Check(e.IsFinished && attack.Enchantment != null && option.Description.GetFormattedText().Contains(attack.Enchantment.Title.GetFormattedText()), "Displayed enchantment must be applied to chosen attack.");
        var actualTip = option.HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().First();
        var expectedTip = attack.Enchantment!.HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().First();
        NativeEventTests.Check(actualTip.Description == expectedTip.Description, "The offer tooltip must use the actual enchantment amount.");
    }

    private static async Task FutureCards()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        await PotionCmd.TryToProcure<FirePotion>(p);
        await PotionCmd.TryToProcure<BlockPotion>(p);
        var e = await NativeEventTests.Begin<TheFutureOfPotions>(p);
        ((RunState)p.RunState).AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Unknown, MegaCrit.Sts2.Core.Rooms.RoomType.Event, e.Id);
        var localId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        RunManager.Instance.SetUpTest((RunState)p.RunState, new NetSingleplayerGameService());
        try
        {
            int count = p.Deck.Cards.Count;
            using (CardSelectCmd.UseSelector(new ChooseReward())) await e.CurrentOptions[0].Chosen();
            NativeEventTests.Check(e.IsFinished && p.Deck.Cards.Count == count + 1 && p.Deck.Cards.Last().IsUpgraded && p.Potions.Count() == 1,
                "Pay one bound potion and obtain one of the upgraded cards, then finish.");
        }
        finally
        {
            RunManager.Instance.CleanUp(false);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = localId;
        }
    }

    private sealed class ChooseReward : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) => throw new InvalidOperationException();
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
        {
            NativeEventTests.Check(options.Count == 3 && options.All(c => c.Card.IsUpgraded && c.Card.Rarity == CardRarity.Common)
                && options.Select(c => c.Card.Type).Distinct().Count() == 1
                && options.All(c => c.Card.Pool.Id == c.Card.Owner.Character.CardPool.Id), "Reward is three upgraded Common character cards of the same type.");
            return new() { card = options[0].Card };
        }
    }

    private static bool CanRemove(MegaCrit.Sts2.Core.Entities.Players.Player p)
#if STS2_PUBLIC
        => p.CanRemovePotions;
#else
        => p.CanUseOrRemovePotions;
#endif
}
