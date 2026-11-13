using AllMatters;
using AllMatters.Encounters;
using AllMatters.Events;
using AllMatters.Jigsaw;
using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using MegaCrit.Sts2.Core.Multiplayer.Game;

internal static class JigsawHuntingHolesMultiownerTests
{
    private static readonly JsonSerializerOptions CompleteStateJson = new() { IncludeFields = true };

    private sealed class StartupObservation
    {
        internal Task? Opening;
        internal Task? Startup;
        internal int OpeningCalls;
    }
    private static StartupObservation? observedStartup;

    private sealed class PendingEntryObservation(EventOption option)
    {
        internal readonly EventOption Option = option;
        internal readonly TaskCompletionSource ChoiceReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseChoice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task? OriginalChoice;
        internal Task? WrappedChoice;
        internal Task? Entry;
        internal Task? Pending;
        internal Func<Task>? CompletedEffects;
    }
    private static PendingEntryObservation? pendingEntry;

    private static void HoldChosenTask(EventOption __instance, ref Task __result)
    {
        if (pendingEntry is not { } pending || !ReferenceEquals(__instance, pending.Option)) return;
        pending.OriginalChoice = __result;
        __result = pending.WrappedChoice = Hold(__result, pending);
        static async Task Hold(Task original, PendingEntryObservation pending)
        {
            await original;
            pending.ChoiceReached.TrySetResult();
            await pending.ReleaseChoice.Task;
            if (pending.CompletedEffects != null) await pending.CompletedEffects();
        }
    }

    private static void ObserveEntryTask(Task __result)
    {
        if (pendingEntry is { } pending) pending.Entry = __result;
    }

    private static void ObservePendingTasks(Task __result)
    {
        if (pendingEntry is { Entry: not null } pending && !__result.IsCompleted) pending.Pending = __result;
    }

    private static void CaptureOpening(Task task)
    {
        if (observedStartup is not { } observation) return;
        observation.Opening = task;
        observation.OpeningCalls++;
    }

    private static void CaptureStartup(Task task)
    {
        if (observedStartup is { } observation) observation.Startup = task;
    }

    private static IEnumerable<CodeInstruction> ObserveOpening(IEnumerable<CodeInstruction> instructions)
    {
        var hook = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart));
        var capture = AccessTools.Method(typeof(JigsawHuntingHolesMultiownerTests), nameof(CaptureOpening));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(hook)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the exact native coordinator opening Task without replacing or repeating the Hook.");
    }

    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
#if STS2_PUBLIC
            "StartCombatInternal");
#else
            "RunTurnLoopAfter");
