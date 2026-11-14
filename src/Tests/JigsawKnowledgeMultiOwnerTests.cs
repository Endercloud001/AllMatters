using System.Diagnostics;
using System.Text.Json;
using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawKnowledgeMultiOwnerTests
{
    private const string Name = "Jigsaw Knowledge two native owners stagger first-round screens and synchronize distinct choices";
    internal static readonly (string Name, Func<Task> Test)[] Cases = [(Name, () => StaggeredFirstChoice(false)),
        (NativeName, () => StaggeredFirstChoice(true)),
        (ThreeRoundsName, () => StaggeredFirstChoice(true, 3))];
    private const string ThreeRoundsName = "Jigsaw Knowledge two native owners naturally stagger all three curse rounds";
    private const string NativeName = "Jigsaw Knowledge two native owners native first turn waits and advances once";
    private static readonly JsonSerializerOptions SnapshotJson = new() { IncludeFields = true };
    private static readonly string[] Roles = ["host", "2"];
    private static string Role => System.Environment.GetEnvironmentVariable("JIGSAW_KNOWLEDGE_ROLE") == "2" ? "2" : "host";
    private static bool IsHost => Role == "host";
    private static string? evidence;
    private static INetGameService? network;
    private static Process? childProcess;

    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Signal(string stage) => File.WriteAllText(Path.Combine(evidence!, Role + "." + stage), "ready");
    private static bool Signaled(string role, string stage) => File.Exists(Path.Combine(evidence!, role + "." + stage));
    private static bool Both(string stage) => Roles.All(role => Signaled(role, stage));
    private static MonsterModel? observedMonster;
    private static int moveCalls;
    private static void ObserveMove(MonsterModel __instance)
    {
        if (ReferenceEquals(__instance, observedMonster)) moveCalls++;
    }

    private static bool SeenFtue(ref bool __result) { __result = true; return false; }

    private static string MonsterRng(MonsterModel monster) =>
#if STS2_PUBLIC
        $"{monster.Rng.Seed}/{monster.Rng.Counter}";
#else
        JsonSerializer.Serialize(monster.Rng.ToSerializable(), SnapshotJson);
#endif

    private static string UnchangedState(RunState run, Player[] owners, MonsterModel monster, CombatState combat) =>
        JsonSerializer.Serialize(new
        {
            RunRng = run.Rng.ToSerializable(), combat.RoundNumber,
            Owners = owners.Select(owner => new
            {
                owner.NetId,
                Rng = owner.PlayerRng.ToSerializable(),
                Deck = owner.Deck.Cards.Select(card => card.ToSerializable()).ToArray(),
                Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }
                    .Select(pile => new { Type = pile, Cards = pile.GetPile(owner).Cards.Select(card => card.ToSerializable()).ToArray() }).ToArray(),
                owner.Creature.CurrentHp, owner.Creature.MaxHp, owner.Creature.Block,
                Energy = owner.PlayerCombatState!.Energy,
                OtherPowers = owner.Creature.Powers.Where(power => power is not CalamityPower and not MindRotPower)
                    .Select(power => new { power.Id, power.Amount }).ToArray()
            }).ToArray(),
            Monster = new
            {
                monster.Creature.CurrentHp, monster.Creature.MaxHp, monster.Creature.Block,
                Powers = monster.Creature.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
                Rng = MonsterRng(monster), Move = monster.NextMove.Id
            }
        }, SnapshotJson);

    private static async Task Frames(int count = 1)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
        {
            network?.Update();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task Until(Func<bool> condition, string stage)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (IsHost && childProcess is { HasExited: true } && !Signaled("2", "done"))
                throw new InvalidOperationException("Knowledge child exited before " + stage + "; evidence=" + evidence);
            if (clock.Elapsed > TimeSpan.FromSeconds(20))
            {
                var state = RunManager.Instance.DebugOnlyGetState();
                Console.Error.WriteLine($"KNOWLEDGE_TIMEOUT stage={stage} room={state?.CurrentRoom?.GetType().Name} hands={string.Join(',', state?.Players.Select(player => PileType.Hand.GetPile(player).Cards.Count) ?? [])} paused={RunManager.Instance.ActionExecutor.IsPaused} action={RunManager.Instance.ActionExecutor.CurrentlyRunningAction}");
                throw new TimeoutException("Knowledge " + Role + " timed out at " + stage + "; evidence=" + evidence);
            }
            await Frames();
        }
    }

    private static JigsawRun PunishedBoss(bool enterBoss = true)
    {
        var map = JigsawRun.Create(75, players: [1UL, 2UL]);
        foreach (ulong owner in new[] { 1UL, 2UL }) Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong owner in new[] { 1UL, 2UL }) Ok(map.CompleteAncient(owner));
        foreach (ulong owner in new[] { 1UL, 2UL }) Ok(map.Continue(owner));
        Ok(map.SetRemainingSteps(0));
        int boss = map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
        var queue = new Queue<int>();
        var previous = new Dictionary<int, int> { [0] = -1 };
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        var path = new List<int>();
        for (int node = boss; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            if (node == boss && !enterBoss) { Check(map.BeginEnter(node) != null, "Both peers prepare the same shared Boss entry."); break; }
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != boss) Ok(map.CompleteRoom(entry.Id));
        }
        Check((!enterBoss || map.CurrentRoomHunted) && map.ActivePlayers.Order().SequenceEqual(new[] { 1UL, 2UL }),
            "Both owners share the accepted punished top-level Boss root.");
        return map;
    }

    private static HeadlessProcessOutput Launch(ushort port, string caseName)
    {
        var start = new ProcessStartInfo(OS.GetExecutablePath())
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--", caseName })
            start.ArgumentList.Add(argument);
        start.Environment["JIGSAW_KNOWLEDGE_ROLE"] = "2";
        start.Environment["JIGSAW_KNOWLEDGE_DIR"] = evidence;
        start.Environment["JIGSAW_KNOWLEDGE_PORT"] = port.ToString();
        return new HeadlessProcessOutput(start);
    }

    private static async Task StaggeredFirstChoice(bool nativeTurn, int curseRounds = 1)
    {
        var manager = RunManager.Instance;
        ulong? previousNetId = LocalContext.NetId;
        NetHostGameService? host = null;
        NetClientGameService? client = null;
        HeadlessProcessOutput? child = null;
        KnowledgeUi? ui = null;
        Harmony? moveObservation = null;
        PlayerChoiceSynchronizer? choiceSync = null;
        Action<Player, uint, NetPlayerChoiceResult>? observeChoice = null;
        var receivedChoices = new List<(ulong Owner, uint Id)>();
        bool runStarted = false;
        var hostPatches = new Harmony("AllMatters.TestHost");
        bool nativeHost = false;
        var previousSave = JigsawNativeSaveHost.Save;
        var actionCache = AccessTools.Field(typeof(ActionTypes), "_cache");
        var previousActionCache = actionCache.GetValue(null);
        IDisposable? openingAtlases = null;
        int readyRound = 0, readyCount = 0, expectedReadyRound = 2;
        var nextTurn = new TaskCompletionSource();
        void Ready(CombatState state)
        {
            if (state.CurrentSide != CombatSide.Player) return;
            readyRound = state.RoundNumber;
            readyCount++;
            if (readyRound == expectedReadyRound) nextTurn.TrySetResult();
        }
        var errors = new List<Exception>();
        void Cleanup(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        try
        {
            MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
            if (nativeTurn) ActionTypes.Initialize();
#if STS2_PUBLIC
            host = IsHost ? new NetHostGameService() : null;
            client = IsHost ? null : new NetClientGameService();
#else
            host = IsHost ? new NetHostGameService(PeerVersionInfo.LocalDefault()) : null;
            client = IsHost ? null : new NetClientGameService(PeerVersionInfo.LocalDefault());
#endif
            network = (INetGameService?)host ?? client!;
            if (host != null)
            {
                evidence = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),
                    "../../.local/knowledge-multi-owner", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(evidence);
                using var socket = new System.Net.Sockets.UdpClient(0);
                ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
                socket.Close();
                Check(!host.StartENetHost(port, 1).HasValue, "The real two-owner ENet host binds loopback.");
                host.ClientConnected += id => host.SetPeerReadyForBroadcasting(id);
                child = Launch(port, curseRounds == 3 ? ThreeRoundsName : nativeTurn ? NativeName : Name); childProcess = child.Process;
                await Until(() => host.ConnectedPeers.Count == 1 && host.ConnectedPeers[0].peerId == 2,
                    "native child authentication");
            }
            else
            {
                evidence = System.Environment.GetEnvironmentVariable("JIGSAW_KNOWLEDGE_DIR")!;
                var transport = new ENetClient(client!);
                client!.Initialize(transport, PlatformType.None);
                client.RegisterMessageHandler<InitialGameInfoMessage>((_, _) => { });
                var connecting = transport.ConnectToHost(2, "127.0.0.1",
                    ushort.Parse(System.Environment.GetEnvironmentVariable("JIGSAW_KNOWLEDGE_PORT")!), CancellationToken.None);
                await Until(() => connecting.IsCompleted, "native client transport handshake");
                Check(!(await connecting).HasValue, "The real child transport connects.");
                await Until(() => client.IsConnected, "native client authentication");
            }
            Signal("connected");
            await Until(() => Both("connected"), "both native peers connected");
            var owners = new[] { Player.CreateForNewRun<Ironclad>(UnlockState.all, 1),
                Player.CreateForNewRun<Silent>(UnlockState.all, 2) };
            var run = RunState.CreateForTest(owners, acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()],
                seed: "KNOWLEDGE_85_TWO_OWNERS");
            run.CurrentActIndex = 1;
            var encounter = ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable();
            CombatState combat;
            MonsterModel monster;
            CombatRoom room;
            JigsawNativeSession session;
            if (nativeTurn)
            {
                openingAtlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
                nativeHost = true;
                moveObservation ??= new Harmony("AllMatters.Tests.KnowledgeMultiOwnerMove");
                moveObservation.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                    prefix: new HarmonyMethod(typeof(JigsawKnowledgeMultiOwnerTests), nameof(SeenFtue)));
                hostPatches.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
                hostPatches.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
                CombatManager.Instance.TurnStarted += Ready;
                manager.SetUpTest(run, network); runStarted = true;
                manager.Launch();
                manager.ActionExecutor.Unpause();
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = network.NetId;
                CombatManager.Instance.Reset(true);
                JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
                manager.GenerateRooms();
                run.Act.SetBossEncounter(ModelDb.Encounter<KnowledgeDemonBoss>());
                await manager.GenerateMap();
                await manager.EnterRoom(new MapRoom());
                var map = PunishedBoss(false);
                JigsawCheckpoint.Capture(run, map);
                JigsawNativeSession.Restore(run);
                session = JigsawNativeSession.TryGet(run)!;
                await Until(() => STS2RitsuLib.Networking.Sidecar.RitsuLibSidecarSessionManager.TryGetPeerFeatures(IsHost ? 2UL : 1UL, out var features) &&
                    (features & STS2RitsuLib.Networking.Sidecar.RitsuLibSidecarPeerFeatures.ManagedNetActions) != 0,
                    "native sidecar capability negotiation");
                Signal("entry-ready");
                await Until(() => Both("entry-ready"), "both prepared shared entries");
                var entering = session.Enter(map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id);
                await Until(() => entering.IsCompleted, "native shared room entry");
                await entering;
                room = (CombatRoom)run.CurrentRoom!;
                combat = room.CombatState;
                monster = room.Encounter.MonstersWithSlots.Single().Item1;
                await Until(() => readyRound == 1 && combat.CurrentSide == CombatSide.Player &&
                    PileType.Hand.GetPile(owners[0]).Cards.Count == 5 &&
                    PileType.Hand.GetPile(owners[1]).Cards.Count == 7, "native opening player turn");
            }
            else
            {
                combat = new CombatState(encounter, run) { RoundNumber = 1 };
                foreach (var owner in owners) { combat.AddPlayer(owner); owner.ResetCombatState(); }
                manager.SetUpTest(run, network); runStarted = true;
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = network.NetId;
                CombatManager.Instance.Reset(true);
                CombatManager.Instance.SetUpCombat(combat);
                encounter.GenerateMonstersWithSlots(run);
                var pair = encounter.MonstersWithSlots.Single();
                monster = pair.Item1;
                var slot = pair.Item2;
                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                room = new CombatRoom(combat);
                run.PushRoom(room);
                JigsawCheckpoint.Capture(run, PunishedBoss());
                JigsawNativeSession.Restore(run);
                session = JigsawNativeSession.TryGet(run)!;
            }
            Check(session.Map!.CurrentRoomHunted && run.CurrentRoomCount == 1 &&
                owners.All(JigsawHuntingNative.RootIsHunted) && manager.RunLobby != null && network.IsConnected,
                "Both true peer roles own one accepted punished Hive Boss combat root.");
            foreach (var owner in owners) { owner.Creature.SetMaxHpInternal(2000); owner.Creature.SetCurrentHpInternal(2000); }
            if (!nativeTurn)
            {
                monster.SetUpForCombat();
                monster.RollMove(combat.PlayerCreatures);
            }
            Check(monster.NextMove.Id == "CURSE_OF_KNOWLEDGE_MOVE", "The untouched Knowledge state machine starts at Curse.");
            ui = await KnowledgeUi.Open(run, room, combat, monster);
            string beforeUnchanged = UnchangedState(run, owners, monster, combat);
            choiceSync = manager.PlayerChoiceSynchronizer;
            observeChoice = (player, choiceId, _) =>
            {
                receivedChoices.Add((player.NetId, choiceId));
                Console.WriteLine($"KNOWLEDGE_MULTI_CHOICE role={Role} owner={player.NetId} choiceId={choiceId}");
            };
            choiceSync.PlayerChoiceReceived += observeChoice;
            observedMonster = monster; moveCalls = 0;
            moveObservation ??= new Harmony("AllMatters.Tests.KnowledgeMultiOwnerMove");
            moveObservation.Patch(AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.PerformMove)),
                prefix: new HarmonyMethod(typeof(JigsawKnowledgeMultiOwnerTests), nameof(ObserveMove)));
