using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(RelicTrader), "GetValidRelics")]
internal static class NativeTraderEligibility
{
    internal static bool Eligible(RelicModel relic) => !relic.IsUsedUp && !relic.IsMelted && !relic.SpawnsPets
        && relic.Rarity != RelicRarity.Starter && (relic.IsWax || relic.Rarity is not (RelicRarity.Event or RelicRarity.Ancient));
    internal static RelicRarity TradeRarity(RelicModel relic) => relic.IsWax ? RelicRarity.Common : relic.Rarity;
    private static bool Prefix(Player player, ref IEnumerable<RelicModel> __result)
    {
        __result = player.Relics.Where(Eligible);
        return false;
    }
}

[HarmonyPatch(typeof(RelicTrader), "get_OwnedRelics")]
internal static class NativeTraderPriority
{
    private static bool Prefix(RelicTrader __instance, ref IReadOnlyList<RelicModel>? ____ownedRelics, ref IReadOnlyList<RelicModel> __result)
    {
        ____ownedRelics ??= __instance.Owner!.Relics.Where(NativeTraderEligibility.Eligible).ToList().StableShuffle(__instance.Rng)
            .OrderBy(NativeTraderEligibility.TradeRarity).Take(3).ToList();
        __result = ____ownedRelics;
        return false;
    }
}
