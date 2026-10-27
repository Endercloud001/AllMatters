using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Events;

[HarmonyPatch(typeof(TheFutureOfPotions), "GenerateInitialOptions")]
internal static class NativeFutureOfPotions
{
    private sealed record Offers(PotionModel CardPotion, PotionModel RelicPotion, CardType Type);
    private static readonly ConditionalWeakTable<TheFutureOfPotions, Offers> Current = new();

    private static Offers Roll(TheFutureOfPotions model, Offers? previous)
    {
        var potions = model.Owner!.Potions.ToArray();
        var pairs = (from first in potions from second in potions where !ReferenceEquals(first, second) select (first, second)).ToArray();
        var changed = pairs.Where(pair => pair.first != previous?.CardPotion && pair.second != previous?.RelicPotion).ToArray();
        var choices = changed.Length > 0 ? changed : pairs;
        var pair = choices[model.Rng.NextInt(choices.Length)];
        CardType[] types = pair.first.Rarity is PotionRarity.Common or PotionRarity.Token
            ? [CardType.Attack, CardType.Skill] : [CardType.Attack, CardType.Skill, CardType.Power];
        return new(pair.first, pair.second, types[model.Rng.NextInt(types.Length)]);
    }

    private static bool Prefix(TheFutureOfPotions __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result = Options(__instance);
        return false;
    }

    private static IReadOnlyList<EventOption> Options(TheFutureOfPotions model)
    {
        var offers = Current.GetValue(model, e => Roll(e, null));
        EventOption Trade(bool cards)
        {
            var potion = cards ? offers.CardPotion : offers.RelicPotion;
            string key = "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_" + (cards ? "CARDS" : "RELIC");
            var description = new LocString("events", key + ".description");
            description.Add("Potion", potion.Title);
            description.Add("Rarity", Rarity(potion).ToLocString());
            description.Add("Type", offers.Type.ToLocString());
            return new EventOption(model, async () =>
            {
                if (model.IsFinished || !model.Owner!.Potions.Contains(potion)) return;
                await PotionCmd.Discard(potion);
                if (cards)
                {
                    var options = CardCreationOptions.ForNonCombatWithUniformOdds([model.Owner.Character.CardPool],
                        c => c.Rarity == Rarity(potion) && c.Type == offers.Type)
                        .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
                    var reward = new CardReward(options, 3, model.Owner);
                    reward.AfterGenerated += () => { foreach (var card in reward.Cards) CardCmd.Upgrade(card); };
                    await RewardsCmd.OfferCustom(model.Owner, [reward]);
                }
                else
                {
                    var rarity = Rarity(potion) switch { CardRarity.Common => RelicRarity.Common, CardRarity.Uncommon => RelicRarity.Uncommon, _ => RelicRarity.Rare };
                    await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(model.Owner, rarity).ToMutable(), model.Owner);
                }
                NativeEventActions.Finish(model, "THE_FUTURE_OF_POTIONS.pages.DONE.description");
            }, new LocString("events", key + ".title"), description, key, potion.HoverTips);
        }
        var refresh = new EventOption(model, model.Owner!.Gold >= 50 ? async () =>
        {
            if (model.Owner.Gold < 50 || model.IsFinished) return;
            await PlayerCmd.LoseGold(50, model.Owner, GoldLossType.Spent);
            var next = Roll(model, offers);
            Current.Remove(model); Current.Add(model, next);
            NativeEventActions.State(model, "THE_FUTURE_OF_POTIONS.pages.INITIAL.description", Options(model));
        } : null, "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_REFRESH");
        return [Trade(true), Trade(false), refresh];
    }

    private static CardRarity Rarity(PotionModel potion) => potion.Rarity switch
    {
        PotionRarity.Rare or PotionRarity.Event => CardRarity.Rare,
        PotionRarity.Uncommon => CardRarity.Uncommon,
        _ => CardRarity.Common
    };
}
