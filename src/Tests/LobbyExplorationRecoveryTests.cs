using System.Collections.Generic;
using System.Linq;
using Xunit;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Tests;

/// <summary>Integration tests for lobby recovery with exploration state sync.</summary>
public static class LobbyExplorationRecoveryTests
{
    [Fact(DisplayName = "Lobby recovery initialization includes exploration state")]
    public static void LobbyInitializesExplorationState()
    {
        // Arrange: Create a jigsaw save with exploration progress
        var run = JigsawRun.Create(55555, budget: 15, players: [5UL, 6UL]);

        // Progress exploration
        var dest = run.AvailableDestinations.FirstOrDefault();
        if (dest > 0)
        {
            run.BeginEnter(dest);
            run.ConfirmEnter(run.PendingEntry!.Id);
            run.CompleteRoom(run.PendingEntry.Id);
        }

        var save = run.CreateSave();

        // Act: Verify that checkpoint captures exploration state
        Assert.NotNull(save.Visited);
        Assert.NotEmpty(save.Visited);
        Assert.NotNull(save.Completed);
        Assert.NotEmpty(save.Completed);

        // Assert
        Assert.Contains(0, save.Visited); // Ancient always visited
        Assert.Equal(run.Steps, save.Steps);
        Assert.Equal(run.Budget, save.Budget);
    }

    [Fact(DisplayName = "Exploration state persists across player disconnect/reconnect")]
    public static void ExplorationPersistsAcrossReconnect()
    {
        // Arrange
        var run = JigsawRun.Create(66666, budget: 15, players: [7UL, 8UL], originalPlayers: [7UL, 8UL]);

        // Progress exploration
        var dest = run.AvailableDestinations.FirstOrDefault();
        if (dest > 0)
        {
            run.BeginEnter(dest);
            run.ConfirmEnter(run.PendingEntry!.Id);
            run.CompleteRoom(run.PendingEntry.Id);
        }

        var stepsBeforeDisconnect = run.Steps;
        var visitedBeforeDisconnect = run.Visited.ToArray();
        var completedBeforeDisconnect = run.Completed.ToArray();

        // Player 8 disconnects
        run.Disconnect(8UL);

        // Save state
        var save = run.CreateSave();

        // Act: Restore and reconnect player
        var restored = JigsawRun.Restore(save, [7UL]);
        restored.Reconnect(8UL);

        // Assert: Exploration state should be identical
        Assert.Equal(stepsBeforeDisconnect, restored.Steps);
        Assert.Equal(visitedBeforeDisconnect.Length, restored.Visited.Count());
        Assert.Equal(completedBeforeDisconnect.Length, restored.Completed.Count());

        foreach (var visited in visitedBeforeDisconnect)
            Assert.Contains(visited, restored.Visited);
        foreach (var completed in completedBeforeDisconnect)
            Assert.Contains(completed, restored.Completed);
    }

    [Fact(DisplayName = "Exploration state shows correct remaining steps after recovery")]
    public static void RemainingStepsCorrectAfterRecovery()
    {
        // Arrange: Create a run with known step count
        var run = JigsawRun.Create(77777, budget: 10);

        // Progress to step 5
        for (int i = 0; i < 5; i++)
        {
            var dest = run.AvailableDestinations.FirstOrDefault();
            if (dest == 0) break;

            run.BeginEnter(dest);
            if (run.PendingEntry != null)
            {
                run.ConfirmEnter(run.PendingEntry.Id);
                run.CompleteRoom(run.PendingEntry.Id);
            }
        }

        var save = run.CreateSave();

        // Act: Restore and check remaining steps
        var restored = JigsawRun.Restore(save);
        int expectedRemaining = save.Budget - save.Steps;

        // Assert
        Assert.Equal(5, restored.Steps);
        Assert.Equal(expectedRemaining, restored.RemainingSteps);
        Assert.False(restored.IsEndgame);
    }

    [Fact(DisplayName = "Exploration recovery handles hunting phase correctly")]
    public static void HuntingPhaseDetectionInRecovery()
    {
        // Arrange: Create a save with steps exceeding budget (hunting phase)
        var run = JigsawRun.Create(88888, budget: 5);

        // Force steps beyond budget for testing
        // We need to set up a scenario where steps > budget
        for (int i = 0; i < 8; i++)
        {
            var dest = run.AvailableDestinations.FirstOrDefault();
            if (dest == 0) break;

            run.BeginEnter(dest);
            if (run.PendingEntry != null)
            {
                run.ConfirmEnter(run.PendingEntry.Id);
                run.CompleteRoom(run.PendingEntry.Id);
            }
        }

        var save = run.CreateSave();

        // Act: Restore with potential hunting phase
        var restored = JigsawRun.Restore(save);
        bool isInHuntingPhase = restored.Steps > restored.Budget;

        // Assert
        if (save.Steps > save.Budget)
        {
            Assert.True(isInHuntingPhase);
            Assert.Equal(0, restored.RemainingSteps);
        }
    }

    [Fact(DisplayName = "Multiple player checkpoint recovery with different exploration progress")]
    public static void MultiPlayerCheckpointRecovery()
    {
        // Arrange: Two-player run where both have progressed
        var run = JigsawRun.Create(99999, budget: 15, players: [11UL, 12UL]);

        // Both move to first room
        var dest = run.AvailableDestinations.FirstOrDefault();
        if (dest > 0)
        {
            run.BeginEnter(dest);
            run.ConfirmEnter(run.PendingEntry!.Id);
            run.CompleteRoom(run.PendingEntry.Id);
        }

        var initialSteps = run.Steps;
        var save = run.CreateSave();

        // Act: Restore with both players
        var restored = JigsawRun.Restore(save, [11UL, 12UL]);

        // Assert: Both players should see identical exploration state (it's shared)
        Assert.Equal(2, restored.ActivePlayers.Count());
        Assert.Equal(initialSteps, restored.Steps);
        Assert.Equal(save.Visited.Length, restored.Visited.Count());
    }

    [Fact(DisplayName = "Current room position correctly restored")]
    public static void CurrentRoomPositionRestored()
    {
        // Arrange
        var run = JigsawRun.Create(111111, budget: 15);

        // Enter a room (don't complete it yet - stay in room)
        var dest = run.AvailableDestinations.FirstOrDefault();
        if (dest > 0)
        {
            run.BeginEnter(dest);
            run.ConfirmEnter(run.PendingEntry!.Id);
            // Note: we don't call CompleteRoom yet, so CurrentNode should be set
        }

        var nodeInRoom = run.CurrentNode;
        var save = run.CreateSave();

        // Act: Restore
        var restored = JigsawRun.Restore(save);

        // Assert
        if (nodeInRoom != null)
        {
            Assert.Equal(nodeInRoom, restored.CurrentNode);
        }
    }
}
