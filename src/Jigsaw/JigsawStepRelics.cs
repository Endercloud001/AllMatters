using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Jigsaw;

internal static class JigsawStepRelics
{
    internal static bool Applies(RelicModel relic) => relic is WingedBoots or GoldenCompass &&
        (relic.IsMutable && relic.Owner?.RunState is { } run ? JigsawNewRunMode.IsEffective(run) :
            JigsawNativeSession.Current is { } session && JigsawNewRunMode.IsEffective(session.Native));

    internal static bool Obtain(RelicModel relic, ref Task result)
    {
        if (!Applies(relic) || relic.Owner?.RunState is not RunState run)
            return true;
        (JigsawNativeSession.TryGet(run) ?? JigsawNativeSession.Enable(run)).ApplyStepRelicPickup();
        result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.AfterObtained))]
internal static class JigsawStepRelicObtained
{
    private static bool Prefix(RelicModel __instance, ref Task __result) => JigsawStepRelics.Obtain(__instance, ref __result);
}

[HarmonyPatch(typeof(GoldenCompass), nameof(GoldenCompass.AfterObtained))]
internal static class JigsawCompassObtained
{
    private static bool Prefix(GoldenCompass __instance, ref Task __result) => JigsawStepRelics.Obtain(__instance, ref __result);
}

[HarmonyPatch(typeof(WingedBoots), nameof(WingedBoots.ShouldAllowFreeTravel))]
internal static class JigsawBootsTravel
{
    private static bool Prefix(WingedBoots __instance, ref bool __result)
    { if (!JigsawStepRelics.Applies(__instance)) return true; __result = false; return false; }
}

[HarmonyPatch(typeof(WingedBoots), nameof(WingedBoots.AfterRoomEntered))]
internal static class JigsawBootsRoom
{
    private static bool Prefix(WingedBoots __instance, ref Task __result)
    { if (!JigsawStepRelics.Applies(__instance)) return true; __result = Task.CompletedTask; return false; }
}

[HarmonyPatch(typeof(WingedBoots),
#if STS2_PUBLIC
    nameof(WingedBoots.IsAllowed)
#else
    "IsAllowedInternal"
#endif
)]
internal static class JigsawBootsEligibility
{
    private static void Postfix(IRunState runState, ref bool __result)
    { if (JigsawNewRunMode.IsEffective(runState)) __result = true; }
}

[HarmonyPatch(typeof(GoldenCompass), nameof(GoldenCompass.ModifyGeneratedMap))]
internal static class JigsawCompassMap
{
    private static bool Prefix(GoldenCompass __instance, ActMap map, ref ActMap __result)
    { if (!JigsawStepRelics.Applies(__instance)) return true; __result = map; return false; }
}

[HarmonyPatch(typeof(GoldenCompass), nameof(GoldenCompass.ModifyUnknownMapPointRoomTypes))]
internal static class JigsawCompassUnknown
{
    private static bool Prefix(GoldenCompass __instance, IReadOnlySet<RoomType> roomTypes, ref IReadOnlySet<RoomType> __result)
    { if (!JigsawStepRelics.Applies(__instance)) return true; __result = roomTypes; return false; }
}

[HarmonyPatch(typeof(RelicModel), "get_DynamicDescription")]
internal static class JigsawStepRelicDescription
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    { if (JigsawStepRelics.Applies(__instance)) __result = new LocString("relics", "AM_JIGSAW_STEP_RELIC.description"); }
}

[HarmonyPatch(typeof(RelicModel), "get_DynamicEventDescription")]
internal static class JigsawStepRelicEventDescription
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    { if (JigsawStepRelics.Applies(__instance)) __result = new LocString("relics", "AM_JIGSAW_STEP_RELIC.description"); }
}


[HarmonyPatch(typeof(WingedBoots), "get_ShowCounter")]
internal static class JigsawBootsCounter
{
    private static void Postfix(WingedBoots __instance, ref bool __result)
    { if (JigsawStepRelics.Applies(__instance)) __result = false; }
}
