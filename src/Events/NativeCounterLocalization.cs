using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeCounterLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_0.title"] = zh ? "无礼之茶" : "Tea of Discourtesy",
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_1.title"] = zh ? "无礼之茶" : "Tea of Discourtesy",
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_2.title"] = zh ? "无礼之茶" : "Tea of Discourtesy",
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_0.description"] = zh ? "免费。在下一场战斗开始时，将[blue]2[/blue]张[gold]晕眩[/gold]加入抽牌堆。" : "Free. At the start of your next combat, add [blue]2[/blue] [gold]Dazed[/gold] to your Draw Pile.",
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_1.description"] = zh ? "免费。在下一场战斗开始时，将[blue]2[/blue]张[gold]受伤[/gold]加入抽牌堆。" : "Free. At the start of your next combat, add [blue]2[/blue] [gold]Wounds[/gold] to your Draw Pile.",
            ["TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_2.description"] = zh ? "免费。在下一场战斗开始时，获得[blue]1[/blue]层[gold]混乱[/gold]。" : "Free. At the start of your next combat, gain [blue]1[/blue] [gold]Confused[/gold].",
            ["WATERLOGGED_SCRIPTORIUM.pages.INITIAL.options.AM_INK.title"] = zh ? "血液墨水" : "Bloody Ink",
            ["WATERLOGGED_SCRIPTORIUM.pages.INITIAL.options.AM_INK.description"] = zh ? "获得[gold]墨水瓶[/gold]。" : "Obtain an [gold]Ink Bottle[/gold].",
            ["WATERLOGGED_SCRIPTORIUM.pages.AM_INK.description"] = __result.GetRawText("WATERLOGGED_SCRIPTORIUM.pages.BLOODY_INK.description")
                .Replace("[red]bright red ink[/red]", "[blue]bright blue ink[/blue]")
                .Replace("[red]亮红色墨水[/red]", "[blue]亮蓝色墨水[/blue]"),
            ["SYMBIOTE.pages.INITIAL.options.AM_ACCEPT.title"] = zh ? "接纳" : "Accept",
            ["SYMBIOTE.pages.INITIAL.options.AM_ACCEPT.description"] = zh ? "获得[gold]共生病毒[/gold]。" : "Obtain [gold]Symbiotic Virus[/gold].",
            ["SYMBIOTE.pages.AM_ACCEPT.description"] = zh ? "你将身体的一部分探入肉块之中，随后认知与形体被一并重塑。\n\n[i][jitter]让咱们走吧，就你还有我\n\n当停止的荒疫正将尖塔吞没\n\n宛如人形溶烂得像外质摆上构筑桌[/jitter][/i]" : "You reach part of your body into the flesh. Mind and form are reshaped together.\n\n[i][jitter]Let us go, just you and I\n\nAs the blight that stopped engulfs the Spire\n\nA human form melts like ectoplasm upon the workbench[/jitter][/i]",
        });
        else if (name == "relics") __result.MergeWith(new Dictionary<string, string>
        {
            ["BONE_TEA.description"] = zh ? "在接下来的[blue]{Combats}[/blue]场战斗中，升级你的初始手牌。" : "Upgrade your opening hand in your next [blue]{Combats}[/blue] combats.",
            ["EMBER_TEA.description"] = zh ? "在接下来的[blue]{Combats}[/blue]场战斗开始时，获得[blue]1[/blue]点[gold]力量[/gold]和[blue]1[/blue]点[gold]敏捷[/gold]。" : "At the start of your next [blue]{Combats}[/blue] combats, gain [blue]1[/blue] [gold]Strength[/gold] and [blue]1[/blue] [gold]Dexterity[/gold].",
            ["TEA_OF_DISCOURTESY.description"] = zh ? "下一场战斗开始时，加入2张晕眩、2张受伤或获得1层混乱（购买时确定）。" : "At the start of your next combat, add 2 Dazed, 2 Wounds, or gain 1 Confused, as shown when chosen.",
            ["ALL_MATTERS_INK_BOTTLE.title"] = zh ? "墨水瓶" : "Ink Bottle",
            ["ALL_MATTERS_INK_BOTTLE.description"] = zh ? "每打出[blue]10[/blue]张牌，抽[blue]1[/blue]张牌。" : "Every [blue]10[/blue] cards you play, draw [blue]1[/blue] card.",
            ["SYMBIOTIC_VIRUS.description"] = zh ? "在你的回合开始时，额外抽[blue]1[/blue]张牌。每打出[blue]5[/blue]张非攻击牌，将[blue]1[/blue]张[gold]晕眩[/gold]加入抽牌堆。" : "At the start of your turn, draw [blue]1[/blue] additional card. Every [blue]5[/blue] non-Attack cards you play, add [blue]1[/blue] [gold]Dazed[/gold] to your Draw Pile.",
            ["HISTORY_COURSE.description"] = zh ? "在你的回合开始时，打出上一回合最后打出的[gold]攻击牌[/gold]或[gold]技能牌[/gold]的复制品。" : "At the start of your turn, play a copy of the last [gold]Attack[/gold] or [gold]Skill[/gold] you played last turn.",
        });
    }
}
