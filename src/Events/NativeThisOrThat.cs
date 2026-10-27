using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.HoverTips;

namespace AllMatters.Events;

[HarmonyPatch(typeof(ThisOrThat), nameof(ThisOrThat.CalculateVars))]
internal static class ThisOrThatAct
{
    private static void Postfix(ThisOrThat __instance)
    {
        int act = __instance.Owner!.RunState.CurrentActIndex + 1;
        __instance.DynamicVars.HpLoss.BaseValue = 6 * (4 - act);
        __instance.DynamicVars.Gold.BaseValue *= act;
    }
}

[HarmonyPatch(typeof(ThisOrThat), "GenerateInitialOptions")]
internal static class ThisOrThatIdentity
{
    private static void Postfix(ThisOrThat __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result = __result.Append(NativeEventActions.Option(__instance, "THIS_OR_THAT.pages.INITIAL.options.AM_REVEAL", async () =>
        {
            int roll = __instance.Rng.NextInt(100);
            if (roll is >= 78 and < 98) await RelicCmd.Obtain<NlothsHungryFace>(__instance.Owner!);
            else if (roll >= 98) await RelicCmd.Obtain<NlothsGift>(__instance.Owner!);
            NativeEventActions.Finish(__instance, "THIS_OR_THAT.pages.AM_REVEAL_" + (roll < 78 ? "NONE" : roll < 98 ? "FACE" : "GIFT") + ".description");
        }, HoverTipFactory.FromRelic<NlothsHungryFace>().Concat(HoverTipFactory.FromRelic<NlothsGift>()).ToArray())).ToArray();
    }
}
