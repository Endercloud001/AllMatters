// SPDX-License-Identifier: GPL-3.0-only
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

#if !STS2_PUBLIC
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.EventEncounters), MethodType.Getter)]
internal static class BanditsBestiary
{
    private static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        var encounter = ModelDb.Encounter<RedMaskBandits>();
        if (!__result.Any(model => model.Id == encounter.Id))
            __result = __result.Append(encounter);
    }
}
#endif

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class BanditsGoldReward
{
    private static void Postfix(RewardsSet __instance, AbstractRoom room)
    {
        if (room is not CombatRoom { Encounter: RedMaskBandits } combat)
            return;
        // Replace only the normal gold reward. Keep inherited cards and potion
        // odds, and leave later native reward-modification hooks intact.
        combat.ExtraRewards.TryGetValue(__instance.Player, out var explicitRewards);
        __instance.Rewards.RemoveAll(reward => reward is GoldReward &&
            (explicitRewards == null || !explicitRewards.Contains(reward)));
    }
}
