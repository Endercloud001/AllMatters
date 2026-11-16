using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Relics;

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class NlothSkipTreasure
{
    private static void Postfix(TreasureRoomRelicSynchronizer __instance,
#if STS2_PUBLIC
        IPlayerCollection ____playerCollection
#else
        IRunState ____runState
#endif
        )
    {
        if (__instance.CurrentRelics == null)
            return;
        foreach (var player in
#if STS2_PUBLIC
            ____playerCollection
#else
            ____runState
#endif
            .Players.Where(NlothsHungryFace.IsEmptyFor))
            __instance.OnPicked(player, null);
    }
}

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.OnPicked))]
internal static class NlothTreasureVote
{
    private static void Prefix(Player player, ref int? index)
    {
        if (NlothsHungryFace.IsEmptyFor(player))
            index = null;
    }
}

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.PickRelicLocally))]
internal static class NlothLocalTreasureVote
{
    private static void Prefix(ulong ____localPlayerId,
#if STS2_PUBLIC
        IPlayerCollection ____playerCollection,
#else
        IRunState ____runState,
#endif
        ref int? index)
    {
        if (NlothsHungryFace.IsEmptyFor(
#if STS2_PUBLIC
            ____playerCollection
#else
            ____runState
#endif
            .Players.Single(p => p.NetId == ____localPlayerId)))
            index = null;
    }
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class NlothTreasureExtras
{
    private static void Postfix(RewardsSet __instance, AbstractRoom room)
    {
        if (room is TreasureRoom && NlothsHungryFace.IsEmptyFor(__instance.Player))
            __instance.Rewards.Clear();
    }
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.GenerateWithoutOffering))]
internal static class NlothFinalTreasureRewards
{
    private static void Postfix(RewardsSet __instance, ref Task __result)
    {
        if (__instance.Room is TreasureRoom && NlothsHungryFace.IsEmptyFor(__instance.Player))
            __result = ClearAfterModifiers(__result, __instance);
    }
    private static async Task ClearAfterModifiers(Task original, RewardsSet rewards)
    {
        await original;
        rewards.Rewards.Clear();
    }
}
