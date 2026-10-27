using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class WhisperingHollowRewards
{
    // A reward-only view preserves the real encounter and combat participants while
    // giving native reward modifiers the same tier as the generated drops.
    private sealed class EliteRewardRoom(CombatRoom source) : CombatRoom(source.CombatState)
    {
        public override RoomType RoomType => RoomType.Elite;
    }

    private static bool Prefix(RewardsSet __instance, AbstractRoom room, ref RewardsSet __result)
    {
        if (room is not CombatRoom { Encounter: WhisperingHollowEncounter } combat) return true;
        // Reuse the native elite reward generator (including potion odds and card tier),
        // without changing the real encounter's ordinary monster identity.
        var elite = new EliteRewardRoom(combat);
        var rewards = (List<Reward>)AccessTools.Method(typeof(RewardsSet), "GenerateRewardsFor")
            .Invoke(__instance, [__instance.Player, elite])!;
        rewards.Add(new RelicReward(ModelDb.Relic<Relics.DeadBranch>().ToMutable(), __instance.Player));
        __result = __instance.EmptyForRoom(elite).WithCustomRewards(rewards);
        return false;
    }
}

[HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.GenerateForRoomEnd))]
internal static class WhisperingHollowCleanup
{
    private static bool Prefix(Player player, AbstractRoom room, ref Task<RewardsSet> __result)
    {
        if (room is not CombatRoom { Encounter: WhisperingHollowEncounter }) return true;
        __result = CleanAndGenerate(player, room);
        return false;
    }

    private static async Task<RewardsSet> CleanAndGenerate(Player player, AbstractRoom room)
    {
        foreach (var card in player.Deck.Cards.OfType<SporeMind>().ToArray())
            await CardPileCmd.RemoveFromDeck(card);
        var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
        await rewards.GenerateWithoutOffering();
        return rewards;
    }
}