#endif
        var capture = AccessTools.Method(typeof(JigsawHuntingHolesMultiownerTests), nameof(CaptureStartup));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the same native startup or turn-loop Task passed to RunSafely.");
    }

    private static bool SkipCombatFtue(ref bool __result) { __result = true; return false; }
    private static bool SkipProfile() => false;

    // Installed before the existing recovery lobby performs saved setup and
    // room entry, so it observes the real fresh process's first opening Task.
    internal sealed class ReloadObservation : IAsyncDisposable
    {
        private readonly StartupObservation state = new();
        private readonly Harmony patch = new("AllMatters.Tests.HolesMultiowner.ReloadStartup");
        private readonly Harmony host = new("AllMatters.TestHost");
        private bool disposed;
        internal int ReadyRound { get; private set; }
        internal Task? Opening => state.Opening;
        internal Task? Startup => state.Startup;
        internal int OpeningCalls => state.OpeningCalls;
        private void PlayerTurnReady(CombatState combat)
        {
            if (ReferenceEquals(combat.RunState, RunManager.Instance.DebugOnlyGetState()) && combat.CurrentSide == CombatSide.Player)
                ReadyRound = combat.RoundNumber;
        }
        internal ReloadObservation()
        {
            observedStartup = state;
            var start = AccessTools.Method(typeof(CombatManager), "StartCombatInternal");
            patch.Patch(AccessTools.Method(start.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(SkipCombatFtue)));
            foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(SkipProfile)));
            CombatManager.Instance.TurnStarted += PlayerTurnReady;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
        }
        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            void Cleanup(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
            Cleanup(() => CombatManager.Instance.Reset(true));
            try { await AwaitStopped(state); } catch (Exception error) { failures.Add(error); }
            Cleanup(() => CombatManager.Instance.TurnStarted -= PlayerTurnReady);
            observedStartup = null;
            Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
            Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            Cleanup(() => patch.UnpatchAll(patch.Id));
            if (failures.Count > 0) throw new AggregateException("Holes fresh-load observer cleanup failed.", failures);
        }
    }

    internal static async Task NetworkOwners(RunState run, RunSaveManager saves, bool reloaded, ReloadObservation? reload,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        if (reloaded)
        {
            if (reload == null) throw new InvalidOperationException("Fresh Holes load requires its pre-setup native opening observer.");
            await RestoredOwners(run, reload, directory, role, until, signal, signaled);
            return;
        }
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        string[] peers = ["host", "2", "3"];
        var observation = new StartupObservation();
        var patch = new Harmony("AllMatters.Tests.HolesMultiowner.NativeStartup");
        var host = new Harmony("AllMatters.TestHost");
        bool hostTouched = false;
        int readyRound = 0;
        Exception? bodyFailure = null;
        var cleanupFailures = new List<Exception>();
        void PlayerTurnReady(CombatState state)
        {
            if (ReferenceEquals(state.RunState, run) && state.CurrentSide == CombatSide.Player)
                readyRound = state.RoundNumber;
        }
        void Cleanup(string step, Action action)
        {
            try { action(); }
            catch (Exception error) { cleanupFailures.Add(new InvalidOperationException($"Holes three-process cleanup failed during {step}.", error)); }
        }
        try
        {
            var start = AccessTools.Method(typeof(CombatManager), "StartCombatInternal");
            var stateMachine = start.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(stateMachine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(SkipCombatFtue)));
            foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
                patch.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(SkipProfile)));
            observedStartup = observation;
            combatManager.TurnStarted += PlayerTurnReady;
            Check(run.Players.Select(player => player.NetId).Order().SequenceEqual(new ulong[] { 1, 2, 3 }) &&
                JigsawNativeSession.Current!.Map!.ActivePlayers.Order().SequenceEqual(new ulong[] { 1, 2, 3 }) &&
                run.Act is Hive,
                "All three authenticated production endpoints keep the same three actual owners in the real Hive run.");
            foreach (var owner in run.Players)
            {
                owner.Creature.SetMaxHpInternal(500);
                owner.Creature.SetCurrentHpInternal(500);
                for (int card = 0; card < 20; card++) owner.Deck.AddInternal(run.CreateCard<DefendIronclad>(owner));
            }
            var parent = ModelDb.Event<FieldOfManSizedHoles>();
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
            Check(run.MapPointHistory.Sum(points => points.Count) == 1,
                "The live three-process fixture reaches the shared Holes roll on its actual first history floor.");
            JigsawCheckpoint.Capture(run, AcceptedRoot(run.Players.Select(owner => owner.NetId).ToArray()));
            JigsawNativeSession.Restore(run);
            await JigsawHuntingNative.SynchronizeMasks(JigsawNativeSession.Current!);
            Check(run.Players.All(owner => owner.Relics.OfType<ArchitectMask>().Count() == 1 &&
                !owner.Relics.OfType<ArchitectMask>().Single().IsTradable),
                "Every actual owner holds its one nontradable mask before the native fight.");
            await manager.EnterRoom(new EventRoom(parent));
            manager.ActionExecutor.Unpause();
            var sync = manager.EventSynchronizer;
            var owners = sync.Events.OrderBy(model => model.Owner!.NetId).ToArray();
            Check(owners.Length == 3 && owners.All(model => model is FieldOfManSizedHoles && model.IsShared &&
                ReferenceEquals(model.Owner!.RunState, run)) && sync.IsShared,
                "Each real endpoint's own synchronizer creates all three actual owner models on the same shared page.");
            var map = JigsawNativeSession.Current!.Map!;
            string mapBefore = JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson);
            var parentRoom = run.CurrentRoom;
            int historyRooms = run.CurrentMapPointHistoryEntry!.Rooms.Count;
            int fight = sync.GetLocalEvent().CurrentOptions.ToList().FindIndex(option => option.TextKey.EndsWith(".AM_FIGHT"));
            int other = sync.GetLocalEvent().CurrentOptions.ToList().FindIndex(option => !option.TextKey.EndsWith(".AM_FIGHT"));
            Check(fight >= 0 && other >= 0 && owners.All(model => model.CurrentOptions[fight].TextKey.EndsWith(".AM_FIGHT")),
                "The three native owners expose the same initial-page AM_FIGHT option.");
            var pending = pendingEntry = new PendingEntryObservation(owners[2].CurrentOptions[fight]);
            patch.Patch(AccessTools.Method(typeof(EventOption), nameof(EventOption.Chosen)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(HoldChosenTask)));
            patch.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveEntryTask)));
            patch.Patch(AccessTools.Method(sync.GetType(), nameof(sync.AwaitPendingOptionTasks)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObservePendingTasks)));
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            string playersBefore = PlayersContents(run);
            string runRngBefore = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
            string ownerRngBefore = OwnersRngContents(owners);
            string parentBefore = ParentsContents(owners);
            void NoAcceptedFight(string reason)
            {
                Check(ReferenceEquals(run.CurrentRoom, parentRoom) && run.CurrentRoomCount == 1 &&
                    observation.OpeningCalls == 0 && !JigsawEventCombatCheckpoint.HasPoint(run) &&
                    !HolesPageState(run).AcceptedFight && pending.OriginalChoice == null &&
                    PlayersContents(run) == playersBefore && OwnersRngContents(owners) == ownerRngBefore &&
                    JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson) == runRngBefore &&
                    ParentsContents(owners) == parentBefore && JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson) == mapBefore &&
                    run.CurrentMapPointHistoryEntry.Rooms.Count == historyRooms, reason);
            }
            signal("holes-event-ready");
            await until(() => peers.All(peer => signaled(peer, "holes-event-ready")), "all three actual endpoints enter the shared Holes event");
            if (role == "host") sync.ChooseLocalOption(fight);
            await until(() => sync.GetPlayerVote(run.Players.Single(player => player.NetId == 1)) == (uint)fight,
                "the real host vote reaches every native synchronizer");
            NoAcceptedFight("One actual host ballot cannot execute any owner or capture half a fight.");
            signal("holes-host-only");
            await until(() => peers.All(peer => signaled(peer, "holes-host-only")), "all three endpoints confirm the unexecuted host-only ballot");
            if (role == "2") sync.ChooseLocalOption(other);
            await until(() => sync.GetPlayerVote(run.Players.Single(player => player.NetId == 2)) == (uint)other,
                "the second real endpoint's mixed vote crosses the native transport");
            if (role == "3") sync.ChooseLocalOption(fight);
            await until(() => sync.GetPlayerVote(run.Players.Single(player => player.NetId == 3)) == (uint)fight,
                "the final real endpoint supplies the third genuinely mixed ballot");
            NoAcceptedFight("All three real mixed ballots leave every owner, parent, root and RNG unchanged with no fight.");
            signal("holes-mixed");
            await until(() => peers.All(peer => signaled(peer, "holes-mixed")), "all real endpoints reject mixed native votes before correction");
            string? completedPlayers = null;
            string? completedRunRng = null;
            string? completedOwnerRng = null;
            int finalGold = run.Players.Single(owner => owner.NetId == 3).Gold;
            pending.CompletedEffects = async () =>
            {
                await PlayerCmd.GainGold(37, owners[2].Owner!);
                owners[2].Rng.NextInt(100);
                run.Rng.Niche.NextInt(100);
                completedPlayers = PlayersContents(run);
                completedRunRng = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
                completedOwnerRng = OwnersRngContents(owners);
            };
            if (role == "2") sync.ChooseLocalOption(fight);
            await until(() => pending.ChoiceReached.Task.IsCompletedSuccessfully && pending.Entry != null &&
                pending.Pending is { IsCompleted: false }, "the unanimous native vote executes three real owners but suspends entry on owner three");
            int frames = 0;
            await until(() => ++frames >= 8, "native transport and frames keep running while the final owner is genuinely pending");
            var actualTasks = ((IEnumerable<Task>)AccessTools.Field(typeof(EventSynchronizer), "_pendingOptionTasks").GetValue(sync)!).ToArray();
            Check(actualTasks.Length == 3 && actualTasks.Count(task => !task.IsCompleted) == 1 &&
                pending.OriginalChoice!.IsCompletedSuccessfully && pending.WrappedChoice is { IsCompleted: false } &&
                pending.Entry is { IsCompleted: false } && run.CurrentRoom is EventRoom && run.CurrentRoomCount == 1 &&
                observation.OpeningCalls == 0 && !JigsawEventCombatCheckpoint.HasPoint(run) && !HolesPageState(run).AcceptedFight &&
                PlayersContents(run) == playersBefore && JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson) == runRngBefore &&
                OwnersRngContents(owners) == ownerRngBefore && JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson) == mapBefore &&
                run.CurrentMapPointHistoryEntry.Rooms.Count == historyRooms,
                "Three actual owner tasks are tracked, only the last remains pending, and no partial point, opening, child history or second root charge appears.");
            signal("holes-owner-held");
            await until(() => peers.All(peer => signaled(peer, "holes-owner-held")), "all three endpoints prove the last native owner task is still held");
            pending.ReleaseChoice.TrySetResult();
            await until(() => pending.WrappedChoice!.IsCompleted && pending.Pending!.IsCompleted && pending.Entry!.IsCompleted,
                "the final owner's complete effects finish before native child entry completes on every endpoint");
            await pending.WrappedChoice!;
            await pending.Pending!;
            await pending.Entry!;
            await until(() => observation.Opening is { IsCompleted: true }, "the exact original native opening Hook completes while transport is pumped");
            await observation.Opening!;
            var child = (CombatRoom)run.CurrentRoom!;
            await until(() => readyRound == child.CombatState.RoundNumber && readyRound >= 1 &&
                run.Players.All(owner => PileType.Hand.GetPile(owner).Cards.Count == 5),
                "three actual combat players complete their one native opening and full first setup");
            Check(observation.Opening!.IsCompletedSuccessfully && observation.Startup != null &&
                child.CombatState.PlayerCreatures.Count() == 3 && run.Players.All(owner => owner.PlayerCombatState != null) &&
                ReferenceEquals(run.BaseRoom, parentRoom) && run.CurrentRoomCount == 2 &&
                child is { Encounter: HolesEncounter, ShouldResumeParentEventAfterCombat: true } && child.ParentEventId == parent.Id &&
                observation.OpeningCalls == 1 && JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson) == mapBefore &&
                run.CurrentMapPointHistoryEntry.Rooms.Count == historyRooms + 1,
                "Three actual combat players share one resumable child and one observed opening, with one child history row and no extra logical charge.");
            foreach (var owner in run.Players) AssertOpening(owner);
            Check(completedPlayers != null && completedRunRng != null && completedOwnerRng != null &&
                completedPlayers != playersBefore && completedRunRng != runRngBefore && completedOwnerRng != ownerRngBefore &&
                run.Players.Single(owner => owner.NetId == 3).Gold == finalGold + 37,
                "The last real owner's delayed gold, event RNG and complete run RNG effects finish before acceptance.");
            Check(JigsawCheckpoint.Slot.TryGet(run, out var point) && point.EventCombat != null &&
                point.EventCombat.Owners.Keys.Order().SequenceEqual(new ulong[] { 1, 2, 3 }),
                "The complete checkpoint retains all three actual event owners.");
            Check(PointOwnersContents(point.EventCombat!) == completedOwnerRng &&
                HolesPageState(run).AcceptedFight && PageOwnersContents(run) == completedOwnerRng,
                "Both accepted checkpoint and Holes parent page preserve every owner's full independent RNG, including owner three's delayed effect.");
            var before = JigsawEventCombatCheckpoint.Before(run);
            Check(before != null && JsonSerializer.Serialize(before.Players.ToArray(), CompleteStateJson) == completedPlayers &&
                JsonSerializer.Serialize(before.SerializableRng, CompleteStateJson) == completedRunRng,
                "The actual source checkpoint contains all complete pre-opening players and run RNG after the final owner's effects.");
            string livePlayers = PlayersContents(run);
            var checkpointPlayers = JsonNode.Parse(completedPlayers!)!.AsArray();
            var synchronizedPlayers = checkpointPlayers.DeepClone().AsArray();
            var maskId = JsonSerializer.SerializeToNode(ModelDb.Relic<ArchitectMask>().Id, CompleteStateJson)!;
            var maskDiscovery = JsonSerializer.SerializeToNode(new[] { ModelDb.Relic<ArchitectMask>().Id }, CompleteStateJson)!;
            ulong localId = role == "host" ? 1UL : ulong.Parse(role);
            Check(checkpointPlayers.Count == 3 && checkpointPlayers.All(player =>
                player!["relics"]!.AsArray().Count(relic => JsonNode.DeepEquals(relic!["id"], maskId)) == 1 &&
                JsonNode.DeepEquals(player["discovered_relics"],
                    player["net_id"]!.GetValue<ulong>() == localId ? maskDiscovery : new JsonArray())),
                "The complete pre-opening checkpoint gives all three owners their mask while only this endpoint records its own discovery.");
            foreach (var player in synchronizedPlayers)
                player!["discovered_relics"] = maskDiscovery.DeepClone();
            Check(JsonNode.DeepEquals(synchronizedPlayers, JsonNode.Parse(livePlayers)),
                "The original combat sync changes only remote mask discovery; every other complete player field remains identical to the checkpoint.");
            string liveRunRng = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
            string liveOwnersRng = OwnersRngContents(owners);
            string liveMonsters = JigsawHuntingCombatTests.Opening(child);
            string liveMonsterRng = MonsterRngContents(child);
            string liveParents = ParentsContents(owners);
            string acceptedPoint = JsonSerializer.Serialize(point, CompleteStateJson);
            await JigsawNativeSaveHost.Save!(null);
            var saved = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            Check(saved.Success && saved.SaveData != null &&
                JsonSerializer.Serialize(saved.SaveData.Players.ToArray(), CompleteStateJson) == completedPlayers &&
                JsonSerializer.Serialize(saved.SaveData.SerializableRng, CompleteStateJson) == completedRunRng,
                "Actual multiplayer save canonicalization keeps all three completed owners and all pre-opening run RNG without live startup leakage.");
            // Inspect the actual compressed native save's existing Ritsu document;
            // do not create a second run inside this live authenticated session.
            string savedPayload = JigsawSaveSource.Serialize(saved.SaveData!);
            using var input = new MemoryStream(Convert.FromBase64String(savedPayload));
            using var compressed = new GZipStream(input, CompressionMode.Decompress);
            var savedDocument = JsonNode.Parse(compressed)!["_ritsulib"]!["run_saved_data"]![ModEntry.ModId]!;
            var savedPoint = savedDocument["jigsaw"]!["data"]!.Deserialize<JigsawCheckpointData>(CompleteStateJson)!;
            var savedPage = savedDocument["holesPage"]!["data"]!.Deserialize<HolesSharedPage.PageState>(CompleteStateJson)!;
            Check(savedPoint.EventCombat != null && PointOwnersContents(savedPoint.EventCombat) == completedOwnerRng &&
                savedPage.AcceptedFight && JsonSerializer.Serialize(savedPage.FightOwners.OrderBy(pair => pair.Key)
                    .Select(pair => new { NetId = pair.Key, Rng = RngContents(pair.Value.Restore()) }).ToArray(), CompleteStateJson) == completedOwnerRng,
                "The actual saved Ritsu document retains all three full owner RNG records in both accepted parent states.");
            Check(PlayersContents(run) == livePlayers && JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson) == liveRunRng &&
                OwnersRngContents(owners) == liveOwnersRng && JigsawHuntingCombatTests.Opening(child) == liveMonsters &&
                MonsterRngContents(child) == liveMonsterRng && ParentsContents(owners) == liveParents &&
                JsonSerializer.Serialize(point, CompleteStateJson) == acceptedPoint &&
                JsonSerializer.Serialize(map.CreateSave(), CompleteStateJson) == mapBefore,
                "Saving changes no live complete player, run/owner/monster RNG, enemy opening, parent or accepted root state.");
            var actualState = OpeningState(run, child);
            string actual = actualState.ToJsonString(CompleteStateJson);
            File.WriteAllText(Path.Combine(directory, role + ".holes-opening.json"), actual);
            File.WriteAllText(Path.Combine(directory, role + ".holes-saved.json"), savedPayload);
            File.WriteAllText(Path.Combine(directory, role + ".holes-process"), Environment.ProcessId.ToString());
            signal("holes-saved");
            await until(() => peers.All(peer => signaled(peer, "holes-saved")), "all three real endpoints complete their native save and full opening evidence");
            foreach (string peer in peers)
                Check(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-opening.json"))),
                    JsonNode.Parse(actual)),
                    "All three production endpoints agree on full players, every RNG, enemies, parents, checkpoint, logical root and history.");
            if (role == "host")
            {
                // Native load omits empty choice Variables; normalize only this
                // actual history object through the game's own TypeInfo once.
                var historyType = JsonSerializationUtility.GetTypeInfo<MapPointHistoryEntry>();
                var savedHistory = JsonSerializer.Deserialize(JsonSerializer.Serialize(run.CurrentMapPointHistoryEntry!, historyType), historyType)!;
                var expected = actualState.DeepClone();
                expected["Players"] = JsonNode.Parse(completedPlayers);
                expected["History"] = JsonSerializer.SerializeToNode(savedHistory, CompleteStateJson);
                expected["Parents"] = ExpectedLockedParents(owners);
                File.WriteAllText(Path.Combine(directory, "combat-checkpoint.json"), savedPayload);
                File.WriteAllText(Path.Combine(directory, "event-opening.json"), JsonSerializer.Serialize(new
                {
                    State = expected,
                    SourceProcesses = peers.ToDictionary(peer => peer,
                        peer => int.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-process"))))
                }, CompleteStateJson));
            }
            pending.ReleaseChoice.TrySetResult();
            Check(observation.OpeningCalls == 1 && run.CurrentRoomCount == 2 && ReferenceEquals(run.CurrentRoom, child),
                "An already released owner source cannot enter another child or apply a second opening.");
            Console.WriteLine($"HOLES_CHECKPOINT_ENET role={role} original=3 active=3 native-owner-tasks=3 delayed-owner=3 opening=1 capture=complete");
            signal("holes-capture-complete");
            await until(() => peers.All(peer => signaled(peer, "holes-capture-complete")), "all three real endpoints finish capture assertions before cleanup");
        }
        catch (Exception error) { bodyFailure = error; }
        finally
        {
            pendingEntry?.ReleaseChoice.TrySetResult();
            // The shared recovery harness owns native session and transport
            // cleanup; this fixture only drains its tasks and resets combat.
            if (pendingEntry is { } pending)
                foreach (var task in new[] { pending.OriginalChoice, pending.WrappedChoice, pending.Pending, pending.Entry }.OfType<Task>().Distinct())
                {
                    try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (Exception error) { cleanupFailures.Add(error); }
                }
            pendingEntry = null;
            Cleanup("native Reset", () => combatManager.Reset(true));
            try { await AwaitStopped(observation); }
            catch (Exception error) { cleanupFailures.Add(error); }
            Cleanup("TurnStarted unsubscribe", () => combatManager.TurnStarted -= PlayerTurnReady);
            observedStartup = null;
            if (hostTouched)
            {
                Cleanup("IsInProgress host restore", () => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
                Cleanup("IsEnding host restore", () => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            }
            Cleanup("test patch removal", () => patch.UnpatchAll(patch.Id));
        }
        if (bodyFailure != null)
        {
            if (cleanupFailures.Count > 0) bodyFailure.Data["HolesMultiownerCleanupFailures"] = new AggregateException(cleanupFailures);
            ExceptionDispatchInfo.Capture(bodyFailure).Throw();
        }
        if (cleanupFailures.Count > 0) throw new AggregateException("Holes multiowner cleanup failed.", cleanupFailures);
    }

    private static async Task RestoredOwners(RunState run, ReloadObservation observation,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        Exception? bodyFailure = null;
        try
        {
            string[] peers = ["host", "2", "3"];
            var manager = RunManager.Instance;
            var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "event-opening.json")))!;
            Check(expected["SourceProcesses"]![role]!.GetValue<int>() != Environment.ProcessId,
                "Each endpoint is a new process loading its predecessor's real complete save, with independent native combat objects.");
            Check(run.CurrentRoomCount == 2 && run.BaseRoom is EventRoom { CanonicalEvent: FieldOfManSizedHoles } parent &&
                run.CurrentRoom is CombatRoom { Encounter: HolesEncounter, ShouldResumeParentEventAfterCombat: true } room &&
                room.ParentEventId == parent.CanonicalEvent.Id && room.CombatState.PlayerCreatures.Count() == 3,
                "The real fresh three-process native load lobby rebuilds the matching Holes parent and new three-player child together.");
            var child = (CombatRoom)run.CurrentRoom!;
            await until(() => observation.Opening is { IsCompleted: true }, "the fresh process's original native opening Hook completes once");
            await observation.Opening!;
            await until(() => observation.ReadyRound == child.CombatState.RoundNumber && observation.ReadyRound >= 1 &&
                run.Players.All(owner => PileType.Hand.GetPile(owner).Cards.Count == 5),
                "the fresh native load completes all three players' full first setup");
            Check(observation.Opening!.IsCompletedSuccessfully && observation.OpeningCalls == 1 && observation.Startup != null,
                "The restored combat observes exactly one genuine Hook and its original native startup Task.");
            foreach (var owner in run.Players) AssertOpening(owner);
            var owners = manager.EventSynchronizer.Events.OrderBy(model => model.Owner!.NetId).ToArray();
            Check(owners.Length == 3 && owners.All(model => model is FieldOfManSizedHoles && model.IsShared && !model.IsFinished &&
                model.CurrentOptions.Count == 1 && model.CurrentOptions.All(option => option.IsLocked && !option.WasChosen &&
                    option.TextKey == "FIELD_OF_MAN_SIZED_HOLES.pages.INITIAL.options.AM_FIGHT")),
                "Every restored native owner has the unfinished dedicated fight parent with its old AM_FIGHT action locked and unchosen.");
            var actual = OpeningState(run, child);
            File.WriteAllText(Path.Combine(directory, role + ".holes-restore-actual.json"), actual.ToJsonString(CompleteStateJson));
            File.WriteAllText(Path.Combine(directory, role + ".holes-restore-expected.json"), expected["State"]!.ToJsonString(CompleteStateJson));
            Check(JsonNode.DeepEquals(actual, expected["State"]),
                "The fresh real load restores full original players, every run and owner RNG, full monster RNG and opening, finite locked parents, checkpoint, accepted root and native history.");
            var combatState = child.CombatState;
            foreach (var option in owners.SelectMany(model => model.CurrentOptions).ToArray())
            {
                await option.Chosen();
                Check(!option.WasChosen, "Each actual retained locked action remains unchosen after its native Chosen method is invoked.");
            }
            Check(ReferenceEquals(run.CurrentRoom, child) && ReferenceEquals(child.CombatState, combatState) &&
                run.CurrentRoomCount == 2 && observation.OpeningCalls == 1 && JsonNode.DeepEquals(OpeningState(run, child), actual),
                "Invoking all actual retained old choices cannot replay combat, change any complete player/RNG/parent/checkpoint/root/history, or apply another opening.");
            File.WriteAllText(Path.Combine(directory, role + ".holes-restored.json"), actual.ToJsonString(CompleteStateJson));
            signal("holes-restore-complete");
            await until(() => peers.All(peer => signaled(peer, "holes-restore-complete")), "all three fresh endpoints finish complete restoration assertions before cleanup");
            foreach (string peer in peers)
                Check(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-restored.json"))), actual),
                    "The three fresh production endpoints agree on the same entire restored opening point.");
            Console.WriteLine($"HOLES_CHECKPOINT_ENET role={role} original=3 active=3 fresh-process=true opening=1 parent=locked restore=complete");
        }
        catch (Exception error) { bodyFailure = error; }
        finally
        {
            try { await observation.DisposeAsync(); }
            catch (Exception error)
            {
                if (bodyFailure == null) bodyFailure = error;
                else bodyFailure.Data["HolesReloadCleanupFailure"] = error;
            }
        }
        if (bodyFailure != null) ExceptionDispatchInfo.Capture(bodyFailure).Throw();
    }

    private static JsonNode OpeningState(RunState run, CombatRoom child) => JsonSerializer.SerializeToNode(new
    {
        Players = JsonNode.Parse(PlayersContents(run)),
        RunRng = JsonSerializer.SerializeToNode(run.Rng.ToSerializable(), CompleteStateJson),
        OwnerRng = JsonNode.Parse(OwnersRngContents(RunManager.Instance.EventSynchronizer.Events)),
        MonsterRng = JsonNode.Parse(MonsterRngContents(child)),
        Enemies = JsonNode.Parse(JigsawHuntingCombatTests.Opening(child)),
        Parents = JsonNode.Parse(ParentsContents(RunManager.Instance.EventSynchronizer.Events)),
        Point = JsonSerializer.SerializeToNode(JigsawCheckpoint.Slot.Get(run), CompleteStateJson),
        Page = JsonSerializer.SerializeToNode(HolesPageState(run), CompleteStateJson),
        Map = JsonSerializer.SerializeToNode(JigsawNativeSession.TryGet(run)!.Map!.CreateSave(), CompleteStateJson),
        History = run.CurrentMapPointHistoryEntry
    }, CompleteStateJson)!;

    private static JsonNode ExpectedLockedParents(IEnumerable<EventModel> owners) => JsonSerializer.SerializeToNode(
        owners.OrderBy(model => model.Owner!.NetId).Select(model => new
        {
            model.Owner!.NetId, model.Id, IsFinished = false,
            Description = new MegaCrit.Sts2.Core.Localization.LocString("events", "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description").GetRawText(),
            Options = new[] { new { TextKey = "FIELD_OF_MAN_SIZED_HOLES.pages.INITIAL.options.AM_FIGHT", IsLocked = true, WasChosen = false } }
        }).ToArray(), CompleteStateJson)!;

    private static string PlayersContents(RunState run) => JsonSerializer.Serialize(
        run.Players.OrderBy(owner => owner.NetId).Select(owner => owner.ToSerializable()).ToArray(), CompleteStateJson);
    private static string OwnersRngContents(IEnumerable<EventModel> owners) => JsonSerializer.Serialize(
        owners.OrderBy(model => model.Owner!.NetId).Select(model => new { model.Owner!.NetId, Rng = RngContents(model.Rng) }).ToArray(), CompleteStateJson);
    private static string PointOwnersContents(JigsawEventCombatPoint point) => JsonSerializer.Serialize(
        point.Owners.OrderBy(pair => pair.Key).Select(pair => new { NetId = pair.Key, Rng = CheckpointOwnerRng(pair.Value) }).ToArray(), CompleteStateJson);
    private static string PageOwnersContents(RunState run) => JsonSerializer.Serialize(
        HolesPageState(run).FightOwners.OrderBy(pair => pair.Key).Select(pair => new { NetId = pair.Key, Rng = RngContents(pair.Value.Restore()) }).ToArray(), CompleteStateJson);
    private static string ParentsContents(IEnumerable<EventModel> owners) => JsonSerializer.Serialize(
        owners.OrderBy(model => model.Owner!.NetId).Select(model => new
        {
            model.Owner!.NetId, model.Id, model.IsFinished, Description = model.Description?.GetRawText(),
            Options = model.CurrentOptions.Select(option => new { option.TextKey, option.IsLocked, option.WasChosen }).ToArray()
        }).ToArray(), CompleteStateJson);
    private static HolesSharedPage.PageState HolesPageState(RunState run)
    {
        var slot = (STS2RitsuLib.RunData.RunSavedData<HolesSharedPage.PageState>)AccessTools.Field(typeof(HolesSharedPage), "Sharing").GetValue(null)!;
        return slot.Get(run);
    }
    private static string CheckpointOwnerRng(JigsawEventCombatRng rng) =>