#if STS2_PUBLIC
            Check(CardSelectCmd.Selector == null, "Public test selector cannot bypass either owner screen.");
#else
            Check(owners.All(owner => manager.CardSelectorStack.GetSelector(owner) == null) &&
                manager.CardSelectorStack.LocalSelector == null, "Beta selectors cannot bypass either owner screen.");
#endif
            Signal("ready");
            await Until(() => Both("ready"), "both mounted combat hosts");
            if (curseRounds == 3)
            {
                Task EndTurn(int round)
                {
                    expectedReadyRound = round + 1;
                    nextTurn = new TaskCompletionSource();
                    var local = owners[(int)network.NetId - 1];
                    manager.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(local, local.PlayerCombatState!.TurnNumber));
                    return nextTurn.Task;
                }
                await ThreeRounds(ui, owners, monster, combat, receivedChoices, EndTurn, () => readyRound, () => readyCount);
            }
            else
            {
            Task performing;
            if (nativeTurn)
            {
                performing = nextTurn.Task;
                var localOwner = owners[(int)network.NetId - 1];
                manager.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(localOwner, localOwner.PlayerCombatState!.TurnNumber));
            }
            else performing = monster.PerformMove();
            await Until(() => ui.Screen != null || performing.IsCompleted, "local first curse screen");
            if (performing.IsFaulted) await performing;
            ui.AssertPending(performing, owners[(int)network.NetId - 1]);
            Signal("screen");
            await Until(() => Both("screen"), "both native choice screens");
            if (nativeTurn)
                Check(readyCount == 1 && readyRound == 1 && combat.CurrentSide == CombatSide.Enemy && moveCalls == 1,
                    "The original enemy turn entered one PerformMove and has not returned while either owner is choosing.");
            if (IsHost)
            {
                await ui.ClickChoice<KnowledgeCalamityChoice>();
                await Until(() => ui.Removals == 1 && ui.ScreenFreed &&
                    owners[0].Creature.GetPowerAmount<CalamityPower>() == 1, "host screen and chosen power");
                Check(ui.CombatFocused && !performing.IsCompleted &&
                    owners[1].Creature.GetPowerAmount<CalamityPower>() == 0,
                    "Host focus returns to combat, but its choice cannot complete or misapply to the waiting child.");
                Signal("host-selected");
                await Until(() => Signaled("2", "pending"), "remote pending check");
                Check(!performing.IsCompleted && owners[1].Creature.GetPowerAmount<MindRotPower>() == 0,
                    "The host still awaits the unchosen remote owner.");
                if (nativeTurn)
                    Check(readyCount == 1 && combat.CurrentSide == CombatSide.Enemy && moveCalls == 1,
                        "The coordinator remains in its first enemy action after host Calamity while the child is pending.");
                Signal("release-child");
            }
            else
            {
                await Until(() => Signaled("host", "host-selected"), "host selected Calamity");
                await Until(() => owners[0].Creature.GetPowerAmount<CalamityPower>() == 1,
                    "host choice synchronizes to the child");
                Check(ui.Screen != null && !ui.SelectionTask!.IsCompleted && !performing.IsCompleted &&
                    owners[1].Creature.GetPowerAmount<MindRotPower>() == 0 &&
                    owners[1].Creature.GetPowerAmount<CalamityPower>() == 0,
                    "The child retains its own screen after host Calamity reaches only owner one.");
                Signal("pending");
                await Until(() => Signaled("host", "release-child"), "host observes pending child");
                await ui.ClickChoice<MindRot>();
                await Until(() => ui.Removals == 1 && ui.ScreenFreed, "child screen release");
            }
            await Until(() => performing.IsCompleted, "both native choices complete");
            await performing;
            Check(moveCalls == 1, nativeTurn ? "The native coordinator invoked the original monster action exactly once on this peer." :
                "This isolated harness invoked the original monster action exactly once on this peer.");
            await Until(() => owners[0].Creature.GetPowerAmount<CalamityPower>() == 1 &&
                owners[1].Creature.GetPowerAmount<MindRotPower>() == 1, "both owner powers converge");
            Check(owners[0].Creature.GetPowerAmount<MindRotPower>() == 0 &&
                owners[1].Creature.GetPowerAmount<CalamityPower>() == 0 && ui.Removals == 1 && ui.ScreenFreed &&
                ui.SelectionTask!.IsCompletedSuccessfully && ui.SelectionCalls == 1,
                "Each original screen resolves once and each owner gets only their selected power.");
            Check(receivedChoices.Count == 2 && receivedChoices.Select(choice => choice.Owner).Order()
                .SequenceEqual(new[] { 1UL, 2UL }),
                "Native choice synchronizer records one choice ID for each actual owner on this peer.");
            var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Power is CalamityPower or MindRotPower).ToArray();
            Check(received.Length == 2 &&
                received.Count(entry => entry.Power is CalamityPower && ReferenceEquals(entry.Actor, owners[0].Creature)) == 1 &&
                received.Count(entry => entry.Power is MindRotPower && ReferenceEquals(entry.Actor, owners[1].Creature)) == 1,
                "Both native OnChosen effects produce exactly one correctly owned history entry.");
            if (!nativeTurn)
                Check(UnchangedState(run, owners, monster, combat) == beforeUnchanged,
                    "Both owner decks and ordered five piles retain exact cards; run/owner/monster RNG, vitals, other powers and current move remain unchanged.");
            Check(ui.Offered.All(card => card.Pile == null && owners.All(owner => !owner.Deck.Cards.Contains(card))),
                "Choice display instances never enter either owner's deck or combat piles.");
            Check(ui.CombatFocused, "After selection this peer's native focus returns to combat.");
            if (nativeTurn)
            {
                await Until(() => readyRound == 2 && combat.CurrentSide == CombatSide.Player &&
                    PileType.Hand.GetPile(owners[0]).Cards.Count == 5 &&
                    PileType.Hand.GetPile(owners[1]).Cards.Count == 4, "next native player turn");
                await Frames(3);
                Check(readyCount == 2 && combat.RoundNumber == 2 && moveCalls == 1,
                    "The coordinator returns exactly once after both owners; no manual move or roll advances it.");
            }
            else monster.RollMove(combat.PlayerCreatures);
            Check(monster.NextMove.Id == "SLAP_MOVE" && ui.SelectionCalls == 1 && moveCalls == 1,
                nativeTurn ? "The original coordinator reaches Slap after both choices and one enemy turn." :
                "One direct original action completes both choices; one manual RollMove reaches native Slap on each peer.");
            var executor = manager.ActionExecutor;
            Console.WriteLine($"KNOWLEDGE_MULTI_EXECUTOR role={Role} paused={executor.IsPaused} running={executor.IsRunning}");
            executor.Unpause();
            await Until(() => !executor.IsRunning, "original action queue drains before scene release");
            await executor.FinishedExecutingActions();
            Console.WriteLine($"KNOWLEDGE_MULTI role={Role} local={network.NetId} selected={ui.ChosenName} " +
                $"powers={owners[0].Creature.GetPowerAmount<CalamityPower>()}/{owners[1].Creature.GetPowerAmount<MindRotPower>()} " +
                $"history={received.Length} remove={ui.Removals} freed={ui.ScreenFreed} next={monster.NextMove.Id}");
            }
            Signal("done");
            await Until(() => Both("done"), "both peer assertions");
            if (host != null)
            {
                // The choice test is complete; detach the room session before the peer leaves.
                session.Close();
                Signal("finish");
                await Until(() => host.ConnectedPeers.Count == 0, "graceful child disconnect");
                Signal("peer-released");
                await Until(() => child!.Process.HasExited, "child exit");
                Check(child!.Process.ExitCode == 0, "The real child reports success; evidence=" + evidence);
            }
            else
            {
                await Until(() => Signaled("host", "finish"), "host accepted both results");
                await ui.DisposeAsync(); ui = null;
                JigsawNativeSession.Current?.Close();
                manager.CleanUp(true); runStarted = false;
                await Until(() => Signaled("host", "peer-released"), "host consumed disconnect");
            }
        }
        finally
        {
            if (nativeHost)
            {
                Cleanup(() => CombatManager.Instance.TurnStarted -= Ready);
                Cleanup(() => hostPatches.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
                Cleanup(() => hostPatches.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
                JigsawNativeSaveHost.Save = previousSave;

            }
            if (nativeTurn) Cleanup(() => actionCache.SetValue(null, previousActionCache));
            if (moveObservation != null) Cleanup(() => moveObservation.UnpatchAll(moveObservation.Id));
            observedMonster = null; moveCalls = 0;
            if (choiceSync != null && observeChoice != null) Cleanup(() => choiceSync.PlayerChoiceReceived -= observeChoice);
            try { if (ui != null) await ui.DisposeAsync(); }
            catch (Exception error) { errors.Add(error); }
            Cleanup(() => JigsawNativeSession.Current?.Close());
            Cleanup(() => CombatManager.Instance.Reset(true));
            if (runStarted) Cleanup(() => manager.CleanUp(false));
            Cleanup(() => { if (network?.IsConnected == true) network.Disconnect(NetError.Quit); });
            if (child != null)
            {
                try { await child.Finish(Path.Combine(evidence!, "2.log"), Path.Combine(evidence!, "2.err")); }
                catch (Exception error) { errors.Add(error); }
            }
            openingAtlases?.Dispose();
            LocalContext.NetId = previousNetId;
            network = null; childProcess = null; evidence = null;
            if (errors.Count != 0) throw new AggregateException("Knowledge multi-owner cleanup failed.", errors);
        }
    }
    private static async Task ThreeRounds(KnowledgeUi ui, Player[] owners, MonsterModel monster, CombatState combat,
        List<(ulong Owner, uint Id)> choices, Func<int, Task> endTurn, Func<int> readyRound, Func<int> readyCount)
    {
        var displays = new List<CardModel>();
        Type[] hostPowers = [typeof(CalamityPower), typeof(TenderPower), typeof(DisintegrationPower)];
        Type[] childPowers = [typeof(MindRotPower), typeof(SlothPower), typeof(WasteAwayPower)];
        decimal[] hostAmounts = [1, 1, 21], childAmounts = [1, 3, 1];
        decimal Amount(Player owner, Type power) => owner.Creature.Powers.Where(item => item.GetType() == power).Sum(item => item.Amount);
        PowerReceivedEntry[] History() => CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().ToArray();
        async Task ReturnToPlayer(Task turn, int round)
        {
            await Until(() => turn.IsCompleted, $"round {round} native player return");
            await turn;
            await Until(() => readyRound() == round + 1 && combat.CurrentSide == CombatSide.Player,
                $"round {round} player side");
            await Frames(3);
            Check(readyCount() == round + 1 && combat.RoundNumber == round + 1 && moveCalls == round,
                $"Round {round} advances exactly once through the real coordinator.");
        }
        for (int curse = 1; curse <= 3; curse++)
        {
            int round = 1 + (curse - 1) * 4, index = curse - 1;
            Check(readyRound() == round && monster.NextMove.Id == "CURSE_OF_KNOWLEDGE_MOVE",
                $"Curse {curse} is reached naturally on round {round}.");
            int historyBefore = History().Length, choicesBefore = choices.Count;
            decimal hostBefore = Amount(owners[0], hostPowers[index]), childBefore = Amount(owners[1], childPowers[index]);
            Check(hostBefore == 0 && childBefore == 0 && Amount(owners[0], childPowers[index]) == 0 &&
                Amount(owners[1], hostPowers[index]) == 0, "This pair has no previous applications to either owner.");
            string stage = $"curse-{curse}-";
            Task turn = endTurn(round);
            await Until(() => ui.SelectionCalls == curse || turn.IsCompleted, stage + "screen");
            if (turn.IsFaulted) await turn;
            ui.AssertPending(turn, owners[IsHost ? 0 : 1], curse);
            var originalScreen = ui.Screen;
            displays.AddRange(ui.Offered);
            Signal(stage + "screen");
            await Until(() => Both(stage + "screen"), stage + "both screens");
            Check(moveCalls == round && readyCount() == round && combat.CurrentSide == CombatSide.Enemy,
                "One original enemy action waits on the actual owner screens.");
            if (IsHost)
            {
                if (curse == 1) await ui.ClickChoice<KnowledgeCalamityChoice>();
                else if (curse == 2) await ui.ClickChoice<KnowledgeTenderChoice>();
                else await ui.ClickChoice<Disintegration>();
                await Until(() => ui.Removals == curse && ui.ScreenFreed &&
                    Amount(owners[0], hostPowers[index]) == hostAmounts[index], stage + "host applied and freed");
                Check(ui.CombatFocused && !turn.IsCompleted, "Host focus returns while the remote choice still blocks the turn.");
                Signal(stage + "host-selected");
                await Until(() => Signaled("2", stage + "pending"), stage + "remote pending");
                Check(!turn.IsCompleted && readyCount() == round && moveCalls == round &&
                    Amount(owners[1], childPowers[index]) == 0, "Waiting owner keeps the native enemy turn suspended.");
                Signal(stage + "release");
            }
            else
            {
                await Until(() => Signaled("host", stage + "host-selected") &&
                    Amount(owners[0], hostPowers[index]) == hostAmounts[index], stage + "host synchronized");
                Check(ReferenceEquals(originalScreen, ui.Screen) && !ui.SelectionTask!.IsCompleted && !turn.IsCompleted &&
                    ui.Removals == curse - 1 && !ui.ScreenFreed && Amount(owners[1], hostPowers[index]) == 0 &&
                    Amount(owners[1], childPowers[index]) == 0, "The child retains its original untouched screen after the host selects.");
                ui.AssertPending(turn, owners[1], curse);
                Signal(stage + "pending");
                await Until(() => Signaled("host", stage + "release"), stage + "release child");
                if (curse == 1) await ui.ClickChoice<MindRot>();
                else if (curse == 2) await ui.ClickChoice<Sloth>();
                else await ui.ClickChoice<WasteAway>();
                await Until(() => ui.Removals == curse && ui.ScreenFreed, stage + "child freed");
            }
            await ReturnToPlayer(turn, round);
            await Until(() => choices.Count == choicesBefore + 2 &&
                Amount(owners[0], hostPowers[index]) == hostAmounts[index] &&
                Amount(owners[1], childPowers[index]) == childAmounts[index], stage + "effects converge");
            var applications = History().Skip(historyBefore).Where(entry =>
                entry.Power.GetType() == hostPowers[index] || entry.Power.GetType() == childPowers[index]).ToArray();
            Check(applications.Length == 2 && applications.Count(entry => entry.Power.GetType() == hostPowers[index] &&
                ReferenceEquals(entry.Actor, owners[0].Creature) && ReferenceEquals(entry.Applier, owners[0].Creature) && entry.Amount == hostAmounts[index]) == 1 &&
                applications.Count(entry => entry.Power.GetType() == childPowers[index] &&
                ReferenceEquals(entry.Actor, owners[1].Creature) && ReferenceEquals(entry.Applier, owners[1].Creature) && entry.Amount == childAmounts[index]) == 1,
                "Each OnChosen records exactly one independently specified amount, actor and applier.");
            Check(choices.Skip(choicesBefore).Select(choice => choice.Owner).Order().SequenceEqual(new[] { 1UL, 2UL }) &&
                Amount(owners[0], childPowers[index]) == 0 && Amount(owners[1], hostPowers[index]) == 0 &&
                ui.SelectionTask!.IsCompletedSuccessfully && ui.SelectionCalls == curse && ui.Removals == curse && ui.ScreenFreed && ui.CombatFocused,
                "Both owner IDs resolve once; the unselected powers remain absent and each screen is freed.");
            foreach (string move in new[] { "SLAP_MOVE", "KNOWLEDGE_OVERWHELMING_MOVE", "PONDER_MOVE" })
            {
                round++;
                Check(monster.NextMove.Id == move, $"Natural round {round} reaches {move}.");
                await ReturnToPlayer(endTurn(round), round);
                Check(ui.SelectionCalls == curse && ui.Removals == curse && choices.Count == curse * 2,
                    "The intervening original action opens no extra choice screen.");
            }
            Console.WriteLine($"KNOWLEDGE_THREE role={Role} curse={curse} round={combat.RoundNumber} moves={moveCalls} choices={choices.Count} removals={ui.Removals} next={monster.NextMove.Id}");
        }
        Check(monster.NextMove.Id == "SLAP_MOVE" && combat.RoundNumber == 13 && ui.SelectionCalls == 3 && choices.Count == 6,
            "The third Ponder returns to Slap with no fourth Curse screen.");
        Check(displays.Count == 6 && displays.All(card => card.Pile == null && owners.All(owner => !owner.Deck.Cards.Contains(card))) &&
            ModelDb.Card<Disintegration>().DynamicVars["DisintegrationPower"].BaseValue == 6,
            "All six local display instances stay outside decks and piles; canonical Disintegration remains six.");
        await Until(() => !RunManager.Instance.ActionExecutor.IsRunning, "three-round action queue drain");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
    }

}

