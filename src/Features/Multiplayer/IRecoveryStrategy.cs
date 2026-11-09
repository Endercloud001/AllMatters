namespace AllMatters.Features.Multiplayer;

/// <summary>
/// Recovery strategy contract for multiplayer session restoration.
/// Implemented by exploration, save, and lobby recovery components.
/// </summary>
public interface IRecoveryStrategy
{
    /// <summary>Recovery phase this strategy handles.</summary>
    RecoveryPhase Phase { get; }

    /// <summary>
    /// Determine if this recovery strategy can handle the current session state.
    /// </summary>
    /// <param name="context">Current recovery context with session state.</param>
    /// <returns>True if this strategy applies to the current state.</returns>
    bool CanRecover(RecoveryContext context);

    /// <summary>
    /// Execute recovery for the session.
    /// </summary>
    /// <param name="context">Recovery context with session state.</param>
    /// <returns>Recovery result indicating success/failure and next phase.</returns>
    Task<RecoveryResult> RecoverAsync(RecoveryContext context);

    /// <summary>
    /// Validate that recovery completed successfully.
    /// </summary>
    /// <param name="context">Recovery context after execution.</param>
    /// <returns>True if state matches expected post-recovery conditions.</returns>
    bool ValidateRecovery(RecoveryContext context);
}

/// <summary>Recovery phase in the restoration flow.</summary>
public enum RecoveryPhase
{
    /// <summary>Initial connection handshake.</summary>
    Connection = 0,

    /// <summary>Player authentication and roster validation.</summary>
    Validation = 1,

    /// <summary>Lobby state synchronization (player list, ready states).</summary>
    Lobby = 2,

    /// <summary>Exploration map and node state recovery.</summary>
    Exploration = 3,

    /// <summary>Checkpoint save state restoration (inventory, progress).</summary>
    SaveState = 4,

    /// <summary>Recovery completed successfully.</summary>
    Completed = 5
}

/// <summary>Context passed through recovery pipeline.</summary>
public sealed class RecoveryContext
{
    /// <summary>Network session identifier.</summary>
    public required ulong SessionId { get; init; }

    /// <summary>Original player roster from checkpoint.</summary>
    public required IReadOnlyList<ulong> OriginalPlayers { get; init; }

    /// <summary>Currently connected players.</summary>
    public required IReadOnlyList<ulong> ConnectedPlayers { get; init; }

    /// <summary>Checkpoint data if available.</summary>
    public object? CheckpointData { get; init; }

    /// <summary>Current recovery phase.</summary>
    public RecoveryPhase CurrentPhase { get; set; }

    /// <summary>Accumulated error messages from recovery attempts.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>Custom state bag for strategy-specific data.</summary>
    public Dictionary<string, object> State { get; } = [];
}

/// <summary>Result of a recovery operation.</summary>
public sealed class RecoveryResult
{
    /// <summary>Whether recovery succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Next phase to transition to (if successful).</summary>
    public RecoveryPhase? NextPhase { get; init; }

    /// <summary>Error message if recovery failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether recovery should be retried.</summary>
    public bool CanRetry { get; init; }

    public static RecoveryResult Ok(RecoveryPhase nextPhase) => new()
    {
        Success = true,
        NextPhase = nextPhase
    };

    public static RecoveryResult Fail(string error, bool canRetry = false) => new()
    {
        Success = false,
        ErrorMessage = error,
        CanRetry = canRetry
    };
}