#if STS2_PUBLIC
        RngContents(new Rng(rng.Seed, rng.Counter));
#else
        RngContents(new Rng(rng.State));
#endif

    private static async Task AwaitStopped(StartupObservation observation)
    {
        if (observation.Startup == null) return;
        try { await observation.Startup.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { } // The observed combat was explicitly reset before awaiting teardown.
    }

    private static void AssertOpening(Player player)
    {
        var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => ReferenceEquals(entry.Actor, player.Creature) &&
                entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();
        Check(player.Creature.GetPowerAmount<WeakPower>() == 1 && player.Creature.GetPowerAmount<FrailPower>() == 1 &&
            player.Creature.GetPowerAmount<VulnerablePower>() == 1 && received.Length == 3 &&
            received.Select(entry => entry.Power.GetType()).SequenceEqual(new[] { typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower) }) &&
            received.All(entry => entry.Amount == 1 && entry.Applier == null),
            "This actual combat opening grants each ordinary Hive debuff once, without retained battle stacks or corrected duplicate commands.");
    }

    private static string EventRng(EventModel model) => RngContents(model.Rng);
    private static string MonsterRngContents(CombatRoom room) => JsonSerializer.Serialize(
        room.CombatState.Enemies.Select(enemy => RngContents(enemy.Monster!.Rng)).ToArray(), CompleteStateJson);
    private static string RngContents(Rng rng) =>
