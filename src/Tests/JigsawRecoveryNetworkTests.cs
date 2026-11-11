using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using Environment = System.Environment;

// The listener replaces NGame's scene ownership only. The load lobby, packet,
// canonicalization, setup, logical restoration and production screen remain real.
internal static class JigsawRecoveryNetworkTests
{
    private const string Prefix = "Jigsaw recovery native load lobby";
    public static readonly (string Name, Func<Task> Test)[] Cases =
        (from phase in new[] { "editing", "generating", "ancient", "exploring", "visited", "pending" }
         from roster in new[] { "all", "absent", "late", "leave" }
         select ($"{Prefix} {phase} {roster}", (Func<Task>)(() => Run(phase, roster))))
        .Concat(new[] { "deferred", "deferred-pending", "entry-retry", "entry-abandon", "entry-after", "entry-disconnect", "shop-lag" }
            .Select(phase => ($"{Prefix} {phase} all", (Func<Task>)(() => Run(phase, "all"))))).ToArray();

    // These are existing native reward/failure paths, previously opt-in by environment.
    // Register each scenario so the full suite actually executes the specified inputs.
    public static readonly (string Name, Func<Task> Test)[] RewardCases =
    [
        ("Jigsaw recovery real rewards exclude the absent original owner", () => NativeRewardScenario("JIGSAW_ABSENT_ANCIENT")),
        ("Jigsaw recovery pending absent reward preserves the whole checkpoint", () => NativeRewardScenario("JIGSAW_PENDING_ANCIENT")),
        ("Jigsaw recovery faulted absent reward preserves the whole checkpoint", () => NativeRewardScenario("JIGSAW_PENDING_ANCIENT", "JIGSAW_PENDING_ANCIENT_FAULT")),
        ("Jigsaw recovery Host exit protects partial native rewards", () => NativeRewardScenario("JIGSAW_HOSTEXIT")),
        ("Jigsaw recovery Host exit rejects a late automatic worker", () => NativeRewardScenario("JIGSAW_HOSTEXIT_WORKER")),
        ("Jigsaw recovery native automatic failure keeps the accepted input", () => NativeRewardScenario("JIGSAW_AUTOMATIC_FAILURE")),
        ("Jigsaw recovery native disconnect recalculates the original consensus", () => NativeRewardScenario("JIGSAW_DISCONNECT_CONSENSUS"))
    ];

    private static async Task NativeRewardScenario(params string[] flags)
    {
        var previous = flags.ToDictionary(flag => flag, Environment.GetEnvironmentVariable);
        string directory = ProjectSettings.GlobalizePath("res://../../.local/batch3/reward-processes");
        Directory.CreateDirectory(directory);
        string? oldTemp = Environment.GetEnvironmentVariable("TEMP"), oldTmp = Environment.GetEnvironmentVariable("TMP");
        try
        {
            foreach (string flag in flags) Environment.SetEnvironmentVariable(flag, "1");
            Environment.SetEnvironmentVariable("TEMP", directory);
            Environment.SetEnvironmentVariable("TMP", directory);
            await JigsawNetworkTests.Run();
        }
        finally
        {
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            Environment.SetEnvironmentVariable("TEMP", oldTemp);
            Environment.SetEnvironmentVariable("TMP", oldTmp);
        }
    }

    private sealed class LoadListener : ILoadRunLobbyListener
    {
        internal LoadRunLobby Lobby = null!;
        internal Task? Loading;
        internal RunState? Native;
        internal string? RestoredMap;
        internal bool AllowEntryFailure;
        public void BeginRun() => Loading = Load();
        private async Task Load()
        {
            Native = RunState.FromSerializable(Lobby.Run);
            await RunManager.Instance.SetUpSavedMultiplayer(Native, Lobby);
            RestoredMap = JsonSerializer.Serialize(JigsawNativeSession.Current!.Map!.CreateSave());
            Require(JigsawCheckpoint.Slot.TryGet(Native, out var checkpoint) && checkpoint.Map != null, "Native load retains the complete source extension.");
            var expectedMap = checkpoint.Map! with { Players = Connected(Lobby).ToArray() };
            Require(JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(expectedMap)), JsonNode.Parse(RestoredMap)), "Setup preserves every persisted map field; only this session's active roster changes.");
            RunManager.Instance.Launch();
            await RunManager.Instance.GenerateMap();
            try { await RunManager.Instance.LoadIntoLatestMapCoord(null); }
            catch (Exception error) when (AllowEntryFailure && error is IOException or InvalidOperationException) { }
        }
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

    private static IEnumerable<ulong> Connected(LoadRunLobby lobby) =>
