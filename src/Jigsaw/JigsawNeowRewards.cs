using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Events;
namespace AllMatters.Jigsaw;

// Exclude the candidate before native selection, rather than rerolling generated options.
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsAllowedAtNeow))]
internal static class JigsawNeowRewards
{
    private static void Postfix(RelicModel __instance, Player player, ref bool __result)
    {
        if (__instance is WingedBoots or GoldenCompass && JigsawNativeSession.Current is { } session && ReferenceEquals(player.RunState, session.Native))
            __result = false;
    }
}

// Tezcatara builds this pool directly, without consulting IsAllowedAtNeow.
// Each getter call returns a fresh list; filtering precedes the native draw.
[HarmonyPatch(typeof(Tezcatara), "get_OptionPool3")]
internal static class JigsawTezcataraRewards
{
    private static void Postfix(Tezcatara __instance, List<EventOption> __result)
    {
        if (JigsawNativeSession.Current is { } session && ReferenceEquals(__instance.Owner?.RunState, session.Native))
            __result.RemoveAll(option => option.Relic is GoldenCompass);
    }
}
