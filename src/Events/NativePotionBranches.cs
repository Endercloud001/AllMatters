using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch]
internal static class NativePotionBranches
{
    private static IEnumerable<MethodBase> TargetMethods()
        => new[] { typeof(BrainLeech), typeof(SapphireSeed) }.Select(t => AccessTools.Method(t, "GenerateInitialOptions"));

    private static void Postfix(EventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (NativePotionPayment.Potion(model) == null) return;
        var options = __result.ToList();
        if (model is BrainLeech)
        {
            options.Add(NativePotionPayment.Option(model, "BRAIN_LEECH.pages.INITIAL.options.AM_STIMULATE", async () =>
            {
                int roll = model.Rng.NextInt(100);
                if (roll is >= 57 and < 80) await CreatureCmd.GainMaxHp(model.Owner!.Creature, 5);
                else if (roll >= 80) await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(model.Owner!, RelicRarity.Common).ToMutable(), model.Owner!);
                NativeEventActions.Finish(model, "BRAIN_LEECH.pages.AM_STIMULATE_" + (roll < 57 ? "NONE" : roll < 80 ? "HP" : "RELIC") + ".description");
            }));
        }
        else
        {
            var enchantment = ModelDb.Enchantment<Sown>();
            bool legal = model.Owner!.Deck.Cards.Count(enchantment.CanEnchant) >= 2;
            var option = NativePotionPayment.Option(model, "SAPPHIRE_SEED.pages.INITIAL.options.AM_CATALYZE", async () =>
            {
                var cards = await CardSelectCmd.FromDeckForEnchantment(model.Owner!, enchantment, 1,
                    new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 2));
                foreach (var card in cards) CardCmd.Enchant<Sown>(card, 1);
                NativeEventActions.Finish(model, "SAPPHIRE_SEED.pages.AM_CATALYZE.description");
            }, legal);
            var preview = enchantment.ToMutable();
            preview.Amount = 1;
            preview.RecalculateValues();
            option.HoverTips = option.HoverTips.Concat(preview.HoverTips).ToArray();
            options.Add(option);
        }
        __result = options;
    }
}
