// SPDX-License-Identifier: GPL-3.0-only
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace AllMatters.Relics;

// Keep the bottle grant outside local keywords. A later AddKeyword(Innate),
// including an upgrade, remains independent when the bottle is removed.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetKeywordsWithSources))]
internal static class BottledKeywords
{
    private static void Postfix(CardModel __instance, KeywordSources sources, ref IReadOnlySet<CardKeyword> __result)
    {
        if (sources.HasFlag(KeywordSources.Global) && BottledCard.IsBound(__instance))
            __result = new HashSet<CardKeyword>(__result) { CardKeyword.Innate };
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsRemovable), MethodType.Getter)]
internal static class BottledRemovalEligibility
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (BottledCard.IsProtected(__instance)) __result = false;
    }
}

// Native transformation validates IsTransformable; direct deck removal does not
// validate IsRemovable, so guard that command as well as its selection screens.
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromDeck), [typeof(IReadOnlyList<CardModel>), typeof(bool)])]
internal static class BottledDeckRemoval
{
    private static void Prefix(IReadOnlyList<CardModel> cards)
    {
        if (cards.Any(BottledCard.IsProtected))
            throw new InvalidOperationException("A bottled card cannot be removed from the deck while its bottle is held.");
    }
}
