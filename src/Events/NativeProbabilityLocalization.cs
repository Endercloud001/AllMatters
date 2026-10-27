using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeProbabilityLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name != "events") return;
        bool zh = __instance.Language == "zhs";
        __result.MergeWith(new Dictionary<string, string>
        {
            ["SLIPPERY_BRIDGE.pages.INITIAL.options.AM_RETURN.title"] = zh ? "返程" : "Turn Back",
            ["SLIPPERY_BRIDGE.pages.INITIAL.options.AM_RETURN.description"] = zh ? "你认为恶劣天气下无法再前进，必须折返。" : "You decide the weather is too severe to continue and turn back.",
            ["SLIPPERY_BRIDGE.pages.AM_RETURN.description"] = zh ? "你认为恶劣天气下无法再前进，必须折返。" : "The weather makes further progress impossible. You turn back.",
            ["SLIPPERY_BRIDGE.pages.INITIAL.options.AM_FORWARD.title"] = zh ? "再撑一会" : "Hold On",
            ["SLIPPERY_BRIDGE.pages.INITIAL.options.AM_FORWARD.description"] = zh ? "坚持向前，有概率随机移除牌组中的一张牌。" : "Keep going, with a chance to remove a random card from your Deck.",
            ["SLIPPERY_BRIDGE.pages.AM_CLEAR.description"] = zh ? "在你的坚持下，骤雨与暴风收敛了锋芒，你顺利通过了木桥。" : "Your persistence pays off. The storm relents, and you cross the bridge safely.",
            ["SLIPPERY_BRIDGE.pages.AM_FALL.description"] = zh ? "桥从中间断裂了，你坠入了深谷。" : "The bridge breaks in the middle. You fall into the ravine.",
            ["ALL_MATTERS_SELF_HELP_READING"] = zh ? "选择一张[gold]{Type}[/gold]牌附魔：[gold]{Enchantment} {Amount}[/gold]。" : "Enchant an [gold]{Type}[/gold] card with [gold]{Enchantment} {Amount}[/gold].",
            ["SELF_HELP_BOOK.pages.READ_PASSAGE.description"] = zh ? "你随便翻到一页：\n\n“输了可不好玩，所以我从来都不会输。”\n\n你读完了这一段，意外地相当给你启发。你决定在作战时用上其中的一些要点。" : "You turn to a random page:\n\n“Losing is no fun, so I never lose.”\n\nUnexpectedly inspired, you decide to put some of these ideas to use in battle.",
            ["SELF_HELP_BOOK.pages.READ_ENTIRE_BOOK.description"] = zh ? "真是本引人入胜的书啊！有几句良言至今深刻在你的脑海之中：\n“获得15点格挡。”\n“跨回合收益。”\n“谁强就削弱谁，所以越强越弱。”\n你的手上沾满了血。" : "What a compelling book! A few wise words remain engraved in your mind:\n“Gain 15 Block.”\n“Benefits across turns.”\n“The stronger they are, the harder they are weakened.”\nYour hands are covered in blood.",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_CARDS.title"] = zh ? "放入一瓶药水" : "Insert a Potion",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_CARDS.description"] = zh ? "失去[red]{Potion}[/red]。从3张[gold]升级过的{Rarity}{Type}[/gold]牌中选择1张。" : "Lose [red]{Potion}[/red]. Choose 1 of 3 [gold]Upgraded {Rarity} {Type}[/gold] cards.",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_RELIC.title"] = zh ? "放入一瓶药水" : "Insert a Potion",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_RELIC.description"] = zh ? "失去[red]{Potion}[/red]。获得1个随机[gold]{Rarity}[/gold]遗物。" : "Lose [red]{Potion}[/red]. Obtain a random [gold]{Rarity}[/gold] Relic.",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_REFRESH.title"] = zh ? "放入一些金币" : "Insert Gold",
            ["THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_REFRESH.description"] = zh ? "支付[red]50[/red]金币，刷新以上选项。" : "Pay [red]50[/red] Gold to refresh the offers.",
            ["BRAIN_LEECH.pages.INITIAL.options.AM_STIMULATE.title"] = zh ? "刺激它" : "Stimulate It",
            ["BRAIN_LEECH.pages.INITIAL.options.AM_STIMULATE.description"] = zh ? "失去[red]{Potion}[/red]，可能获得最大生命值或遗物奖励。" : "Lose [red]{Potion}[/red]. You may gain Max HP or a Relic.",
            ["BRAIN_LEECH.pages.AM_STIMULATE_NONE.description"] = zh ? "你用药水往头上砸去，在强烈地刺激下，脑蛭一下子弹得远远的。" : "You smash the potion against your head. The intense stimulus sends the brain leech flying away.",
            ["BRAIN_LEECH.pages.AM_STIMULATE_HP.description"] = zh ? "你用药水往头上砸去，在强烈地刺激下，有关于生机的知识进入你的脑海中。" : "You smash the potion against your head. The intense stimulus fills your mind with knowledge of vitality.",
            ["BRAIN_LEECH.pages.AM_STIMULATE_RELIC.description"] = zh ? "你用药水往头上砸去，在强烈地刺激下，有关于方位的知识进入你的脑海中。\n\n待脑蛭离去后，你顺着这段知识寻找，果然找到了一件物品。" : "You smash the potion against your head. Knowledge of a location fills your mind.\n\nAfter the leech leaves, you follow that knowledge and discover an item.",
            ["SAPPHIRE_SEED.pages.INITIAL.options.AM_CATALYZE.title"] = zh ? "用药剂催化" : "Catalyze with a Potion",
            ["SAPPHIRE_SEED.pages.INITIAL.options.AM_CATALYZE.description"] = zh ? "失去[red]{Potion}[/red]，选择[gold]2[/gold]张牌附魔：[green]播种[/green]。" : "Lose [red]{Potion}[/red]. Enchant [gold]2[/gold] cards with [green]Sown[/green].",
            ["SAPPHIRE_SEED.pages.AM_CATALYZE.description"] = zh ? "你在[green]密林[/green]一番寻觅，总算找到了一个[blue]通风良好[/blue]的花盆。然后你四处跋涉，寻找一个无人侵扰的地点，能让阳光从墙上的裂缝中倾泻下来。你小心翼翼地用树皮和藤蔓将花盆加固，为它提供一些[green]迷彩遮掩[/green]，然后往里面放入了一些肥沃营养的土壤，接着把[blue]种子[/blue]埋了进去。\n\n最后，你从怀中拿出一瓶药水洒在了土上。\n\n[jitter]小种子，快长大[/jitter]…\n\n在你的不懈努力下，[green]两株幼苗[/green]从土中萌发出来。\n\n感谢你的辛勤努力。" : "Searching the [green]jungle[/green], you find a [blue]well-ventilated[/blue] pot and a quiet place where sunlight enters through cracks in the wall. You reinforce the pot with bark and vines for [green]camouflage[/green], add rich soil and bury the [blue]seed[/blue].\n\nFinally, you pour a potion onto the soil.\n\n[jitter]Little seed, grow[/jitter]…\n\nYour efforts are rewarded as [green]two seedlings[/green] emerge.\n\nThank you for your hard work.",
        });
    }
}