#if STS2_PUBLIC
        lobby.ConnectedPlayerIds;
#else
        lobby.PlayerIds;
#endif

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task<SerializableRun> Fixture(RunSaveManager saves, string phase)
    {
        var players = Enumerable.Range(1, 3).Select(id => Player.CreateForNewRun(
            ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
        var native = RunState.CreateForTest(players: players, acts: [ModelDb.Act<Overgrowth>()], seed: "RECOVERY_81");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            if (phase.StartsWith("deferred", StringComparison.Ordinal))
            {
                native.ExtraFields.StartedWithNeow = true;
                native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
                native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            }
            foreach (var player in players)
            {
                MegaCrit.Sts2.Core.Commands.CardCmd.Upgrade(player.Deck.Cards[0]);
                player.AddRelicInternal(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.GoldenPearl>().ToMutable());
                await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainGold((int)player.NetId * 17, player);
                await MegaCrit.Sts2.Core.Commands.PotionCmd.TryToProcure<MegaCrit.Sts2.Core.Models.Potions.FirePotion>(player);
            }
            bool entryFault = phase.StartsWith("entry-", StringComparison.Ordinal);
            bool shop = phase == "shop-lag";
            var map = entryFault || shop ? Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed, players: players.Select(player => player.NetId)))
                .First(candidate => candidate.Pool[0].Nodes[0].Room == (shop ? JigsawRoom.Shop : JigsawRoom.Treasure)) : JigsawRun.Create(81, players: players.Select(player => player.NetId));
            if (entryFault || shop)
            {
                Require(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success, "Failure fixture has a real reachable Treasure.");
                foreach (var player in players) Require(map.ChooseEnd(player.NetId, EndChoice.Manual).Success, "The fault fixture uses genuine manual consensus.");
                Require(map.Inspect().Success, "The fault fixture passes production validation.");
                foreach (var player in players) Require(map.CompleteAncient(player.NetId).Success, "Fault fixture owners have completed Ancient.");
                foreach (var player in players) Require(map.Continue(player.NetId).Success, "Fault fixture owners continue exploration.");
                Require(map.BeginEnter(1) != null, "The checkpoint keeps its already chosen Treasure destination.");
            }
            else if (phase == "editing")
            {
                Require(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success, "Editing fixture contains a real accepted placement.");
                Require(map.Take(2, 1).Success, "Editing fixture has a held piece that must be cleared in the new session.");
            }
            else
            {
                foreach (var player in players) Require(map.ChooseEnd(player.NetId, EndChoice.Automatic).Success, "Fixture authorizes automatic layout through the public API.");
                if (phase != "generating")
                {
                    Require(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)).Success, "Fixture uses an accepted production layout.");
                    if (phase != "ancient")
                    {
                        if (phase.StartsWith("deferred", StringComparison.Ordinal)) map.Disconnect(3);
                        foreach (ulong owner in map.ActivePlayers) Require(map.CompleteAncient(owner).Success, "Fixture completes the active owners' logical Ancient boundary.");
                        foreach (ulong owner in map.ActivePlayers) Require(map.Continue(owner).Success, "Fixture enters shared exploration.");
                        if (phase == "visited")
                        {
                            var entry = map.BeginEnter(map.AvailableDestinations.First())!;
                            Require(map.ConfirmEnter(entry.Id).Success && map.CompleteRoom(entry.Id).Success, "Visited fixture has one completed shared destination.");
                        }
                        if (phase is "pending" or "deferred-pending") Require(map.BeginEnter(map.AvailableDestinations.First()) != null, "Pending fixture already chose its destination.");
                    }
                }
            }
            JigsawCheckpoint.Capture(native, map);
            var save = RunManager.Instance.ToSave(null);
            save.Modifiers.Add(ModelDb.Modifier<AllMattersJigsawMode>().ToMutable().ToSerializable());
            JigsawSaveSource.CaptureDocument(save, native);
            await saves.SaveRun(save, true);
            var loaded = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            Require(loaded.Success && loaded.SaveData != null, "Native multiplayer canonicalization reads the complete fixture.");
            return loaded.SaveData!;
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static Process Launch(string role, string directory, string filter, ushort? port = null)
    {
        var start = new ProcessStartInfo(OS.GetExecutablePath())
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--", filter })
            start.ArgumentList.Add(argument);
        start.Environment["JIGSAW_RECOVERY_ROLE"] = role;
        start.Environment["JIGSAW_RECOVERY_DIR"] = directory;
        if (port != null) start.Environment["JIGSAW_RECOVERY_PORT"] = port.ToString();
        var process = Process.Start(start)!;
        TextWriter Writer(string extension) => TextWriter.Synchronized(new StreamWriter(new FileStream(
            Path.Combine(directory, role + extension), FileMode.Create, System.IO.FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true });
        var output = Writer(".log");
        var errors = Writer(".err");
        process.OutputDataReceived += (_, args) => { if (args.Data != null) output.WriteLine(args.Data); else output.Dispose(); };
        process.ErrorDataReceived += (_, args) => { if (args.Data != null) errors.WriteLine(args.Data); else errors.Dispose(); };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static async Task Run(string phase, string roster)
    {
        string name = $"{Prefix} {phase} {roster}";
        string? role = Environment.GetEnvironmentVariable("JIGSAW_RECOVERY_ROLE");
        if (role == null)
        {
            string evidence = ProjectSettings.GlobalizePath("res://../../.local/batch3/recovery/" + phase + "-" + roster + "-" + Guid.NewGuid());
            Directory.CreateDirectory(evidence);
            using var process = Launch("host", evidence, name);
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)); }
            finally { if (!process.HasExited) process.Kill(true); }
            process.WaitForExit(); // Flush redirected asynchronous log callbacks before inspecting evidence.
            Console.WriteLine($"RECOVERY evidence={evidence} host exit={process.ExitCode}");
            Require(process.ExitCode == 0, "Native recovery Host failed; inspect " + evidence);
            foreach (string errorLog in Directory.EnumerateFiles(evidence, "*.err"))
                Require(!File.ReadLines(errorLog).Any(line => line.StartsWith("ERROR:", StringComparison.Ordinal) || line.StartsWith("FATAL:", StringComparison.Ordinal) || line.StartsWith("[ERROR]", StringComparison.Ordinal) || line.StartsWith("[FATAL]", StringComparison.Ordinal)),
                    "Native recovery endpoint reported an engine error; inspect " + errorLog);
            return;
        }
        MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        tree.Root.Size = new(1280, 720);
        string directory = Environment.GetEnvironmentVariable("JIGSAW_RECOVERY_DIR")!;
        var children = new List<Process>();
