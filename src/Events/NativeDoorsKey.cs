using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(DoorsOfLightAndDark), "GenerateInitialOptions")]
internal static class NativeDoorsKey
{
    private static void Postfix(DoorsOfLightAndDark __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result = __result.Append(NativeEventActions.Option(__instance, "DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.AM_KEY", async () =>
        {
            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), __instance.Owner!.Creature, 5, isFromCard: false);
            if (!__instance.Owner.Creature.IsDead) await RelicCmd.Obtain<CursedKey>(__instance.Owner);
            NativeEventActions.Finish(__instance, "DOORS_OF_LIGHT_AND_DARK.pages.AM_KEY.description");
        }, HoverTipFactory.FromRelic<CursedKey>().ToArray()).ThatDecreasesMaxHp(5)).ToArray();
    }
}
