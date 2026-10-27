using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Relics;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeTrashRelicLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name != "relics") return;
        bool zh = __instance.Language == "zhs";
        __result.MergeWith(new Dictionary<string, string>
        {
            ["SWORD_OF_STONE.description"] = zh
                ? "击败 [blue]{Elites}[/blue] 名精英或 Boss 敌人后，变为一件强力遗物。"
                : "After defeating [blue]{Elites}[/blue] Elite or Boss enemies, transform into a powerful relic.",
            ["TOUGH_BANDAGES.description"] = zh
                ? "每当你在自己的回合丢弃一张牌，获得 [blue]{Block}[/blue] 点[gold]格挡[/gold]。"
                : "Whenever you discard a card during your turn, gain [blue]{Block}[/blue] [gold]Block[/gold].",
            ["TOUGH_BANDAGES.flavor"] = zh ? "“医不自治。”" : "“Physician, heal thyself.”",
            ["CHARONS_ASHES.description"] = zh
                ? "每当你[gold]消耗[/gold]一张牌，对所有敌人造成 [blue]{Damage}[/blue] 点伤害。"
                : "Whenever you [gold]Exhaust[/gold] a card, deal [blue]{Damage}[/blue] damage to ALL enemies.",
            ["CHARONS_ASHES.flavor"] = zh ? "“在那道光中，我找到了救赎。”" : "“Within that light, I found salvation.”",
        });
    }
}