#if STS2_PUBLIC
        var host = role == "host" ? new NetHostGameService() : null;
        var client = role != "host" ? new NetClientGameService() : null;
#else
        var host = role == "host" ? new NetHostGameService(PeerVersionInfo.LocalDefault()) : null;
        var client = role != "host" ? new NetClientGameService(PeerVersionInfo.LocalDefault()) : null;
#endif
        INetGameService net = (INetGameService?)host ?? client!;
        async Task Until(Func<bool> predicate, string stage)
        {
            var timer = Stopwatch.StartNew();
            while (!predicate())
            {
                if (timer.Elapsed.TotalSeconds > 20)
                {
                    var executor = RunManager.Instance.ActionExecutor;
                    throw new TimeoutException($"Recovery {role}: {stage}; paused={executor.IsPaused} running={executor.IsRunning} action={executor.CurrentlyRunningAction} recovery={JigsawNativeSession.Current?.EntryRecoveryTask?.Status} round={JigsawNativeSession.Current?.EntryRound}; evidence={directory}");
                }
                net.Update();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        void Signal(string stage) => File.WriteAllText(Path.Combine(directory, role + "." + stage), "ready");
        bool Signaled(string peer, string stage) => File.Exists(Path.Combine(directory, peer + "." + stage));
        var listener = new LoadListener();
        bool entryFault = phase.StartsWith("entry-", StringComparison.Ordinal);
        listener.AllowEntryFailure = entryFault;
        if (entryFault && phase != "entry-disconnect" && role == "2")
        {
            JigsawEntryFailureHost.Fail = phase != "entry-after";
            JigsawEntryFailureHost.Late = phase == "entry-after";
        }
        void NativeReady() => Signal("native-ready");
        if (phase == "entry-disconnect")
        {
            RunManager.Instance.RoomEntered += NativeReady;
            if (role == "3")
                JigsawEntryFailureHost.Wrap = async original =>
                {
                    await original;
                    await Until(() => Signaled("host", "native-ready") && Signaled("2", "native-ready"), "both remaining endpoints report real native readiness before this endpoint leaves");
                    net.Disconnect(NetError.Quit, true);
                    throw new IOException("Injected disconnection before RoomEntered, after the other two endpoints are ready.");
                };
        }
        JigsawScreen? screen = null;
        bool deferred = phase.StartsWith("deferred", StringComparison.Ordinal);
        bool unfinishedAncient = phase is "editing" or "generating" or "ancient";
        bool shopLag = phase == "shop-lag";
        using var eventScope = deferred || unfinishedAncient || shopLag ? HeadlessCombatInProgress.EnterEvent() : null;
        using var atlases = deferred || unfinishedAncient || shopLag ? HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas") : null;
        var rewardHarmony = new HarmonyLib.Harmony("AllMatters.Tests.RecoveryDeferred");
        if (deferred || unfinishedAncient)
            rewardHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(EventSynchronizer), nameof(EventSynchronizer.BeginEvent)),
                prefix: new HarmonyLib.HarmonyMethod(typeof(JigsawNetworkTests), nameof(JigsawNetworkTests.ForceReward)));
        try
        {
            var io = new MockGodotFileIo("recovery-81-" + role);
            var saves = new RunSaveManager(81, io, new MigrationManager(io), forceSynchronous: true);
            JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), true);
            if (host != null)
            {
                var save = await Fixture(saves, phase);
                using var socket = new System.Net.Sockets.UdpClient(0);
                ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
                socket.Close();
                Require(!host.StartENetHost(port, 2).HasValue, "The actual ENet Host starts.");
                host.ClientConnected += id => host.SetPeerReadyForBroadcasting(id);
                listener.Lobby = new LoadRunLobby(net, listener, save);
                listener.Lobby.AddLocalHostPlayer();
                foreach (string child in roster == "absent" ? new[] { "2" } : new[] { "2", "3" })
                    children.Add(Launch(child, directory, name, port));
            }
            else
            {
                if (role == "3" && roster == "late") await Until(() => Signaled("host", "two-in-lobby"), "late original player waits for the two-player lobby");
                var transport = new ENetClient(client!);
                client!.Initialize(transport, PlatformType.None);
                net.RegisterMessageHandler<InitialGameInfoMessage>((_, _) => { }); // NGame normally consumes the native handshake's display metadata.
                var connect = transport.ConnectToHost(ulong.Parse(role), "127.0.0.1", ushort.Parse(Environment.GetEnvironmentVariable("JIGSAW_RECOVERY_PORT")!), CancellationToken.None);
                await Until(() => connect.IsCompleted, "native transport handshake");
                Require(!(await connect).HasValue, "Native transport connects.");
                await Until(() => client.IsConnected, "native authentication");
                net.RegisterMessageHandler<ClientLoadJoinResponseMessage>((message, _) => listener.Lobby = new LoadRunLobby(net, listener, message));
                net.SendMessage(new ClientLoadJoinRequestMessage());
            }
            await Until(() => listener.Lobby != null && Connected(listener.Lobby).Count() >= 2, "native load lobby receives full save and identities");
            if (host != null) Signal("two-in-lobby");
            int connectedCount = roster == "absent" ? 2 : 3;
            await Until(() => Connected(listener.Lobby).Count() == connectedCount, "expected original players join");
            Signal("joined");
            var originalRoles = connectedCount == 2 ? new[] { "host", "2" } : new[] { "host", "2", "3" };
            await Until(() => originalRoles.All(peer => Signaled(peer, "joined")), "all endpoints observe the same load lobby");
            if (roster == "leave")
            {
                if (role == "3")
                {
                    listener.Lobby.CleanUp(true);
                    Signal("left");
                    return;
                }
                await Until(() => Signaled("3", "left") && Connected(listener.Lobby).Count() == 2, "native disconnect removes only the current member");
            }
            ulong[] active = roster is "absent" or "leave" || phase == "entry-disconnect" ? [1, 2] : [1, 2, 3];
            var expected = listener.Lobby.Run;
            Require(expected.Players.Count == 3, "The native packet retains all three original inventories.");
            listener.Lobby.SetReady(true);
            await Until(() => listener.Loading?.IsCompleted == true, "native ready/start callback finishes real saved setup and room restoration");
            await listener.Loading!;
            if (phase == "entry-disconnect" && role == "3") return;
            var session = JigsawNativeSession.Current;
            if (phase == "entry-disconnect")
                await Until(() => session?.Map?.ActivePlayers.Count == 2, "Host removes the departed original player from the current entry quorum");
            Require(session?.Map != null && session.Map.ActivePlayers.Order().SequenceEqual(active), "The restored production map uses the actual load-lobby participants.");
            var map = session!.Map!;
            Require(map.OriginalPlayers.Order().SequenceEqual(new ulong[] { 1, 2, 3 }), "Absent original characters remain in the map.");
            if (phase is "exploring" or "visited")
                Require(listener.Native!.CurrentRoom is MapRoom && map.Phase == JigsawPhase.Exploring, "The production resume callback enters the completed exploration map.");
            if (phase == "editing") Require(map.Phase == JigsawPhase.Editing && map.Holdings.Count == 0 && map.Placements.Count == 1, "Editing keeps accepted placements and clears the former session's holdings.");
            if (phase is "generating" or "ancient") Require(map.Phase == JigsawPhase.Ancient && listener.Native!.CurrentRoom is EventRoom, "The production resume callback completes generation or reopens the unfinished Ancient.");
            if (phase == "pending") Require(map.Steps == 1 && map.PendingEntry == null && map.CurrentNode != null, "Native readiness confirms the already chosen pending destination exactly once.");
            foreach (var original in expected.Players)
                Require(JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(original)), JsonNode.Parse(JsonSerializer.Serialize(listener.Native.Players.Single(player => player.NetId == original.NetId).ToSerializable()))), "Native load restores the complete player document, including upgraded cards, relics and potions.");
            if (deferred)
            {
                var completed = listener.Native!.Players.Where(player => player.NetId != 3)
                    .ToDictionary(player => player.NetId, player => JsonSerializer.Serialize(player.ToSerializable()));
                Require(map.HasDeferredAncient && session.BeforeAncient != null && listener.Native.CurrentRoom is EventRoom,
                    "A returning original owner enters native Ancient before exploration can continue.");
                Require(map.AvailableDestinations.Count == 0, "Outstanding native rewards block exploration.");
                RunManager.Instance.ActionExecutor.Unpause();
                using var selector = MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new JigsawNetworkTests.LegalSelector());
                Signal("debt-ready");
                await Until(() => new[] { "host", "2", "3" }.All(peer => Signaled(peer, "debt-ready")), "all replicas enter deferred Ancient");
                if (net.NetId == 3)
                {
#if STS2_PUBLIC
                    const string reward = "PRECISE_SCISSORS";
#else
                    const string reward = "POMANDER";
#endif
                    int option = RunManager.Instance.EventSynchronizer.GetLocalEvent().CurrentOptions.ToList().FindIndex(item => item.Relic?.Id.Entry == reward);
                    Require(option >= 0, "The owing player receives a real native reward option.");
                    RunManager.Instance.EventSynchronizer.ChooseLocalOption(option);
                }
                await Until(() => session.AncientReady && session.BeforeAncient == null, "Host records the owing player's completed native option on every endpoint");
                await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
                int? chosen = map.PendingEntry?.NodeId;
                var continuation = session.Continue();
                await Until(() => continuation.IsCompleted, "native pending-room continuation completes while transport is pumped");
                await continuation;
                await Until(() => session.DeferredAncient == null && (chosen == null ? listener.Native.CurrentRoom is MapRoom : map.CurrentNode == chosen), "every endpoint returns to its preserved exploration source");
                Require(completed.All(pair => JsonSerializer.Serialize(listener.Native.Players.Single(player => player.NetId == pair.Key).ToSerializable()) == pair.Value),
                    "Settling the returning owner never repeats completed owners' full native reward effects.");
                Require(map.Steps == (chosen == null ? 0 : 1) && map.FirstProgress == (chosen == null ? 0 : 1) && map.CurrentNode == chosen && map.PendingEntry == null &&
                    map.CreateSave().AncientCompleted.Order().SequenceEqual(new ulong[] { 1, 2, 3 }), "Deferred reward changes no exploration entry or progress.");
                listener.RestoredMap = JsonSerializer.Serialize(map.CreateSave());
            }
            HeadlessUiHost.EnsureInputActions();
            screen = session.CreateScreen(new MemoryJigsawTutorialProfile { Sentence = 6 });
            await HeadlessUiHost.Mount(screen);
            Require(screen.IsInsideTree() && screen.IsVisibleInTree(), "The actual production screen presents the recovered map.");
            if (phase is "exploring" or "visited") Require(map.AvailableDestinations.Count > 0, "Recovered exploration exposes the next decision.");
            File.WriteAllText(Path.Combine(directory, role + ".map.json"), listener.RestoredMap);
            Console.WriteLine($"RECOVERY restored role={role} original=3 active={active.Length} pool={map.Pool.Count} placements={map.Placements.Count} phase={map.Phase} steps={map.Steps}");
            Signal("restored");
            var remaining = active.Length == 2 ? new[] { "host", "2" } : new[] { "host", "2", "3" };
            await Until(() => remaining.All(peer => Signaled(peer, "restored")), "all production endpoints restore before Host exit");
            foreach (string peer in remaining)
                Require(JsonNode.DeepEquals(JsonNode.Parse(listener.RestoredMap!), JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".map.json")))), "All endpoints have the same complete map, RNG, operation input and exploration document after native setup.");
            if (shopLag)
            {
                RunManager.Instance.ActionExecutor.Unpause();
                Require(listener.Native!.CurrentRoom is MerchantRoom && map.CurrentNode == 1 && map.Steps == 1,
                    "Every endpoint mounts the same actual native shop before the first purchase.");
                var inventories = ((MerchantRoom)listener.Native.CurrentRoom).Inventories;
                var first = listener.Native.Players.Single(player => player.NetId == 1);
                var delayed = listener.Native.Players.Single(player => player.NetId == 2);
                int firstGold = first.Gold, firstDeck = first.Deck.Cards.Count;
                int delayedGold = delayed.Gold, delayedDeck = delayed.Deck.Cards.Count;
                int firstPrice = inventories.Single(inventory => inventory.Player.NetId == 1).CardEntries.First().Cost;
                Signal("shop-ready");
                await Until(() => remaining.All(peer => Signaled(peer, "shop-ready")), "all endpoints observe complete inventories before the first owner buys");
                if (net.NetId == 1)
                    Require(await inventories.Single(inventory => inventory.Player.NetId == 1).CardEntries.First().OnTryPurchaseWrapper(inventories.Single(inventory => inventory.Player.NetId == 1)),
                        "The first owner purchases through the exact native wrapper used by the shop UI.");
                await Until(() => first.Gold == firstGold - firstPrice && first.Deck.Cards.Count == firstDeck + 1 &&
                    !inventories.Single(inventory => inventory.Player.NetId == 1).CardEntries.First().IsStocked,
                    "native reward messages and the production stock update converge on every endpoint");
                Require(delayed.Gold == delayedGold && delayed.Deck.Cards.Count == delayedDeck && inventories.Single(inventory => inventory.Player.NetId == 2).CardEntries.First().IsStocked,
                    "The delayed owner keeps their independent untouched inventory and native player state.");
                Signal("first-purchased");
                await Until(() => remaining.All(peer => Signaled(peer, "first-purchased")), "all replicas observe the first sale before the delayed owner uses a service");
                using var selector = MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new JigsawNetworkTests.LegalSelector());
                int removalCost = inventories.Single(inventory => inventory.Player.NetId == 2).CardRemovalEntry.Cost;
                if (net.NetId == 2)
                    Require(await inventories.Single(inventory => inventory.Player.NetId == 2).CardRemovalEntry.OnTryPurchaseWrapper(inventories.Single(inventory => inventory.Player.NetId == 2)),
                        "The delayed owner uses the real native card-removal service.");
                await Until(() => delayed.Gold == delayedGold - removalCost && delayed.Deck.Cards.Count == delayedDeck - 1 &&
                    inventories.Single(inventory => inventory.Player.NetId == 2).CardRemovalEntry.Used,
                    "native card removal and the used service state synchronize on every endpoint");
                string stock = JsonSerializer.Serialize(map.CreateSave().ShopStates);
                File.WriteAllText(Path.Combine(directory, role + ".shop-stock"), stock);
                Signal("shop-purchased");
                await Until(() => remaining.All(peer => Signaled(peer, "shop-purchased")), "all owners finish the real native transactions before comparing stock");
                foreach (string peer in remaining)
                    Require(JsonNode.DeepEquals(JsonNode.Parse(stock), JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".shop-stock")))),
                        "Every endpoint saves the same complete stock after one owner buys and another later removes a card.");
            }
            if (entryFault)
            {
                RunManager.Instance.ActionExecutor.Unpause();
                if (phase is "entry-after" or "entry-disconnect")
                {
                    await Until(() => map.PendingEntry == null && map.CurrentNode == 1 && map.Steps == 1, "a callback failure after readiness still confirms the one common entry");
                }
                else
                {
                    await Until(() => session.FailedEntryCheckpoint != null, "the failed endpoint makes the shared entry retryable on every replica");
                    Require(map.PendingEntry?.NodeId == 1 && map.CurrentNode == null && map.Steps == 0, "Failure before reliable readiness charges no endpoint.");
                    Signal("failed");
                    await Until(() => remaining.All(peer => Signaled(peer, "failed")), "all endpoints observe the failure before Host chooses recovery");
                    if (host != null)
                    {
                        var recovery = phase == "entry-abandon" ? session.AbandonEntry() : session.Enter(1);
                        await Until(() => recovery.IsCompleted, "Host recovery request finishes while transport is pumped");
                        await recovery;
                    }
                    await Until(() =>
                    {
                        if (session.EntryRecoveryTask is { IsFaulted: true } failure) failure.GetAwaiter().GetResult();
                        return JigsawNativeSession.Current is { } current && current != session &&
                            (phase == "entry-abandon" ? current.Map?.PendingEntry == null && current.Map?.CurrentNode == null : current.Map?.CurrentNode == 1);
                    },
                        "native multiplayer reload replaces the whole failed run on every endpoint");
                    session = JigsawNativeSession.Current!;
                    map = session.Map!;
                    Require(ReferenceEquals(RunManager.Instance.NetService, net) && net.IsConnected && map.ActivePlayers.Order().SequenceEqual(active),
                        "Whole-run recovery preserves the original authenticated transport and active roster.");
                    Require(map.Steps == (phase == "entry-abandon" ? 0 : 1) && map.PendingEntry == null &&
                        map.OriginalPlayers.Order().SequenceEqual(new ulong[] { 1, 2, 3 }), "Retry charges once; abandonment restores selection and preserves all original characters.");
                }
                File.WriteAllText(Path.Combine(directory, role + ".fault-result"), $"{map.CurrentNode}:{map.Steps}:{map.CreateSave().DestinationRandomState}");
                Signal("fault-done");
                await Until(() => remaining.All(peer => Signaled(peer, "fault-done")), "all endpoints finish the recovery decision");
                foreach (string peer in remaining)
                    Require(File.ReadAllText(Path.Combine(directory, peer + ".fault-result")) == File.ReadAllText(Path.Combine(directory, role + ".fault-result")), "All replicas agree after native entry recovery.");
                var entryDescriptor = (STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActionDescriptor<JigsawEditorIntent>)HarmonyLib.AccessTools.Field(typeof(JigsawEditorAction), "descriptor").GetValue(null)!;
                foreach (var (operation, sequence) in new[] { (JigsawEditorOperation.EntryReady, 100L), (JigsawEditorOperation.EntryFailed, 101L) })
                    Require(STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActions.Request(RunManager.Instance, entryDescriptor,
                        new JigsawEditorIntent(session.Native.CurrentActIndex, operation, Sequence: sequence, EditRevision: map.EditRevision,
                            CompletedOwner: net.NetId, EntryRevision: map.CreateSave().NextEntry, EntryRound: Math.Max(0, session.EntryRound - 1))),
                        "Repeated and stale authenticated entry notifications reach the real native queue.");
                for (int frame = 0; frame < 30; frame++) { net.Update(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
                Require(map.PendingEntry == null && map.Steps == (phase == "entry-abandon" ? 0 : 1),
                    "Old-round and repeated entry notifications cannot reopen a completed decision or charge another step.");
            }
            if (unfinishedAncient)
            {
                RunManager.Instance.ActionExecutor.Unpause();
                if (phase == "editing")
                {
                    var button = (Button)HarmonyLib.AccessTools.Field(typeof(JigsawScreen), "automatic").GetValue(screen)!;
                    var position = button.GetGlobalRect().GetCenter();
                    Require(!button.Disabled && button.IsVisibleInTree(), "The recovered editor exposes its real automatic-consensus button.");
                    Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position });
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position });
                }
                await Until(() => map.Phase == JigsawPhase.Ancient && listener.Native!.CurrentRoom is EventRoom,
                    "recovered editor or generation continues through production consensus into native Ancient");
                Signal("ancient-ready");
                await Until(() => remaining.All(peer => Signaled(peer, "ancient-ready")), "all native Ancient endpoints are ready for their individual options");
                using var selector = MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new JigsawNetworkTests.LegalSelector());
