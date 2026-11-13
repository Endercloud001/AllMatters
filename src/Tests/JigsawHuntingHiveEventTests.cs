using AllMatters.Encounters;
using AllMatters.Jigsaw;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

internal static class JigsawHuntingHiveEventTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw hunting Hive event child native opening gives ordinary Weak Frail Vulnerable one", EventChildOpening),
     ("Jigsaw hunting Hive event opening preserves Artifact order for one to four combat roles", ArtifactBoundaries),
     ("Jigsaw hunting Hive event opening excludes safe roots and unrelated native scopes", ScopeBoundaries),
     ("Jigsaw hunting Hive event named elite and boss use ordinary opening while top elite keeps Weak two", EncounterTierBoundaries),
     ("Jigsaw hunting Hive event opening excludes a native no acts run", NoActsOpening),
     ("Jigsaw hunting Hive real Holes AM_FIGHT native coordinator opens once across a complete later turn", RealHolesOpening),
     ("Jigsaw hunting Hive native awaited opening rejects stale identities and preserves dependency failures", AwaitedOpeningBoundaries),
     ("Jigsaw hunting Hive native awaited first Weak stops remaining opening commands after invalidation", AwaitedWeakBoundaries)];

    private sealed class NativeAwaitProbe(Creature target)
    {
        internal readonly Creature Target = target;
        internal readonly TaskCompletionSource Reached = new();
        internal readonly TaskCompletionSource Release = new();
        internal Exception? Failure;
    }
    private static NativeAwaitProbe? nativeAwait;

    private static void PauseNativeMaxHp(Creature creature, ref Task<decimal> __result)
    {
        if (nativeAwait is { } probe && ReferenceEquals(probe.Target, creature))
            __result = PauseAfterNativeMaxHp(__result, probe);
    }

    private static async Task<decimal> PauseAfterNativeMaxHp(Task<decimal> original, NativeAwaitProbe probe)
    {
        decimal result = await original;
        probe.Reached.TrySetResult();
        await probe.Release.Task;
        if (probe.Failure != null) throw probe.Failure;
        return result;
    }

    public sealed class AwaitedOpeningPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
        public override async Task BeforeCombatStart()
        {
            if (nativeAwait is { } probe && ReferenceEquals(Owner, probe.Target))
                await CreatureCmd.SetMaxHp(Owner, Owner.MaxHp);
        }
    }

    public sealed class AwaitedWeakPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
        public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
            decimal amount, Creature? applier, CardModel? cardSource)
        {
            if (nativeAwait is { } probe && ReferenceEquals(Owner, probe.Target) && power is WeakPower &&
                ReferenceEquals(power.Owner, Owner) && amount == 1 && applier == null)
                await CreatureCmd.SetMaxHp(Owner, Owner.MaxHp);
        }
    }

    private static async Task AwaitedOpeningBoundaries()
    {
        ModelDb.Inject(typeof(AwaitedOpeningPower));
        var patch = new Harmony("AllMatters.Tests.HiveEvent.NativeHookAwait");
        patch.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingHiveEventTests), nameof(PauseNativeMaxHp)));
        try
        {
            foreach (string boundary in new[] { "closed-session", "replacement-run", "replacement-map", "replacement-room", "dependency-failure" })
            {
                using var fixture = new EventBattle(4);
                var target = fixture.Players[0].Creature;
                await PowerCmd.Apply<AwaitedOpeningPower>(new ThrowingPlayerChoiceContext(), target, 1, target, null);
                var probe = new NativeAwaitProbe(target);
                nativeAwait = probe;
                Task? operation = null;
                try
                {
                    // The real Hook awaits its real PowerModel listener's native HP command.
                    operation = Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && fixture.Players.All(player =>
                        OpeningAmounts(player).All(amount => amount == 0) && DebuffsFor(player).Length == 0),
                        "The original native opening Task is genuinely waiting before any Hive opening command.");
                    if (boundary == "closed-session") JigsawNativeSession.TryGet(fixture.Run)!.Close();
                    if (boundary == "replacement-run")
                    {
                        RunManager.Instance.CleanUp(false);
                        var replacement = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                            acts: [ModelDb.Act<Hive>()], seed: "HUNTING_85_HIVE_AWAIT_REPLACEMENT");
                        RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                        Check(!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), fixture.Run),
                            "A distinct actual run replaces the awaiting opening's native manager state.");
                    }
                    if (boundary == "replacement-map")
                    {
                        var oldMap = JigsawNativeSession.TryGet(fixture.Run)!.Map!;
                        JigsawCheckpoint.Capture(fixture.Run, AcceptedRoot(fixture.Players[0].NetId));
                        JigsawNativeSession.Restore(fixture.Run);
                        Check(JigsawNativeSession.TryGet(fixture.Run)?.Map is { } replacement &&
                            !ReferenceEquals(oldMap, replacement) && replacement.CurrentNode == oldMap.CurrentNode &&
                            replacement.CurrentRoomHunted && JigsawHuntingNative.RootIsHunted(fixture.Players[0]),
                            "A different restored map has the same accepted hunted node, so only the old map identity is stale.");
                    }
                    if (boundary == "replacement-room")
                    {
                        fixture.Run.PushRoom(new CombatRoom(new CombatState(ModelDb.Encounter<HolesEncounter>().ToMutable(), fixture.Run))
                            { ParentEventId = fixture.Room.ParentEventId });
                        Check(!ReferenceEquals(fixture.Run.CurrentRoom, fixture.Room),
                            "A different real combat room replaces the awaiting opening's current room.");
                    }
                    if (boundary == "dependency-failure") probe.Failure = new InvalidOperationException("Injected native opening HP dependency failure");
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check(boundary == "dependency-failure" ? ReferenceEquals(failure, probe.Failure) : failure == null,
                        $"Boundary={boundary}: the real Hook completes or propagates the exact native dependency exception.");
                    Check(fixture.Players.All(player => OpeningAmounts(player).All(amount => amount == 0) && DebuffsFor(player).Length == 0),
                        $"Boundary={boundary}: old native opening work never applies Hive debuffs after its awaited dependency.");
                }
                finally
                {
                    probe.Release.TrySetResult();
                    if (operation != null) try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { }
                    nativeAwait = null;
                }
            }
        }
        finally { nativeAwait = null; patch.UnpatchAll(patch.Id); }
    }

    private static async Task AwaitedWeakBoundaries()
    {
        ModelDb.Inject(typeof(AwaitedWeakPower));
        var patch = new Harmony("AllMatters.Tests.HiveEvent.NativePowerAwait");
        patch.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingHiveEventTests), nameof(PauseNativeMaxHp)));
        try
        {
            foreach (bool failDependency in new[] { false, true })
            {
                using var fixture = new EventBattle(4);
                var player = fixture.Players[0];
                await PowerCmd.Apply<AwaitedWeakPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, player.Creature, null);
                var probe = new NativeAwaitProbe(player.Creature);
                nativeAwait = probe;
                Task? operation = null;
                try
                {
                    operation = Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && OpeningAmounts(player).SequenceEqual(new[] { 1m, 0m, 0m }) &&
                        DebuffsFor(player) is [{ Power: WeakPower, Amount: 1, Applier: null }] &&
                        fixture.Players.Skip(1).All(other => OpeningAmounts(other).All(amount => amount == 0) && DebuffsFor(other).Length == 0),
                        "The first real Weak command has applied once and is awaiting its native AfterPowerAmountChanged dependency.");
                    if (failDependency) probe.Failure = new InvalidOperationException("Injected native Weak HP dependency failure");
                    else JigsawNativeSession.TryGet(fixture.Run)!.Close();
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check(failDependency ? ReferenceEquals(failure, probe.Failure) : failure == null,
                        "The actual first command preserves its dependency error, or finishes normally after the session closes.");
                    Check(OpeningAmounts(player).SequenceEqual(new[] { 1m, 0m, 0m }) &&
                        DebuffsFor(player) is [{ Power: WeakPower, Amount: 1, Applier: null }] &&
                        fixture.Players.Skip(1).All(other => OpeningAmounts(other).All(amount => amount == 0) && DebuffsFor(other).Length == 0),
                        "The completed first Weak is retained; Frail, Vulnerable and every later combat role receive no further command.");
                }
                finally
                {
                    probe.Release.TrySetResult();
                    if (operation != null) try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { }
                    nativeAwait = null;
                }
            }
        }
        finally { nativeAwait = null; patch.UnpatchAll(patch.Id); }
    }

    private sealed class NativeOpeningObservation
    {
        internal Task? Opening;
        internal Task? Startup;
        internal int OpeningCalls;
    }
    private static NativeOpeningObservation? nativeOpening;

    private static void CaptureOpening(Task task)
    {
        if (nativeOpening is not { } observation) return;
        observation.Opening = task;
        observation.OpeningCalls++;
    }

    private static void CaptureStartup(Task task)
    {
        if (nativeOpening is { } observation) observation.Startup = task;
    }

    private static IEnumerable<CodeInstruction> ObserveOpening(IEnumerable<CodeInstruction> instructions)
    {
        var hook = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart));
        var capture = AccessTools.Method(typeof(JigsawHuntingHiveEventTests), nameof(CaptureOpening));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(hook)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the one real coordinator call's exact BeforeCombatStart Task without replacing or repeating it.");
    }

    private static IEnumerable<CodeInstruction> ObserveStartup(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(CombatManager),
#if STS2_PUBLIC
            "StartCombatInternal");
