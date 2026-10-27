using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeNlothLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["THIS_OR_THAT.pages.INITIAL.options.AM_REVEAL.title"] = zh ? "揪出真身" : "Reveal the Owner",
            ["THIS_OR_THAT.pages.INITIAL.options.AM_REVEAL.description"] = zh ? "看看手臂的主人是谁。" : "See who owns those arms.",
            ["THIS_OR_THAT.pages.AM_REVEAL_NONE.description"] = zh ? "你用尽全力攻向坑洞，一个驼着背、背后长出几条触手的奇怪生物蹦了出来。\n\n它惊恐地看了你一眼，随后头也不回地消失在黑暗之中。" : "You strike at the hole with all your might. A strange hunched creature with several tentacles leaps out.\n\nIt gives you a terrified glance, then vanishes into the dark without looking back.",
            ["THIS_OR_THAT.pages.AM_REVEAL_FACE.description"] = zh ? "你用尽全力攻向坑洞，一个驼着背、背后长出几条触手的奇怪生物蹦了出来。\n\n它可怜巴巴地拖着脚向后逃去，但你注意到它怀里掉出来一个物件。\n\n当你发现这个物件并不友善时，为时已晚。" : "You strike at the hole with all your might. A strange hunched creature with several tentacles leaps out.\n\nIt shuffles away pitifully, dropping something from its arms.\n\nBy the time you realize the object is unfriendly, it is too late.",
            ["THIS_OR_THAT.pages.AM_REVEAL_GIFT.description"] = zh ? "你用尽全力攻向坑洞，一个驼着背、背后长出几条触手的奇怪生物蹦了出来。\n\n它可怜巴巴地拖着脚向后逃去，但你注意到它怀里掉出来一个物件。\n\n是一个平平无奇的礼物盒。" : "You strike at the hole with all your might. A strange hunched creature with several tentacles leaps out.\n\nIt shuffles away pitifully, dropping something from its arms.\n\nIt is an ordinary-looking gift box.",
        });
        else if (name == "relics") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_NLOTHS_HUNGRY_FACE.title"] = zh ? "恩洛斯的饥饿的脸" : "N'loth's Hungry Face",
            ["ALL_MATTERS_NLOTHS_HUNGRY_FACE.description"] = zh ? "你打开的下一个非Boss宝箱将是空的。" : "The next non-Boss chest you open will be empty for you.",
            ["ALL_MATTERS_NLOTHS_GIFT.title"] = zh ? "恩洛斯的礼物" : "N'loth's Gift",
            ["ALL_MATTERS_NLOTHS_GIFT.description"] = zh ? "使你在怪物奖励中遇见[gold]稀有[/gold]牌的几率变为[green]3[/green]倍。" : "Triple your chance to find [gold]Rare[/gold] cards in monster rewards.",
        });
    }
}
