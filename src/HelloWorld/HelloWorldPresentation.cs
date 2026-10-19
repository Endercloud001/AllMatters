using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using HelloWorldCard = MegaCrit.Sts2.Core.Models.Cards.HelloWorld;

namespace AllMatters.HelloWorld;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class HelloWorldLocalizationPatch
{
    internal const string CardDescriptionKey = "ALL_MATTERS_HELLO_WORLD.description";

    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool chinese = __instance.Language == "zhs";
        if (name == "cards")
        {
            string upgraded = chinese
                ? "在你的回合开始时，将一张[gold]升级过的[/gold]随机[gold]普通[/gold]牌加入你的手牌。"
                : "At the start of your turn, add an [gold]Upgraded[/gold] random [gold]Common[/gold] card into your [gold]Hand[/gold].";
            string original = __result.GetRawText("HELLO_WORLD.description");
            __result.MergeWith(new Dictionary<string, string>
            {
                [CardDescriptionKey] = "{IfUpgraded:show:" + upgraded + "|" + original + "}"
            });
        }
        else if (name == "powers")
        {
            string entry = ModelDb.GetId<AllMattersHelloWorldPlusPower>().Entry;
            __result.MergeWith(new Dictionary<string, string>
            {
                [entry + ".title"] = __result.GetRawText("HELLO_WORLD_POWER.title") + "+",
                [entry + ".description"] = chinese
                    ? "在你的回合开始时，将{Amount}张[gold]升级过的[/gold]随机[gold]普通[/gold]牌加入你的手牌。"
                    : "At the start of your turn, add {Amount} [gold]Upgraded[/gold] random [gold]Common[/gold] card(s) into your [gold]Hand[/gold]."
            });
        }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Description), MethodType.Getter)]
internal static class HelloWorldDescriptionPatch
{
    private static void Postfix(CardModel __instance, ref LocString __result)
    {
        if (__instance is HelloWorldCard)
        {
            __result = new LocString("cards", HelloWorldLocalizationPatch.CardDescriptionKey);
        }
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.PackedIconPath), MethodType.Getter)]
internal static class HelloWorldPowerIconPatch
{
    private static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is AllMattersHelloWorldPlusPower)
        {
            __result = ModelDb.Power<HelloWorldPower>().PackedIconPath;
        }
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ResolvedBigIconPath), MethodType.Getter)]
internal static class HelloWorldPowerBigIconPatch
{
    private static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is AllMattersHelloWorldPlusPower)
        {
            __result = ModelDb.Power<HelloWorldPower>().ResolvedBigIconPath;
        }
    }
}
