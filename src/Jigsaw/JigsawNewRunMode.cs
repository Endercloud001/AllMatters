using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.RunData;

namespace AllMatters.Jigsaw;

public sealed class JigsawModeIdentity
{
    public bool Enabled { get; set; }
}

// Ritsu commits this lobby-owned data into LobbyBeginRunMessage and imports it
// before InitializeNewRun. It never enters the native gameplay modifier list.
internal static class JigsawNewRunMode
{
    internal static readonly RunSavedData<JigsawModeIdentity> Slot =
        RunSavedDataStore.For(ModEntry.ModId).Register<JigsawModeIdentity>("jigsaw_mode");
}

[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
internal static class JigsawStageNewRunMode
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(StartRunLobby __instance)
    {
        JigsawNewRunMode.Slot.Lobby.Set(__instance, new JigsawModeIdentity
        {
            Enabled = __instance.NetService.Type == NetGameType.Host &&
                __instance.GameMode == GameMode.Standard && __instance.DailyTime == null && JigsawPreferences.Enabled
        });
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class JigsawActivateMultiplayerNewRun
{
    private static void Postfix(RunState state, DateTimeOffset? dailyTime)
    {
        if (dailyTime == null && state.GameMode == GameMode.Standard &&
           JigsawNewRunMode.Slot.TryGet(state, out var identity) && identity.Enabled)
            JigsawNativeSession.Enable(state);
    }
}
