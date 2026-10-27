using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class TeaTrialRewards
{
    private sealed class EliteRewardRoom(CombatRoom source) : CombatRoom(source.CombatState)
    {
        public override RoomType RoomType => RoomType.Elite;
    }

    private static bool Prefix(RewardsSet __instance, AbstractRoom room, ref RewardsSet __result)
    {
        if (room is not CombatRoom combat || combat.Encounter is not (ChampionEncounter or TrialEncounter)) return true;
        // Enemy identity stays on the actual encounter. Only reward generation and
        // its modifiers see the elite tier, including the elite gold range.
        var elite = new EliteRewardRoom(combat);
        var rewards = (List<Reward>)AccessTools.Method(typeof(RewardsSet), "GenerateRewardsFor")
            .Invoke(__instance, [__instance.Player, elite])!;
        if (combat.Encounter is ChampionEncounter)
            rewards.Add(new RelicReward(ModelDb.Relic<Relics.ChampionBelt>().ToMutable(), __instance.Player));
        __result = __instance.EmptyForRoom(elite).WithCustomRewards(rewards);
        return false;
    }
}