#else
            "RunTurnLoopAfter");
#endif
        var capture = AccessTools.Method(typeof(JigsawHuntingHiveEventTests), nameof(CaptureStartup));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(start)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
        Check(matches == 1, "Observe the same startup or turn-loop Task that the native room-loaded caller passes to RunSafely.");
    }

    private static bool SkipCombatFtue(ref bool __result) { __result = true; return false; }

    private static async Task RealHolesOpening()
    {
        var observation = new NativeOpeningObservation();
        var manager = RunManager.Instance;
        var combatManager = CombatManager.Instance;
        var previousNetId = LocalContext.NetId;
        RunState? run = null;
        Harmony? patch = null;
        Harmony? host = null;
        IDisposable? atlases = null;
        bool hostTouched = false;
        Exception? bodyFailure = null;
        var cleanupFailures = new List<Exception>();
        int readyRound = 0;
        void PlayerTurnReady(CombatState state)
        {
            if (ReferenceEquals(state.RunState, run) && state.CurrentSide == CombatSide.Player) readyRound = state.RoundNumber;
        }
        void RecordCleanupFailure(string step, Exception error)
        {
            var failure = new InvalidOperationException($"Holes lifecycle cleanup failed during {step}.", error);
            cleanupFailures.Add(failure);
            Console.Error.WriteLine(failure);
        }
        void Cleanup(string step, Action action)
        {
            try { action(); } catch (Exception error) { RecordCleanupFailure(step, error); }
        }
        try
        {
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            patch = new Harmony("AllMatters.Tests.HiveEvent.NativeOpening");
            var start = AccessTools.Method(typeof(CombatManager), "StartCombatInternal");
            var stateMachine = start.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            patch.Patch(AccessTools.Method(stateMachine, "MoveNext"),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHiveEventTests), nameof(ObserveOpening)));
            patch.Patch(AccessTools.Method(typeof(CombatManager), nameof(CombatManager.AfterCombatRoomLoaded)),
                transpiler: new HarmonyMethod(typeof(JigsawHuntingHiveEventTests), nameof(ObserveStartup)));
            patch.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingHiveEventTests), nameof(SkipCombatFtue)));
            nativeOpening = observation;
            run = SharedHolesRun();
            var player = run.Players[0];
            combatManager.TurnStarted += PlayerTurnReady;
            manager.SetUpTest(run, new NetSingleplayerGameService());
            combatManager.Reset(true);
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = player.NetId;
            player.Creature.SetMaxHpInternal(500);
            player.Creature.SetCurrentHpInternal(500);
            for (int card = 0; card < 20; card++) player.Deck.AddInternal(run.CreateCard<DefendIronclad>(player));
            EventModel model;
            var parent = ModelDb.Event<FieldOfManSizedHoles>();
            using (HeadlessCombatInProgress.EnterEvent())
            {
                manager.GenerateRooms();
                await manager.GenerateMap();
                run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
                run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
                Check(run.MapPointHistory.Sum(points => points.Count) == 1,
                    "The deterministic shared-page seed is evaluated at its actual first native history entry.");
                JigsawCheckpoint.Capture(run, AcceptedRoot(player.NetId));
                JigsawNativeSession.Restore(run);
                await manager.EnterRoom(new EventRoom(parent));
                model = manager.EventSynchronizer.GetLocalEvent();
                Check(model is FieldOfManSizedHoles && model.IsShared && manager.EventSynchronizer.IsShared &&
                    model.CurrentOptions.Count(option => option.TextKey.EndsWith(".AM_FIGHT")) == 1,
                    "The actual shared native Holes page offers exactly one mod AM_FIGHT option.");
            }
            var map = JigsawNativeSession.TryGet(run)!.Map!;
            int steps = map.Steps;
            int remaining = map.RemainingSteps;
            int? root = map.CurrentNode;
            Check(root != null && map.CurrentRoomHunted && map.Nodes.Single(node => node.Id == root).Room == JigsawRoom.Unknown &&
                OpeningAmounts(player).All(amount => amount == 0) && DebuffsFor(player).Length == 0,
                "The actual event is on an accepted hunted Unknown root, before any combat opening command.");
            host = new Harmony("AllMatters.TestHost");
            hostTouched = true;
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
            int fight = model.CurrentOptions.ToList().FindIndex(option => option.TextKey.EndsWith(".AM_FIGHT"));
            manager.EventSynchronizer.ChooseLocalOption(fight);
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            await UntilNative(() => observation.Opening != null, observation,
                "The native option synchronizer and event-combat coordinator reach the actual opening Hook.");
            // Public startup ends after first-turn setup; beta's turn-loop Task remains live.
            // Both coordinators await this exact observed Hook Task before starting that turn.
            await observation.Opening!.WaitAsync(TimeSpan.FromSeconds(5));
            Check(observation.Opening.IsCompletedSuccessfully && observation.OpeningCalls == 1 && observation.Startup != null,
                "The exact native coordinator Hook Task completed once within the captured real startup.");
            Check(run.CurrentRoomCount == 2 && run.BaseRoom is EventRoom eventRoom &&
                eventRoom.CanonicalEvent.Id == parent.Id && run.CurrentRoom is CombatRoom
                { Encounter: HolesEncounter, ParentEventId: var parentId } && parentId == parent.Id,
                "The real AM_FIGHT choice created its own Holes child combat under the original event.");
            var room = (CombatRoom)run.CurrentRoom!;
            AssertOrdinary(player, 0, "actual AM_FIGHT opening");