internal sealed class KnowledgeUi
{
    private static KnowledgeUi? active;
    private readonly Window viewport = ((SceneTree)Engine.GetMainLoop()).Root;
    private readonly Vector2I size;
    private readonly Vector2I content;
    private readonly Window.ContentScaleModeEnum mode;
    private readonly Window.ContentScaleAspectEnum aspect;
    private readonly bool testing;
    private readonly Func<bool> autoSlayer;
    private readonly System.Collections.IDictionary pools;
    private readonly object? previousCards;
    private readonly object? previousHolders;
    private readonly System.Reflection.FieldInfo consoleField;
    private readonly object? previousConsole;
    private readonly List<StringName> addedActions = [];
    private readonly Harmony patches = new("AllMatters.Tests.KnowledgeMultiOwnerUi");
    private IDisposable? atlases;
    private JigsawNativeMapUiHost? ui;
    private NMapScreen? map;
    private NOverlayStack? overlays;
    private NCapstoneContainer? capstone;
    private NSceneContainer? rooms;
    private NHotkeyManager? hotkeys;
    private NControllerManager? controller;
    private NCursorManager? cursor;
    private NTargetManager? target;
    private NMultiplayerPlayerStateContainer? players;
    private Node? hoverTips;
    private MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole? console;
    private MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake? shake;
    private NodePool<NCard>? cardsPool;
    private NodePool<NGridCardHolder>? holdersPool;
    private NCombatRoom? combatNode;
    private bool replacedPools;
    private bool disposed;
    private bool screenFreed;
    private NChooseACardSelectionScreen? selectedScreen;
    private Task<IEnumerable<CardModel>>? selectionTask;
    private CardModel[] offered = [];
    private int selectionCalls;
    private int removals;

