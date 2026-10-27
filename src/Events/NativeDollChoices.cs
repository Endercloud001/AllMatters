using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

internal static class NativeDollChoices
{
    // Reuse the three native choices, including their narrative and audio cleanup.
    private static List<EventOption> Choices(DollRoom model)
    {
        var originals = (Array)AccessTools.Field(typeof(DollRoom), "_dolls").GetValue(null)!;
        var choices = originals.Cast<object>().Select(choice => (EventOption)AccessTools.Method(typeof(DollRoom), "OptionFromChoice").Invoke(model, [choice])!).ToList();
        choices.Add(NativeEventActions.Option(model, "DOLL_ROOM.pages.INITIAL.options.AM_DOLL", async () =>
        {
            await RelicCmd.Obtain<DuVuDoll>(model.Owner!);
            NativeEventActions.Finish(model, "DOLL_ROOM.pages.AM_DOLL.description");
        }, HoverTipFactory.FromRelic<DuVuDoll>().ToArray()));
        return choices;
    }
    internal static async Task Pick(DollRoom model, int count, int hp, string page)
    {
        if (hp > 0) await NativeEventActions.LoseHp(model, hp);
        if (model.Owner!.Creature.IsDead) return;
        var all = Choices(model);
        var choices = Enumerable.Range(0, all.Count).ToList().StableShuffle(model.Rng).Take(count).Select(i => all[i]).ToArray();
        if (count == 1) await choices[0].Chosen();
        else NativeEventActions.State(model, "DOLL_ROOM.pages." + page + ".description", choices);
    }
}

[HarmonyPatch(typeof(DollRoom), "ChooseRandom")]
internal static class NativeDollRandom
{
    private static bool Prefix(DollRoom __instance, ref Task __result)
    {
        __result = NativeDollChoices.Pick(__instance, 1, 0, "");
        return false;
    }
}
[HarmonyPatch(typeof(DollRoom), "TakeSomeTime")]
internal static class NativeDollTwo
{
    private static bool Prefix(DollRoom __instance, ref Task __result)
    {
        __result = NativeDollChoices.Pick(__instance, 2, __instance.DynamicVars["TakeTimeHpLoss"].IntValue, "TAKE_SOME_TIME");
        return false;
    }
}
[HarmonyPatch(typeof(DollRoom), "Examine")]
internal static class NativeDollAll
{
    private static bool Prefix(DollRoom __instance, ref Task __result)
    {
        __result = NativeDollChoices.Pick(__instance, 4, __instance.DynamicVars["ExamineHpLoss"].IntValue, "EXAMINE");
        return false;
    }
}