#if STS2_PUBLIC
                const string reward = "PRECISE_SCISSORS";
#else
                const string reward = "POMANDER";
#endif
                int option = RunManager.Instance.EventSynchronizer.GetLocalEvent().CurrentOptions.ToList().FindIndex(item => item.Relic?.Id.Entry == reward);
                Require(option >= 0, "Every current player can continue the recovered real Ancient.");
                RunManager.Instance.EventSynchronizer.ChooseLocalOption(option);
                await Until(() => session.AncientReady && session.BeforeAncient == null, "all current owners finish real native rewards");
                await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
                await session.Continue();
                await Until(() => map.Phase == JigsawPhase.Exploring, "new-session continue votes finish and expose exploration");
                session.ShowMap();
                RunManager.Instance.ActionExecutor.Unpause();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            if (phase is "exploring" or "visited" || unfinishedAncient)
            {
                RunManager.Instance.ActionExecutor.Unpause();
                await Until(() => map.AvailableDestinations.Count > 1, "the recovered map exposes distinct destination ballots");
                int[] choices = map.AvailableDestinations.Order().ToArray();
                int choice = choices[(int)(net.NetId % (ulong)Math.Min(2, choices.Length))];
                int steps = map.Steps;
                var target = map.Nodes.Single(node => node.Id == choice);
                var position = screen.MapControl.GlobalPosition + screen.GridToCanvas(target.Center);
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position });
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position });
                Console.WriteLine($"RECOVERY ballot role={role} target={choice} at={position} canvas={screen.MapControl.GetGlobalRect()} votes={string.Join(',', map.DestinationVotes.Select(v => v.Key + ":" + v.Value))}");
                await Until(() => map.CurrentNode != null && map.Steps == steps + 1, "engine-routed votes choose and natively enter one common destination");
                Require(map.PendingEntry == null && map.DestinationVotes.Count == 0 && listener.Native!.CurrentRoom is not (null or MapRoom), "Native RoomEntered confirms one room, consumes all ballots and charges exactly once.");
                File.WriteAllText(Path.Combine(directory, role + ".destination"), $"{map.CurrentNode}:{map.Steps}:{map.CreateSave().DestinationRandomState}");
                Signal("entered");
                await Until(() => remaining.All(peer => Signaled(peer, "entered")), "all peers confirm the same native destination");
                foreach (string peer in remaining)
                    Require(File.ReadAllText(Path.Combine(directory, peer + ".destination")) == File.ReadAllText(Path.Combine(directory, role + ".destination")), "Real endpoints agree on the selected target, step count and independent random state.");
                if (listener.Native!.CurrentRoom is CombatRoom { IsPreFinished: false } or EventRoom)
                {
                    var descriptor = (STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActionDescriptor<JigsawEditorIntent>)HarmonyLib.AccessTools.Field(typeof(JigsawEditorAction), "descriptor").GetValue(null)!;
                    var premature = new JigsawEditorIntent(listener.Native.CurrentActIndex, JigsawEditorOperation.CompleteRoom,
                        Sequence: 100, EditRevision: map.EditRevision, CompletedOwner: net.NetId, EntryRevision: map.CreateSave().NextEntry);
                    Require(STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, premature), "The authenticated native action reaches the production completion handler.");
                    for (int frame = 0; frame < 30; frame++) { net.Update(); await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
                    Require(map.RoomCompletedPlayers.Count == 0 && map.CurrentNode != null && map.Steps == steps + 1, "Premature authenticated completion cannot skip the native room or diverge on any endpoint.");
                }
            }
            if (host != null)
            {
                RunManager.Instance.CleanUp(false);
                Signal("stopped");
                await Until(() => children.All(child => child.HasExited), "clients finish after the native Host ends");
                Require(children.All(child => child.ExitCode == 0), "All native recovery Client processes pass.");
            }
            else await Until(() => Signaled("host", "stopped"), "native Host shutdown");
        }
        finally
        {
            rewardHarmony.UnpatchAll(rewardHarmony.Id);
            RunManager.Instance.RoomEntered -= NativeReady;
            JigsawEntryFailureHost.Reset();
            if (GodotObject.IsInstanceValid(screen)) screen!.Free();
            foreach (var child in children) { if (!child.HasExited) child.Kill(true); child.Dispose(); }
            RunManager.Instance.CleanUp(false);
            JigsawNativeSaveHost.Save = null;
            if (net.IsConnected) net.Disconnect(NetError.Quit);
            tree.Root.Size = originalSize;
        }
    }
}
