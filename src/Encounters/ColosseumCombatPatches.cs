// SPDX-License-Identifier: GPL-3.0-only
using AllMatters.Events;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs.History;

namespace AllMatters.Encounters;

#if !STS2_PUBLIC
// 0.111.0 retains each participant's readiness after the first combat.
// Reset once for the whole event, before any second-combat handler runs.
// Public 0.107.1 has no such synchronizer: portrait events create a fresh
// CombatState on local-owner entry and must not reset combat-layout state.
[HarmonyPatch(typeof(EventSynchronizer), nameof(EventSynchronizer.ResumeEvents))]
internal static class ColosseumCombatReset
{
    private static void Prefix(EventSynchronizer __instance, AbstractRoom exitedRoom)
    {
        if (exitedRoom is not CombatRoom { Encounter: ColosseumFirstEncounter }) return;
        if (__instance.GetLocalEvent() is not Colosseum) return;
        __instance.BeforeExitingRoom();
        __instance.GenerateInternalCombatStateIfNecessary(__instance.GetLocalEvent());
    }
}
#endif

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
internal static class ColosseumReplayInitialization
{
    private static void Prefix(RunManager __instance, AbstractRoom room)
    {
        if (room is not CombatRoom combat || !Colosseum.IsSecondCombat(combat)) return;
        var writer = __instance.CombatReplayWriter;
        // Both targets stop recording at first victory. Begin recording before
        // the second room's setup/actions, without a process-global event flag.
        if (!writer.IsEnabled || writer.IsRecordingReplay) return;
        var snapshot = __instance.ToSave(null);
        // RunReplay loads PreFinishedRoom even when it is not finished. A null
        // room would roll this map point again. Build a replay-only live room
        // without relaxing native restrictions on ordinary event combat saves.
        var replayRoom = new CombatRoom(combat.Encounter!, combat.CombatState.RunState).ToSerializable();
        replayRoom.ParentEventId = combat.ParentEventId;
        replayRoom.ShouldResumeParentEvent = false;
        snapshot.PreFinishedRoom = replayRoom;

        // The entry method has not appended this room yet. ToSave only copies
        // outer history lists, so replace the node before appending to avoid
        // adding a duplicate second room to the real run's history.
        var actHistory = snapshot.MapPointHistory.Last();
        var point = actHistory.Last();
        actHistory[^1] = new MapPointHistoryEntry
        {
            MapPointType = point.MapPointType,
            PlayerStats = [.. point.PlayerStats],
            Rooms = [.. point.Rooms, new MapPointRoomHistoryEntry
            {
                RoomType = combat.RoomType,
                ModelId = combat.ModelId,
            }],
        };
        writer.RecordInitialState(snapshot);
    }
}