#if STS2_PUBLIC
        JsonSerializer.Serialize(new { rng.Seed, rng.Counter }, CompleteStateJson);
#else
        JsonSerializer.Serialize(rng.ToSerializable(), CompleteStateJson);
#endif

    internal static RunState CreateFixtureRun(Player[] players)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            string candidate = "HUNTING_85_HOLES_CHECKPOINT" + seed;
            // Select with separate native RNGs before binding these players.
            // The live first Unknown history floor is asserted to be one,
            // and the actual initialized Holes page must still be shared.
            var rng = new Rng(new RunRngSet(candidate).Seed);
            rng.NextInt(100);
            if (rng.NextInt(100) >= 33) continue;
            var run = RunState.CreateForTest(players: players,
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                seed: candidate);
            run.CurrentActIndex = 1;
            return run;
        }
        throw new InvalidOperationException("No deterministic native shared Holes seed was found.");
    }

    private static JigsawRun AcceptedRoot(ulong[] owners)
    {
        var map = JigsawRun.Create(75, players: owners);
        foreach (ulong owner in owners) Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong owner in owners) Ok(map.CompleteAncient(owner));
        foreach (ulong owner in owners) Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss) continue;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Unknown && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(0));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentNode == target && map.CurrentRoomHunted,
            "Public room-entry transitions establish the intended safe or hunted Unknown root.");
        return map;
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
}
