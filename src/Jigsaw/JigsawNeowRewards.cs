using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
namespace AllMatters.Jigsaw;

// Exclude the candidate before native selection, rather than rerolling generated options.
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsAllowedAtNeow))]
internal static class JigsawNeowRewards
{
    private static void Postfix(RelicModel __instance, Player player, ref bool __result)
    {
        if (__instance is WingedBoots && JigsawNativeSession.Current is { } session && ReferenceEquals(player.RunState, session.Native))
            __result = false;
    }
}
