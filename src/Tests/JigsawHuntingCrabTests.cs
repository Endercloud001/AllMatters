using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Backgrounds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

internal static class JigsawHuntingCrabTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting crab native double arm opening grants Artifact three and preserves directions", Opening),
      ("Jigsaw hunting crab safe and hunted openings keep fixed layers and unrelated scaling for one to four combat roles", Scaling),
      ("Jigsaw hunting crab non Jigsaw other act and event scopes retain native openings", ScopeBoundaries),
      ("Jigsaw hunting crab reentrant Artifact source retains native scaling on both arms", ReentrantScaling),
      ("Jigsaw hunting crab awaited native initialization rejects stale identities and preserves dependency failures", AwaitBoundaries) ];

    private static async Task Opening()
    {
        using var battle = new CrabBattle();
        await battle.MountBackground();
        Check(JigsawHuntingNative.RootIsHunted(battle.Player), "The actual native Boss room has an accepted hunted root.");
        await battle.Crusher.AfterAddedToRoom();
        await battle.Rocket.AfterAddedToRoom();
        CheckNativeOpening(battle);
        CheckArtifacts(battle, 3);
    }

    private static void CheckNativeOpening(CrabBattle battle)
    {
        Check(battle.Crusher.Creature.GetPowerAmount<BackAttackLeftPower>() == 1 &&
            battle.Rocket.Creature.GetPowerAmount<BackAttackRightPower>() == 1 &&
            battle.Crusher.Creature.GetPowerAmount<CrabRagePower>() == 1 &&
            battle.Rocket.Creature.GetPowerAmount<CrabRagePower>() == 1 &&
            battle.Players.All(player => player.Creature.GetPowerAmount<SurroundedPower>() == 1),
            "Both actual native initializers preserve left/right direction, Crab Rage and every combat role's Surrounded.");
        Check(battle.Background.Visible, "The actual Kaiser Crab scene is made visible by its original initializer.");
        Check(battle.Combat.Encounter.MonstersWithSlots.Any(pair => pair.Item1 == battle.Crusher && pair.Item2 == "crusher") &&
            battle.Combat.Encounter.MonstersWithSlots.Any(pair => pair.Item1 == battle.Rocket && pair.Item2 == "rocket"),
            "The two original encounter models remain in their original named slots.");
        Check(battle.Crusher.NextMove.Id == "THRASH_MOVE", "The original Crusher opening move remains Thrash.");
#if STS2_PUBLIC
        Check(battle.Rocket.NextMove.Id == "TARGETING_RETICLE_MOVE", "The original public Rocket opening move remains Targeting Reticle.");
#else
        Check(battle.Rocket.NextMove.Id == "CALCULATING_MOVE", "The current beta Rocket opening move remains Calculating.");
#endif
    }

    private static void CheckArtifacts(CrabBattle battle, int expected)
    {
        foreach (var arm in new MonsterModel[] { battle.Crusher, battle.Rocket })
        {
            Check(arm.Creature.GetPowerAmount<ArtifactPower>() == expected,
                $"The {arm.GetType().Name} opens with Artifact {expected}; actual={arm.Creature.GetPowerAmount<ArtifactPower>()}.");
            var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Power is ArtifactPower && ReferenceEquals(entry.Actor, arm.Creature)).ToArray();
            Check(received.Length == (expected == 0 ? 0 : 1) &&
                received.All(entry => entry.Amount == expected && ReferenceEquals(entry.Applier, arm.Creature)),
                "The native history records the exact opening grant without an intermediate scaled amount.");
        }
    }

    private static async Task Scaling()
    {
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            using var battle = new CrabBattle(count, hunted ? OpeningScope.Hunted : OpeningScope.Safe);
            await battle.MountBackground();
            Check(battle.Run.Players.Count == 1 && battle.Combat.Players.Count == count,
                "One legal native session owner supplies one to four actual combat roles; these are not remote clients.");
            await battle.Crusher.AfterAddedToRoom();
            await battle.Rocket.AfterAddedToRoom();
            CheckNativeOpening(battle);
            CheckArtifacts(battle, hunted ? 3 : 0);
            foreach (var arm in new MonsterModel[] { battle.Crusher, battle.Rocket })
            {
                await PowerCmd.Remove<ArtifactPower>(arm.Creature);
                var independent = await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), arm.Creature, 2, arm.Creature, null);
                Check(independent?.Amount == count + 1 && CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                    .Last(entry => entry.Power is ArtifactPower && ReferenceEquals(entry.Actor, arm.Creature)).Amount == count + 1,
                    "After removing the opening power, another real Artifact source on the same arm retains native multiplayer scaling.");
            }
        }
    }

    private static async Task ScopeBoundaries()
    {
        foreach (var scope in new[] { OpeningScope.NonJigsaw, OpeningScope.OtherAct, OpeningScope.Event })
        {
            using var battle = new CrabBattle(4, scope);
            await battle.MountBackground();
            Check(scope != OpeningScope.NonJigsaw || JigsawNativeSession.TryGet(battle.Run) == null,
                "The non-Jigsaw control has no session rather than merely a safe root.");
            Check(scope != OpeningScope.Event || battle.Run.CurrentRoomCount == 2 &&
                battle.Run.BaseRoom is EventRoom parent && ((CombatRoom)battle.Run.CurrentRoom!).ParentEventId == parent.CanonicalEvent.Id,
                "The event control has a real matching parent and child room stack.");
            Check(scope != OpeningScope.OtherAct || battle.Run.Act is Glory, "The other-act control is actually in Glory.");
            await battle.Crusher.AfterAddedToRoom();
            await battle.Rocket.AfterAddedToRoom();
            await Hook.BeforeCombatStart(battle.Run, battle.Combat);
            CheckNativeOpening(battle);
            CheckArtifacts(battle, 0);
            foreach (var player in battle.Players)
            {
                int expected = scope == OpeningScope.Event ? 1 : 0;
                Check(player.Creature.GetPowerAmount<WeakPower>() == expected &&
                    player.Creature.GetPowerAmount<FrailPower>() == expected && player.Creature.GetPowerAmount<VulnerablePower>() == expected,
                    "Event-named Boss arms receive only the Hive ordinary player opening, while unrelated scopes receive no Hive rule.");
            }
        }
    }

    private sealed class Reentry(Creature target)
    {
        internal readonly Creature Target = target;
        internal bool Started;
        internal int Amount;
    }
    private static Reentry? reentry;

    private static async Task ReentrantScaling()
    {
        var patch = new Harmony("AllMatters.Tests.CrabReentry");
        patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforePowerAmountChanged)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingCrabTests), nameof(Reenter)));
        try
        {
            for (int count = 1; count <= 4; count++)
            {
                using var battle = new CrabBattle(count);
                await battle.MountBackground();
                foreach (var arm in new MonsterModel[] { battle.Crusher, battle.Rocket })
                {
                    var probe = new Reentry(arm.Creature);
                    reentry = probe;
                    await arm.AfterAddedToRoom();
                    Check(probe.Started && probe.Amount == count + 1 && arm.Creature.GetPowerAmount<ArtifactPower>() == 3,
                        "A different Artifact instance granted inside the awaited opening command keeps native scaling, then the opening grants three.");
                    var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                        .Where(entry => entry.Power is ArtifactPower && ReferenceEquals(entry.Actor, arm.Creature)).ToArray();
                    Check(received.Length == 2 && received[0].Amount == count + 1 && received[1].Amount == 3,
                        "Actual history separates the independent reentrant source from the fixed opening source.");
                }
                CheckNativeOpening(battle);
            }
        }
        finally { reentry = null; patch.UnpatchAll(patch.Id); }
    }

    private static void Reenter(PowerModel power, Creature target, ref Task __result)
    {
        if (power is ArtifactPower && reentry is { Started: false } probe && ReferenceEquals(probe.Target, target))
        {
            probe.Started = true;
            __result = TemporaryArtifact(__result, probe);
        }
    }

    private static async Task TemporaryArtifact(Task original, Reentry probe)
    {
        await original;
        await Task.Yield();
        var power = await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), probe.Target, 2, probe.Target, null);
        Check(power != null, "The reentrant source creates its own actual mutable Artifact.");
        probe.Amount = power!.Amount;
        await PowerCmd.Remove(power);
    }

    private sealed class AwaitProbe(Creature target)
    {
        internal readonly Creature Target = target;
        internal readonly TaskCompletionSource Reached = new();
        internal readonly TaskCompletionSource Release = new();
        internal bool Started;
        internal Exception? Failure;
    }
    private static AwaitProbe? awaited;

    private static async Task AwaitBoundaries()
    {
        var patch = new Harmony("AllMatters.Tests.CrabAwait");
        patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforePowerAmountChanged)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingCrabTests), nameof(PauseNativeDirection)));
        try
        {
            foreach (bool right in new[] { false, true })
            foreach (string boundary in new[] { "detached-arm", "dead-arm", "closed-session", "replacement-map", "replacement-room", "replacement-run", "dependency-failure" })
            {
                using var battle = new CrabBattle();
                await battle.MountBackground();
                MonsterModel arm = right ? battle.Rocket : battle.Crusher;
                var probe = new AwaitProbe(arm.Creature);
                awaited = probe;
                Task? operation = null;
                try
                {
                    operation = arm.AfterAddedToRoom();
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && arm.Creature.GetPowerAmount<ArtifactPower>() == 0,
                        "The real native initializer awaits its real direction command before granting any opening Artifact.");
                    if (boundary == "detached-arm")
                    {
                        battle.Combat.RemoveCreature(arm.Creature, unattach: false);
                        Check(!battle.Combat.ContainsCreature(arm.Creature) && ReferenceEquals(arm.CombatState, battle.Combat),
                            "The arm left the actual enemy roster while retaining its original combat reference.");
                    }
                    else if (boundary == "dead-arm")
                    {
                        arm.Creature.SetCurrentHpInternal(0);
                        Check(!arm.Creature.IsAlive, "The suspended original initializer now owns an actual zero-HP arm.");
                    }
                    else if (boundary == "closed-session") JigsawNativeSession.TryGet(battle.Run)!.Close();
                    else if (boundary == "replacement-map")
                    {
                        var previousMap = JigsawNativeSession.TryGet(battle.Run)!.Map;
                        JigsawCheckpoint.Capture(battle.Run, AcceptedRoot(battle.Player.NetId, true, JigsawRoom.Boss));
                        JigsawNativeSession.Restore(battle.Run);
                        Check(!ReferenceEquals(previousMap, JigsawNativeSession.TryGet(battle.Run)!.Map), "The awaiting initializer loses its original map identity.");
                    }
                    else if (boundary == "replacement-room")
                    {
                        var previousRoom = battle.Run.CurrentRoom;
                        battle.Run.PopCurrentRoom();
                        battle.Run.PushRoom(new CombatRoom(battle.Combat));
                        Check(!ReferenceEquals(previousRoom, battle.Run.CurrentRoom), "A different native room now owns the same combat.");
                    }
                    else if (boundary == "replacement-run")
                    {
                        var replacement = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                            acts: [ModelDb.Act<Overgrowth>()], seed: "CRAB_REPLACEMENT");
                        RunManager.Instance.CleanUp(false);
                        RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                        Check(!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), battle.Run), "The native manager now owns a replacement run.");
                    }
                    else probe.Failure = new InvalidOperationException("Injected native crab direction dependency failure");
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check(probe.Failure == null ? failure == null : ReferenceEquals(failure, probe.Failure),
                        "The original initialization completes unchanged or propagates its exact dependency failure.");
                    Check(arm.Creature.GetPowerAmount<ArtifactPower>() == 0 && !CombatManager.Instance.History.Entries
                        .OfType<PowerReceivedEntry>().Any(entry => entry.Power is ArtifactPower),
                        "An invalidated or failed original initializer appends no Artifact command or history.");
                }
                finally
                {
                    probe.Release.TrySetResult();
                    if (operation != null) try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { }
                    awaited = null;
                }
            }
        }
        finally { awaited = null; patch.UnpatchAll(patch.Id); }
    }

    private static void PauseNativeDirection(PowerModel power, Creature target, ref Task __result)
    {
        if (power is BackAttackLeftPower or BackAttackRightPower && awaited is { Started: false } probe && ReferenceEquals(probe.Target, target))
        {
            probe.Started = true;
            __result = PauseDirection(__result, probe);
        }
    }

    private static async Task PauseDirection(Task original, AwaitProbe probe)
    {
        await original;
        probe.Reached.TrySetResult();
        await probe.Release.Task;
        if (probe.Failure != null) throw probe.Failure;
    }

    private enum OpeningScope { Hunted, Safe, NonJigsaw, OtherAct, Event }

    // Only scene ownership is supplied here. The real two-slot encounter,
    // initializers, background scene and PowerCmd/history remain native.
    private sealed class CrabBattle : IDisposable
    {
        private readonly ulong? previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        private readonly Harmony harmony = new("AllMatters.Tests.CrabBackground");
        private IDisposable? atlases;
        private NCombatRoom? scene;
        private NCombatBackground? backgroundRoot;
        internal Player Player { get; }
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal Crusher Crusher { get; }
        internal Rocket Rocket { get; }
        internal NKaiserCrabBossBackground Background => backgroundRoot!.GetNode<NKaiserCrabBossBackground>("%KaiserCrab");

        internal CrabBattle(int count = 1, OpeningScope scope = OpeningScope.Hunted)
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Player = Players[0];
            Run = RunState.CreateForTest([Player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_85_CRAB");
            Run.CurrentActIndex = scope == OpeningScope.OtherAct ? 2 : 1;
            var encounter = ModelDb.Encounter<KaiserCrabBoss>().ToMutable();
            Combat = new CombatState(encounter, Run) { RoundNumber = 1 };
            try
            {
                Combat.AddPlayer(Player);
                Player.ResetCombatState();
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = Player.NetId;
                CombatManager.Instance.Reset(true);
                CombatManager.Instance.SetUpCombat(Combat);
                foreach (var player in Players.Skip(1))
                { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in encounter.MonstersWithSlots)
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                Crusher = Combat.Enemies.Select(creature => creature.Monster).OfType<Crusher>().Single();
                Rocket = Combat.Enemies.Select(creature => creature.Monster).OfType<Rocket>().Single();
                var parent = ModelDb.Event<FieldOfManSizedHoles>();
                var room = new CombatRoom(Combat) { ParentEventId = scope == OpeningScope.Event ? parent.Id : null };
                if (scope == OpeningScope.Event)
                    Run.PushRoom(new EventRoom(parent));
                Run.PushRoom(room);
                if (scope != OpeningScope.NonJigsaw)
                {
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(Player.NetId, scope != OpeningScope.Safe,
                        scope == OpeningScope.Event ? JigsawRoom.Unknown : JigsawRoom.Boss));
                    JigsawNativeSession.Restore(Run);
                }
                foreach (var arm in new MonsterModel[] { Crusher, Rocket })
                {
                    arm.SetUpForCombat();
                    arm.RollMove(Combat.PlayerCreatures);
                }
            }
            catch { Dispose(); throw; }
        }

        internal async Task MountBackground()
        {
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            JigsawHuntingNativeTests.RegisterScripts();
            await PreloadManager.LoadRoomCombatAssets(Combat.Encounter, Run);
            bool testing = TestMode.IsOn;
            try
            {
                TestMode.IsOn = false;
                scene = NCombatRoom.Create((CombatRoom)Run.CurrentRoom!, CombatRoomMode.ActiveCombat)!;
                backgroundRoot = Combat.Encounter.CreateBackground(Run.Act, NCombatRoom.GenerateBackgroundRngForCurrentPoint(Run));
            }
            finally { TestMode.IsOn = testing; }
            AccessTools.Property(typeof(NCombatRoom), nameof(NCombatRoom.Background)).SetValue(scene, backgroundRoot);
            current = this;
            harmony.Patch(AccessTools.PropertyGetter(typeof(NCombatRoom), nameof(NCombatRoom.Instance)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingCrabTests), nameof(RoomInstance)));
            await HeadlessUiHost.Mount(backgroundRoot);
            Check(Background.IsInsideTree() && !Background.Visible,
                "The encounter's actual Kaiser Crab background is mounted and initially hidden by native _Ready.");
        }

        internal NCombatRoom? Scene => scene;

        public void Dispose()
        {
            try
            {
                if (GodotObject.IsInstanceValid(backgroundRoot)) backgroundRoot.Free();
                if (GodotObject.IsInstanceValid(scene)) scene.Free();
            }
            finally
            {
                current = null;
                harmony.UnpatchAll(harmony.Id);
                atlases?.Dispose();
                JigsawNativeSession.TryGet(Run)?.Close();
                CombatManager.Instance.Reset(true);
                RunManager.Instance.CleanUp(false);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId;
            }
        }
    }

    private static CrabBattle? current;
    private static bool RoomInstance(ref NCombatRoom? __result)
    {
        if (current?.Scene == null) return true;
        __result = current.Scene;
        return false;
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool hunted, JigsawRoom targetRoom)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.TryDequeue(out int currentNode))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(currentNode)))
            {
                int next = edge.First == currentNode ? edge.Second : edge.First;
                if (previous.TryAdd(next, currentNode)) queue.Enqueue(next);
            }
        int boss = map.Nodes.First(node => node.Room == targetRoom && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = boss; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != boss) Ok(map.CompleteRoom(entry.Id));
        }
        return map;
    }

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
}
