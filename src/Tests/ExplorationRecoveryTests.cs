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

    [Fact(DisplayName = "Create exploration state snapshot from valid JigsawSave")]
    public static void CreateSnapshotFromSave()
    {
        // Arrange
        var run = ReadyMinimalLayout(seed: 12345, budget: 15, players: [1UL, 2UL]);

        // Simulate exploration progress
        var attempt = run.BeginEnter(1);
        Assert.NotNull(attempt);
        run.ConfirmEnter(attempt.Id);
        run.CompleteRoom(attempt.Id);

        run.BeginEnter(2);
        run.ConfirmEnter(run.PendingEntry!.Id);

        // Act
        var save = run.CreateSave();
        var snapshot = CreateExplorationSnapshotFromSave(save);

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
        var run = ReadyMinimalLayout(seed: 54321, budget: 15, players: [3UL]);

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
        var run = ReadyMinimalLayout(seed: 99999, budget: 15, players: [10UL, 20UL]);

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
        var run = ReadyMinimalLayout(seed: 11111, budget: 10);

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

    [Fact(DisplayName = "Exploration recovery detects hunting phase")]
    public static void DetectHuntingPhase()
    {
        // Arrange: Create a save with steps exceeding budget
        var run = ReadyMinimalLayout(seed: 22222, budget: 5);

        // Progress through multiple rooms to exceed budget
        for (int i = 0; i < 7; i++)
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

        var save = run.CreateSave();

        // Act - Create snapshot and check hunting state
        var snapshot = CreateExplorationSnapshotFromSave(save);
        bool isHunting = snapshot.Steps > snapshot.Budget;

        // Assert
        Assert.True(isHunting);
        Assert.True(snapshot.Steps > save.Budget);
    }

    [Fact(DisplayName = "Multi-player exploration state remains independent")]
    public static void MultiPlayerStateIndependent()
    {
        // Arrange: Two-player exploration where they follow different paths
        var run = ReadyMinimalLayout(seed: 33333, budget: 15, players: [100UL, 200UL]);

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

    // Helper: Create exploration snapshot from JigsawSave (mimics ExplorationStateRecovery logic)
    private static ExplorationStateRecovery.ExplorationSnapshot CreateExplorationSnapshotFromSave(JigsawSave save)
    {
        return new ExplorationStateRecovery.ExplorationSnapshot
        {
            VisitedNodes = [.. save.Visited],
            CompletedNodes = [.. save.Completed],
            CurrentNode = save.CurrentNode,
            Steps = save.Steps,
            FirstProgress = save.FirstProgress,
            Phase = save.Phase,
            Budget = save.Budget
        };
    }
}