    internal NChooseACardSelectionScreen? Screen => selectedScreen;
    internal Task<IEnumerable<CardModel>>? SelectionTask => selectionTask;
    internal IReadOnlyList<CardModel> Offered => offered;
    internal int SelectionCalls => selectionCalls;
    internal int Removals => removals;
    internal bool ScreenFreed => screenFreed && selectedScreen != null && !GodotObject.IsInstanceValid(selectedScreen);
    internal bool CombatFocused => combatNode != null && ActiveScreenContext.Instance.IsCurrent(combatNode);
    internal string? ChosenName { get; private set; }

    private KnowledgeUi()
    {
        size = viewport.Size; content = viewport.ContentScaleSize;
        mode = viewport.ContentScaleMode; aspect = viewport.ContentScaleAspect;
        testing = TestMode.IsOn;
        autoSlayer = NonInteractiveMode.AutoSlayerCheck;
        consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
        previousConsole = consoleField.GetValue(null);
        pools = (System.Collections.IDictionary)AccessTools.Field(typeof(NodePool), "_pools").GetValue(null)!;
        previousCards = pools[typeof(NCard)]; previousHolders = pools[typeof(NGridCardHolder)];
    }

    private static bool SkipNativeLifecycle() => false;
    private static bool SkipAudio() => false;
    private static bool CreatureNode(MegaCrit.Sts2.Core.Entities.Creatures.Creature __instance, ref NCreature? __result)
    {
        __result = active?.combatNode?.GetCreatureNode(__instance);
        return false;
    }
    private static void ObserveSelection(NChooseACardSelectionScreen __instance, Task<IEnumerable<CardModel>> __result)
    {
        var owner = active;
        if (owner == null) return;
        owner.screenFreed = false;
        owner.selectedScreen = __instance;
        owner.selectionTask = __result;
        owner.selectionCalls++;
        __instance.TreeExited += () => owner.screenFreed = true;
    }
    private static void ObserveRemoval(IOverlayScreen screen)
    {
        if (ReferenceEquals(screen, active?.selectedScreen)) active!.removals++;
    }

