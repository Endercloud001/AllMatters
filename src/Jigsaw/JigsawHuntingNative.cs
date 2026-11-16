using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingNative
{
    internal static bool RootIsHunted(Player player) =>
        player.RunState is RunState native && JigsawNativeSession.TryGet(native) is { Map: { } map } &&
        (map.CurrentNode != null ? map.CurrentRoomHunted : map.PendingEntry != null && map.HuntingActive);

    internal static async Task SynchronizeMasks(JigsawNativeSession session)
    {
        if (session.Map is not { } map)
            return;
        foreach (var player in session.Native.Players.Where(p => map.OriginalPlayers.Contains(p.NetId)))
        {
            var masks = player.Relics.OfType<ArchitectMask>().ToArray();
            foreach (var mask in masks.Skip(map.HuntingActive ? 1 : 0))
                await RelicCmd.Remove(mask);
            if (map.HuntingActive && masks.Length == 0)
                await RelicCmd.Obtain<ArchitectMask>(player);
        }
    }

    internal static async Task CompleteRoom(JigsawNativeSession session, bool wasHunting)
    {
        await SynchronizeMasks(session);
        if (session.Map is not { } map || wasHunting == map.HuntingActive)
            return;
        JigsawCheckpoint.Capture(session.Native, map);
        if (RunManager.Instance.ShouldSave)
            await SaveManager.Instance.SaveRun(session.Native.CurrentRoom is { IsPreFinished: true } room ? room : null);
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsTradable), MethodType.Getter)]
internal static class JigsawMaskNotTradable
{
    private static void Postfix(RelicModel __instance, ref bool __result)
    { if (__instance is ArchitectMask) __result = false; }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class JigsawClearMaskBeforeRewards
{
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result)
    {
        if (runState is RunState native && JigsawNativeSession.TryGet(native) is { Map: { } map } session &&
            native.CurrentRoomCount == 1 && ReferenceEquals(native.CurrentRoom, room) && map.IsEndgame)
            __result = Clear(session, __result);
    }
    private static async Task Clear(JigsawNativeSession session, Task previous)
    {
        // The native caller awaits this hook before capturing the victory reward point.
        await previous;
        bool cleared = session.Map!.CompleteMapBossVictory();
        await JigsawHuntingNative.SynchronizeMasks(session);
        if (cleared)
            session.ShowHuntingReleased();
    }
}
