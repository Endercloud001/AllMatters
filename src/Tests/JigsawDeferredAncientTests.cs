using System.Text.Json;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawDeferredAncientTests
{
    internal const string Name = "Jigsaw deferred Ancient rewards preserve exploration and completed owners across native reloads";

    internal static async Task Run()
    {
        MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var players = new ulong[] { 1, 2, 3 }.Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, id)).ToArray();
        var original = RunState.CreateForTest(players: players, acts: [ModelDb.Act<Overgrowth>()], seed: "DEFERRED_ANCIENT_94");
        RunManager.Instance.SetUpTest(original, new NetSingleplayerGameService());
        JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
        LocalContext.NetId = 1;
        SerializableRun point;
        string[] completedPlayers;
        try
        {
            original.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.EnterMapCoord(original.Map.StartingMapPoint.coord);
            await RunManager.Instance.EnterRoom(new MapRoom());
            var map = JigsawRun.Create(94, players: [2UL, 3UL], originalPlayers: [1UL, 2UL, 3UL]);
            Require(map.Take(2, 0).Success && map.Drop(2, new(3, 0)).Success && map.AddEdge(2, 0, 1).Success &&
                map.ChooseEnd(2, EndChoice.Manual).Success && map.ChooseEnd(3, EndChoice.Manual).Success && map.Inspect().Success &&
                map.CompleteAncient(2).Success && map.CompleteAncient(3).Success && map.Continue(2).Success && map.Continue(3).Success,
                "Prepare a finalized exploration map with one original owner still owing Ancient.");
            foreach (var player in players)
            {
                player.Creature.SetCurrentHpInternal(23 + (int)player.NetId);
                player.Gold = 200 + (int)player.NetId;
            }
            completedPlayers = players.Skip(1).Select(PlayerState).ToArray();
            JigsawCheckpoint.Capture(original, map);
            point = RunManager.Instance.ToSave(null);
            JigsawSaveSource.CaptureDocument(point, original);
        }
        finally { JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }

        SerializableRun finished;
        using (var live = await Restore(point))
        {
            var session = JigsawNativeSession.Current!;
            var native = session.Native;
            Require(session.Map is { Phase: JigsawPhase.Exploring, Steps: 0, CurrentNode: null, PendingEntry: null } &&
                session.BeforeAncient != null && native.CurrentRoom is EventRoom,
                "The connected returning owner enters a real deferred Ancient without resetting exploration.");
            var events = RunManager.Instance.EventSynchronizer.Events.ToArray();
            Require(events.Where(e => e.Owner!.NetId != 1).All(e => e.IsFinished) && !events.Single(e => e.Owner!.NetId == 1).IsFinished,
                "Only the owner with outstanding Ancient receives live native options.");
            Require(native.Players.Skip(1).Select(PlayerState).SequenceEqual(completedPlayers),
                "Beginning deferred Ancient must not repeat completed players' healing or inventory effects.");
            var interrupted = RunManager.Instance.ToSave(native.CurrentRoom);
            if (Environment.GetEnvironmentVariable("JIGSAW_DEFERRED_EVIDENCE") is { } evidence)
            {
                File.WriteAllText(Path.Combine(evidence, "before.json"), JsonSerializer.Serialize(new { live.Source.Players, live.Source.MapPointHistory }));
                File.WriteAllText(Path.Combine(evidence, "interrupted.json"), JsonSerializer.Serialize(new { interrupted.Players, interrupted.MapPointHistory }));
            }
            Require(interrupted.Players.Select(p => JsonSerializer.Serialize(p)).SequenceEqual(live.Source.Players.Select(p => JsonSerializer.Serialize(p))) &&
                JsonSerializer.Serialize(interrupted.MapPointHistory) == JsonSerializer.Serialize(live.Source.MapPointHistory),
                "An unfinished deferred reward saves the complete original native point.");
            var ancient = RunManager.Instance.EventSynchronizer.GetLocalEvent();
            Require(ancient is AncientEventModel && ancient.CurrentOptions.Count > 0, "Deferred owner receives real native Ancient options.");
            var selected = ancient.CurrentOptions.Select((option, index) => (option, index))
                .FirstOrDefault(value => value.option.Relic?.Id.Entry == "GOLDEN_PEARL");
            int index = selected.option == null ? 0 : selected.index;
            using (MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new NativeEventTests.Select(native.Players[0].Deck.Cards.First())))
            {
                RunManager.Instance.EventSynchronizer.ChooseLocalOption(index);
                await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
            }
            await session.Continue();
            Require(session.BeforeAncient == null && session.AncientReady && native.CurrentRoom is MapRoom,
                "The real choice completes debt and returns to the preserved exploration map.");
            Require(native.Players.Skip(1).Select(PlayerState).SequenceEqual(completedPlayers), "Completed owners retain their entire native player DTOs.");
            Require(session.Map!.CreateSave().AncientCompleted.Order().SequenceEqual(new ulong[] { 1, 2, 3 }) &&
                session.Map.Steps == 0 && session.Map.FirstProgress == 0 &&
                JsonSerializer.Serialize(native.Rng.ToSerializable()) == JsonSerializer.Serialize(point.SerializableRng) &&
                native.VisitedMapCoords.SequenceEqual(point.VisitedMapCoords),
                "Debt completion retains map progression, run RNG and chronological coordinates.");
            finished = RunManager.Instance.ToSave(null);
            JigsawSaveSource.CaptureDocument(finished, native);
        }
        using (var live = await Restore(finished))
        {
            var session = JigsawNativeSession.Current!;
            Require(session.BeforeAncient == null && session.AncientReady && session.Native.CurrentRoom is MapRoom,
                "A subsequent native session resumes exploration without issuing Ancient again.");
            Require(session.Native.Players.Select(PlayerState).SequenceEqual(finished.Players.Select(p => JsonSerializer.Serialize(p))),
                "Every native player persists after debt is completed once.");
        }
    }

    private static string PlayerState(Player player) => JsonSerializer.Serialize(player.ToSerializable());

    private static async Task<Live> Restore(SerializableRun point)
    {
#if STS2_PUBLIC
        var host = new NetHostGameService();
#else
        var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
#endif
        using var socket = new System.Net.Sockets.UdpClient(0);
        ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
        socket.Close();
        Require(!host.StartENetHost(port, 2).HasValue, "Start actual native ENet host.");
        var lobby = new LoadRunLobby(host, new Listener(), point);
        var live = new Live(host, lobby, JigsawSaveSource.Copy(lobby.Run));
        try
        {
            lobby.AddLocalHostPlayer();
            JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
            var state = RunState.FromSerializable(lobby.Run);
            await RunManager.Instance.SetUpSavedMultiplayer(state, lobby);
            LocalContext.NetId = host.NetId;
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            return live;
        }
        catch { live.Dispose(); throw; }
    }

    private sealed record Live(NetHostGameService Host, LoadRunLobby Lobby, SerializableRun Source) : IDisposable
    {
        public void Dispose()
        {
            RunManager.Instance.CleanUp(false);
            JigsawNativeSaveHost.Save = null;
            Lobby.CleanUp(disconnectSession: true);
            if (Host.IsConnected) Host.Disconnect(NetError.Quit);
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
        public void BeginRun() => throw new InvalidOperationException("Test invokes native setup directly.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
