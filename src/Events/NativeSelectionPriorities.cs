using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.Events;

[HarmonyPatch(typeof(DoorsOfLightAndDark), "Light")]
internal static class NativeLightPriority
{
    private static bool Prefix(DoorsOfLightAndDark __instance, ref Task __result)
    {
        var cards = __instance.Owner!.Deck.Cards.Where(c => c.IsUpgradable).ToList().StableShuffle(__instance.Rng)
            .OrderBy(c => c.Rarity == CardRarity.Basic ? 1 : 0).Take(__instance.DynamicVars.Cards.IntValue);
        foreach (var card in cards) CardCmd.Upgrade(card);
        NativeEventActions.Finish(__instance, "DOORS_OF_LIGHT_AND_DARK.pages.LIGHT.description");
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(MorphicGrove), "GenerateInitialOptions")]
internal static class NativeMorphicChoices
{
    internal static CardRarity? Higher(CardModel card) => card.Rarity switch
    {
        CardRarity.Basic => CardRarity.Common,
        CardRarity.Common => CardRarity.Uncommon,
        CardRarity.Uncommon => CardRarity.Rare,
        _ => null
    };
    internal static CardModel[] Options(MorphicGrove model, CardModel card)
    {
        var next = Higher(card);
        return !next.HasValue ? [] : model.Owner!.Character.CardPool.GetUnlockedCards(model.Owner.UnlockState, model.Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Rarity == next.Value && c.Id != card.Id).ToArray();
    }
    internal static CardModel[] Targets(MorphicGrove model) => model.Owner!.Deck.Cards
        .Where(c => c.IsTransformable && Options(model, c).Length > 0).ToArray();

    private static void Postfix(MorphicGrove __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (Targets(__instance).Length >= 2) return;
        var list = __result.ToList();
        list[0] = new EventOption(__instance, null, "MORPHIC_GROVE.pages.INITIAL.options.AM_GROUP_LOCKED");
        __result = list;
    }
}

[HarmonyPatch(typeof(MorphicGrove), "Group")]
internal static class NativeMorphicTransform
{
    private static bool Prefix(MorphicGrove __instance, ref Task __result)
    {
        __result = Transform(__instance);
        return false;
    }
    private static async Task Transform(MorphicGrove model)
    {
        var targets = NativeMorphicChoices.Targets(model).ToList().StableShuffle(model.Rng)
            .OrderBy(c => c.Rarity == CardRarity.Basic ? 0 : c.Rarity == CardRarity.Common ? 1 : 2).Take(2).ToArray();
        if (targets.Length < 2) return;
        await PlayerCmd.LoseGold(model.Owner!.Gold, model.Owner, GoldLossType.Stolen);
        foreach (var original in targets)
        {
            var replacement = CardFactory.CreateRandomCardForTransform(original, NativeMorphicChoices.Options(model, original), false, model.Rng);
            await CardCmd.Transform(original, replacement, CardPreviewStyle.EventLayout);
        }
        NativeEventActions.Finish(model, "MORPHIC_GROVE.pages.GROUP.description");
    }
}
