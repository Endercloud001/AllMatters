using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawSingleplayerTransitionTests
{
    internal const string Name = "Jigsaw native singleplayer creation ignores a disposed multiplayer lobby";

    internal static async Task Run()
    {
#if STS2_PUBLIC
        var host = new NetHostGameService();
#else
        var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
#endif
        using var socket = new System.Net.Sockets.UdpClient(0);
        ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
        socket.Close();
        if (host.StartENetHost(port, 2).HasValue)
            throw new InvalidOperationException("Native ENet host preparation failed.");
        var previous = RunState.CreateForTest(players: new ulong[] { 1, 2, 3 }.Select(id =>
            Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, id)).ToArray(),
            acts: [ModelDb.Act<Overgrowth>()], seed: "PREVIOUS_MULTIPLAYER");
        bool preference = JigsawPreferences.Enabled;
        try
        {
            RunManager.Instance.SetUpTest(previous, host);
            var disposedLobby = RunManager.Instance.RunLobby;
            if (disposedLobby == null)
                throw new InvalidOperationException("The native multiplayer setup must create its real run lobby.");
            RunManager.Instance.CleanUp(false);
            var solo = RunState.CreateForTest(players: [Player.CreateForNewRun(ModelDb.Character<Ironclad>(),
                UnlockState.all, new NetSingleplayerGameService().NetId)], acts: [ModelDb.Act<Overgrowth>()], seed: "SOLO_AFTER_MULTIPLAYER");
            JigsawPreferences.Enabled = true;
            RunManager.Instance.SetUpNewSingleplayer(solo, shouldSave: false);
            if (!ReferenceEquals(RunManager.Instance.RunLobby, disposedLobby))
                throw new InvalidOperationException("This regression must cross the native retained, disposed lobby boundary.");
            await RunManager.Instance.GenerateMap();
            await JigsawNativeSession.Current!.StartAct();
            var map = JigsawNativeSession.Current.Map!;
            if (!map.ActivePlayers.SequenceEqual(new ulong[] { 1 }) || !map.OriginalPlayers.SequenceEqual(new ulong[] { 1 }))
                throw new InvalidOperationException("A new singleplayer map must use only its current native player, regardless of the old multiplayer roster.");
        }
        finally
        {
            JigsawPreferences.Enabled = preference;
            RunManager.Instance.CleanUp(false);
            if (host.IsConnected) host.Disconnect(NetError.Quit);
        }
    }
}
