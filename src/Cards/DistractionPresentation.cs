using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace AllMatters.Cards;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class DistractionLocalization
{
    internal const string DescriptionKey = "ALL_MATTERS_DISTRACTION.description";

    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name != "cards") return;
        string original = __result.GetRawText("DISTRACTION.description");
        string description = __instance.Language switch
        {
            "zhs" => original.Replace("随机", "随机{IfUpgraded:show:[gold]升级过的[/gold]|}", StringComparison.Ordinal),
            "eng" => original.Replace("random ", "random {IfUpgraded:show:[gold]Upgraded[/gold] |}", StringComparison.Ordinal),
            _ => "Add a random {IfUpgraded:show:[gold]Upgraded[/gold] |}[gold]Skill[/gold] into your [gold]Hand[/gold]. It costs [blue]0[/blue] this turn.",
        };
        __result.MergeWith(new Dictionary<string, string> { [DescriptionKey] = description });
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Description), MethodType.Getter)]
internal static class DistractionDescription
{
    private static void Postfix(CardModel __instance, ref LocString __result)
    {
        if (__instance is Distraction) __result = new LocString("cards", DistractionLocalization.DescriptionKey);
    }
}
