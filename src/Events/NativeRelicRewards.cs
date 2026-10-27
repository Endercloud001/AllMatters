using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LuminousChoir), "GenerateInitialOptions")]
internal static class NativeChoirMushroom
{
    private static void Postfix(LuminousChoir __instance, ref IReadOnlyList<EventOption> __result)
        => __result = __result.Append(NativeEventActions.Option(__instance, "LUMINOUS_CHOIR.pages.INITIAL.options.AM_MUSHROOM", async () =>
        {
            await NativeEventActions.LoseHp(__instance, 14);
            if (!__instance.Owner!.Creature.IsDead) await RelicCmd.Obtain<StrangeMushroom>(__instance.Owner);
            NativeEventActions.Finish(__instance, "LUMINOUS_CHOIR.pages.AM_MUSHROOM.description");
        }, HoverTipFactory.FromRelic<StrangeMushroom>().ToArray()).ThatDoesDamage(14)).ToArray();
}

[HarmonyPatch(typeof(ByrdonisNest), "GenerateInitialOptions")]
internal static class NativeNestMeat
{
    private static void Postfix(ByrdonisNest __instance, ref IReadOnlyList<EventOption> __result)
        => __result = __result.Append(NativeEventActions.Option(__instance, "BYRDONIS_NEST.pages.INITIAL.options.AM_HELP", async () =>
        {
            await NativeEventActions.LoseHp(__instance, 12);
            if (!__instance.Owner!.Creature.IsDead) await RelicCmd.Obtain<MeatOnTheBone>(__instance.Owner);
            NativeEventActions.Finish(__instance, "BYRDONIS_NEST.pages.AM_HELP.description");
        }, HoverTipFactory.FromRelic<MeatOnTheBone>().ToArray()).ThatDoesDamage(12)).ToArray();
}
