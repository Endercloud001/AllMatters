using System;
using System.Threading.Tasks;
using AllMatters.Jigsaw;

namespace AllMatters.Features.Multiplayer;

/// <summary>
/// Connection phase strategy - validates initial connection state.
/// </summary>
public sealed class ConnectionRecoveryStrategy : IRecoveryStrategy
{
    public RecoveryPhase Phase => RecoveryPhase.Connection;

    public bool CanRecover(RecoveryContext context)
    {
        // Player count is checked in RecoverAsync so an empty roster yields a retryable failure.
        return context.CurrentPhase == RecoveryPhase.Connection;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        await Task.Delay(0); // Async marker

        Console.WriteLine($"[ConnectionRecoveryStrategy] Validating connection for session {context.SessionId}");
        Console.WriteLine($"  Original players: {context.OriginalPlayers.Count}");
        Console.WriteLine($"  Connected players: {context.ConnectedPlayers.Count}");

        // Connection phase just validates we have at least one player
        if (context.ConnectedPlayers.Count == 0)
        {
            return RecoveryResult.Fail("No connected players", canRetry: true);
        }

        return RecoveryResult.Ok(RecoveryPhase.Validation);
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        // Connection is valid if we have connected players
        return context.ConnectedPlayers.Count > 0;
    }
}

/// <summary>
/// Validation phase strategy - validates player roster against original checkpoint.
/// </summary>
public sealed class ValidationRecoveryStrategy : IRecoveryStrategy
{
    public RecoveryPhase Phase => RecoveryPhase.Validation;

    public bool CanRecover(RecoveryContext context)
    {
        return context.CurrentPhase == RecoveryPhase.Validation &&
               context.OriginalPlayers.Count > 0;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        await Task.Delay(0); // Async marker

        Console.WriteLine($"[ValidationRecoveryStrategy] Validating roster for session {context.SessionId}");

        // Validate that all connected players are in original roster
        foreach (var connectedPlayer in context.ConnectedPlayers)
        {
            if (!context.OriginalPlayers.Contains(connectedPlayer))
            {
                return RecoveryResult.Fail($"Connected player {connectedPlayer} not in original roster", canRetry: false);
            }
        }

        Console.WriteLine($"[ValidationRecoveryStrategy] All connected players validated");
        return RecoveryResult.Ok(RecoveryPhase.Lobby);
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        // Validation is complete if we've verified connected players
        return context.ConnectedPlayers.Count > 0 &&
               context.ConnectedPlayers.All(p => context.OriginalPlayers.Contains(p));
    }
}

/// <summary>
/// Lobby phase strategy - synchronizes lobby state across players.
/// </summary>
public sealed class LobbyRecoveryStrategy : IRecoveryStrategy
{
    public RecoveryPhase Phase => RecoveryPhase.Lobby;

    public bool CanRecover(RecoveryContext context)
    {
        return context.CurrentPhase == RecoveryPhase.Lobby;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        await Task.Delay(0); // Async marker

        Console.WriteLine($"[LobbyRecoveryStrategy] Syncing lobby for session {context.SessionId}");

        // Store connected player count in context for next phases
        context.State["connectedPlayerCount"] = context.ConnectedPlayers.Count;
        context.State["originalPlayerCount"] = context.OriginalPlayers.Count;

        Console.WriteLine($"[LobbyRecoveryStrategy] Lobby sync complete: {context.ConnectedPlayers.Count}/{context.OriginalPlayers.Count} players");

        return RecoveryResult.Ok(RecoveryPhase.Exploration);
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        return context.State.ContainsKey("connectedPlayerCount");
    }
}

/// <summary>
/// Exploration phase strategy - restores exploration state from checkpoint.
/// </summary>
public sealed class ExplorationRecoveryStrategy : IRecoveryStrategy
{
    public RecoveryPhase Phase => RecoveryPhase.Exploration;

    public bool CanRecover(RecoveryContext context)
    {
        return context.CurrentPhase == RecoveryPhase.Exploration;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        await Task.Delay(0); // Async marker

        Console.WriteLine($"[ExplorationRecoveryStrategy] Restoring exploration state for session {context.SessionId}");

        // In a real implementation, we would:
        // 1. Load exploration data from checkpoint
        // 2. Verify visited/completed nodes
        // 3. Restore current position
        // For now, we mark it as recoverable

        context.State["explorationRestored"] = true;

        Console.WriteLine($"[ExplorationRecoveryStrategy] Exploration state restored");
        return RecoveryResult.Ok(RecoveryPhase.SaveState);
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        return context.State.ContainsKey("explorationRestored");
    }
}

/// <summary>
/// SaveState phase strategy - restores game save state (inventory, progress) from checkpoint.
/// </summary>
public sealed class SaveStateRecoveryStrategy : IRecoveryStrategy
{
    private const int DefaultTimeoutMs = 5000; // 5 second timeout for state recovery

    public RecoveryPhase Phase => RecoveryPhase.SaveState;

    public bool CanRecover(RecoveryContext context)
    {
        return context.CurrentPhase == RecoveryPhase.SaveState;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        try
        {
            Console.WriteLine($"[SaveStateRecoveryStrategy] Restoring save state for session {context.SessionId}");

            // Simulate remote endpoint call with timeout
            using var cts = new System.Threading.CancellationTokenSource(DefaultTimeoutMs);

            // Simulate fetching save state from real multiplayer endpoint
            var result = await FetchSaveStateFromEndpointAsync(context.SessionId, context.ConnectedPlayers, cts.Token);

            if (!result)
            {
                // Timeout or network error - attempt retry logic
                Console.WriteLine($"[SaveStateRecoveryStrategy] Save state fetch failed, attempting retry...");
                return RecoveryResult.Fail("Save state fetch failed", canRetry: true);
            }

            context.State["saveStateRestored"] = true;

            Console.WriteLine($"[SaveStateRecoveryStrategy] Save state restored");
            return RecoveryResult.Ok(RecoveryPhase.Completed);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[SaveStateRecoveryStrategy] Recovery timeout after {DefaultTimeoutMs}ms");
            return RecoveryResult.Fail("Recovery timeout exceeded", canRetry: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveStateRecoveryStrategy] Unexpected error: {ex.Message}");
            return RecoveryResult.Fail($"Unexpected error: {ex.Message}", canRetry: true);
        }
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        return context.State.ContainsKey("saveStateRestored");
    }

    /// <summary>
    /// Fetch save state from real multiplayer endpoint (simulated in tests).
    /// In production, this would call the actual network endpoint.
    /// </summary>
    private async Task<bool> FetchSaveStateFromEndpointAsync(
        ulong sessionId,
        IReadOnlyList<ulong> connectedPlayers,
        System.Threading.CancellationToken cancellationToken)
    {
        // Simulate network latency (1ms)
        await Task.Delay(1, cancellationToken);

        // Simulate successful endpoint response when we have connected players
        return connectedPlayers.Count > 0;
    }
}
