using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using AllMatters.Relics;

internal static class NativeCandleTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native candle cheese offers six Common and two Uncommon distinct character cards and adds chosen two", Cheese),
        ("Native candle acquisition pays14 Max HP and produces a usable relic", CandleChoice),
        ("Native candle curses cost1, obey available energy and lose2 HP before native Exhaust", Play),
    ];
    private static async Task Cheese()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        var e = await NativeEventTests.Begin<RoomFullOfCheese>(p);
        ((RunState)p.RunState).AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Unknown, MegaCrit.Sts2.Core.Rooms.RoomType.Event, e.Id);
        int count = p.Deck.Cards.Count;
        using (CardSelectCmd.UseSelector(new Choose())) await e.CurrentOptions[0].Chosen();
        NativeEventTests.Check(e.IsFinished && p.Deck.Cards.Count == count + 2, "Add exactly the two selected cards and finish.");
    }

    private static async Task CandleChoice()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        int max = p.Creature.MaxHp;
        var e = await NativeEventTests.Begin<RoomFullOfCheese>(p);
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_CANDLE")).Chosen();
        NativeEventTests.Check(e.IsFinished && p.Creature.MaxHp == max - 14 && p.Relics.OfType<BlueCandle>().Count() == 1, "Pay14 Max HP and obtain Blue Candle.");
    }

    private static async Task Play()
    {
        var (p, combat) = NativeEventTests.Combat();
        var curse = combat.CreateCard<Greed>(p);
        await CardPileCmd.Add(curse, PileType.Hand);
        p.PlayerCombatState!.Energy = 1;
        NativeEventTests.Check(!curse.CanPlay(), "Curse remains unplayable without a candle.");
        var candle = await RelicCmd.Obtain<BlueCandle>(p);
        NativeEventTests.Check(curse.EnergyCost.GetAmountToSpend() == 1 && curse.CanPlay(), "Candle grants real1-energy playability.");
        p.PlayerCombatState.Energy = 0;
        NativeEventTests.Check(!curse.CanPlay(), "Candle must not bypass resource restrictions.");
        p.PlayerCombatState.Energy = 1;
        int hp = p.Creature.CurrentHp;
        await curse.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(p.Creature.CurrentHp == hp - 2 && p.PlayerCombatState.ExhaustPile.Cards.Contains(curse), "Actual auto play loses2 HP and exhausts the curse through native resolution.");
        await RelicCmd.Remove(candle);
        var second = combat.CreateCard<Greed>(p);
        await CardPileCmd.Add(second, PileType.Hand);
        NativeEventTests.Check(!second.CanPlay() && second.EnergyCost.Canonical == -1 && second.Keywords.Contains(CardKeyword.Unplayable), "Losing candle restores original curse restrictions and costs.");
    }

    private sealed class Choose : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            NativeEventTests.Check(cards.Length == 8 && cards.Select(c => c.Id).Distinct().Count() == 8
                && cards.Count(c => c.Rarity == CardRarity.Common) == 6 && cards.Count(c => c.Rarity == CardRarity.Uncommon) == 2,
                "Offer six unique Common and two unique Uncommon cards.");
            NativeEventTests.Check(cards.All(c => c.Pool.Id == c.Owner.Character.CardPool.Id) && minSelect == 2 && maxSelect == 2, "Use only character cards, choosing exactly two.");
            return Task.FromResult<IEnumerable<CardModel>>(cards.Take(2));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException();
    }
}
