using System.Collections.Generic;
using System.Linq;
using Xunit;
using AllMatters.Jigsaw;

namespace AllMatters.Tests;

/// <summary>Tests for exploration state recovery in multiplayer disconnection scenarios.</summary>
public static class ExplorationRecoveryTests
{
    /// <summary>
    /// Creates a minimal JigsawRun ready for exploration (Ancient phase completed).
    /// Equivalent to JigsawDomainTests.Ready() but with minimal budget.
    /// </summary>
    private static JigsawRun ReadyMinimalLayout(int seed = 74, int budget = 15, bool doubleBoss = false, IEnumerable<ulong>? players = null)
    {
        var run = JigsawRun.Create(seed, budget, doubleBoss, players);
        var playerList = players?.ToList() ?? [1UL];
        var firstPlayer = playerList[0];

        Place(run, firstPlayer, 0, 3, 0);
        Ok(run.AddEdge(firstPlayer, 0, 1));

        // All players must vote to authorize inspection
        foreach (var player in playerList)
        {
            Ok(run.ChooseEnd(player, EndChoice.Manual));
        }

        Ok(run.Inspect());

        // Complete Ancient phase for all players
        foreach (var player in playerList)
        {
            Ok(run.CompleteAncient(player));
        }

        // Continue for all players
        foreach (var player in playerList)
        {
            Ok(run.Continue(player));
        }

        return run;
    }

    private static void Ok(JigsawResult result)
    {
        if (!result.Success)
            throw new InvalidOperationException($"Setup failed: {result.Reason}");
    }

    private static void Place(JigsawRun run, ulong player, int piece, int x, int y)
    {
        Ok(run.Take(player, piece));
        Ok(run.Drop(player, new(x, y)));
    }

    [Fact(DisplayName = "Production checkpoint reader loads exploration fields from valid JigsawSave")]
    public static void CreateSnapshotFromSave()
    {
        // Arrange
        var run = ReadyMinimalLayout(seed: 12345, budget: 15, doubleBoss: false, players: [1UL, 2UL]);

        // Simulate exploration progress
        var attempt = run.BeginEnter(1);
        Assert.NotNull(attempt);
        run.ConfirmEnter(attempt.Id);
        run.CompleteRoom(attempt.Id);

        run.BeginEnter(2);
        run.ConfirmEnter(run.PendingEntry!.Id);

        // Act
        var save = run.CreateSave();
        var snapshot = ReadCheckpointSnapshot(save);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(save.Phase, snapshot.Phase);
        Assert.Equal(save.Steps, snapshot.Steps);
        Assert.Equal(save.Budget, snapshot.Budget);
        Assert.Equal(save.Visited.Length, snapshot.VisitedNodes.Count);
        Assert.Equal(save.Completed.Length, snapshot.CompletedNodes.Count);
        foreach (var visited in save.Visited)
            Assert.Contains(visited, snapshot.VisitedNodes);
    }

    [Fact(DisplayName = "Restore exploration state preserves visited rooms")]
    public static void RestorePreservesVisited()
    {
        // Arrange
        var run = ReadyMinimalLayout(seed: 54321, budget: 15, doubleBoss: false, players: [3UL]);

        // Visit and complete multiple rooms
        var nodeIds = new List<int>();
        for (int i = 1; i <= 3; i++)
        {
            var attempt = run.BeginEnter(i);
            Assert.NotNull(attempt);
            run.ConfirmEnter(attempt.Id);
            nodeIds.Add(run.CurrentNode ?? 0);
            run.CompleteRoom(attempt.Id);
        }

        var save = run.CreateSave();

        // Act - Restore from save
        var restored = JigsawRun.Restore(save, [3UL]);

        // Assert
        Assert.Equal(save.Visited.Length, restored.Visited.Count());
        Assert.Equal(save.Completed.Length, restored.Completed.Count());
        foreach (var visited in save.Visited)
            Assert.Contains(visited, restored.Visited);
        foreach (var completed in save.Completed)
            Assert.Contains(completed, restored.Completed);
    }

    [Fact(DisplayName = "Exploration state recovery handles player reconnection")]
    public static void HandlePlayerReconnection()
    {
        // Arrange
        var run = ReadyMinimalLayout(seed: 99999, budget: 15, doubleBoss: false, players: [10UL, 20UL]);

        // Progress in exploration
        var attempt = run.BeginEnter(1);
        Assert.NotNull(attempt);
        run.ConfirmEnter(attempt.Id);
        run.CompleteRoom(attempt.Id);

        // Player 20 disconnects
        run.Disconnect(20UL);
        Assert.DoesNotContain(20UL, run.ActivePlayers);

        var save = run.CreateSave();

        // Act - Restore with only player 10, then reconnect player 20
        var restored = JigsawRun.Restore(save, [10UL]);
        Assert.Single(restored.ActivePlayers);

        var reconnectResult = restored.Reconnect(20UL);

        // Assert
        Assert.True(reconnectResult.Success);
        Assert.Contains(20UL, restored.ActivePlayers);
        // Exploration state should still be preserved
        Assert.Equal(save.Steps, restored.Steps);
        Assert.Equal(2, restored.Completed.Count()); // Ancient (node 0) + first room (node 1)
    }

