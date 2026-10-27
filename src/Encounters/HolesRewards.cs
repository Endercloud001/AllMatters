using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class HolesRewards
{
    private static bool Prefix(RewardsSet __instance, AbstractRoom room, ref RewardsSet __result)
    {
        if (room is not CombatRoom { Encounter: HolesEncounter } combat) return true;
        var rewards = (List<Reward>)AccessTools.Method(typeof(RewardsSet), "GenerateRewardsFor")
            .Invoke(__instance, [__instance.Player, combat])!;
        if (combat.GoldProportion > 0)
            rewards.Add(new GoldReward((int)Math.Round(combat.Encounter.MinGoldReward * combat.GoldProportion),
                (int)Math.Round(combat.Encounter.MaxGoldReward * combat.GoldProportion), __instance.Player));
        var options = CardCreationOptions.ForRoom(__instance.Player, RoomType.Monster);
#if !STS2_PUBLIC
        options = options.WithFlags(CardCreationFlags.IsFromCombat);
#endif
        rewards.Add(new CardReward(options, 3, __instance.Player));
        __result = __instance.EmptyForRoom(combat).WithCustomRewards(rewards);
        return false;
    }
}
