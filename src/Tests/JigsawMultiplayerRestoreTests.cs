using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawMultiplayerRestoreTests
{
    internal const string DeferredName = "Jigsaw native multiplayer deserialization waits for the new lobby";
    internal const string SetupName = "Jigsaw native multiplayer setup restores only the connected ENet host";

    internal static async Task DeferredActivation()
    {
        var save = await CreateCheckpoint();
        var restored = RunState.FromSerializable(save);
        Require(restored.Players.Select(player => player.NetId).SequenceEqual(new ulong[] { 1, 2, 3 }),
            "Deserialization retains all native characters, including the absent original owner.");
        Require(JigsawCheckpoint.Slot.TryGet(restored, out var checkpoint) && checkpoint.Map is { } mapData &&
            mapData.Placements.Length == 1 && mapData.OriginalPlayers!.SequenceEqual(new ulong[] { 1, 2, 3 }),
            "Deserialization retains the complete jigsaw document before the live lobby exists.");
        Require(RunManager.Instance.DebugOnlyGetState() == null && JigsawNativeSession.Current == null && JigsawNativeSession.TryGet(restored) == null,
            "Reading the save must not activate a run against the previous network service.");
        bool rejected = false;
        try { JigsawNativeSession.Enable(restored); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Explicit multiplayer activation still requires a connected matching native session.");
    }

    private static async Task<SerializableRun> CreateCheckpoint()
    {
        var participants = new ulong[] { 1, 2, 3 }.Select(id =>
            Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, id)).ToArray();
        var native = RunState.CreateForTest(players: participants, acts: [ModelDb.Act<Overgrowth>()], seed: "RESTORE_LOBBY_91");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        SerializableRun save;
        try
        {
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var map = JigsawRun.Create(91, players: [1UL, 2UL], originalPlayers: [1UL, 2UL, 3UL]);
            Require(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success, "Prepare a real accepted placement.");
            JigsawCheckpoint.Capture(native, map);
            save = RunManager.Instance.ToSave(null);
            save.Modifiers.Add(ModelDb.Modifier<AllMattersJigsawMode>().ToMutable().ToSerializable());
            JigsawSaveSource.CaptureDocument(save, native);
        }
        finally { RunManager.Instance.CleanUp(false); }

        return save;
    }

    internal static async Task HostSetup()
    {
        var save = await CreateCheckpoint();
#if STS2_PUBLIC
        var host = new NetHostGameService();
#else
        var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
#endif
        using var socket = new System.Net.Sockets.UdpClient(0);
        ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
        socket.Close();
        LoadRunLobby? lobby = null;
        try
        {
            Require(!host.StartENetHost(port, 2).HasValue, "Start the actual native ENet host.");
            lobby = new LoadRunLobby(host, new Listener(), save);
            lobby.AddLocalHostPlayer();
            var restored = RunState.FromSerializable(lobby.Run);
            Require(JigsawNativeSession.TryGet(restored) == null, "The load screen deserializes before activating its native session.");
            await RunManager.Instance.SetUpSavedMultiplayer(restored, lobby);
            var session = JigsawNativeSession.TryGet(restored);
            Require(session?.Map is { } && ReferenceEquals(JigsawNativeSession.Current, session),
                "Successful native setup activates the restored jigsaw session.");
            var map = session!.Map!;
            Require(restored.Players.Count == 3 && map.OriginalPlayers.Count == 3 && map.ActivePlayers.SequenceEqual(new[] { host.NetId }),
                "The live map keeps every original character but activates only the host present in this load lobby.");
#if STS2_PUBLIC
            var connected = RunManager.Instance.RunLobby.ConnectedPlayerIds;
#else
            var connected = RunManager.Instance.RunLobby.PlayerIds;
#endif
            Require(connected.SequenceEqual(new[] { host.NetId }), "The native RunLobby also excludes absent saved characters.");
            Require(map.Phase == JigsawPhase.Editing && map.Placements.Count == 1,
                "Setup preserves the accepted editing layout without starting generation or replaying rewards.");
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            Require(ReferenceEquals(session.Map, map) && map.Placements.Count == 1 && map.ActivePlayers.Count == 1,
                "Native map and room restoration retain the same restored logical map and active membership.");
        }
        finally
        {
            RunManager.Instance.CleanUp(false);
            lobby?.CleanUp(disconnectSession: true);
            if (host.IsConnected) host.Disconnect(NetError.Quit);
        }
    }

    private sealed class Listener : ILoadRunLobbyListener
    {
#if STS2_PUBLIC
        public void PlayerConnected(ulong playerId) { }
#else
        public void PlayerConnected(LoadRunLobbyPlayer player) { }
#endif
        public void PlayerReadyChanged(ulong playerId) { }
        public void RemotePlayerDisconnected(ulong playerId) { }
        public void LocalPlayerDisconnected(NetErrorInfo info) { }
        public Task<bool> ShouldAllowRunToBegin() => Task.FromResult(true);
        public void BeginRun() => throw new InvalidOperationException("This test invokes the load screen's setup entry directly.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
