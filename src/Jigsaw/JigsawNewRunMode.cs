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
    internal static bool IsEligible(GameMode mode, DateTimeOffset? dailyTime = null, bool isCustom = false) =>
        !isCustom && dailyTime == null && (mode == GameMode.Standard
#if !STS2_PUBLIC
            || mode == GameMode.Dread
#endif
        );

    internal static bool IsEffective(IRunState state)
    {
        if (state is not RunState native || !IsEligible(native.GameMode, null,
#if STS2_PUBLIC
            false
#else
            native.IsCustomGame
#endif
        ))
            return false;
        return JigsawNativeSession.TryGet(native) != null || Slot.TryGet(native, out var identity) && identity.Enabled;
    }

    internal static readonly RunSavedData<JigsawModeIdentity> Slot =
        RunSavedDataStore.For(ModEntry.ModId).Register<JigsawModeIdentity>("jigsaw_mode");
}

[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
internal static class JigsawStageNewRunMode
{
    private static bool IsLobbyEligible(StartRunLobby lobby) =>
        JigsawNewRunMode.IsEligible(lobby.GameMode, lobby.DailyTime?.serverTime,
#if STS2_PUBLIC
            false
#else
            lobby.IsCustom
#endif
        );

    [HarmonyPriority(Priority.First)]
    private static void Prefix(StartRunLobby __instance)
    {
        JigsawNewRunMode.Slot.Lobby.Set(__instance, new JigsawModeIdentity
        {
            Enabled = __instance.NetService.Type == NetGameType.Host &&
                IsLobbyEligible(__instance) && JigsawPreferences.Enabled
        });
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class JigsawActivateMultiplayerNewRun
{
    private static void Postfix(RunState state, DateTimeOffset? dailyTime)
    {
        if (JigsawNewRunMode.IsEligible(state.GameMode, dailyTime,
#if STS2_PUBLIC
            false
#else
            state.IsCustomGame
#endif
        ) &&
           JigsawNewRunMode.Slot.TryGet(state, out var identity) && identity.Enabled)
            JigsawNativeSession.Enable(state);
    }
}
