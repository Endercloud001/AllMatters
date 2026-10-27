using AllMatters.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(SpiritGrafter), "GenerateInitialOptions")]
internal static class NativeSpiritGrafter
{
    private static void Postfix(SpiritGrafter __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (!NativeSelectionVariant.Alternative(model)) return;
        var options = __result.ToList();
        options[0] = NativeEventActions.Option(model, "SPIRIT_GRAFTER.pages.INITIAL.options.AM_CHRYSALIS", async () =>
        {
            await CreatureCmd.Heal(model.Owner!.Creature, 25);
            var card = model.Owner.RunState.CreateCard<Chrysalis>(model.Owner);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
            NativeEventActions.Finish(model, "SPIRIT_GRAFTER.pages.LET_IT_IN.description");
        }, HoverTipFactory.FromCardWithCardHoverTips<Chrysalis>().ToArray());
        __result = options;
    }
}
