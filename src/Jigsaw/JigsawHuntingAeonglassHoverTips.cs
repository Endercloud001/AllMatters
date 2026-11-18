using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.HoverTips), MethodType.Getter)]
internal static class JigsawHuntingAeonglassHoverTips
{
    private static void Postfix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not WitheringPresencePower presence || !presence.IsMutable || !presence.IsVisible ||
            presence.Owner.Monster is not Aeonglass glass || !JigsawHuntingBosses.IsHuntedBoss(glass))
            return;

        // Keep the original Wither preview (including its native upgrade count)
        // and keyword tips. Only this targeted power's rule description changes.
        var description = new LocString("powers", "AM_HUNTED_AEONGLASS.description");
        description.Add("CardsPerTrigger", JigsawHuntingBosses.AeonglassCardsPerTrigger);
        description.Add("CardsLeft", presence.DynamicVars["CardsLeft"].IntValue);
        description.Add("Wound", ModelDb.Card<Wound>().Title);
        description.Add("Burn", ModelDb.Card<Burn>().Title);
        description.Add("Void", ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Void>().Title);
        description.Add("Dazed", ModelDb.Card<Dazed>().Title);
        description.Add("Wither", ModelDb.Card<Wither>().Title);
        var tips = __result.ToList();
        tips[0] = new HoverTip(presence, description.GetFormattedText(), true);
        __result = tips;
    }
}
