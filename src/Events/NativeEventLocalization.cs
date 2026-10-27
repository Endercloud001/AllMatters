using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeEventLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events")
        {
            __result.MergeWith(new Dictionary<string, string>
            {
                ["ALL_MATTERS_NATIVE_EMPTY"] = "",
                ["TABLET_OF_TRUTH.pages.DECIPHER_5.description"] = zh
                    ? "你将自己的一切耗尽，试图解读出最后的词语，然后你失去了意识……\n当你苏醒过来时，你发现地板上刻了一个词。\n“[i]Designant.[/i]”"
                    : "You exhaust everything trying to decipher the final words, then lose consciousness…\nWhen you awaken, a word is carved into the floor.\n“[i]Designant.[/i]”",
                ["PUNCH_OFF.pages.INITIAL.options.AM_LEAVE.title"] = zh ? "快跑！" : "Run!",
                ["PUNCH_OFF.pages.INITIAL.options.AM_LEAVE.description"] = zh ? "离开此事件。" : "Leave.",
                ["CRYSTAL_SPHERE.pages.INITIAL.options.AM_LEAVE.title"] = zh ? "活在当下" : "Live in the Present",
                ["CRYSTAL_SPHERE.pages.INITIAL.options.AM_LEAVE.description"] = zh ? "扭头就走，不去看所谓的未来。" : "Turn away from the supposed future.",
                ["INFESTED_AUTOMATON.pages.INITIAL.options.AM_LEAVE.title"] = zh ? "离开" : "Leave",
                ["INFESTED_AUTOMATON.pages.INITIAL.options.AM_LEAVE.description"] = zh ? "对来路不明的构装体还是应该谨慎。" : "Be wary of constructs of unknown origin.",
                ["WELCOME_TO_WONGOS.pages.INITIAL.options.LEAVE.description"] = zh ? "失去[red]8[/red]点生命值。" : "Lose [red]8[/red] HP.",
                ["WELCOME_TO_WONGOS.pages.AM_LEAVE.description"] = zh ? "你付出了生命的代价，离开了百货。" : "You leave the store at a cost to your health.",
                ["BUGSLAYER.pages.INITIAL.options.AM_REST.title"] = zh ? "学习休息的技巧" : "Learn to Rest",
                ["BUGSLAYER.pages.INITIAL.options.AM_REST.description"] = zh ? "回复[green]{AmHeal}[/green]点生命。" : "Heal [green]{AmHeal}[/green] HP.",
                ["BUGSLAYER.pages.AM_REST.description"] = zh ? "“原来如此……你想学习战斗之外的技巧。很好…”\n\n他短暂地停顿了一下，思考着能传授给你什么…\n\n“休息也是很重要的一部分。”他铺开一张草席就这么躺下了。" : "“I see… You wish to learn a skill beyond battle. Very well…”\n\nHe pauses, pondering what he could teach you.\n\n“Rest is important, too.” He spreads out a straw mat and lies down.",
                ["COLORFUL_PHILOSOPHERS.pages.INITIAL.options.AM_GEM.title"] = zh ? "棱彩" : "Prismatic",
                ["COLORFUL_PHILOSOPHERS.pages.INITIAL.options.AM_GEM.description"] = zh ? "获得[gold]棱彩宝石[/gold]。" : "Obtain [gold]Prismatic Gem[/gold].",
                ["REFLECTIONS.pages.INITIAL.options.AM_MOVE.title"] = zh ? "挪动镜子" : "Move the Mirror",
                ["REFLECTIONS.pages.INITIAL.options.AM_MOVE.description"] = zh ? "失去[red]10[/red]点生命，有[gold]50%[/gold]的概率获得[gold]多利之镜[/gold]。" : "Lose [red]10[/red] HP. [gold]50%[/gold] chance to obtain [gold]Dolly's Mirror[/gold].",
                ["REFLECTIONS.pages.AM_MOVE.description"] = zh ? "从我的镜中我谛视了自己的[purple]空虚[/purple]，\n我不断地看到那模糊的[purple]恐惧[/purple]：[jitter]这便是我[/jitter]。\n只有到镜子中我才能找到我的真面目，\n在镜中我不断地[blue]审视[/blue]着自我。" : "In my mirror I gaze upon my [purple]emptiness[/purple],\nAgain I see that vague [purple]fear[/purple]: [jitter]this is me[/jitter].\nOnly in the mirror can I find my true face,\nThere I endlessly [blue]examine[/blue] myself.",
                ["SPIRALING_WHIRLPOOL.pages.INITIAL.options.AM_REACH.title"] = zh ? "探入" : "Reach In",
                ["SPIRALING_WHIRLPOOL.pages.INITIAL.options.AM_REACH.description"] = zh ? "获得一件随机遗物。将一张[red]苦恼[/red]加入你的牌组。" : "Obtain a random Relic. Add [red]Writhe[/red] to your Deck.",
                ["SPIRALING_WHIRLPOOL.pages.AM_REACH.description"] = zh ? "你愚蠢地将手臂探入螺旋漩涡的中心，打破了这里的平衡。\n泉水瞬间变得冰冷刺骨并将你死死攫住，在你手臂上凿刻出无形涡旋。\n指尖触碰到了某种硬物，你在剧痛中一阵摸索，总算从中拽出了一件遗物！" : "You foolishly reach into the center of the whirlpool, disturbing its balance.\nThe water turns bitterly cold and grips you, carving invisible spirals into your arm.\nYour fingertips touch something solid. Through the pain, you finally pull out a relic!",
                ["THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.AM_EXIT.title"] = zh ? "耐心寻找出口" : "Patiently Find an Exit",
                ["THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.AM_EXIT.description"] = zh ? "失去[red]12[/red]点生命。获得1个随机遗物。" : "Lose [red]12[/red] HP. Obtain a random Relic.",
                ["SUNKEN_TREASURY.pages.INITIAL.options.SECOND_CHEST.description"] = zh ? "获得[gold]{LargeChestGold}[/gold]金币。得到[red]{AmBranch}[/red]张[red]贪婪[/red]。" : "Gain [gold]{LargeChestGold}[/gold] Gold. Add [red]{AmBranch} Greed[/red] to your Deck.",
                ["WOOD_CARVINGS.pages.INITIAL.options.AM_OIL.title"] = zh ? "蛇" : "Snake",
                ["WOOD_CARVINGS.pages.INITIAL.options.AM_OIL.description"] = zh ? "获得一瓶[gold]异蛇之油[/gold]。" : "Obtain [gold]Snecko Oil[/gold].",
            });
        }
        else if (name == "cards")
        {
            __result.MergeWith(new Dictionary<string, string>
            {
                ["PECK.description"] = zh ? "先去除目标至多[blue]15[/blue]点[gold]格挡[/gold]，再造成{Damage:diff()}点伤害{Repeat:diff()}次。" : "Remove up to [blue]15[/blue] [gold]Block[/gold] from the target, then deal {Damage:diff()} damage {Repeat:diff()} times.",
                ["TORIC_TOUGHNESS.description"] = zh ? "获得{Block:diff()}点[gold]格挡[/gold]。下回合获得[blue]7[/blue]点[gold]格挡[/gold]。" : "Gain {Block:diff()} [gold]Block[/gold]. Next turn gain [blue]7[/blue] [gold]Block[/gold].",
            });
        }
        else if (name == "relics")
        {
            __result.MergeWith(new Dictionary<string, string>
            {
                ["BIG_MUSHROOM.description"] = zh ? "拾起时，将你的最大生命值提升[green]{MaxHp}[/green]。在每场战斗开始时，少抽[red]{Cards}[/red]张牌。" : "Upon pickup, gain [green]{MaxHp}[/green] Max HP. Draw [red]{Cards}[/red] fewer cards at the start of each combat.",
                ["FRAGRANT_MUSHROOM.description"] = zh ? "拾起时，失去[red]{HpLoss}[/red]点生命，然后选择至多[green]{Cards}[/green]张牌[gold]升级[/gold]。" : "Upon pickup, lose [red]{HpLoss}[/red] HP, then choose up to [green]{Cards}[/green] cards to [gold]Upgrade[/gold].",
                ["FRAGRANT_MUSHROOM.eventDescription"] = zh ? "获得[gold]芳香蘑菇[/gold]。拾起时，失去[red]{HpLoss}[/red]点生命，然后选择至多[green]{Cards}[/green]张牌[gold]升级[/gold]。" : "Obtain [gold]Fragrant Mushroom[/gold]. Upon pickup, lose [red]{HpLoss}[/red] HP, then choose up to [green]{Cards}[/green] cards to [gold]Upgrade[/gold].",
                ["BIG_MUSHROOM.eventDescription"] = zh ? "获得[gold]大蘑菇[/gold]。拾起时，最大生命值提升[green]{MaxHp}[/green]。每场战斗的初始手牌少抽[red]{Cards}[/red]张牌。" : "Obtain [gold]Big Mushroom[/gold]. Upon pickup, gain [green]{MaxHp}[/green] Max HP. Draw [red]{Cards}[/red] fewer cards in your opening hand each combat.",
            });
        }
    }
}
