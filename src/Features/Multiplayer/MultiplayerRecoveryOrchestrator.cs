using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AllMatters.Features.Multiplayer;

/// <summary>
/// Orchestrates multiplayer session recovery across multiple recovery strategies.
/// Manages recovery state machine transitions and coordinates recovery pipeline.
/// </summary>
public sealed class MultiplayerRecoveryOrchestrator
{
    private readonly List<IRecoveryStrategy> strategies;
    private readonly Dictionary<ulong, RecoverySessionState> activeSessions = [];
    private RecoveryContext? currentContext;
    private RecoveryPhase currentPhase = RecoveryPhase.Connection;

    /// <summary>Create orchestrator with recovery strategies.</summary>
    public MultiplayerRecoveryOrchestrator(params IRecoveryStrategy[] recoveryStrategies)
    {
        if (recoveryStrategies == null || recoveryStrategies.Length == 0)
            throw new ArgumentException("At least one recovery strategy is required.");

        strategies = recoveryStrategies.OrderBy(s => s.Phase).ToList();
    }

    /// <summary>Initialize recovery for a session.</summary>
    /// <param name="sessionId">Network session identifier.</param>
    /// <param name="originalPlayers">Original player roster from checkpoint.</param>
    /// <param name="connectedPlayers">Currently connected players.</param>
    /// <param name="checkpointData">Optional checkpoint state to restore.</param>
    /// <returns>Recovery context prepared for pipeline execution.</returns>
    public RecoveryContext InitializeRecovery(
        ulong sessionId,
        IReadOnlyList<ulong> originalPlayers,
        IReadOnlyList<ulong> connectedPlayers,
        object? checkpointData = null)
    {
        currentContext = new RecoveryContext
        {
            SessionId = sessionId,
            OriginalPlayers = originalPlayers,
            ConnectedPlayers = connectedPlayers,
            CheckpointData = checkpointData,
            CurrentPhase = RecoveryPhase.Connection
        };

        activeSessions[sessionId] = new RecoverySessionState
        {
            SessionId = sessionId,
            Phase = RecoveryPhase.Connection,
            StartedAt = DateTime.UtcNow
        };

        Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Initialized recovery for session {sessionId}");
        Console.WriteLine($"  Original players: {originalPlayers.Count}, Connected: {connectedPlayers.Count}");

        return currentContext;
    }

    /// <summary>
    /// Execute recovery pipeline, transitioning through phases until completion or failure.
    /// </summary>
    /// <param name="context">Recovery context to process.</param>
    /// <returns>Final recovery outcome.</returns>
    public async Task<RecoveryOutcome> ExecuteRecoveryAsync(RecoveryContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        currentContext = context;
        var sessionState = activeSessions.GetValueOrDefault(context.SessionId);
        if (sessionState == null)
            return RecoveryOutcome.Failed("Session not initialized");

        try
        {
            while (currentPhase != RecoveryPhase.Completed)
            {
                Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Entering phase: {currentPhase}");

                var strategy = strategies.FirstOrDefault(s => s.Phase == currentPhase);
                if (strategy == null)
                {
                    return RecoveryOutcome.Failed($"No strategy for phase {currentPhase}");
                }

                if (!strategy.CanRecover(context))
                {
                    Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Strategy cannot recover in current state");
                    return RecoveryOutcome.Failed($"Recovery not applicable at phase {currentPhase}");
                }

                var result = await strategy.RecoverAsync(context);
                if (!result.Success)
                {
                    sessionState.Phase = RecoveryPhase.Completed;
                    context.Errors.Add(result.ErrorMessage ?? "Unknown error");
                    Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Recovery failed: {result.ErrorMessage}");
                    return RecoveryOutcome.Failed(result.ErrorMessage ?? "Recovery failed", canRetry: result.CanRetry);
                }

                if (!strategy.ValidateRecovery(context))
                {
                    context.Errors.Add($"Validation failed for phase {currentPhase}");
                    return RecoveryOutcome.Failed($"Post-recovery validation failed at {currentPhase}");
                }

                if (result.NextPhase == null)
                    break;

                currentPhase = result.NextPhase.Value;
                context.CurrentPhase = currentPhase;
                sessionState.Phase = currentPhase;
            }

            currentPhase = RecoveryPhase.Completed;
            sessionState.Phase = RecoveryPhase.Completed;
            sessionState.CompletedAt = DateTime.UtcNow;

            Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Recovery completed for session {context.SessionId}");
            return RecoveryOutcome.Succeeded();
        }
        catch (Exception ex)
        {
            context.Errors.Add(ex.Message);
            sessionState.Phase = RecoveryPhase.Completed;
            Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Unexpected error: {ex.Message}");
            return RecoveryOutcome.Failed($"Unexpected error: {ex.Message}", canRetry: true);
        }
    }

    /// <summary>Get current recovery phase for a session.</summary>
    public RecoveryPhase GetSessionPhase(ulong sessionId)
    {
        return activeSessions.TryGetValue(sessionId, out var state)
            ? state.Phase
            : RecoveryPhase.Connection;
    }

    /// <summary>Get all active recovery sessions.</summary>
    public IReadOnlyDictionary<ulong, RecoverySessionState> GetActiveSessions() =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<ulong, RecoverySessionState>(activeSessions);

    /// <summary>Clear session from recovery pipeline.</summary>
    public void CompleteSession(ulong sessionId)
    {
        if (activeSessions.Remove(sessionId))
        {
            Console.WriteLine($"[MultiplayerRecoveryOrchestrator] Cleared session {sessionId}");
        }
    }
}

/// <summary>Recovery state for a single session.</summary>
public sealed class RecoverySessionState
{
    public required ulong SessionId { get; init; }
    public RecoveryPhase Phase { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan ElapsedTime => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>Final recovery outcome.</summary>
public sealed class RecoveryOutcome
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool CanRetry { get; init; }

    public static RecoveryOutcome Succeeded() => new() { Success = true };

    public static RecoveryOutcome Failed(string error, bool canRetry = false) => new()
    {
        Success = false,
        ErrorMessage = error,
        CanRetry = canRetry
    };
}
