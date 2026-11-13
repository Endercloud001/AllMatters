using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingHiveEliteTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting Hive elite opening gives Weak two through native commands", EliteOpening),
      ("Jigsaw hunting Hive elite opening preserves native Artifact and scope for one to four combat roles", OpeningBoundaries) ];

    private static async Task EliteOpening()
    {
        using var fixture = new OpeningBattle(1, OpeningScope.HuntedHive);
        var player = fixture.Players[0];
        Check(fixture.Run.CurrentRoom is CombatRoom { ParentEventId: null, RoomType: RoomType.Elite } &&
            JigsawHuntingNative.RootIsHunted(player), "The native Hive elite consumes a matching accepted hunted Elite root.");
        await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
        Check(player.Creature.GetPowerAmount<WeakPower>() == 2,
            $"The real Hive elite opening gives exactly two Weak; actual={player.Creature.GetPowerAmount<WeakPower>()}.");
        Check(player.Creature.GetPowerAmount<FrailPower>() == 0 && player.Creature.GetPowerAmount<VulnerablePower>() == 0,
            "The elite opening does not also apply the ordinary Frail and Vulnerable effects.");
        var received = DebuffsFor(player);
        Check(received.Length == 1 && received[0].Power is WeakPower && received[0].Amount == 2,
            "Native PowerCmd history records one successful Weak two on the original player target.");
    }

    private static async Task OpeningBoundaries()
    {
        for (int count = 1; count <= 4; count++)
        foreach (var scope in new[] { OpeningScope.SafeHive, OpeningScope.HuntedHive })
        foreach (int artifact in new[] { 0, 1, 2 })
        {
            using var fixture = new OpeningBattle(count, scope);
            bool hunted = scope == OpeningScope.HuntedHive;
            Check(fixture.Battle.Combat.Players.Count == count && fixture.Run.Players.Count == 1 &&
                JigsawHuntingNative.RootIsHunted(fixture.Players[0]) == hunted,
                "The legal single-owner run supplies the requested actual combat roles and accepted root.");
            foreach (var player in fixture.Players)
                if (artifact > 0)
                    await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                        artifact, player.Creature, null);
            await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
            foreach (var player in fixture.Players)
            {
                int expectedWeak = hunted && artifact == 0 ? 2 : 0;
                Check(player.Creature.GetPowerAmount<WeakPower>() == expectedWeak &&
                    player.Creature.GetPowerAmount<FrailPower>() == 0 && player.Creature.GetPowerAmount<VulnerablePower>() == 0,
                    $"Hive elite count={count}, hunted={hunted}, Artifact={artifact}: each role receives only Weak {expectedWeak}.");
                Check(player.Creature.GetPowerAmount<ArtifactPower>() == Math.Max(0, artifact - (hunted ? 1 : 0)),
                    "Native Artifact blocks the single opening debuff and consumes exactly one layer only on a hunted root.");
                var received = DebuffsFor(player);
                Check(received.Length == (expectedWeak > 0 ? 1 : 0) &&
                    received.All(entry => entry.Power is WeakPower && entry.Amount == 2 && entry.Applier == null),
                    "Native history records one unblocked Weak two per role, without ordinary or blocked applications.");
                if (artifact == 0)
                {
                    await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), player.Creature, 4,
                        fixture.Battle.Monster.Creature, null);
                    Check(player.Creature.GetPowerAmount<WeakPower>() == expectedWeak + 4 &&
                        DebuffsFor(player).Last() is { Amount: 4 } later &&
                        ReferenceEquals(later.Applier, fixture.Battle.Monster.Creature),
                        "An independent later Weak source retains its four stacks and original native applier.");
                }
            }
        }
        foreach (var scope in new[] { OpeningScope.OtherAct, OpeningScope.NonJigsaw, OpeningScope.EventCombat })
        {
            using var fixture = new OpeningBattle(4, scope);
            Check(scope != OpeningScope.OtherAct || fixture.Run.Act is Glory,
                "The other-act counterexample uses the native Glory act.");
            Check(scope != OpeningScope.NonJigsaw || JigsawNativeSession.TryGet(fixture.Run) == null,
                "The non-Jigsaw counterexample has no bound session.");
            Check(scope != OpeningScope.EventCombat || fixture.Run.CurrentRoom is CombatRoom { ParentEventId: not null },
                "The event counterexample has an actual native parent-event identity.");
            await Hook.BeforeCombatStart(fixture.Run, fixture.Battle.Combat);
            foreach (var player in fixture.Players)
            {
                if (scope == OpeningScope.EventCombat)
                    Check(DebuffsFor(player).All(entry => entry.Power is not WeakPower || entry.Amount != 2),
                        "An event combat must not receive the elite Weak-two rule; ordinary event enhancement is separate coverage.");
                else
                    Check(DebuffsFor(player).Length == 0 && player.Creature.GetPowerAmount<WeakPower>() == 0 &&
                        player.Creature.GetPowerAmount<FrailPower>() == 0 && player.Creature.GetPowerAmount<VulnerablePower>() == 0,
                        "A non-Hive or non-Jigsaw native opening receives no Hive debuffs.");
            }
        }
    }

    private enum OpeningScope { HuntedHive, SafeHive, OtherAct, NonJigsaw, EventCombat }

    private sealed class OpeningBattle : IDisposable
    {
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal HeadlessTestSetup.EncounterCombatScope Battle { get; }

        internal OpeningBattle(int count, OpeningScope scope)
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            // One real session owner; extra roles exercise actual native combat targets, not remote clients.
            Run = RunState.CreateForTest([Players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()],
                seed: "HUNTING_85_HIVE_ELITE");
            Run.CurrentActIndex = scope == OpeningScope.OtherAct ? 2 : 1;
            Battle = new HeadlessTestSetup.EncounterCombatScope(Run, ModelDb.Encounter<EntomancerElite>().ToMutable());
            try
            {
                foreach (var player in Players.Skip(1))
                { player.RunState = Run; Battle.Combat.AddPlayer(player); player.ResetCombatState(); }
                Run.PushRoom(new CombatRoom(Battle.Combat)
                {
                    ParentEventId = scope == OpeningScope.EventCombat ? ModelDb.Event<InfestedAutomaton>().Id : null
                });
                if (scope != OpeningScope.NonJigsaw)
                {
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(scope != OpeningScope.SafeHive, Players[0].NetId,
                        scope == OpeningScope.EventCombat ? JigsawRoom.Unknown : JigsawRoom.Elite));
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

    private static PowerReceivedEntry[] DebuffsFor(Player player) => CombatManager.Instance.History.Entries
        .OfType<PowerReceivedEntry>().Where(entry => ReferenceEquals(entry.Actor, player.Creature) &&
            entry.Power is WeakPower or FrailPower or VulnerablePower).ToArray();

    private static JigsawRun AcceptedRoot(bool hunted, ulong owner, JigsawRoom roomType)
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
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room == JigsawRoom.Boss) continue;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == roomType && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "The accepted room entry establishes the requested authoritative punishment.");
        return map;
    }

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
}
