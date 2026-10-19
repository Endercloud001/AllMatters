using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using HelloWorldCard = MegaCrit.Sts2.Core.Models.Cards.HelloWorld;

namespace AllMatters.HelloWorld;

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Cards.HelloWorld), "OnUpgrade")]
internal static class HelloWorldUpgradePatch
{
    private static bool Prefix() => false;
}

[HarmonyPatch(typeof(HelloWorldPower), nameof(HelloWorldPower.BeforeHandDraw))]
internal static class HelloWorldTurnPatch
{
    private static bool Prefix(HelloWorldPower __instance, Player player, ref Task __result)
    {
        var upgraded = __instance.Owner.GetPower<AllMattersHelloWorldPlusPower>();
        if (upgraded == null || upgraded.AmountOnTurnStart < 1)
        {
            return true;
        }
        __result = upgraded.Generate(player, Math.Max(0, __instance.AmountOnTurnStart));
        return false;
    }
}

[HarmonyPatch(typeof(HelloWorldCard), "OnPlay")]
internal static class HelloWorldPlayPatch
{
    private static bool Prefix(HelloWorldCard __instance, PlayerChoiceContext choiceContext, ref Task __result)
    {
        if (!__instance.IsUpgraded)
        {
            return true;
        }
        __result = PlayUpgraded(__instance, choiceContext);
        return false;
    }

    private static async Task PlayUpgraded(HelloWorldCard card, PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.TriggerAnim(card.Owner.Creature, "PowerUp", card.Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<AllMattersHelloWorldPlusPower>(choiceContext, card.Owner.Creature, 1m, card.Owner.Creature, card);
    }
}
