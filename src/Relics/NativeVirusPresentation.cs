using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Relics;

[HarmonyPatch(typeof(RelicModel), "get_PackedIconPath")]
internal static class NativeVirusIcon
{
    private static void Postfix(RelicModel __instance, ref string __result)
    {
        if (__instance is SymbioticVirus) __result = "res://AllMatters/images/relics/symbiotic_virus.png";
    }
}
[HarmonyPatch(typeof(RelicModel), "get_PackedIconOutlinePath")]
internal static class NativeVirusOutline
{
    private static void Postfix(RelicModel __instance, ref string __result)
    {
        if (__instance is SymbioticVirus) __result = "res://AllMatters/images/relics/outlines/symbiotic_virus.png";
    }
}
[HarmonyPatch(typeof(RelicModel), "get_BigIconPath")]
internal static class NativeVirusBigIcon
{
    private static void Postfix(RelicModel __instance, ref string __result)
    {
        if (__instance is SymbioticVirus) __result = "res://AllMatters/images/relics/symbiotic_virus.png";
    }
}
[HarmonyPatch(typeof(SymbioticVirus), "get_ExtraHoverTips")]
internal static class NativeVirusHoverTips
{
    private static void Postfix(ref IEnumerable<IHoverTip> __result) => __result = HoverTipFactory.FromCardWithCardHoverTips<Dazed>();
}
