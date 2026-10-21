// SPDX-License-Identifier: GPL-3.0-only
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Encounters;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class EventCombatRewards
{
    private static bool Prefix(RewardsSet __instance, AbstractRoom room, ref RewardsSet __result)
    {
        if (room is CombatRoom { Encounter: ColosseumFirstEncounter })
        {
            __result = __instance.EmptyForRoom(room);
            return false;
        }
        if (room is not CombatRoom combat ||
            (combat.Encounter is not MindBloomEncounter && !AllMatters.Events.Colosseum.IsSecondCombat(combat))) return true;
        var player = __instance.Player;
        var options = CardCreationOptions.ForRoom(player, RoomType.Boss);
#if !STS2_PUBLIC
        options = options.WithFlags(CardCreationFlags.IsFromCombat);
#endif
        // Generate at the native reward stage instead of putting a flagged CardReward
        // in ExtraRewards: the latter cannot be serialized by either game version.
        // Avoid rolling discarded default drops; GenerateWithoutOffering/Offer still
        // owns native population, RNG, modification hooks and selection synchronization.
        __result = __instance.EmptyForRoom(room).WithCustomRewards(
        [
            new GoldReward(100, 100, player),
            new RelicReward(RelicRarity.Rare, player),
            new CardReward(options, 3, player),
        ]);
        return false;
    }
}