#if STS2_PUBLIC
            const bool skipsFirstDurationTick = true;
#else
            const bool skipsFirstDurationTick = false;
#endif
            PowerModel[] openingPowers = [player.Creature.GetPower<WeakPower>()!, player.Creature.GetPower<FrailPower>()!,
                player.Creature.GetPower<VulnerablePower>()!];
            Check(room.CombatState.CurrentSide == CombatSide.Player &&
                openingPowers.All(power => power.SkipNextDurationTick == skipsFirstDurationTick),
                "The player-side opening retains this native version's initial duration-skip policy.");
            await UntilNative(() => readyRound == room.CombatState.RoundNumber && readyRound >= 1 &&
                PileType.Hand.GetPile(player).Cards.Count == 5, observation,
                "The same native coordinator completes its first player turn and five-card hand.");
            int oldRound = readyRound;
            int hp = player.Creature.CurrentHp;
            int historyRooms = run.CurrentMapPointHistoryEntry!.Rooms.Count;
            combatManager.SetReadyToEndTurn(player, false);
            await UntilNative(() => readyRound > oldRound && room.CombatState.CurrentSide == CombatSide.Player &&
                PileType.Hand.GetPile(player).Cards.Count == 5, observation,
                "Native readiness drives a real enemy attack and completes the next player turn.");
            Check(player.Creature.CurrentHp < hp && room.CombatState.Enemies.Single().GetPowerAmount<AllMatters.Powers.FadingPower>() == 4,
                "Transient performed exactly its first real attack and decremented its original Fading once.");
            var laterDebuffs = DebuffsFor(player);
            Type[] openingTypes = [typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower)];
            var decayTypes = skipsFirstDurationTick ? Array.Empty<Type>() : openingTypes;
            // Both versions tick at Enemy turn end. Public skips its first tick for every player debuff;
            // beta only sets that skip when a debuff is applied during the Enemy side, unlike this opening.
            Check(observation.OpeningCalls == 1 && laterDebuffs.Length == 3 + decayTypes.Length &&
                laterDebuffs.Take(3).Select(entry => entry.Power.GetType()).SequenceEqual(openingTypes) &&
                laterDebuffs.Take(3).All(entry => entry.Amount == 1) &&
                laterDebuffs.Skip(3).Select(entry => entry.Power.GetType()).SequenceEqual(decayTypes) &&
                laterDebuffs.Skip(3).All(entry => entry.Amount == -1) &&
                laterDebuffs.All(entry => entry.Applier == null) &&
                OpeningAmounts(player).All(amount => amount == (skipsFirstDurationTick ? 1 : 0)) &&
                openingPowers.All(power => !power.SkipNextDurationTick),
                "The opening grants each debuff once; the completed native Enemy turn records exactly this version's first duration tick.");
            Check(ReferenceEquals(run.CurrentRoom, room) && map.CurrentNode == root && map.Steps == steps &&
                map.RemainingSteps == remaining && run.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms,
                "The next actual turn neither charges exploration nor changes the accepted root or appends another room.");
        }
        catch (Exception error)
        {
            bodyFailure = error;
            Console.Error.WriteLine("Holes lifecycle body failure (before cleanup):");
            Console.Error.WriteLine(error);
        }
        finally
        {
            Cleanup("native Reset", () => combatManager.Reset(true));
            try
            {
                if (observation.Startup != null)
                    await observation.Startup.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException) { } // Native cancellation is expected after teardown was requested.
            catch (Exception error) { RecordCleanupFailure("awaiting native startup after Reset", error); }
            Cleanup("TurnStarted unsubscribe", () => combatManager.TurnStarted -= PlayerTurnReady);
            Cleanup("session close", () => { if (run != null) JigsawNativeSession.TryGet(run)?.Close(); });
            Cleanup("native run cleanup", () => manager.CleanUp(false));
            Cleanup("LocalContext restore", () => LocalContext.NetId = previousNetId);
            nativeOpening = null;
            // Keep native lifecycle predicates until Reset and its task have both been observed.
            if (hostTouched)
            {
                Cleanup("IsInProgress host restore", () => host!.Patch(
                    AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix")));
                Cleanup("IsEnding host restore", () => host!.Patch(
                    AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                    prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix")));
            }
            Cleanup("test patch removal", () => { if (patch != null) patch.UnpatchAll(patch.Id); });
            Cleanup("atlas disposal", () => atlases?.Dispose());
        }
        if (bodyFailure != null)
        {
            if (cleanupFailures.Count > 0) bodyFailure.Data["HolesLifecycleCleanupFailures"] = new AggregateException(cleanupFailures);
            ExceptionDispatchInfo.Capture(bodyFailure).Throw();
        }
        if (cleanupFailures.Count > 0) throw new AggregateException("Holes lifecycle cleanup failed.", cleanupFailures);
    }

    private static RunState SharedHolesRun()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var run = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                seed: "HUNTING_85_HIVE_REAL_HOLES" + seed);
            run.CurrentActIndex = 1;
            var rng = new Rng(run.Rng.Seed);
            rng.NextInt(100);
            if (rng.NextInt(100) < 33) return run;
        }
        throw new InvalidOperationException("No deterministic native shared Holes seed was found.");
    }

    private static async Task UntilNative(Func<bool> ready, NativeOpeningObservation observation, string reason)
    {
        var tree = Godot.Engine.GetMainLoop();
        for (int frame = 0; frame < 300; frame++)
        {
            if (observation.Startup is { IsFaulted: true } failed) await failed;
            if (ready()) return;
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        }
        Check(ready(), reason);
    }

    private static async Task EventChildOpening()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player],
            acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
            seed: "HUNTING_85_HIVE_EVENT");
        run.CurrentActIndex = 1;
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<HolesEncounter>().ToMutable());
        try
        {
            var parent = ModelDb.Event<FieldOfManSizedHoles>();
            var eventRoom = new EventRoom(parent);
            var room = new CombatRoom(battle.Combat) { ParentEventId = parent.Id };
            run.PushRoom(eventRoom);
            run.PushRoom(room);
            var map = AcceptedRoot(player.NetId);
            JigsawCheckpoint.Capture(run, map);
            JigsawNativeSession.Restore(run);
            Check(run.Act is Hive && run.CurrentRoomCount == 2 && ReferenceEquals(run.BaseRoom, eventRoom) &&
                ReferenceEquals(run.CurrentRoom, room) && room.ParentEventId == eventRoom.CanonicalEvent.Id &&
                room.RoomType == RoomType.Monster && room.Encounter is HolesEncounter &&
                ReferenceEquals(room.CombatState, battle.Combat) &&
                ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run) && JigsawHuntingNative.RootIsHunted(player),
                "The real Hive run has the matching Holes event parent, ordinary combat child and accepted hunted Unknown root.");
            Check(battle.Combat.Players.Count == 1 && ReferenceEquals(battle.Combat.Players[0], player) &&
                OpeningAmounts(player).SequenceEqual(new[] { 0m, 0m, 0m }) && DebuffsFor(player).Length == 0,
                "The native combat owner starts without opening debuffs or their history entries.");

            await Hook.BeforeCombatStart(run, battle.Combat);

            var amounts = OpeningAmounts(player);
            Check(amounts.SequenceEqual(new[] { 1m, 1m, 1m }),
                $"The Hive event child receives ordinary Weak/Frail/Vulnerable one; actual={string.Join("/", amounts)}.");
            var received = DebuffsFor(player);
            Check(received.Length == 3 && received.Count(entry => entry.Power is WeakPower) == 1 &&
                received.Count(entry => entry.Power is FrailPower) == 1 &&
                received.Count(entry => entry.Power is VulnerablePower) == 1 &&
                received.All(entry => entry.Amount == 1 && entry.Applier == null),
                "Native PowerCmd history records exactly one application of each ordinary opening debuff on its original target.");
        }
        finally { JigsawNativeSession.TryGet(run)?.Close(); }
    }

    private static decimal[] OpeningAmounts(Player player) =>
        [player.Creature.GetPowerAmount<WeakPower>(), player.Creature.GetPowerAmount<FrailPower>(),
         player.Creature.GetPowerAmount<VulnerablePower>()];

    private static PowerReceivedEntry[] DebuffsFor(Player player) => CombatManager.Instance.History.Entries
        .OfType<PowerReceivedEntry>().Where(entry => ReferenceEquals(entry.Actor, player.Creature) &&
            entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();

    private static async Task ArtifactBoundaries()
    {
        for (int count = 1; count <= 4; count++)
        for (int artifact = 0; artifact <= 3; artifact++)
        {
            using var fixture = new EventBattle(count);
            Check(fixture.Run.Players.Count == 1 && fixture.Battle.Combat.Players.Count == count &&
                fixture.Players.All(player => ReferenceEquals(player.RunState, fixture.Run)) &&
                JigsawHuntingNative.RootIsHunted(fixture.Players[0]),
                "One native run owner supplies one to four actual combat roles, not remote clients.");
            foreach (var player in fixture.Players)
                if (artifact > 0)
                    await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                        artifact, player.Creature, null);
            await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
            foreach (var player in fixture.Players)
                AssertOrdinary(player, artifact, $"roles={count}, Artifact={artifact}");
        }
    }

    private static async Task ScopeBoundaries()
    {
        foreach (var scope in new[] { OpeningScope.Safe, OpeningScope.NonJigsaw, OpeningScope.OtherAct,
            OpeningScope.ParentMismatch, OpeningScope.MissingParent, OpeningScope.NonUnknownRoot,
            OpeningScope.WrongRun, OpeningScope.WrongCombat })
        {
            using var fixture = new EventBattle(4, scope);
            var run = fixture.Run;
            foreach (var player in fixture.Players)
                await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                    3, player.Creature, null);
            if (scope == OpeningScope.Safe)
                Check(JigsawNativeSession.TryGet(run)?.Map is { CurrentRoomHunted: false, RemainingSteps: 0 } &&
                    !JigsawHuntingNative.RootIsHunted(fixture.Players[0]),
                    "The accepted Unknown root spends its last step and stays safe at zero for its event child.");
            if (scope == OpeningScope.NonJigsaw)
                Check(JigsawNativeSession.TryGet(run) == null, "The native Hive event has no Jigsaw session.");
            if (scope == OpeningScope.OtherAct)
                Check(run.Act is Glory, "The unrelated event is in the actual Glory act.");
            if (scope == OpeningScope.ParentMismatch)
                Check(run.BaseRoom is EventRoom parent && fixture.Room.ParentEventId != parent.CanonicalEvent.Id,
                    "The real EventRoom and combat child deliberately have different parent identities.");
            if (scope == OpeningScope.MissingParent)
                Check(run.CurrentRoomCount == 1 && run.BaseRoom is CombatRoom && fixture.Room.ParentEventId != null,
                    "An orphan combat parent ID is not a real event stack.");
            if (scope == OpeningScope.NonUnknownRoot)
            {
                var map = JigsawNativeSession.TryGet(run)!.Map!;
                Check(map.CurrentRoomHunted && map.Nodes.Single(node => node.Id == map.CurrentNode).Room == JigsawRoom.Combat,
                    "A hunted ordinary map combat cannot be relabeled as an accepted Unknown event root.");
            }
            if (scope == OpeningScope.WrongRun)
            {
                RunManager.Instance.CleanUp(false);
                var replacement = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                    acts: [ModelDb.Act<Hive>()], seed: "HUNTING_85_HIVE_EVENT_REPLACEMENT");
                RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                Check(ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), replacement) &&
                    !ReferenceEquals(replacement, run), "The native manager now owns a different actual run.");
            }
            ICombatState hookCombat = fixture.Battle.Combat;
            Player? unrelated = null;
            if (scope == OpeningScope.WrongCombat)
            {
                unrelated = Player.CreateForNewRun<Ironclad>(UnlockState.all, 9);
                unrelated.RunState = run;
                var other = new CombatState(ModelDb.Encounter<HolesEncounter>().ToMutable(), run) { RoundNumber = 1 };
                other.AddPlayer(unrelated);
                unrelated.ResetCombatState();
                hookCombat = other;
                Check(!ReferenceEquals(fixture.Room.CombatState, hookCombat) &&
                    ReferenceEquals(run.CurrentRoom, fixture.Room) && JigsawHuntingNative.RootIsHunted(fixture.Players[0]),
                    "The current hunted event child remains intact while the Hook receives another native combat.");
            }
            await Hook.BeforeCombatStart(run, hookCombat);
            foreach (var player in fixture.Players)
                Check(OpeningAmounts(player).All(amount => amount == 0) && DebuffsFor(player).Length == 0 &&
                    player.Creature.GetPowerAmount<ArtifactPower>() == 3,
                    $"Scope={scope}: no Hive opening command adds history or consumes native Artifact.");
            if (unrelated != null)
                Check(OpeningAmounts(unrelated).All(amount => amount == 0) && DebuffsFor(unrelated).Length == 0,
                    "The mismatched Hook combat also receives no opening debuffs.");
        }
    }

    private static async Task NoActsOpening()
    {
        using var fixture = new EventBattle(1, OpeningScope.NoActs);
        Check(fixture.Run.Acts.Count == 0 && JigsawNativeSession.TryGet(fixture.Run) == null &&
            fixture.Run.CurrentRoomCount == 2 && fixture.Run.BaseRoom is EventRoom,
            "The native no-acts, non-Jigsaw fixture still presents a matching event combat stack.");
        await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
        Check(OpeningAmounts(fixture.Players[0]).All(amount => amount == 0) && DebuffsFor(fixture.Players[0]).Length == 0,
            "A native event opening outside Jigsaw does not inspect a missing act or apply Hive debuffs.");
    }

    private static async Task EncounterTierBoundaries()
    {
        foreach (var tier in new[] { RoomType.Elite, RoomType.Boss })
        {
            // Constructed tier counterexamples exercise the event rule; these are not Holes option executions.
            using var fixture = new EventBattle(4, tier: tier);
            Check(fixture.Room.RoomType == tier && fixture.Run.CurrentRoomCount == 2 &&
                fixture.Run.BaseRoom is EventRoom parent && fixture.Room.ParentEventId == parent.CanonicalEvent.Id &&
                JigsawHuntingNative.RootIsHunted(fixture.Players[0]),
                "A matching event stack carries the deliberately named native elite or boss encounter.");
            for (int index = 0; index < fixture.Players.Length; index++)
                if (index > 0)
                    await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), fixture.Players[index].Creature,
                        index, fixture.Players[index].Creature, null);
            await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
            for (int index = 0; index < fixture.Players.Length; index++)
                AssertOrdinary(fixture.Players[index], index, $"event tier={tier}, Artifact={index}");
        }
        using var elite = new EventBattle(4, tier: RoomType.Elite, eventChild: false);
        Check(elite.Run.CurrentRoomCount == 1 && elite.Room.ParentEventId == null &&
            elite.Room.RoomType == RoomType.Elite && JigsawHuntingNative.RootIsHunted(elite.Players[0]),
            "The control is a native top-level elite on an accepted hunted Elite map root.");
        for (int index = 1; index < elite.Players.Length; index++)
            await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), elite.Players[index].Creature,
                index, elite.Players[index].Creature, null);
        await Hook.BeforeCombatStart(elite.Run, elite.Battle.Combat);
        for (int index = 0; index < elite.Players.Length; index++)
        {
            var player = elite.Players[index];
            var history = DebuffsFor(player);
            Check(OpeningAmounts(player).SequenceEqual(new[] { index == 0 ? 2m : 0m, 0m, 0m }) &&
                player.Creature.GetPowerAmount<ArtifactPower>() == Math.Max(0, index - 1) &&
                history.Length == (index == 0 ? 1 : 0) &&
                history.All(entry => entry.Power is WeakPower && entry.Amount == 2 && entry.Applier == null),
                "The top-level elite retains one native Weak-two command and consumes at most one Artifact layer.");
        }
    }

    private static void AssertOrdinary(Player player, int artifact, string scope)
    {
        var expected = new[] { artifact < 1 ? 1m : 0m, artifact < 2 ? 1m : 0m, artifact < 3 ? 1m : 0m };
        var expectedTypes = new[] { typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower) }.Skip(artifact).ToArray();
        var history = DebuffsFor(player);
        Check(OpeningAmounts(player).SequenceEqual(expected) &&
            player.Creature.GetPowerAmount<ArtifactPower>() == Math.Max(0, artifact - 3),
            $"{scope}: Artifact blocks Weak then Frail then Vulnerable, consuming one layer per command.");
        Check(history.Select(entry => entry.Power.GetType()).SequenceEqual(expectedTypes) &&
            history.All(entry => entry.Amount == 1 && entry.Applier == null),
            $"{scope}: native history contains only each unblocked ordinary debuff once, in command order, on its original target.");
    }

    private enum OpeningScope { Hunted, Safe, NonJigsaw, NoActs, OtherAct, ParentMismatch, MissingParent,
        NonUnknownRoot, WrongRun, WrongCombat }

    private sealed class EventBattle : IDisposable
    {
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal HeadlessTestSetup.EncounterCombatScope Battle { get; }
        internal CombatRoom Room { get; }

        internal EventBattle(int count, OpeningScope scope = OpeningScope.Hunted,
            RoomType tier = RoomType.Monster, bool eventChild = true)
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([Players[0]],
                acts: scope == OpeningScope.NoActs ? [] : [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                seed: "HUNTING_85_HIVE_EVENT_BOUNDARIES");
            if (scope != OpeningScope.NoActs) Run.CurrentActIndex = scope == OpeningScope.OtherAct ? 2 : 1;
            EncounterModel encounter = tier switch
            {
                RoomType.Elite => ModelDb.Encounter<EntomancerElite>().ToMutable(),
                RoomType.Boss => ModelDb.Encounter<WaterfallGiantBoss>().ToMutable(),
                _ => ModelDb.Encounter<HolesEncounter>().ToMutable()
            };
            Battle = new HeadlessTestSetup.EncounterCombatScope(Run, encounter);
            try
            {
                foreach (var player in Players.Skip(1))
                { player.RunState = Run; Battle.Combat.AddPlayer(player); player.ResetCombatState(); }
                var parent = ModelDb.Event<FieldOfManSizedHoles>();
                if (eventChild && scope != OpeningScope.MissingParent) Run.PushRoom(new EventRoom(parent));
                Room = new CombatRoom(Battle.Combat)
                {
                    ParentEventId = !eventChild ? null : scope == OpeningScope.ParentMismatch ?
                        ModelDb.Event<InfestedAutomaton>().Id : parent.Id
                };
                Run.PushRoom(Room);
                if (scope is not (OpeningScope.NonJigsaw or OpeningScope.NoActs))
                {
                    var root = !eventChild ? JigsawRoom.Elite : scope == OpeningScope.NonUnknownRoot ? JigsawRoom.Combat : JigsawRoom.Unknown;
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(Players[0].NetId, scope != OpeningScope.Safe, root));
                    JigsawNativeSession.Restore(Run);
                }
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            Battle.Dispose();
        }
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool hunted = true, JigsawRoom roomType = JigsawRoom.Unknown)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
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
        int target = map.Nodes.First(node => node.Room == roomType && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            if (!hunted && node == target) Ok(map.SetRemainingSteps(1));
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentNode == target && map.CurrentRoomHunted == hunted && (hunted || map.RemainingSteps == 0),
            "Public room-entry transitions establish the accepted root punishment, including a safe last step reaching zero.");
        return map;
    }

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
}
