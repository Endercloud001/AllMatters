using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.Events;

[HarmonyPatch(typeof(WhisperingHollow), nameof(WhisperingHollow.CalculateVars))]
internal static class WhisperingCosts
{
    private static void Postfix(WhisperingHollow __instance)
    {
        __instance.DynamicVars.Gold.BaseValue -= 10;
        __instance.DynamicVars.HpLoss.BaseValue = __instance.Rng.NextInt(2) == 0 ? 9 : 18;
    }
}

[HarmonyPatch(typeof(WhisperingHollow), "GenerateInitialOptions")]
internal static class WhisperingHug
{
    private static void Postfix(WhisperingHollow __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        int count = model.DynamicVars.HpLoss.IntValue / 9;
        bool legal = model.Owner!.Deck.Cards.Count(c => c.IsTransformable) >= count;
        var options = __result.ToList();
        options[1] = new EventOption(model, legal ? async () =>
        {
            var cards = await CardSelectCmd.FromDeckForTransformation(model.Owner!, new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, count));
            foreach (var card in cards) await CardCmd.TransformToRandom(card, model.Rng, CardPreviewStyle.EventLayout);
            await NativeEventActions.LoseHp(model, model.DynamicVars.HpLoss.BaseValue);
            NativeEventActions.Finish(model, "WHISPERING_HOLLOW.pages.HUG.description");
        } : null, "WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_" + count, HoverTipFactory.Static(StaticHoverTip.Transform))
            .ThatDoesDamage(model.DynamicVars.HpLoss.BaseValue);
        __result = options;
    }
}
