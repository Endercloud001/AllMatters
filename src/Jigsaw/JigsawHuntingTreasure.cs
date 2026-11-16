using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingTreasure
{
    internal static bool IsBlocked(Player player) => JigsawHuntingNative.RootIsHunted(player) &&
        player.RunState.CurrentRoom is TreasureRoom && player.RunState.CurrentRoomCount == 1;
}
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldGenerateTreasure))]
internal static class JigsawHuntingChestRelics
{
    private static void Postfix(Player player, ref bool __result)
    {
        if (JigsawHuntingTreasure.IsBlocked(player))
            __result = false;
    }
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.GenerateWithoutOffering))]
internal static class JigsawHuntingChestExtras
{
    private static bool Prefix(RewardsSet __instance, ref Task __result)
    {
        if (__instance.Room is not TreasureRoom || !JigsawNativeExploration.RejectTreasureTask(__instance.Player))
            return true;
        __instance.Rewards.Clear();
        __result = Task.CompletedTask;
        return false;
    }
    private static void Postfix(RewardsSet __instance, ref Task __result)
    {
        if (__instance.Room is TreasureRoom room && JigsawHuntingTreasure.IsBlocked(__instance.Player) &&
            ReferenceEquals(room, __instance.Player.RunState.CurrentRoom) &&
            JigsawNativeSession.TryGet((RunState)__instance.Player.RunState) is { Map: { } map } session)
        {
            __result = Finish(__result, __instance, session, map, map.CreateSave().NextEntry);
            JigsawNativeExploration.TreasureTaskBegan(session, __instance.Player.NetId, __result);
        }
    }

    private static async Task Finish(Task original, RewardsSet set, JigsawNativeSession session, JigsawRun map, long entry)
    {
        await original;
        if (JigsawNativeSession.Current != session || !ReferenceEquals(session.Map, map) || map.CreateSave().NextEntry != entry)
            throw new OperationCanceledException("宝箱所属会话或房间已改变。");
        set.Rewards.Clear();
    }
}

[HarmonyPatch(typeof(OneOffSynchronizer), "DoTreasureRoomRewards", [typeof(Player)])]
internal static class JigsawHuntingChestPayment
{
    private static bool Prefix(Player player, ref Task<int> __result)
    {
        if (JigsawNativeExploration.RejectTreasureTask(player))
        {
            __result = Task.FromResult(0);
            return false;
        }
        if (!JigsawHuntingTreasure.IsBlocked(player))
            return true;
        __result = Task.FromResult(0);
        return false;
    }

    private static void Postfix(Player player, ref Task<int> __result)
    {
        if (player.RunState is not RunState native || JigsawNativeSession.TryGet(native) is not { Map: { } map } session)
            return;
        bool marked = native.CurrentRoom is TreasureRoom && map.IsSpoilsDestination;
        if (marked && !JigsawNativeExploration.RejectTreasureTask(player))
            __result = PayAfter(__result, player, session, map);
        if (JigsawHuntingTreasure.IsBlocked(player) || marked)
            JigsawNativeExploration.TreasureSourceBegan(session, player.NetId, __result);
    }

    private static async Task<int> PayAfter(Task<int> original, Player player, JigsawNativeSession session, JigsawRun map)
    {
        var room = session.Native.CurrentRoom;
        int gold = await original;
        if (JigsawNativeSession.Current != session || !ReferenceEquals(session.Map, map) || !ReferenceEquals(session.Native.CurrentRoom, room))
            throw new OperationCanceledException("藏宝图宝箱已改变。");
        return gold + await JigsawNativeQuestMarkers.Pay(player, session, map, room);
    }
}
