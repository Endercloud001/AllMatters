using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllMatters.Features.Multiplayer;
using Xunit;

namespace AllMatters.Tests.Integration;

/// <summary>
/// End-to-end integration tests for multiplayer recovery pipeline.
/// Tests the complete recovery flow from connection through state restoration.
/// </summary>
public sealed class MultiplayerRecoveryE2ETests
{
    /// <summary>
    /// Test single-player reconnection scenario.
    /// Player disconnects and reconnects; recovery should restore session state.
    /// </summary>
    [Fact(DisplayName = "Single player reconnection should complete recovery pipeline")]
    public async Task SinglePlayerReconnection_Should_RestoreSessionState()
    {
        // Arrange: Set up a multiplayer session with one player
        const ulong playerId = 1001UL;
        const ulong sessionId = 100UL;

        var orchestrator = CreateTestOrchestrator();
        var originalPlayers = new[] { playerId };
        var connectedPlayers = new[] { playerId };

        // Act: Initialize recovery context and execute pipeline
        var context = orchestrator.InitializeRecovery(
            sessionId,
            originalPlayers,
            connectedPlayers,
            checkpointData: null
        );

        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert: Verify recovery completed successfully
        Assert.True(outcome.Success, $"Recovery failed: {outcome.ErrorMessage}");
        Assert.Equal(RecoveryPhase.Completed, context.CurrentPhase);
        Assert.Empty(context.Errors);
    }

    /// <summary>
    /// Test multi-player simultaneous disconnection and reconnection.
    /// Multiple players disconnect; recovery should sync all upon reconnection.
    /// </summary>
    [Fact(DisplayName = "Multi-player simultaneous reconnection should sync all players")]
    public async Task MultiPlayerSimultaneousDisconnection_Should_SyncAllPlayers()
    {
        // Arrange: Set up session with 3 players
        const ulong sessionId = 200UL;
        var originalPlayers = new[] { 1001UL, 1002UL, 1003UL };
        var connectedPlayers = originalPlayers; // All 3 reconnect

        var orchestrator = CreateTestOrchestrator();

        // Act: Recover from network partition with all players reconnected
        var context = orchestrator.InitializeRecovery(
            sessionId,
            originalPlayers,
            connectedPlayers,
            checkpointData: null
        );

        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert: Verify multi-player sync
        Assert.True(outcome.Success);
        Assert.Equal(RecoveryPhase.Completed, context.CurrentPhase);
        Assert.Equal(3, context.ConnectedPlayers.Count);
        Assert.True(context.State.ContainsKey("connectedPlayerCount"));
        Assert.Equal(3, (int)context.State["connectedPlayerCount"]);
        Assert.Equal(3, (int)context.State["originalPlayerCount"]);
    }

    /// <summary>
    /// Test network partition recovery with partial reconnection.
    /// Multiple players disconnect; only some reconnect within timeout window.
    /// </summary>
    [Fact(DisplayName = "Partial player reconnection should complete with degraded roster")]
    public async Task NetworkPartition_Should_HandlePartialReconnection()
    {
        // Arrange: Set up session with 3 players, only 2 reconnect
        const ulong sessionId = 300UL;
        var originalPlayers = new[] { 2001UL, 2002UL, 2003UL };
        var connectedPlayers = new[] { 2001UL, 2003UL }; // P2 offline

        var orchestrator = CreateTestOrchestrator();

        // Act: Attempt recovery with partial roster
        var context = orchestrator.InitializeRecovery(
            sessionId,
            originalPlayers,
            connectedPlayers,
            checkpointData: null
        );

        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert: Verify degradation handling
        Assert.True(outcome.Success);
        Assert.Equal(RecoveryPhase.Completed, context.CurrentPhase);
        Assert.Equal(2, context.ConnectedPlayers.Count);
        // Session should still recover with 2/3 players
        Assert.True(context.State.ContainsKey("connectedPlayerCount"));
        Assert.Equal(2, (int)context.State["connectedPlayerCount"]);
        Assert.Equal(3, (int)context.State["originalPlayerCount"]);
    }

    /// <summary>
    /// Test recovery failure when no players are connected.
    /// </summary>
    [Fact(DisplayName = "Recovery should fail when no players are connected")]
    public async Task NoConnectedPlayers_Should_FailRecovery()
    {
        // Arrange
        const ulong sessionId = 400UL;
        var originalPlayers = new[] { 3001UL, 3002UL };
        var connectedPlayers = Array.Empty<ulong>(); // No one connected

        var orchestrator = CreateTestOrchestrator();

        // Act
        var context = orchestrator.InitializeRecovery(
            sessionId,
            originalPlayers,
            connectedPlayers,
            checkpointData: null
        );

        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert
        Assert.False(outcome.Success);
        Assert.True(outcome.CanRetry);
    }

    /// <summary>
    /// Test recovery failure when connected player is not in original roster.
    /// </summary>
    [Fact(DisplayName = "Recovery should fail when connected player not in original roster")]
    public async Task UnknownPlayer_Should_FailRecovery()
    {
        // Arrange
        const ulong sessionId = 500UL;
        var originalPlayers = new[] { 4001UL, 4002UL };
        var connectedPlayers = new[] { 9999UL }; // Unknown player

        var orchestrator = CreateTestOrchestrator();

        // Act
        var context = orchestrator.InitializeRecovery(
            sessionId,
            originalPlayers,
            connectedPlayers,
            checkpointData: null
        );

        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert
        Assert.False(outcome.Success);
        Assert.Contains("not in original roster", outcome.ErrorMessage ?? "");
    }

    /// <summary>
    /// Test session tracking during recovery.
    /// </summary>
    [Fact(DisplayName = "Orchestrator should track active sessions")]
    public async Task SessionTracking_Should_ManageActiveSessions()
    {
        // Arrange
        const ulong session1 = 1001UL;
        const ulong session2 = 1002UL;

        var orchestrator = CreateTestOrchestrator();

        var context1 = orchestrator.InitializeRecovery(
            session1,
            new[] { 101UL },
            new[] { 101UL },
            null
        );

        var context2 = orchestrator.InitializeRecovery(
            session2,
            new[] { 102UL },
            new[] { 102UL },
            null
        );

        // Act: Complete recovery for both sessions
        var outcome1 = await orchestrator.ExecuteRecoveryAsync(context1);
        var outcome2 = await orchestrator.ExecuteRecoveryAsync(context2);

        // Assert: Both should complete
        Assert.True(outcome1.Success);
        Assert.True(outcome2.Success);

        var activeSessions = orchestrator.GetActiveSessions();
        Assert.Equal(2, activeSessions.Count);

        // Complete sessions
        orchestrator.CompleteSession(session1);
        activeSessions = orchestrator.GetActiveSessions();
        Assert.Single(activeSessions);

        orchestrator.CompleteSession(session2);
        activeSessions = orchestrator.GetActiveSessions();
        Assert.Empty(activeSessions);
    }

    /// <summary>
    /// Helper to create a test multiplayer orchestrator with all recovery strategies.
    /// </summary>
    private static MultiplayerRecoveryOrchestrator CreateTestOrchestrator()
    {
        return new MultiplayerRecoveryOrchestrator(
            new ConnectionRecoveryStrategy(),
            new ValidationRecoveryStrategy(),
            new LobbyRecoveryStrategy(),
            new ExplorationRecoveryStrategy(),
            new SaveStateRecoveryStrategy()
        );
    }
}
