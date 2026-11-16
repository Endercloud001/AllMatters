#if !STS2_PUBLIC
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch]
internal static class NativeMuseumOfTorture
{
    private const string Pages = "MUSEUM_OF_TORTURE.pages.";
    private const string TakeOption = Pages + "DISPLAY.options.TAKE";
    private const string EscapeOption = Pages + "DISPLAY.options.BREAK_OUT";
    private const int EscapeHpLoss = 10;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EventModel), "SetEventState", [typeof(LocString), typeof(IEnumerable<EventOption>)])]
    private static void AddCursePreview(EventModel __instance, IEnumerable<EventOption> eventOptions)
    {
        if (__instance is not MuseumOfTorture)
            return;

        var take = eventOptions.FirstOrDefault(option => option.TextKey == TakeOption);
        if (take != null)
            take.HoverTips = take.HoverTips.Concat(HoverTipFactory.FromCardWithCardHoverTips<Guilty>());
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MuseumOfTorture), "TakeRelic")]
    private static bool TakeRelicPrefix(MuseumOfTorture __instance, RelicModel ____offeredRelic, ref Task __result)
    {
        __result = TakeRelic(__instance, ____offeredRelic);
        return false;
    }

    private static async Task TakeRelic(MuseumOfTorture museum, RelicModel offeredRelic)
    {
        await RelicCmd.Obtain(offeredRelic.ToMutable(), museum.Owner!);
        await CardPileCmd.AddCurseToDeck<Guilty>(museum.Owner!);
        NativeEventActions.Finish(museum, Pages + "TAKE.description");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MuseumOfTorture), "BreakOut")]
    private static bool BreakOutPrefix(MuseumOfTorture __instance, ref Task __result)
    {
        __result = BreakOut(__instance);
        return false;
    }

    private static async Task BreakOut(MuseumOfTorture museum)
    {
        await NativeEventActions.LoseHp(museum, EscapeHpLoss);
        NativeEventActions.Finish(museum, Pages + "BREAK_OUT.description");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
    private static void LocalizeOptions(LocManager __instance, string name, LocTable __result)
    {
        if (name != "events")
            return;

        bool zh = __instance.Language == "zhs";
        __result.MergeWith(new Dictionary<string, string>
        {
            [TakeOption + ".description"] = zh
                ? "获得[gold]{Relic}[/gold]。将一张[red]愧疚[/red]加入你的牌组。"
                : "Obtain [gold]{Relic}[/gold]. Add [red]Guilty[/red] to your Deck.",
            [EscapeOption + ".description"] = zh
                ? $"失去[red]{EscapeHpLoss}[/red]点生命。"
                : $"Lose [red]{EscapeHpLoss}[/red] HP."
        });
    }
}
#endif
