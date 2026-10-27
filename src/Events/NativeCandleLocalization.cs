using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeCandleLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["ROOM_FULL_OF_CHEESE.pages.INITIAL.options.GORGE.description"] = zh ? "从[gold]8[/gold]张随机[gold]普通[/gold]或[gold]罕见[/gold]牌中选择[gold]2[/gold]张加入到你的牌组。" : "Choose [gold]2[/gold] of [gold]8[/gold] random [gold]Common[/gold] or [gold]Uncommon[/gold] cards to add to your Deck.",
            ["ROOM_FULL_OF_CHEESE.pages.INITIAL.options.AM_CANDLE.title"] = zh ? "取走蜡烛" : "Take the Candle",
            ["ROOM_FULL_OF_CHEESE.pages.INITIAL.options.AM_CANDLE.description"] = zh ? "失去[red]14[/red]点最大生命值，获得[gold]蓝蜡烛[/gold]。" : "Lose [red]14[/red] Max HP. Obtain [gold]Blue Candle[/gold].",
            ["ROOM_FULL_OF_CHEESE.pages.AM_CANDLE.description"] = zh ? "你环视了一圈，最后把视线锁定在了一根特别的蜡烛上。\n\n当你用手拿起它时，灼热的火舌吞噬了你的一部分生命。\n\n但你同时也注意到——缠绕在你身上的诅咒松动了。" : "You look around and finally focus on one unusual candle.\n\nAs you pick it up, its scorching flame consumes part of your life.\n\nAt the same time, you feel the curses around you loosen.",
        });
        else if (name == "relics") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_BLUE_CANDLE.title"] = zh ? "蓝蜡烛" : "Blue Candle",
            ["ALL_MATTERS_BLUE_CANDLE.description"] = zh ? "战斗中可以打出原本不能被打出的[gold]诅咒[/gold]牌（默认为[blue]1[/blue]费；原本有费用的诅咒保留原费）。打出诅咒牌会让你失去[red]2[/red]点生命并将其[gold]消耗[/gold]。" : "You can play otherwise [gold]Unplayable Curse[/gold] cards in combat, for [blue]1[/blue] Energy by default; curses with existing costs keep them. Playing a Curse makes you lose [red]2[/red] HP and [gold]Exhausts[/gold] it.",
        });
    }
}
