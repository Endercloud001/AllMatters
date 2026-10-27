using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace AllMatters.Cards;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.CanonicalKeywords), MethodType.Getter)]
internal static class OutmaneuverSly
{
    private static void Postfix(CardModel __instance, ref IEnumerable<CardKeyword> __result)
    {
        if (__instance is Outmaneuver) __result = __result.Append(CardKeyword.Sly).Distinct();
    }
}

[HarmonyPatch(typeof(Distraction), "OnUpgrade")]
internal static class DistractionUpgrade
{
    private static bool Prefix() => false;
}

[HarmonyPatch(typeof(Distraction), "OnPlay")]
internal static class DistractionPlay
{
    private static bool Prefix(Distraction __instance, ref Task __result)
    {
        if (!__instance.IsUpgraded) return true;
        __result = Play(__instance);
        return false;
    }

    private static async Task Play(Distraction source)
    {
        var owner = source.Owner;
        var card = CardFactory.GetDistinctForCombat(owner,
            owner.Character.CardPool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
                .Where(candidate => candidate.Type == CardType.Skill), 1, owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (card == null) return;
        CardCmd.Upgrade(card);
        card.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
    }
}