    internal static async Task<KnowledgeUi> Open(RunState run, CombatRoom room, CombatState combat, MonsterModel monster)
    {
        var host = new KnowledgeUi();
        active = host;
        try { await host.Mount(run, room, combat, monster); return host; }
        catch { await host.DisposeAsync(); throw; }
    }

    private async Task Mount(RunState run, CombatRoom room, CombatState combat, MonsterModel monster)
    {
        viewport.ContentScaleSize = new Vector2I(1920, 1080);
        viewport.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        viewport.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        viewport.Size = new Vector2I(1920, 1080);
        HeadlessUiHost.EnsureInputActions();
        foreach (string name in new[] { "debug_hide_intents", "debug_hide_combat_ui", "debug_hide_play_container", "debug_hide_hand",
            "debug_hide_hp_bars", "debug_hide_text_vfx", "debug_hide_targeting_ui", "debug_slow_rewards", "debug_hide_mp_targeting", "debug_hide_mp_intents" }
            .Concat(Enumerable.Range(1, 10).Select(index => "mega_select_card_" + index))
            .Concat(new[] { DebugHotkey.speedUp.ToString(), DebugHotkey.speedDown.ToString(),
                DebugHotkey.hideProceedButton.ToString(), DebugHotkey.hideHoverTips.ToString() }))
        {
            StringName action = name;
            if (InputMap.HasAction(action)) continue;
            InputMap.AddAction(action); addedActions.Add(action);
        }
        atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        JigsawHuntingNativeTests.RegisterScripts();
        ui = new JigsawNativeMapUiHost();
        RunManager.Instance.GenerateRooms();
        await ui.Open(JigsawNativeSession.TryGet(run)!);
        JigsawNativeSession.TryGet(run)!.HideMap();
        var game = NGame.Instance!;
        var nativeRun = NRun.Instance!;
        nativeRun.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
        hotkeys = new NHotkeyManager();
        AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
        await HeadlessUiHost.Mount(hotkeys);
        controller = new NControllerManager();
        AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, controller);
        cursor = new NCursorManager();
        AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, cursor);
        target = new NTargetManager();
        // The selection uses this container only for nullable hover callbacks.
        // The full multiplayer top bar needs a RelicInventory scene absent here.
        players = new NMultiplayerPlayerStateContainer();
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(nativeRun.GlobalUi, target);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(nativeRun.GlobalUi, players);
        console = new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false };
        consoleField.SetValue(null, console);
        shake = new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake();
        AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, shake);
        foreach (string method in new[] { "_EnterTree", "_Ready", "_ExitTree" })
            patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(SkipNativeLifecycle)));
        // This headless camera has no authored shake presets; damage and its hooks stay native.
        patches.Patch(AccessTools.Method(typeof(NGame), nameof(NGame.ScreenShake), [typeof(ShakeStrength), typeof(ShakeDuration), typeof(float)]),
            prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(SkipNativeLifecycle)));
        game.MouseFilter = Control.MouseFilterEnum.Ignore;
        hoverTips = new Node { Name = "HoverTipsContainer" };
        game.AddChild(hoverTips);
        AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, hoverTips);
        patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]),
            prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(SkipAudio)));
        patches.Patch(AccessTools.Method(typeof(NMapScreen), "_Ready"), prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(SkipNativeLifecycle)));
        patches.Patch(AccessTools.Method(typeof(NMapScreen), "_Process"), prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(SkipNativeLifecycle)));
        map = new NMapScreen();
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(nativeRun.GlobalUi, map);
        capstone = new NCapstoneContainer();
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(nativeRun.GlobalUi, capstone);
        overlays = new NOverlayStack { ProcessMode = Node.ProcessModeEnum.Always, MouseFilter = Control.MouseFilterEnum.Ignore };
        var backstop = new Control { Name = "OverlayBackstop" };
        overlays.AddChild(backstop);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(nativeRun.GlobalUi, overlays);
        AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(nativeRun, new ScreenStateTracker(map, capstone, overlays));
        nativeRun.GlobalUi.AddChild(map); map.Hide(); map.SetProcess(false);
        nativeRun.GlobalUi.AddChild(overlays);
        overlays.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rooms = new NSceneContainer { ProcessMode = Node.ProcessModeEnum.Always };
        AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(nativeRun, rooms);
        nativeRun.AddChild(rooms);
        rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await HeadlessUiHost.Mount(game);
        game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        nativeRun.MoveChild(nativeRun.GlobalUi, nativeRun.GetChildCount() - 1);
        replacedPools = true;
        pools.Remove(typeof(NCard)); pools.Remove(typeof(NGridCardHolder));
        cardsPool = NodePool.Init<NCard>(NCard.AssetPaths.First(), 0);
        holdersPool = NodePool.Init<NGridCardHolder>(NGridCardHolder.AssetPaths.Single(), 0);
        TestMode.IsOn = false;
        NonInteractiveMode.AutoSlayerCheck = () => false;
        combatNode = NCombatRoom.Create(room, CombatRoomMode.ActiveCombat)!;
        AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(rooms, combatNode);
        rooms.AddChild(combatNode);
        combatNode.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        combatNode.Ui.Activate(combat);
        patches.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature), "GetCreatureNode"),
            prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(CreatureNode)));
        patches.Patch(AccessTools.Method(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen.CardsSelected)),
            postfix: new HarmonyMethod(typeof(KnowledgeUi), nameof(ObserveSelection)));
        patches.Patch(AccessTools.Method(typeof(NOverlayStack), nameof(NOverlayStack.Remove)),
            prefix: new HarmonyMethod(typeof(KnowledgeUi), nameof(ObserveRemoval)));
        ActiveScreenContext.Instance.Update();
        await Frames(3);
        Check(combatNode.GetCreatureNode(monster.Creature) != null,
            "The actual native combat scene mounts the Knowledge monster in this peer.");
    }

    internal void AssertPending(Task performing, Player localOwner, int curse = 1)
    {
        Check(selectedScreen != null && selectionTask != null && !selectionTask.IsCompleted && !performing.IsCompleted,
            "Original PerformMove awaits this peer's actual owner screen.");
        Check(ReferenceEquals(overlays!.Peek(), selectedScreen) && overlays.ScreenCount == 1 &&
            ActiveScreenContext.Instance.IsCurrent(selectedScreen!), "The actual overlay and focus own this peer's pending screen.");
        var holders = selectedScreen!.GetNode<Control>("CardRow").GetChildren().OfType<NGridCardHolder>().ToArray();
        Check(holders.Length == 2 && (curse switch
            {
                1 => holders[0].CardModel is MindRot && holders[1].CardModel is KnowledgeCalamityChoice,
                2 => holders[0].CardModel is Sloth && holders[1].CardModel is KnowledgeTenderChoice,
                3 => holders[0].CardModel is WasteAway && holders[1].CardModel is Disintegration &&
                    holders[1].CardModel.DynamicVars["DisintegrationPower"].BaseValue == 21,
                _ => false
            }) &&
            holders.All(holder => ReferenceEquals(holder.CardModel.Owner, localOwner)),
            "This native screen presents the specified pair and values for its local owner and curse round.");
        offered = holders.Select(holder => holder.CardModel).ToArray();
        Console.WriteLine($"KNOWLEDGE_MULTI_SCREEN owner={localOwner.NetId} cards={string.Join(',', offered.Select(card => card.Id.Entry))}");
    }

    internal async Task ClickChoice<T>() where T : CardModel
    {
        ulong OpenedTicks() => (ulong)AccessTools.Field(typeof(NChooseACardSelectionScreen), "_openedTicks")
            .GetValue(selectedScreen)!;
        var clock = Stopwatch.StartNew();
        while (Time.GetTicksMsec() - OpenedTicks() <= 350 && clock.Elapsed < TimeSpan.FromSeconds(3))
            await Frames();
        ulong opened = OpenedTicks(), now = Time.GetTicksMsec();
        Console.WriteLine($"KNOWLEDGE_MULTI_GUARD selected={typeof(T).Name} opened={opened} now={now} " +
            $"age={now - opened} elapsed={clock.ElapsedMilliseconds} taskComplete={selectionTask!.IsCompleted}");
        Check(now - opened > 350, "The actual owner screen did not pass its native 350 ms tick guard.");
        Check(!selectionTask.IsCompleted, "Another input completed the owner's native choice before its intended click.");
        var holder = selectedScreen!.GetNode<Control>("CardRow").GetChildren().OfType<NGridCardHolder>()
            .Single(item => item.CardModel is T);
        Vector2 point = holder.Hitbox.GetGlobalRect().GetCenter();
        ChosenName = typeof(T).Name;
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Frames();
        Console.WriteLine($"KNOWLEDGE_MULTI_HOVER selected={ChosenName} hovered={viewport.GuiGetHoveredControl()?.GetPath()}");
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true,
            Position = point, GlobalPosition = point }, true);
        await Frames();
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false,
            Position = point, GlobalPosition = point }, true);
        await Frames(2);
        Console.WriteLine($"KNOWLEDGE_MULTI_CLICK selected={ChosenName} point={point} " +
            $"taskComplete={selectionTask.IsCompleted} removes={removals}");
    }

    private static async Task Frames(int count = 1)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
    private static void FreePool<T>(NodePool<T>? pool) where T : Node, IPoolable
    {
        if (pool == null) return;
        var used = (IEnumerable<T>)AccessTools.Field(typeof(NodePool<T>), "_usedObjects").GetValue(pool)!;
        foreach (var node in used.Concat(pool.DebugFreeObjects).Distinct().ToArray())
            if (GodotObject.IsInstanceValid(node)) node.Free();
    }

    internal async Task DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (overlays != null && GodotObject.IsInstanceValid(overlays)) overlays.Free();
            if (combatNode != null && GodotObject.IsInstanceValid(combatNode)) combatNode.Free();
            if (hoverTips != null && GodotObject.IsInstanceValid(hoverTips)) hoverTips.Free();
            foreach (Node? node in new Node?[] { map, capstone, rooms, hotkeys, controller, cursor, target, players, console, shake })
                if (node != null && GodotObject.IsInstanceValid(node)) node.Free();
            // Native QueueFreeSafely returns cards to the live pools next frame.
            await Frames(2);
            FreePool(holdersPool); FreePool(cardsPool);
        }
        finally
        {
            if (replacedPools)
            {
                pools.Remove(typeof(NCard)); pools.Remove(typeof(NGridCardHolder));
                if (previousCards != null) pools[typeof(NCard)] = previousCards;
                if (previousHolders != null) pools[typeof(NGridCardHolder)] = previousHolders;
            }
            consoleField.SetValue(null, previousConsole);
            try { ui?.Dispose(); }
            finally
            {
                patches.UnpatchAll(patches.Id);
                atlases?.Dispose();
                foreach (var action in addedActions) InputMap.EraseAction(action);
                TestMode.IsOn = testing; NonInteractiveMode.AutoSlayerCheck = autoSlayer;
                viewport.ContentScaleSize = content; viewport.ContentScaleMode = mode;
                viewport.ContentScaleAspect = aspect; viewport.Size = size;
                active = null;
            }
        }
    }
}
