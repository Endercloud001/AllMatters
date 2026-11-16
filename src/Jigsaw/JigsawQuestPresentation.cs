using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(RelicModel), "get_Description")]
internal static class JigsawFurDescription
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not FurCoat)
            return;
        bool effective = __instance.IsMutable && __instance.Owner?.RunState is { } run
            ? JigsawNewRunMode.IsEffective(run)
            : JigsawNativeSession.Current is { } session && JigsawNewRunMode.IsEffective(session.Native);
        if (effective)
            __result = new LocString("relics", "AM_JIGSAW_FUR_COAT.description");
    }
}
