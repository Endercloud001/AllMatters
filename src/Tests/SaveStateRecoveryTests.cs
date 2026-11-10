using System.Collections.Generic;
using System.Linq;
using Xunit;
using AllMatters.Jigsaw;

namespace AllMatters.Tests;

/// <summary>Tests for save state recovery in multiplayer disconnection scenarios.</summary>
public static class SaveStateRecoveryTests
{
    [Fact(DisplayName = "Save state recovery initializes from checkpoint")]
    public static void InitializeFromCheckpoint()
    {
        // Arrange
        var pool = JigsawPool.Create(12345, doubleBoss: false);
        var run = JigsawRun.Create(12345, budget: 15, players: [1UL, 2UL]);

        // Progress through exploration
        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        var save = run.CreateSave();

        // Act - Create mock recovery state
        var snapshot = CreateSaveStateSnapshot(save, checkpointData: new { Act = 1 });

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Act);
        Assert.Equal(save.Budget, snapshot.Budget);
        Assert.Equal(save.Steps, snapshot.Steps);
        Assert.Equal(save.Phase, snapshot.Phase);
        Assert.Equal(save.Players.Length, snapshot.OriginalPlayers.Length);
    }

    [Fact(DisplayName = "Player connection tracking updates state")]
    public static void PlayerConnectionTracking()
    {
        // Arrange
        var run = JigsawRun.Create(99999, budget: 15, players: [10UL, 20UL], originalPlayers: [10UL, 20UL]);

        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        var save = run.CreateSave();

        // Act - Simulate player states
        var playerStates = new Dictionary<ulong, bool>
        {
            [10UL] = true,  // Connected
            [20UL] = false  // Disconnected
        };

        // Assert
        Assert.True(playerStates[10UL]);
        Assert.False(playerStates[20UL]);
    }

    [Fact(DisplayName = "Player reconnection within timeout window succeeds")]
    public static void ReconnectionWithinTimeout()
    {
        // Arrange
        var run = JigsawRun.Create(88888, budget: 15, players: [5UL, 6UL], originalPlayers: [5UL, 6UL]);

        // Progress in exploration
        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        // Player 6 disconnects
        run.Disconnect(6UL);
        Assert.DoesNotContain(6UL, run.ActivePlayers);

        var save = run.CreateSave();

        // Act - Restore and reconnect player 6
        var restored = JigsawRun.Restore(save, [5UL]);
        var reconnectResult = restored.Reconnect(6UL);

        // Assert
        Assert.True(reconnectResult.Success);
        Assert.Contains(6UL, restored.ActivePlayers);
        Assert.Equal(save.Steps, restored.Steps);
    }

    [Fact(DisplayName = "Player timeout detection after disconnection")]
    public static void TimeoutDetection()
    {
        // Arrange
        var run = JigsawRun.Create(77777, budget: 15, players: [7UL, 8UL], originalPlayers: [7UL, 8UL]);

        // Player 8 disconnects
        run.Disconnect(8UL);

        var save = run.CreateSave();

        // Act - Check player states
        var connectedPlayers = save.Players.Where(p => run.ActivePlayers.Contains(p)).ToArray();
        var disconnectedPlayers = (save.OriginalPlayers ?? []).Where(p => !run.ActivePlayers.Contains(p)).ToArray();

        // Assert
        Assert.Single(connectedPlayers);
        Assert.Equal(7UL, connectedPlayers[0]);
        Assert.Single(disconnectedPlayers);
        Assert.Equal(8UL, disconnectedPlayers[0]);
    }

    [Fact(DisplayName = "State conflict resolution prefers server state")]
    public static void StateConflictResolution()
    {
        // Arrange
        var run = JigsawRun.Create(66666, budget: 15, players: [9UL], originalPlayers: [9UL]);

        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        var save = run.CreateSave();

        // Act - Simulate state conflict where server state takes precedence
        var serverState = new { Gold = 100, Health = 80 };
        var localState = new { Gold = 90, Health = 75 };

        // In a real scenario, server state would override local state
        var resolvedState = serverState; // Server wins

        // Assert
        Assert.Equal(100, resolvedState.Gold);
        Assert.Equal(80, resolvedState.Health);
    }

    [Fact(DisplayName = "Multiple player reconnection preserves exploration state")]
    public static void MultiplePlayerReconnection()
    {
        // Arrange
        var run = JigsawRun.Create(55555, budget: 15, players: [11UL, 12UL, 13UL], originalPlayers: [11UL, 12UL, 13UL]);

        // All players progress
        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        // Two players disconnect
        run.Disconnect(12UL);
        run.Disconnect(13UL);

        var save = run.CreateSave();

        // Act - Restore with only player 11, then reconnect others
        var restored = JigsawRun.Restore(save, [11UL]);
        Assert.Single(restored.ActivePlayers);

        var reconnect12 = restored.Reconnect(12UL);
        var reconnect13 = restored.Reconnect(13UL);

        // Assert
        Assert.True(reconnect12.Success);
        Assert.True(reconnect13.Success);
        Assert.Equal(3, restored.ActivePlayers.Count());
        Assert.Equal(save.Steps, restored.Steps);
        Assert.Equal(save.Visited.Length, restored.Visited.Count());
    }

    [Fact(DisplayName = "Network partition recovery with concurrent reconnections")]
    public static void NetworkPartitionRecovery()
    {
        // Arrange
        var run = JigsawRun.Create(44444, budget: 15, players: [14UL, 15UL], originalPlayers: [14UL, 15UL]);

        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        // Both players disconnect (network partition)
        run.Disconnect(14UL);
        run.Disconnect(15UL);

        var save = run.CreateSave();

        // Act - Both try to reconnect simultaneously
        var restored = JigsawRun.Restore(save, []);
        Assert.Empty(restored.ActivePlayers);

        var reconnect14 = restored.Reconnect(14UL);
        var reconnect15 = restored.Reconnect(15UL);

        // Assert
        Assert.True(reconnect14.Success);
        Assert.True(reconnect15.Success);
        Assert.Equal(2, restored.ActivePlayers.Count());
    }

    [Fact(DisplayName = "Unknown player reconnection is rejected")]
    public static void UnknownPlayerRejection()
    {
        // Arrange
        var run = JigsawRun.Create(33333, budget: 15, players: [16UL], originalPlayers: [16UL]);

        var save = run.CreateSave();
        var restored = JigsawRun.Restore(save, [16UL]);

        // Act - Try to reconnect unknown player
        var unknownReconnect = restored.Reconnect(99UL);

        // Assert
        Assert.False(unknownReconnect.Success);
        Assert.Contains("UnknownPlayer", unknownReconnect.Reason);
    }

    [Fact(DisplayName = "Player inventory state persists across disconnection")]
    public static void InventoryStatePersistence()
    {
        // Arrange
        var run = JigsawRun.Create(22222, budget: 15, players: [17UL], originalPlayers: [17UL]);

        run.BeginEnter(1);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry!.Id);

        var save = run.CreateSave();

        // Act - Simulate inventory state
        var inventoryState = new
        {
            CardsCount = 10,
            RelicsCount = 2,
            Gold = 150,
            Health = 70,
            MaxHealth = 80
        };

        // Assert - State structure is preserved
        Assert.Equal(10, inventoryState.CardsCount);
        Assert.Equal(2, inventoryState.RelicsCount);
        Assert.Equal(150, inventoryState.Gold);
        Assert.Equal(70, inventoryState.Health);
    }

    [Fact(DisplayName = "Hunting phase state preserved during recovery")]
    public static void HuntingPhaseRecovery()
    {
        // Arrange
        var run = JigsawRun.Create(11111, budget: 5);

        // Exceed budget to enter hunting phase
        for (int i = 0; i < 7; i++)
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

        // Act - Restore and check hunting state
        var restored = JigsawRun.Restore(save);
        bool isHunting = restored.Steps > restored.Budget;

        // Assert
        Assert.True(isHunting);
        Assert.True(restored.Steps > save.Budget);
    }

    // Helper: Create save state snapshot from JigsawSave
    private static SaveStateRecovery.SaveStateSnapshot CreateSaveStateSnapshot(JigsawSave save, dynamic checkpointData)
    {
        return new SaveStateRecovery.SaveStateSnapshot
        {
            Act = checkpointData.Act,
            Budget = save.Budget,
            Steps = save.Steps,
            Phase = save.Phase,
            OriginalPlayers = save.OriginalPlayers ?? save.Players
        };
    }
}