    [Fact(DisplayName = "Exploration step tracking persists after recovery")]
    public static void StepTrackingPersists()
    {
        // Arrange
        var run = ReadyMinimalLayout(seed: 11111, budget: 10, doubleBoss: false);

        // Make several room entries to increment steps
        for (int i = 0; i < 5; i++)
        {
            var dest = run.AvailableDestinations.FirstOrDefault();
            if (dest == 0) break;

            var attempt = run.BeginEnter(dest);
            if (attempt != null)
            {
                run.ConfirmEnter(attempt.Id);
                run.CompleteRoom(attempt.Id);
            }
        }

        var initialSteps = run.Steps;
        var save = run.CreateSave();

        // Act - Restore
        var restored = JigsawRun.Restore(save);

        // Assert
        Assert.Equal(initialSteps, restored.Steps);
        Assert.Equal(run.RemainingSteps, restored.RemainingSteps);
    }

    [Fact(DisplayName = "Production checkpoint reader reports the independent hunting phase")]
    public static void DetectHuntingPhase()
    {
        var run = ReadyMinimalLayout(seed: 22222, budget: 15);
        Ok(run.SetRemainingSteps(1));
        var entry = run.BeginEnter(run.AvailableDestinations.First())!;
        Ok(run.ConfirmEnter(entry.Id));
        Assert.False(run.HuntingActive);
        Ok(run.CompleteRoom(entry.Id));
        var snapshot = ReadCheckpointSnapshot(run.CreateSave());
        Assert.True(snapshot.HuntingActive);
        Assert.Equal(0, snapshot.RemainingSteps);
        Assert.True(snapshot.Steps < snapshot.Budget);
    }

    [Fact(DisplayName = "Multi-player exploration state remains independent")]
    public static void MultiPlayerStateIndependent()
    {
        // Arrange: Two-player exploration where they follow different paths
        var run = ReadyMinimalLayout(seed: 33333, budget: 15, doubleBoss: false, players: [100UL, 200UL]);

        // Both enter first room and complete it (visited set will be updated)
        var firstDest = run.AvailableDestinations.First();
        var attempt = run.BeginEnter(firstDest);
        Assert.NotNull(attempt);
        run.ConfirmEnter(attempt.Id);
        run.CompleteRoom(attempt.Id);

        var save = run.CreateSave();

        // Act - Restore exploration state
        var restored = JigsawRun.Restore(save, [100UL, 200UL]);

        // Assert - Both players should see the same map state (visited/completed are global in Jigsaw)
        Assert.Equal(save.Visited.Length, restored.Visited.Count());
        Assert.Equal(save.Completed.Length, restored.Completed.Count());
    }

    /// <summary>
    /// Issue #92: hunting state survives a real save and restore. Steps are pushed past
    /// the budget through the public entry API only — a shop is the one room that stays
    /// available after it has been visited, so revisiting it needs no private state.
    /// The map's visit record is one shared set on the run, not a copy per player — the
    /// issue text's "per-player visit record" does not exist in the production code.
    /// </summary>
    [Fact(DisplayName = "Issue 92 hunting state survives save and restore")]
    public static void HuntingStateSurvivesRestore()
    {
        // The automatic path gives a full 14-piece layout, so it contains shops;
        // ReadyMinimalLayout places only piece 0 and may have none.
        // Seed and budget follow JigsawDomainTests.AutomaticTransaction; the solver needs a full-budget layout.
        var run = JigsawRun.Create(75, budget: 15);
        Place(run, 1UL, 0, 3, 0);
        Ok(run.AddEdge(1UL, 0, 1));
        Ok(run.ChooseEnd(1UL, EndChoice.Automatic));
        var candidate = JigsawAutomatic.Solve(run, 0);
        Assert.NotNull(candidate);
        Ok(run.FinishAutomatic(run.AutomaticOperation!, candidate));
        Ok(run.CompleteAncient(1UL));
        Ok(run.Continue(1UL));

        Ok(run.SetRemainingSteps(1));
        var shop = run.Nodes.First(node => node.Room == JigsawRoom.Shop).Id;
        // Walk the reachable map through the public entry API, preferring the shop so
        // steps keep climbing after every other room has been visited.
        for (int guard = 0; guard < 128 && run.Steps <= run.Budget; guard++)
        {
            int destination = run.AvailableDestinations.Contains(shop)
                ? shop
                : run.AvailableDestinations.First();
            var attempt = run.BeginEnter(destination);
            Assert.NotNull(attempt);
            Assert.True(run.ConfirmEnter(attempt.Id).Success);
            Assert.True(run.CompleteRoom(attempt.Id).Success);
        }

        Assert.True(run.Steps > run.Budget, "The public shop revisit path never reached the hunting phase.");
        Assert.Equal(0, run.RemainingSteps);

        var save = run.CreateSave();
        var restored = JigsawRun.Restore(save);

        Assert.True(restored.HuntingActive, "The authoritative hunting lifecycle survives CreateSave + Restore.");
        Assert.Equal(0, restored.RemainingSteps);
        Assert.Equal(run.Visited.OrderBy(id => id), restored.Visited.OrderBy(id => id));

        var snapshot = ReadCheckpointSnapshot(save);
        Assert.True(snapshot.HuntingActive, "The recovery snapshot must report the hunting phase.");
        Assert.Equal(0, snapshot.RemainingSteps);
    }

    // Uses the actual checkpoint slot and production reader, without an attached native session.
    internal static ExplorationStateRecovery.ExplorationSnapshot ReadCheckpointSnapshot(JigsawSave save)
    {
        var player = MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1);
        var native = MegaCrit.Sts2.Core.Runs.RunState.CreateForTest(players: [player], acts: [], seed: "RECOVERY_READER");
        JigsawCheckpoint.Slot.Set(native, new() { Version = 2, Act = 0, Map = save });
        var reader = ExplorationStateRecovery.GetOrCreate(native)!;
        Assert.True(reader.InitializeFromCheckpoint());
        return reader.GetSnapshot()!;
    }
}
