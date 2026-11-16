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
    private static List<RewardsSet>? observedRewards;
    private static void ObserveRewards(RewardsSet set) => observedRewards?.Add(set);
    private static void WatchRewards(Harmony patch)
    {
        observedRewards = [];
        patch.Patch(AccessTools.Method(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.BeginRewardsSet)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingHolesMultiownerTests), nameof(ObserveRewards)));
    }

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
        internal ReloadObservation(bool rewards = false)
        {
            observedStartup = state;
            if (rewards) WatchRewards(patch);
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
            observedRewards = null;
            Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
            Cleanup(() => host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            Cleanup(() => patch.UnpatchAll(patch.Id));
            if (failures.Count > 0) throw new AggregateException("Holes fresh-load observer cleanup failed.", failures);
        }
    }

    internal static async Task NetworkOwners(RunState run, RunSaveManager saves, bool reloaded, ReloadObservation? reload, bool rewards, bool completed, string restoredMap,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        if (reloaded)
        {
            if (reload == null) throw new InvalidOperationException("Fresh Holes load requires its pre-setup native opening observer.");
            if (rewards) await RestoredRewards(run, saves, reload, completed, restoredMap, directory, role, until, signal, signaled);
            else await RestoredOwners(run, reload, restoredMap, directory, role, until, signal, signaled);
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
            if (rewards) WatchRewards(patch);
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
            // The native prefinished reward loader re-enters the last visited
            // coordinate. Match the existing single-owner Holes fixture before
            // entering the source event; battle rollback has its own load seam.
            if (rewards) run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
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
            if (rewards)
            {
                await CaptureRewards(run, saves, observation, directory, role, until, signal, signaled);
                return;
            }
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
            observedRewards = null;
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

    private static async Task CaptureRewards(RunState run, RunSaveManager saves, StartupObservation observation,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        string[] peers = ["host", "2", "3"];
        var manager = RunManager.Instance;
        var child = (CombatRoom)run.CurrentRoom!;
        var owners = manager.EventSynchronizer.Events.OrderBy(model => model.Owner!.NetId).ToArray();
        string mapBefore = JsonSerializer.Serialize(JigsawNativeSession.Current!.Map!.CreateSave(), CompleteStateJson);
        foreach (var owner in run.Players)
        {
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), owner.Creature, 11, ValueProp.Unpowered,
                child.CombatState.Enemies.First());
            await PlayerCmd.GainGold(19 + (int)owner.NetId, owner);
        }
        run.Rng.Niche.NextInt(100);
        foreach (var owner in owners) owner.Rng.NextInt(100);
        signal("holes-victory-ready");
        await until(() => peers.All(peer => signaled(peer, "holes-victory-ready")), "all replicas finish independent pre-victory effects");
        string? postPlayers = null, postRng = null, postOwners = null, postMap = null;
        JsonNode? postHistory = null;
        var previousSave = JigsawNativeSaveHost.Save;
        JigsawNativeSaveHost.Save = async room =>
        {
            if (ReferenceEquals(room, child) && child.IsPreFinished)
            {
                postPlayers = PlayersContents(run);
                postRng = JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson);
                postOwners = OwnersRngContents(owners);
                postHistory = NativeHistory(run);
                postMap = JsonSerializer.Serialize(JigsawNativeSession.Current!.Map!.CreateSave(), CompleteStateJson);
            }
            await previousSave!(room);
        };
        try
        {
            var kill = CreatureCmd.Kill(child.CombatState.Enemies.ToArray(), force: true);
            await until(() => kill.IsCompleted, "actual enemy deaths finish while native transport is pumped");
            await kill;
            // Same native finalization seam as the single-owner reward fixture.
            // No room/save flag is assigned by this test.
            if (!child.IsPreFinished)
            {
                var finish = (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes)
                    .Invoke(CombatManager.Instance, null)!;
                await until(() => finish.IsCompleted, "the real native victory finalization and save complete");
                await finish;
            }
        }
        finally { JigsawNativeSaveHost.Save = previousSave; }
        Check(postPlayers != null && postRng != null && postOwners != null && postMap == mapBefore &&
            PlayersContents(run) == postPlayers && JsonSerializer.Serialize(run.Rng.ToSerializable(), CompleteStateJson) == postRng &&
            OwnersRngContents(owners) == postOwners && JsonNode.DeepEquals(NativeHistory(run), postHistory),
            "Independent sampling immediately before the native victory save remains unchanged after all native finalization work.");
        var won = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
        Check(won.Success && won.SaveData?.PreFinishedRoom is
            { IsPreFinished: true, ShouldResumeParentEvent: true, ParentEventId: var parentId } &&
            parentId == child.ParentEventId && child.IsPreFinished &&
            JigsawCheckpoint.Slot.TryGet(run, out var point) && point.EventCombat == null &&
            JsonSerializer.Serialize(won.SaveData.Players.ToArray(), CompleteStateJson) == postPlayers &&
            JsonSerializer.Serialize(won.SaveData.SerializableRng, CompleteStateJson) == postRng &&
            PageOwnersContents(run) == postOwners &&
            JsonSerializer.Serialize(JigsawNativeSession.Current!.Map!.CreateSave(), CompleteStateJson) == mapBefore,
            "Native three-owner victory saves the full independently sampled postbattle players, run RNG and all owner RNG before any reward generation, clears rollback and preserves the accepted root.");
        Check(observedRewards is { Count: 0 }, "The actual victory save precedes every native room-end reward offer.");
        string payload = JigsawSaveSource.Serialize(won.SaveData!);
        await child.OfferRoomEndRewards();
        await until(() => observedRewards is { Count: 3 } && observedRewards.All(set => set.Rewards.All(reward => reward.IsPopulated)),
            "the original room generates and offers one complete native reward set for each actual owner");
        var sets = observedRewards!.OrderBy(set => set.Player.NetId).ToArray();
        CheckRewardSets(sets);
        Check(sets.All(set => !manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set) &&
            set.Rewards.All(reward => !reward.SuccessfullySelected)), "All three original owners remain genuinely unclaimed at the saved reward boundary.");
        var state = RewardState(run, child);
        // Fresh parent reconstruction retains the accepted finite page, rather
        // than the former session's chosen initial option instances.
        var expected = state.DeepClone();
        expected["Parents"] = ExpectedLockedParents(owners);
        expected["History"] = NativeHistory(run);
        var rewards = RewardContents(sets);
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-source.json"), state.ToJsonString(CompleteStateJson));
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-contents.json"), rewards.ToJsonString(CompleteStateJson));
        File.WriteAllText(Path.Combine(directory, role + ".holes-process"), Environment.ProcessId.ToString());
        signal("holes-reward-sampled");
        await until(() => peers.All(peer => signaled(peer, "holes-reward-sampled")), "all original endpoints independently sample the generated reward state");
        foreach (string peer in peers)
        {
            Check(JsonNode.DeepEquals(state, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-source.json")))) &&
                JsonNode.DeepEquals(rewards, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-contents.json")))),
                "All three original endpoints agree on the entire generated state and complete owner-specific reward contents.");
        }
        if (role == "host")
        {
            File.WriteAllText(Path.Combine(directory, "combat-checkpoint.json"), payload);
            File.WriteAllText(Path.Combine(directory, "event-opening.json"), JsonSerializer.Serialize(new
            {
                State = expected, Rewards = rewards,
                SourceProcesses = peers.ToDictionary(peer => peer,
                    peer => int.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-process"))))
            }, CompleteStateJson));
        }
        // Release real source stacks only after capture; skip cannot pay gold,
        // cards or potions and is not persisted over the earlier victory point.
        manager.RewardsSetSynchronizer.SkipLocalRewardsSet();
        await until(() => sets.All(set => manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)), "all source owners synchronize their cleanup skips");
        Check(PlayersContents(run) == state["Players"]!.ToJsonString(CompleteStateJson) &&
            sets.All(set => set.Rewards.All(reward => !reward.SuccessfullySelected)), "Original source cleanup pays none of the saved rewards.");
        Console.WriteLine($"HOLES_REWARD_ENET role={role} original=3 active=3 save=before-generation capture=complete");
        signal("holes-reward-capture-complete");
        await until(() => peers.All(peer => signaled(peer, "holes-reward-capture-complete")), "all source endpoints finish reward capture before cleanup");
    }

    private static async Task RestoredRewards(RunState run, RunSaveManager saves, ReloadObservation observation, bool completed, string restoredMap,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        string[] peers = ["host", "2", "3"];
        var manager = RunManager.Instance;
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "event-opening.json")))!;
        expected["State"]!["Map"]!["Players"] = JsonNode.Parse(restoredMap)!["Players"]!.DeepClone();
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-entry.json"), JsonSerializer.Serialize(new
        {
            Process = Environment.ProcessId, SourceProcess = expected["SourceProcesses"]![role],
            run.CurrentRoomCount, Current = run.CurrentRoom?.GetType().FullName, Base = run.BaseRoom?.GetType().FullName,
            BaseEvent = (run.BaseRoom as EventRoom)?.CanonicalEvent.GetType().FullName,
            CurrentRoom = run.CurrentRoom is CombatRoom entryRoom ? entryRoom.ToSerializable() : null,
            observation.OpeningCalls, Rewards = observedRewards?.Count,
            State = run.CurrentRoom is CombatRoom entryChild ? RewardState(run, entryChild) : null
        }, CompleteStateJson));
        Check(expected["SourceProcesses"]![role]!.GetValue<int>() != Environment.ProcessId &&
            run.CurrentRoomCount == 2 && run.BaseRoom is EventRoom { CanonicalEvent: FieldOfManSizedHoles } parent &&
            run.CurrentRoom is CombatRoom { Encounter: HolesEncounter, IsPreFinished: true, ShouldResumeParentEventAfterCombat: true } room &&
            room.ParentEventId == parent.CanonicalEvent.Id && observation.OpeningCalls == 0,
            "Each fresh process loads the actual native prefinished reward child and its matching Holes parent without a combat opening.");
        var child = (CombatRoom)run.CurrentRoom!;
        await until(() => observedRewards is { Count: 3 } && observedRewards.All(set => set.Rewards.All(reward => reward.IsPopulated)),
            "fresh native room loading regenerates every owner's complete reward set");
        var sets = observedRewards!.OrderBy(set => set.Player.NetId).ToArray();
        CheckRewardSets(sets);
        var actual = RewardState(run, child);
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-restore-actual.json"), actual.ToJsonString(CompleteStateJson));
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-restore-expected.json"), expected["State"]!.ToJsonString(CompleteStateJson));
        Check(JsonNode.DeepEquals(actual, expected["State"]) && JsonNode.DeepEquals(RewardContents(sets), expected["Rewards"]),
            "Fresh reward generation exactly matches independently sampled original full players, all RNG, parents, point/page, map, history, room and every gold/card/potion reward.");
        Check(sets.All(set => !manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set) && set.Rewards.All(reward => !reward.SuccessfullySelected)) &&
            manager.EventSynchronizer.Events.All(model => !model.IsFinished && model.CurrentOptions.All(option => option.IsLocked)),
            "No saved reward is preclaimed and all three accepted parents stay unfinished and locked until actual Proceed.");
        var baselines = sets.ToDictionary(set => set.Player.NetId, set => new
        {
            set.Player.Gold, Deck = set.Player.Deck.Cards.Select(card => card.ToSerializable()).ToArray(),
            Potions = set.Player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id).ToArray(),
            GoldReward = set.Rewards.OfType<GoldReward>().Sum(reward => reward.Amount),
            Cards = set.Rewards.OfType<CardReward>().Select(reward => new { reward.Cards.First().Id, reward.Cards.First().CurrentUpgradeLevel }).ToArray(),
            PotionRewards = set.Rewards.OfType<PotionReward>().Select(reward => reward.Potion!.Id).ToArray()
        });
        using var selector = HeadlessNativeApi.UseSelector(new JigsawHuntingCombatTests.TakeCardReward());
        ulong localId = role == "host" ? 1UL : ulong.Parse(role);
        var local = sets.Single(set => set.Player.NetId == localId);
        // Potion discovery is profile/local-owner state. Its ordered player
        // append is predicted from the verified preclaim source document and
        // the independent profile set before any endpoint is allowed to claim.
        var discoveries = expected["State"]!["Players"]!.AsArray().Select(player => new
        {
            NetId = player!["net_id"]!.GetValue<ulong>(),
            Potions = player["discovered_potions"]!.DeepClone().AsArray()
        }).ToArray();
        var seenPotions = SaveManager.Instance.Progress.DiscoveredPotions.ToHashSet();
        foreach (var reward in local.Rewards.OfType<PotionReward>())
            if (seenPotions.Add(reward.Potion!.Id))
                discoveries.Single(player => player.NetId == localId).Potions.Add(JsonSerializer.SerializeToNode(reward.Potion.Id, CompleteStateJson));
        var discoveryExpected = JsonSerializer.SerializeToNode(discoveries, CompleteStateJson)!;
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-discovery-expected.json"), discoveryExpected.ToJsonString(CompleteStateJson));
        signal("holes-reward-restored");
        await until(() => peers.All(peer => signaled(peer, "holes-reward-restored")), "all fresh replicas validate complete source state and independently sample claim inventories/profile discovery before any claim");
        foreach (var reward in local.Rewards.ToArray())
        {
            var selection = manager.RewardsSetSynchronizer.SelectLocalReward(reward);
            await until(() => selection.IsCompleted, "a real owner reward selection completes through the native transport");
            Check(await selection, "The actual local native synchronized reward is accepted.");
        }
        await until(() => sets.All(set => manager.RewardsSetSynchronizer.IsRewardsSetCompleted(set)), "all three owners' actual reward selections finish on every replica");
        foreach (var set in sets)
        {
            var baseline = baselines[set.Player.NetId];
            Check(set.Rewards.All(reward => reward.SuccessfullySelected) && set.Player.Gold == baseline.Gold + baseline.GoldReward &&
                set.Player.Deck.Cards.Count == baseline.Deck.Length + baseline.Cards.Length &&
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(set.Player.Deck.Cards.Take(baseline.Deck.Length).Select(card => card.ToSerializable()).ToArray(), CompleteStateJson),
                    JsonSerializer.SerializeToNode(baseline.Deck, CompleteStateJson)) &&
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(set.Player.Deck.Cards.Skip(baseline.Deck.Length).Select(card => new { card.Id, card.CurrentUpgradeLevel }).ToArray(), CompleteStateJson),
                    JsonSerializer.SerializeToNode(baseline.Cards, CompleteStateJson)) &&
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(set.Player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id).ToArray(), CompleteStateJson),
                    JsonSerializer.SerializeToNode(baseline.Potions.Concat(baseline.PotionRewards).ToArray(), CompleteStateJson)),
                "Every owner receives exactly the offered gold, the selector's exact two card identities and upgrade levels and any offered potions, preserving the previous ordered inventory.");
        }
        var claimed = RewardState(run, child);
        foreach (var discovery in discoveries)
            Check(JsonNode.DeepEquals(claimed["Players"]!.AsArray().Single(player => player!["net_id"]!.GetValue<ulong>() == discovery.NetId)!["discovered_potions"], discovery.Potions),
                "Complete ordered potion discovery equals its independent preclaim prediction: only this endpoint's first-seen, actually procured local potion is appended.");
        bool rejected = false;
        try { await manager.RewardsSetSynchronizer.SelectLocalReward(local.Rewards[0]); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && JsonNode.DeepEquals(RewardState(run, child), claimed),
            "Selecting an already completed native reward stack is rejected with no second payment or complete-state change.");
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-claimed.json"), claimed.ToJsonString(CompleteStateJson));
        signal("holes-reward-claimed");
        await until(() => peers.All(peer => signaled(peer, "holes-reward-claimed")), "all fresh owners finish exact claims and duplicate rejection");
        foreach (string peer in peers)
        {
            var peerExpected = claimed.DeepClone();
            foreach (var discovery in JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-discovery-expected.json")))!.AsArray())
                peerExpected["Players"]!.AsArray().Single(player => player!["net_id"]!.GetValue<ulong>() == discovery!["NetId"]!.GetValue<ulong>())!["discovered_potions"] = discovery!["Potions"]!.DeepClone();
            Check(JsonNode.DeepEquals(peerExpected, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-claimed.json")))),
                "All three fresh endpoints agree on every claimed field with each complete local discovery list independently predicted before claims; no field or array is ignored.");
        }
        // EventModel.SetEventFinished clears options; Holes ClearFight clears
        // only these four accepted-fight fields. Everything else must remain.
        var finishedExpected = claimed.DeepClone();
        foreach (var finishedParent in finishedExpected["Parents"]!.AsArray())
        {
            finishedParent!["IsFinished"] = true;
            finishedParent["Options"] = new JsonArray();
        }
        finishedExpected["Page"]!["AcceptedFight"] = false;
        finishedExpected["Page"]!["FightRoot"] = null;
        finishedExpected["Page"]!["FightEntry"] = 0;
        finishedExpected["Page"]!["FightOwners"] = new JsonObject();
        var proceed = HeadlessNativeApi.Proceed(manager);
        await until(() => proceed.IsCompleted, "real terminal Proceed resumes the parent while transport is pumped");
        await proceed;
        var pending = manager.EventSynchronizer.AwaitPendingOptionTasks();
        await until(() => pending.IsCompleted, "all native parent resume work finishes");
        await pending;
        Check(run.CurrentRoomCount == 1 && run.CurrentRoom is EventRoom && observation.OpeningCalls == 0 && observedRewards.Count == 3 &&
            manager.EventSynchronizer.Events.Count() == 3 && manager.EventSynchronizer.Events.All(model => model.IsFinished &&
                model.Description!.LocEntryKey == "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description" && model.CurrentOptions.All(option => option.IsLocked)) &&
            JsonNode.DeepEquals(JsonNode.Parse(PlayersContents(run)), claimed["Players"]) &&
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(run.Rng.ToSerializable(), CompleteStateJson), claimed["RunRng"]) &&
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(JigsawNativeSession.Current!.Map!.CreateSave(), CompleteStateJson), claimed["Map"]),
            "Actual terminal Proceed finishes all three dedicated Holes parents without another opening, reward, player/RNG change or exploration charge.");
        var finished = RewardState(run, child);
        File.WriteAllText(Path.Combine(directory, role + ".holes-reward-finished.json"), finished.ToJsonString(CompleteStateJson));
        Check(JsonNode.DeepEquals(finished, finishedExpected),
            "Proceed changes exactly the independently specified finished parent options and accepted-fight page fields; complete players, all RNG, checkpoint, history, root and exited child descriptor remain intact.");
        Console.WriteLine($"HOLES_REWARD_ENET role={role} original=3 active=3 fresh-process=true opening=0 claimed=all duplicate=rejected parent=finished restore=complete");
        signal("holes-reward-finished");
        await until(() => peers.All(peer => signaled(peer, "holes-reward-finished")), "all fresh endpoints finish real native Proceed before cleanup");
        foreach (string peer in peers)
        {
            var peerExpected = finishedExpected.DeepClone();
            foreach (var discovery in JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-discovery-expected.json")))!.AsArray())
                peerExpected["Players"]!.AsArray().Single(player => player!["net_id"]!.GetValue<ulong>() == discovery!["NetId"]!.GetValue<ulong>())!["discovered_potions"] = discovery!["Potions"]!.DeepClone();
            Check(JsonNode.DeepEquals(peerExpected, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-reward-finished.json")))),
                "Every finished endpoint retains the complete independently specified terminal state, including its original predicted local discovery arrays.");
        }
        if (completed)
            await CaptureCompleted(run, saves, observation, child, finishedExpected, directory, role, until, signal, signaled);
    }

    private static async Task CaptureCompleted(RunState run, RunSaveManager saves, ReloadObservation observation, CombatRoom child,
        JsonNode finished, string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        string[] peers = ["host", "2", "3"];
        var manager = RunManager.Instance;
        var session = JigsawNativeSession.Current!;
        var map = session.Map!;
        int root = map.CurrentNode!.Value;
        // ToSave first captures the active map into the host's live checkpoint.
        // Its old persisted roster can differ from the new lobby's independently
        // verified live roster; other endpoints do not perform this host save.
        var liveExpected = finished.DeepClone();
        if (role == "host") liveExpected["Point"]!["Map"] = finished["Map"]!.DeepClone();
        var completedMap = finished["Map"]!.DeepClone();
        completedMap["Completed"]!.AsArray().Add(root);
        completedMap["CurrentNode"] = null;
        completedMap["Hunting"]!["RootHunted"] = false;
        var expectedPoint = finished["Point"]!.DeepClone();
        expectedPoint["Map"] = completedMap.DeepClone();
        // Host's full saved players include its own ordered discovery arrays.
        // A third process restores that one host document on every endpoint;
        // it does not inherit the earlier client's in-memory profile additions.
        var expected = new JsonObject
        {
            ["Players"] = finished["Players"]!.DeepClone(), ["RunRng"] = finished["RunRng"]!.DeepClone(),
            ["History"] = finished["History"]!.DeepClone(), ["Page"] = finished["Page"]!.DeepClone(),
            ["Point"] = expectedPoint, ["Map"] = completedMap,
            ["SourceEndpoint"] = role, ["SourceProcess"] = Environment.ProcessId
        };
        File.WriteAllText(Path.Combine(directory, role + ".holes-completed-before-exit.json"), finished.ToJsonString(CompleteStateJson));
        File.WriteAllText(Path.Combine(directory, role + ".holes-completed-process"), Environment.ProcessId.ToString());
        async Task<(string Payload, JsonNode State)> SaveCanonical(string stage, JsonNode oracle)
        {
            Check(manager.ShouldSave, "Real saved-multiplayer setup enables native saves at this explicit boundary.");
            await session.WaitForSave();
            await SaveManager.Instance.SaveRun(null);
            await session.WaitForSave();
            var saved = saves.LoadAndCanonicalizeMultiplayerRunSave(1);
            Check(saved.Success && saved.SaveData != null, "The native host completion save is independently readable.");
            string payload = JigsawSaveSource.Serialize(saved.SaveData!);
            var jsonType = JsonSerializationUtility.GetTypeInfo<SerializableRun>();
            File.WriteAllText(Path.Combine(directory, stage + "-native-save.json"), JsonSerializer.Serialize(saved.SaveData!, jsonType));
            // Reconstruct a distinct document; no oracle is filled from actual.
            var savedRun = RunState.FromSerializable(JigsawSaveSource.Deserialize(payload));
            var actual = CompletedState(savedRun, JigsawCheckpoint.Slot.Get(savedRun).Map!);
            File.WriteAllText(Path.Combine(directory, stage + "-save-actual.json"), actual.ToJsonString(CompleteStateJson));
            File.WriteAllText(Path.Combine(directory, stage + "-save-expected.json"), oracle.ToJsonString(CompleteStateJson));
            CheckCompletedState(actual, oracle, "Host canonical save matches every independently predicted completion field at stage " + stage + ".");
            Check(JsonNode.DeepEquals(RewardState(run, child), liveExpected),
                "Saving a projected completed root cannot mutate the complete live player/RNG/parent/page/checkpoint/map/history state.");
            return (payload, actual);
        }
        // A finished narrative alone is not an authenticated map departure.
        if (role == "host")
        {
            Check(map.RoomCompletedPlayers.Count == 0, "Proceed alone has completed no owner's root exit.");
            await SaveCanonical("completed-no-exit", liveExpected);
            signal("holes-completed-no-exit-checked");
        }
        await until(() => signaled("host", "holes-completed-no-exit-checked"), "host independently saves and verifies the finished parent before any owner exits");
        if (role == "3")
            await until(() => signaled("host", "holes-completed-partial-checked"), "the third owner waits for a real two-owner partial-exit save");
        // The host supplies the native map object, not an NEventRoom button.
        // Real patched Open(false) authenticates and broadcasts this owner.
        var nativeMap = new MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen();
        try { nativeMap.Open(false); }
        finally { nativeMap.Free(); }
        signal("holes-completed-exit");
        if (role != "3")
        {
            await until(() => signaled("host", "holes-completed-exit") && signaled("2", "holes-completed-exit") &&
                map.RoomCompletedPlayers.Count == 2 && map.RoomCompletedPlayers.Contains(1UL) && map.RoomCompletedPlayers.Contains(2UL),
                "exactly two authenticated owners exit while the third remains in the finished root");
            Check(JsonNode.DeepEquals(RewardState(run, child), liveExpected), "Partial exits leave the entire finished point intact.");
            signal("holes-completed-partial");
            if (role == "host")
            {
                await until(() => signaled("2", "holes-completed-partial"), "the second owner independently verifies the partial boundary");
                await SaveCanonical("completed-partial", liveExpected);
                signal("holes-completed-partial-checked");
            }
        }
        await until(() => peers.All(peer => signaled(peer, "holes-completed-exit")) && map.ActivePlayers.All(map.RoomCompletedPlayers.Contains),
            "all three real patched map exits authenticate their own native owner through the ordered transport");
        Check(JsonNode.DeepEquals(RewardState(run, child), liveExpected) && map.CurrentNode == root &&
            map.DestinationVotes.Count == 0 && observation.OpeningCalls == 0 && observedRewards!.Count == 3,
            "Authenticated multiplayer exits preserve the full live root, claimed inventories, all RNG and parents while destination voting remains untouched.");
        File.WriteAllText(Path.Combine(directory, role + ".holes-completed-after-exit.json"), RewardState(run, child).ToJsonString(CompleteStateJson));
        signal("holes-completed-verified");
        await until(() => peers.All(peer => signaled(peer, "holes-completed-verified")), "every endpoint verifies the full pre-save state before host persistence");
        if (role == "host")
        {
            // OnEdited deliberately defers normal live-room edit saves while
            // CurrentNode remains set. This is the explicit legal SaveRun
            // boundary after all real exits, using the existing isolated IO.
            var captured = await SaveCanonical("completed", expected);
            var repeated = await SaveCanonical("completed-repeat", expected);
            Check(JsonNode.DeepEquals(captured.State, repeated.State) &&
                repeated.State["Map"]!["Completed"]!.AsArray().Count(node => node!.GetValue<int>() == root) == 1 &&
                map.CurrentNode == root && map.ActivePlayers.All(map.RoomCompletedPlayers.Contains) &&
                map.DestinationVotes.Count == 0 && observation.OpeningCalls == 0 && observedRewards!.Count == 3,
                "Repeated real host saves project the same root exactly once without changing live voting, charging steps or reopening combat/rewards.");
            expected["SourceProcesses"] = JsonSerializer.SerializeToNode(peers.ToDictionary(peer => peer,
                peer => int.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-completed-process")))), CompleteStateJson);
            File.WriteAllText(Path.Combine(directory, "completed-checkpoint.json"), repeated.Payload);
            File.WriteAllText(Path.Combine(directory, "completed-expected.json"), expected.ToJsonString(CompleteStateJson));
        }
        signal("holes-completed-captured");
        await until(() => peers.All(peer => signaled(peer, "holes-completed-captured")), "all second-round processes finish completion capture before shutdown");
    }

    internal static async Task RestoredCompleted(RunState run, ReloadObservation observation, string restoredMap,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        string[] peers = ["host", "2", "3"];
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "completed-expected.json")))!;
        // The only new-session map field comes from the existing independently
        // checked LoadListener snapshot, not from this post-load actual.
        expected["Map"]!["Players"] = JsonNode.Parse(restoredMap)!["Players"]!.DeepClone();
        Check(expected["SourceEndpoint"]!.GetValue<string>() == "host" &&
            expected["SourceProcesses"]!.AsObject().All(pair => pair.Value!.GetValue<int>() != Environment.ProcessId) &&
            run.CurrentRoomCount == 1 && run.CurrentRoom is MapRoom && run.BaseRoom is MapRoom &&
            observation.OpeningCalls == 0 && observedRewards is { Count: 0 } &&
            !RunManager.Instance.EventSynchronizer.Events.Any(),
            "A third fresh process naturally loads the completed host point as MapRoom with no recreated Holes parent, combat opening or reward set.");
        var actual = CompletedState(run, JigsawNativeSession.Current!.Map!.CreateSave());
        File.WriteAllText(Path.Combine(directory, role + ".holes-completed-restore-actual.json"), actual.ToJsonString(CompleteStateJson));
        File.WriteAllText(Path.Combine(directory, role + ".holes-completed-restore-expected.json"), expected.ToJsonString(CompleteStateJson));
        CheckCompletedState(actual, expected,
            "Fresh native load retains the host's entire claimed players and discovery profile arrays, every run RNG field, full history, completed root and cleared finite fight page without a second exploration charge.");
        signal("holes-completed-restored");
        await until(() => peers.All(peer => signaled(peer, "holes-completed-restored")), "all third-round endpoints verify the same full completed host document");
        foreach (string peer in peers)
            Check(JsonNode.DeepEquals(actual, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, peer + ".holes-completed-restore-actual.json")))),
                "Every fresh endpoint agrees on the complete host-saved point without carrying another endpoint's earlier local discovery mutation.");
        Console.WriteLine($"HOLES_COMPLETED_ENET role={role} original=3 active=3 fresh-process=true current=MapRoom opening=0 rewards=0 restore=complete");
    }

    private static JsonNode CompletedState(RunState run, JigsawSave map) => new JsonObject
    {
        ["Players"] = JsonNode.Parse(PlayersContents(run)),
        ["RunRng"] = JsonSerializer.SerializeToNode(run.Rng.ToSerializable(), CompleteStateJson),
        ["History"] = NativeHistory(run), ["Page"] = JsonSerializer.SerializeToNode(HolesPageState(run), CompleteStateJson),
        ["Point"] = JsonSerializer.SerializeToNode(JigsawCheckpoint.Slot.Get(run), CompleteStateJson),
        ["Map"] = JsonSerializer.SerializeToNode(map, CompleteStateJson)
    };

    private static void CheckCompletedState(JsonNode actual, JsonNode expected, string reason) =>
        Check(new[] { "Players", "RunRng", "History", "Page", "Point", "Map" }.All(field => JsonNode.DeepEquals(actual[field], expected[field])), reason);

    private static JsonNode RewardState(RunState run, CombatRoom child) => JsonSerializer.SerializeToNode(new
    {
        Players = JsonNode.Parse(PlayersContents(run)),
        RunRng = JsonSerializer.SerializeToNode(run.Rng.ToSerializable(), CompleteStateJson),
        OwnerRng = JsonNode.Parse(OwnersRngContents(RunManager.Instance.EventSynchronizer.Events)),
        Parents = JsonNode.Parse(ParentsContents(RunManager.Instance.EventSynchronizer.Events)),
        Point = JsonSerializer.SerializeToNode(JigsawCheckpoint.Slot.Get(run), CompleteStateJson),
        Page = JsonSerializer.SerializeToNode(HolesPageState(run), CompleteStateJson),
        Map = JsonSerializer.SerializeToNode(JigsawNativeSession.Current!.Map!.CreateSave(), CompleteStateJson),
        History = NativeHistory(run), Room = child.ToSerializable()
    }, CompleteStateJson)!;

    private static JsonNode? NativeHistory(RunState run)
    {
        var type = JsonSerializationUtility.GetTypeInfo<MapPointHistoryEntry>();
        return JsonSerializer.SerializeToNode(JsonSerializer.Deserialize(JsonSerializer.Serialize(run.CurrentMapPointHistoryEntry!, type), type), CompleteStateJson);
    }

    private static JsonNode RewardContents(IEnumerable<RewardsSet> sets) => JsonSerializer.SerializeToNode(sets.Select(set => new
    {
        set.Player.NetId,
        Rewards = set.Rewards.Select(reward => new
        {
            Type = reward.GetType().FullName, Data = reward is CardReward ? null : reward.ToSerializable(),
            Potion = (reward as PotionReward)?.Potion?.Id,
            Cards = (reward as CardReward)?.Cards.Select(card => card.ToSerializable()).ToArray()
        }).ToArray()
    }).ToArray(), CompleteStateJson)!;

    private static void CheckRewardSets(RewardsSet[] sets) => Check(sets.Select(set => set.Player.NetId).SequenceEqual(new ulong[] { 1, 2, 3 }) &&
        sets.All(set => set.Rewards.All(reward => reward.IsPopulated) && set.Rewards.OfType<GoldReward>().Count() == 2 &&
            set.Rewards.OfType<CardReward>().Count() == 2 && !set.Rewards.OfType<RelicReward>().Any()),
        "Every actual owner receives the complete native doubled Holes gold/card offers and no relic reward.");

    private static async Task RestoredOwners(RunState run, ReloadObservation observation, string restoredMap,
        string directory, string role, Func<Func<bool>, string, Task> until,
        Action<string> signal, Func<string, string, bool> signaled)
    {
        Exception? bodyFailure = null;
        try
        {
            string[] peers = ["host", "2", "3"];
            var manager = RunManager.Instance;
            var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "event-opening.json")))!;
            // LoadListener already checks the full persisted map before entry;
            // only its active roster follows this new native lobby's order.
            expected["State"]!["Map"]!["Players"] = JsonNode.Parse(restoredMap)!["Players"]!.DeepClone();
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
