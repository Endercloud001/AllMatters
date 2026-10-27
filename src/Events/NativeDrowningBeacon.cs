using AllMatters.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(DrowningBeacon), "GenerateInitialOptions")]
internal static class NativeDrowningBeacon
{
    private static void Postfix(DrowningBeacon __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (NativePotionPayment.Potion(__instance) == null) return;
        var option = NativePotionPayment.Option(__instance, "DROWNING_BEACON.pages.INITIAL.options.AM_LIGHT", async () =>
        {
            var card = __instance.Owner!.RunState.CreateCard<Brilliance>(__instance.Owner);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
            NativeEventActions.Finish(__instance, "DROWNING_BEACON.pages.AM_LIGHT.description");
        });
        option.HoverTips = option.HoverTips.Concat(HoverTipFactory.FromCardWithCardHoverTips<Brilliance>());
        __result = __result.Append(option).ToArray();
    }
}
