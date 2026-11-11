using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawRecoveryPresentationTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw recovery presentation registries share one instance under concurrent access", ConcurrentRegistries),
        ("Jigsaw recovery presentation native restore shares live progress with map and default counter", () => RestoredSharedMap(false)),
        ("Jigsaw recovery presentation native restore crosses hunting boundary with live progress", () => RestoredSharedMap(true))
    ];

    private static Task ConcurrentRegistries()
    {
        for (int round = 0; round < 12; round++)
        {
            var native = HeadlessTestSetup.Run("RECOVERY_REGISTRY_92_" + round,
                MegaCrit.Sts2.Core.Models.ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>());
            var results = new object?[8, 3];
            var errors = new Exception?[8];
            using var start = new Barrier(8);
            var threads = Enumerable.Range(0, 8).Select(index => new Thread(() =>
            {
                start.SignalAndWait();
                try
                {
                    results[index, 0] = LobbyRecovery.GetOrCreate(native);
                    results[index, 1] = ExplorationStateRecovery.GetOrCreate(native);
                    results[index, 2] = SaveStateRecovery.GetOrCreate(native);
                }
                catch (Exception error) { errors[index] = error; }
            })).ToArray();
            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();
            Require(errors.All(error => error == null), "Concurrent first access must not throw: " +
                string.Join("; ", errors.OfType<Exception>().Select(error => error.Message)));
            for (int registry = 0; registry < 3; registry++)
                Require(results[0, registry] != null && Enumerable.Range(1, 7)
                    .All(index => ReferenceEquals(results[0, registry], results[index, registry])),
                    "Every caller must receive the same recovery for one native run.");
        }
        return Task.CompletedTask;
    }

    private static async Task RestoredSharedMap(bool huntingBoundary)
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
        HeadlessUiHost.EnsureInputActions();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        tree.Root.Size = new(1280, 720);
        var native = RunState.CreateForTest(Enumerable.Range(1, 3).Select(id =>
            Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray(),
            acts: [ModelDb.Act<Overgrowth>()], seed: "RECOVERY_PRESENTATION_92");
        var io = new MockGodotFileIo("issue92-recovery-presentation-memory-only");
        var saves = new RunSaveManager(92, io, new MigrationManager(io), forceSynchronous: true);
        JigsawScreen? screen = null;
        Control? bar = null;
#if STS2_PUBLIC
        var host = new NetHostGameService();
#else
        var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
#endif
        LoadRunLobby? loadLobby = null;
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), false);
        try
        {
            native.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var sourceMap = ExploringMap();
            var sourceSave = sourceMap.CreateSave();
            if (huntingBoundary)
                sourceSave = sourceSave with { Steps = sourceMap.Budget, NextEntry = sourceMap.Budget };
            JigsawCheckpoint.Slot.Set(native, new() { Version = 1, Act = native.CurrentActIndex, Map = sourceSave });
            await saves.SaveRun(RunManager.Instance.ToSave(null), false);
            var loaded = saves.LoadRunSave();
            Require(loaded.Success && loaded.SaveData != null, "The isolated native checkpoint must deserialize successfully.");
            RunManager.Instance.CleanUp(false);
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Require(JigsawNativeSession.TryGet(restored) == null, "Deserializing a save must leave the native session inactive.");
            var exploration = ExplorationStateRecovery.GetOrCreate(restored)!;
            var lobby = LobbyRecovery.GetOrCreate(restored)!;
            Require(exploration.GetSnapshot() is { } fallback && exploration.IsInitialized &&
                fallback.Steps == sourceSave.Steps && fallback.CurrentNode == sourceSave.CurrentNode &&
                fallback.VisitedNodes.SetEquals(sourceSave.Visited) && fallback.CompletedNodes.SetEquals(sourceSave.Completed),
                "Before activation, recovery retains the real loaded checkpoint as its fallback.");
            Require(lobby.GetLobbyState() is { IsRecovered: true, OriginalPlayerCount: 3, SaveState: { } saved, ExplorationState: { } savedExploration } &&
                saved.Steps == sourceSave.Steps && savedExploration.CurrentNode == sourceSave.CurrentNode,
                "The inactive lobby reads the attached checkpoint even when native postfixes ran before its slot was attached.");
            using var socket = new System.Net.Sockets.UdpClient(0);
            ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
            socket.Close();
            Require(!host.StartENetHost(port, 2).HasValue, "Activate the actual native multiplayer host for the restored run.");
            loadLobby = new LoadRunLobby(host, new LoadListener(), loaded.SaveData!);
            loadLobby.AddLocalHostPlayer();
            await RunManager.Instance.SetUpSavedMultiplayer(restored, loadLobby);
            RunManager.Instance.Launch();
            JigsawNativeSession.Restore(restored, checkpoint: loaded.SaveData);
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            var session = JigsawNativeSession.TryGet(restored)!;
            var map = session.Map!;
#if STS2_PUBLIC
            var connectedIds = RunManager.Instance.RunLobby.ConnectedPlayerIds;
#else
            var connectedIds = RunManager.Instance.RunLobby.PlayerIds;
#endif
            Require(connectedIds.SequenceEqual(new[] { host.NetId }), "Only the real connected host participates in native synchronization: " + string.Join(",", connectedIds));
            Require(ReferenceEquals(session, JigsawNativeSession.Current), "The displayed map belongs to the restored active native run.");
            Require(lobby.OnPlayerConnected(1) == LobbyRecovery.ConnectionResult.Admitted &&
                lobby.OnPlayerConnected(2) == LobbyRecovery.ConnectionResult.Admitted,
                "Two original players read the same recovered exploration state.");
            map.Disconnect(3);
            var initialSteps = map.Steps;
            var initialCurrent = map.CurrentNode!.Value;
            var initialCompleted = map.Completed.Count;
            AssertProjection(map, exploration, lobby);

            screen = session.CreateScreen(new MemoryJigsawTutorialProfile { Sentence = 6 });
            bar = new Control { Size = new(1280, 80) };
            var boss = new Control { Position = new(500, 8), Size = new(48, 48) };
            bar.AddChild(boss);
            var counter = JigsawStepCounter.Attach(bar, boss);
            await HeadlessUiHost.Mount(screen, bar);
            Require(counter.GetChild<Label>(1).Text == Math.Max(0, map.Budget - initialSteps).ToString(),
                "The default production step counter reads the restored session without a test data source.");
            Require(screen.MapControl.GetNodeOrNull<Sprite2D>($"Room{initialCurrent}/Visited") is { Visible: true },
                "The restored current ordinary room displays its native visit ring.");

            // Room completion below is a domain input. Destination entry is routed through
            // the engine and the production screen/session callback, including native readiness.
            Require(map.CompleteRoom(map.CreateSave().NextEntry).Success, "Finish the saved logical room before choosing another destination.");
            screen.Refresh();
            AssertProjection(map, exploration, lobby);
            Require(exploration.GetSnapshot()!.CompletedNodes.Count == initialCompleted + 1 &&
                exploration.GetSnapshot()!.CurrentNode == null, "Completion must update the recovery immediately after load.");
            int destination = map.AvailableDestinations.First(id => map.Nodes.Single(node => node.Id == id).Room == JigsawRoom.Treasure);
            var target = map.Nodes.Single(node => node.Id == destination);
            for (int index = 0; index < screen.NavigableNodeIds.Count && screen.FocusedNodeId != destination; index++)
                screen.Navigate(1);
            var position = screen.MapControl.GlobalPosition + screen.GridToCanvas(target.Center);
            Input.ParseInputEvent(new InputEventMouseButton
            {
                Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true
            });
            for (int frame = 0; frame < 240 && map.CurrentNode != destination; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseButton
            {
                Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false
            });
            Require(map.CurrentNode == destination && map.Steps == initialSteps + 1 && restored.CurrentRoom is TreasureRoom,
                $"Engine input must enter the selected native room and charge exactly one step after restore. Current={map.CurrentNode}, target={destination}, steps={map.Steps}, room={restored.CurrentRoom?.GetType().Name}, pending={map.PendingEntry}, click={position}, canvas={screen.MapControl.GetGlobalRect()}.");
            AssertProjection(map, exploration, lobby);
            Require(exploration.IsHunting() == huntingBoundary, "The live recovery crosses the hunting boundary only after exceeding the budget.");
            session.ShowMap();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Require(counter.GetChild<Label>(1).Text == Math.Max(0, map.Budget - initialSteps - 1).ToString(),
                "The default step counter refreshes after restored engine input.");
            Require(screen.MapControl.GetNodeOrNull<Sprite2D>($"Room{destination}/Visited") is { Visible: true },
                "A destination entered after restore receives its visible visit ring.");
            Require(map.Reconnect(3).Success && exploration.IsNodeVisited(destination),
                "A late original player observes the same shared visit record without resetting it.");
            await session.WaitForSave();
        }
        finally
        {
            screen?.Free();
            bar?.Free();
            JigsawNativeSaveHost.Save = null;
            RunManager.Instance.CleanUp(false);
            loadLobby?.CleanUp(disconnectSession: true);
            if (host.IsConnected) host.Disconnect(NetError.Quit);
            tree.Root.Size = originalSize;
        }
    }

    private sealed class LoadListener : ILoadRunLobbyListener
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
        public void BeginRun() => throw new InvalidOperationException("The test invokes native saved-run setup directly.");
    }

    private static JigsawRun ExploringMap()
    {
        var map = JigsawRun.Create(92, players: [1UL, 2UL, 3UL]);
        foreach (ulong player in map.ActivePlayers.ToArray()) Require(map.ChooseEnd(player, EndChoice.Automatic).Success, "Authorize the generated fixture.");
        Require(JigsawAutomatic.Solve(map, 0) is { } layout && map.FinishAutomatic(map.AutomaticOperation!, layout).Success,
            "Use a valid generated pool and successful map.");
        foreach (ulong player in map.ActivePlayers.ToArray()) Require(map.CompleteAncient(player).Success, "Complete fixture Ancient rewards.");
        foreach (ulong player in map.ActivePlayers.ToArray()) Require(map.Continue(player).Success, "Continue into fixture exploration.");
        for (int entry = 0; entry < 56; entry++)
        {
            bool treasureAvailable = map.AvailableDestinations.Any(id => map.Nodes.Single(node => node.Id == id).Room == JigsawRoom.Treasure);
            int next = map.AvailableDestinations.First(id => !map.Visited.Contains(id) &&
                map.Nodes.Single(node => node.Id == id).Room is not (JigsawRoom.Treasure or JigsawRoom.Shop or JigsawRoom.Boss or JigsawRoom.SecondBoss));
            var attempt = map.BeginEnter(next)!;
            Require(map.ConfirmEnter(attempt.Id).Success, "Advance the source fixture through accepted entries.");
            if (treasureAvailable) return map;
            Require(map.CompleteRoom(attempt.Id).Success, "Complete the fixture path until a treasure becomes reachable.");
        }
        throw new InvalidOperationException("The generated fixture must expose a treasure room.");
    }

    private static void AssertProjection(JigsawRun map, ExplorationStateRecovery exploration, LobbyRecovery lobby)
    {
        var snapshot = exploration.GetSnapshot();
        var state = lobby.GetLobbyState();
        Require(snapshot != null && snapshot.VisitedNodes.SetEquals(map.Visited) && snapshot.CompletedNodes.SetEquals(map.Completed) &&
            snapshot.CurrentNode == map.CurrentNode && snapshot.Steps == map.Steps && snapshot.FirstProgress == map.FirstProgress &&
            snapshot.Phase == map.Phase && snapshot.Budget == map.Budget,
            "Recovery must project the active production map, including changes since the loaded checkpoint.");
        Require(map.Visited.All(exploration.IsNodeVisited) && map.Completed.All(exploration.IsNodeCompleted) &&
            exploration.GetRemainingSteps() == map.RemainingSteps && exploration.IsHunting() == (map.Steps > map.Budget),
            "Exploration queries must read the same current map as the recovery snapshot.");
        Require(state.ExplorationState is { } shared && shared.CurrentNode == map.CurrentNode && shared.Steps == map.Steps &&
            shared.VisitedCount == map.Visited.Count && shared.CompletedCount == map.Completed.Count &&
            shared.RemainingSteps == map.RemainingSteps && shared.IsHunting == (map.Steps > map.Budget) &&
            state.SaveState is { } save && save.Steps == map.Steps && save.Budget == map.Budget && save.Phase == map.Phase.ToString(),
            "Lobby summaries must expose the same live exploration and save-stage projection to all players.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
