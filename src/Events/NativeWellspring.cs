using AllMatters.Potions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models;

namespace AllMatters.Events;

[HarmonyPatch(typeof(Wellspring), "GenerateInitialOptions")]
internal static class NativeWellspring
{
    private static void Postfix(Wellspring __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        var options = __result.ToList();
        options[0] = NativeEventActions.Option(model, "WELLSPRING.pages.INITIAL.options.BOTTLE", async () =>
        {
            await PotionCmd.TryToProcure<Ambrosia>(model.Owner!);
            NativeEventActions.Finish(model, "WELLSPRING.pages.BOTTLE.description");
        }, ModelDb.Potion<Ambrosia>().HoverTips.ToArray());
        options.Add(NativeEventActions.Option(model, "WELLSPRING.pages.INITIAL.options.AM_LEAVE",
            () => { NativeEventActions.Finish(model, "WELLSPRING.pages.AM_LEAVE.description"); return Task.CompletedTask; }));
        __result = options;
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeWellspringLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["WELLSPRING.pages.INITIAL.description"] = zh ? "你跟着一阵低沉的轰鸣声找到了源头：居然是一处平静的泉水，让你心生向往。\n\n泉水的颜色摄人心魄，你还能从中看见一些发光的尘埃。\n\n看起来十分诱人，这不可能出什么差错的吧？" : "You follow a low rumble to its source: a calm spring that draws you closer.\n\nIts mesmerizing waters hold flecks of glowing dust.\n\nIt looks so inviting. What could possibly go wrong?",
            ["WELLSPRING.pages.INITIAL.options.BOTTLE.description"] = zh ? "获得[blue]1[/blue]瓶[gold]仙馔密酒[/gold]。" : "Obtain [blue]1[/blue] [gold]Ambrosia[/gold].",
            ["WELLSPRING.pages.INITIAL.options.AM_LEAVE.title"] = zh ? "离开" : "Leave",
            ["WELLSPRING.pages.INITIAL.options.AM_LEAVE.description"] = zh ? "什么都不做。" : "Do nothing.",
            ["WELLSPRING.pages.AM_LEAVE.description"] = zh ? "你什么都没有做，只是快步远离了这诡异的泉水。" : "You do nothing, quickly distancing yourself from the uncanny spring.",
        });
        else if (name == "potions") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_AMBROSIA.title"] = zh ? "仙馔密酒" : "Ambrosia",
            ["ALL_MATTERS_AMBROSIA.description"] = zh ? "本回合，你对敌人造成的攻击伤害变为[blue]3[/blue]倍。本场战斗你无法获得任何战利品。" : "This turn, your powered attack damage to enemies is tripled. You forfeit all your combat loot this combat.",
        });
        else if (name == "powers") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_POWER_AMBROSIA_POWER.title"] = zh ? "仙馔密酒" : "Ambrosia",
            ["ALL_MATTERS_POWER_AMBROSIA_POWER.description"] = zh ? "本回合对敌人的攻击伤害变为3倍。本场战斗没有个人战利品。" : "Powered attack damage to enemies is tripled this turn. No personal loot this combat.",
        });
    }
}
