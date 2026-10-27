using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeSelectionLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "cards") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_CHRYSALIS.title"] = zh ? "结茧" : "Chrysalis",
            ["ALL_MATTERS_CHRYSALIS.description"] = zh ? "在你的抽牌堆中加入{Cards:diff()}张随机技能牌。它们在本场战斗中可以被免费打出。" : "Add {Cards:diff()} random Skills into your Draw Pile. They can be played for free this combat.",
        });
        else if (name == "monsters") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_MONSTER_WRITHING_MASS.name"] = zh ? "扭曲团块" : "Writhing Mass",
            ["ALL_MATTERS_MONSTER_WRITHING_MASS.moves.PARASITE.title"] = zh ? "寄生" : "Parasite",
            ["ALL_MATTERS_MONSTER_WRITHING_MASS.moves.WITHER.title"] = zh ? "凋零" : "Wither",
            ["ALL_MATTERS_MONSTER_WRITHING_MASS.moves.FLAIL.title"] = zh ? "三连击" : "Flail",
            ["ALL_MATTERS_MONSTER_WRITHING_MASS.moves.SHIELD.title"] = zh ? "木盾" : "Wooden Shield",
        });
        else if (name == "powers") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_POWER_REACTIVE_POWER.title"] = zh ? "扭动" : "Reactive",
            ["ALL_MATTERS_POWER_REACTIVE_POWER.description"] = zh ? "首回合寄生锁定。之后每次受到未被格挡的攻击伤害时，等概率改变意图，排除上次实际行动。未受攻击时使用木盾，可连续。" : "Opening Parasite is locked. Afterwards, each unblocked attack hit rerolls intent equally, excluding the last performed action. Without attacks, uses Wooden Shield, which may repeat.",
            ["ALL_MATTERS_POWER_REACTIVE_POWER.smartDescription"] = zh ? "首回合后，每次受到未被格挡的攻击伤害时改变意图，排除上次实际行动。" : "After the opening turn, each unblocked attack hit rerolls intent, excluding the last performed action.",
        });
        else if (name == "relics") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_DEAD_BRANCH.title"] = zh ? "腐化树枝" : "Dead Branch",
            ["ALL_MATTERS_DEAD_BRANCH.description"] = zh ? "每当你[gold]消耗[/gold]一张牌，将一张随机牌加入你的手牌。" : "Whenever you [gold]Exhaust[/gold] a card, add a random card to your Hand.",
            ["ALL_MATTERS_DEAD_BRANCH.flavor"] = zh ? "枯枝中仍有某种生命在蠢动。" : "Something still stirs within the dead wood.",
            ["ALL_MATTERS_CURSED_KEY.title"] = zh ? "诅咒钥匙" : "Cursed Key",
            ["ALL_MATTERS_CURSED_KEY.description"] = zh ? "你每次打开一个非Boss宝箱都有[gold]50%[/gold]的概率获得一张随机的[red]诅咒[/red]。在每回合开始时获得[blue]1[/blue]点[gold]能量[/gold]。" : "Whenever you open a non-Boss chest, [gold]50%[/gold] chance to gain a random [red]Curse[/red]. At the start of each turn, gain [blue]1[/blue] [gold]Energy[/gold].",
        });
        else if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_BURN.title"] = zh ? "焚烧树木" : "Burn the Tree",
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_BURN.description"] = zh ? "进行一场[red]艰难的战斗[/red]。胜利后获得[purple]腐化树枝[/purple]。" : "Enter a [red]difficult battle[/red]. Obtain [purple]Dead Branch[/purple] upon victory.",
            ["WHISPERING_HOLLOW.pages.AM_BURN.description"] = zh ? "你点燃了一团火，向这棵诡异的树掷去。在一阵痛苦的哀嚎之后，它竟变成了一个[red]活物[/red]！\n\n所幸它的生命已是风中残烛，你在它的遗骸中拾到了珍贵的[gold]宝物[/gold]。" : "You light a fire and hurl it at the eerie tree. With a cry of agony, it becomes a [red]living creature[/red]!\n\nFortunately, its life is already fading. You find precious [gold]treasure[/gold] among its remains.",
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_1.title"] = zh ? "拥抱树木" : "Hug a Tree",
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_1.description"] = zh ? "失去[red]9[/red]点生命。选择[gold]1[/gold]张牌[gold]变化[/gold]。" : "Lose [red]9[/red] HP. [gold]Transform 1[/gold] card.",
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_2.title"] = zh ? "拥抱树木" : "Hug a Tree",
            ["WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_2.description"] = zh ? "失去[red]18[/red]点生命。选择[gold]2[/gold]张牌[gold]变化[/gold]。" : "Lose [red]18[/red] HP. [gold]Transform 2[/gold] cards.",
            ["MORPHIC_GROVE.pages.INITIAL.options.GROUP.description"] = zh ? "失去[red]所有金币[/red]。变化[gold]2[/gold]张牌。" : "Lose [red]all Gold[/red]. [gold]Transform 2[/gold] cards.",
            ["MORPHIC_GROVE.pages.INITIAL.options.AM_GROUP_LOCKED.title"] = zh ? "大群变形灵" : "The Group",
            ["MORPHIC_GROVE.pages.INITIAL.options.AM_GROUP_LOCKED.description"] = zh ? "需要[red]2[/red]张能够提升稀有度的合法变化目标。" : "Requires [red]2[/red] legal cards with higher-rarity transformation options.",
            ["DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.LIGHT.description"] = zh ? "[gold]升级2[/gold]张牌，优先非初始牌。" : "[gold]Upgrade 2[/gold] cards, prioritizing non-Basic cards.",
            ["DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.AM_KEY.title"] = zh ? "看门人先进门" : "Gatekeeper First",
            ["DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.AM_KEY.description"] = zh ? "失去[red]5[/red]点最大生命值，获得[gold]诅咒钥匙[/gold]。" : "Lose [red]5[/red] Max HP. Obtain [gold]Cursed Key[/gold].",
            ["DOORS_OF_LIGHT_AND_DARK.pages.AM_KEY.description"] = zh ? "你拎起那瘦弱的守门人，一把将它扔进了[purple]暗之门[/purple]中！\n\n门中传来一阵痛苦的哀嚎，随后一把钥匙被扔到了你面前。\n\n显然这东西上有强烈的[red]诅咒之力[/red]......要带着吗？" : "You lift the frail gatekeeper and throw it into the [purple]dark door[/purple]!\n\nAn agonized wail comes from within, then a key is tossed at your feet.\n\nIt clearly carries a powerful [red]curse[/red]… Take it?",
            ["ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_1.title"] = zh ? "情绪觉察" : "Emotional Awareness",
            ["ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_1.description"] = zh ? "支付[red]125[/red]金币。选择[gold]1[/gold]张牌[gold]升级[/gold]（不足时升级所有可升级牌）。" : "Pay [red]125[/red] Gold. Choose [gold]1[/gold] card to [gold]Upgrade[/gold] (all eligible cards if fewer are available).",
            ["ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_2.title"] = zh ? "蜘蛛针灸" : "Arachnid Acupuncture",
            ["ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_2.description"] = zh ? "支付[red]250[/red]金币。选择[gold]2[/gold]张牌[gold]升级[/gold]（不足时升级所有可升级牌）。" : "Pay [red]250[/red] Gold. Choose [gold]2[/gold] cards to [gold]Upgrade[/gold] (all eligible cards if fewer are available).",
            ["ZEN_WEAVER.pages.AM_UPGRADE.description"] = zh ? "你的思绪渐渐清明，熟悉的技艺也变得更加精湛。" : "Your thoughts grow clear, and familiar techniques become more refined.",
            ["SPIRIT_GRAFTER.pages.INITIAL.options.AM_CHRYSALIS.title"] = zh ? "接纳" : "Let It In",
            ["SPIRIT_GRAFTER.pages.INITIAL.options.AM_CHRYSALIS.description"] = zh ? "回复[green]25[/green]点生命。将一张[gold]结茧[/gold]加入你的牌组。" : "Heal [green]25[/green] HP. Add [gold]Chrysalis[/gold] to your Deck.",
            ["AMALGAMATOR.pages.INITIAL.options.AM_FUSE.title"] = zh ? "融合...？" : "Fuse…?",
            ["AMALGAMATOR.pages.INITIAL.options.AM_FUSE.description"] = zh ? "随机移除[red]2[/red]张卡牌。将一张[gold]升级过的稀有[/gold]卡牌加入你的牌组。" : "Remove [red]2[/red] random cards. Add an [gold]Upgraded Rare[/gold] card to your Deck.",
            ["AMALGAMATOR.pages.AM_FUSE.description"] = zh ? "[jitter]锵！锵！！！[/jitter]\n[orange]熔合者[/orange]重重地锤击着它的骨砧。\n关于战斗、灵魂与能力策略的回忆，在每一次敲击下都磨砺得愈发清晰。\n你那最究极的技艺，已重现于你的心头。" : "[jitter]Clang! CLANG!!![/jitter]\nThe [orange]Amalgamator[/orange] strikes its bone anvil.\nMemories of combat, souls and strategies sharpen with every blow.\nYour ultimate technique returns to mind.",
            ["LOST_WISP.pages.INITIAL.options.AM_BURN.title"] = zh ? "将物品丢入火中" : "Throw Something into the Fire",
            ["LOST_WISP.pages.INITIAL.options.AM_BURN.description"] = zh ? "失去最大生命值的[red]10%[/red]。选择[gold]1[/gold]张牌移除。" : "Lose [red]10%[/red] of your Max HP. Remove [gold]1[/gold] card.",
            ["LOST_WISP.pages.AM_BURN.description"] = zh ? "你将一件旧物丢入火中，溅起的火星灼伤了你的手臂。\n\n所幸，那件东西已经再也不会回来了。" : "You throw an old possession into the fire. Flying sparks burn your arm.\n\nAt least that thing will never return.",
        });
    }
}
