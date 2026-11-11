using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace AllMatters.Jigsaw;

internal static class JigsawNativeEntryReload
{
    [ThreadStatic] private static INetGameService? retained;

    private sealed class RetainScope(INetGameService service) : IDisposable
    {
        private readonly INetGameService? previous = retained;
        public void Enter() => retained = service;
        public void Dispose() => retained = previous;
    }

    internal static bool ShouldDisconnect(INetGameService service) => !ReferenceEquals(retained, service);

    private sealed class Listener : ILoadRunLobbyListener
    {
        public void BeginRun() { }
#if STS2_PUBLIC
        public void PlayerConnected(ulong playerId) { }
#else
        public void PlayerConnected(LoadRunLobbyPlayer player) { }
#endif
        public void RemotePlayerDisconnected(ulong playerId) { }
        public void PlayerReadyChanged(ulong playerId) { }
        public void LocalPlayerDisconnected(NetErrorInfo info) { }
        public Task<bool> ShouldAllowRunToBegin() => Task.FromResult(true);
    }

    internal static async Task Load(SerializableRun checkpoint, RunState restored, IReadOnlyCollection<ulong> active, long round)
    {
        var manager = RunManager.Instance;
        var service = manager.NetService;
        var oldInput = manager.InputSynchronizer;
        if (service is not (NetHostGameService or NetClientGameService) || !service.IsConnected)
            throw new InvalidOperationException("多人进入恢复需要仍连接的原生会话。");
#if !STS2_PUBLIC
        var modded = manager.RunLobby?.Players.ToDictionary(player => player.id, player => player.isModded) ?? [];
#endif
        service.SetBufferMessages(true);
        try
        {
            using (var scope = new RetainScope(service))
            {
                scope.Enter();
                manager.CleanUp(false);
            }
            oldInput.Dispose();
            if (!service.IsConnected)
                throw new IOException("原生联机连接已断开，无法恢复进入点。");
            var lobby = new LoadRunLobby(service, new Listener(), checkpoint);
#if STS2_PUBLIC
            foreach (ulong player in active)
                lobby.ConnectedPlayerIds.Add(player);
#else
            foreach (ulong player in active)
                lobby.Players.Add(new LoadRunLobbyPlayer { id = player, isModded = modded.GetValueOrDefault(player) });
#endif
            lobby.CleanUp(false);
            await manager.SetUpSavedMultiplayer(restored, lobby);
            JigsawNativeSession.Current?.SetEntryRound(round);
            if (!TestMode.IsOn)
                await MegaCrit.Sts2.Core.Nodes.NGame.Instance!.LoadRun(restored, null);
            else
            {
                manager.Launch();
                await manager.GenerateMap()!;
                await manager.LoadIntoLatestMapCoord(null);
            }
        }
        catch
        {
            try
            { manager.CleanUp(false); }
            finally
            {
                if (service.IsConnected)
                    service.Disconnect(NetError.Quit, true);
            }
            throw;
        }
    }
}

[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.Disconnect))]
internal static class JigsawKeepHostForEntryReload
{
    private static bool Prefix(NetHostGameService __instance) => JigsawNativeEntryReload.ShouldDisconnect(__instance);
}

[HarmonyPatch(typeof(NetClientGameService), nameof(NetClientGameService.Disconnect))]
internal static class JigsawKeepClientForEntryReload
{
    private static bool Prefix(NetClientGameService __instance) => JigsawNativeEntryReload.ShouldDisconnect(__instance);
}
