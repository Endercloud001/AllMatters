## Multiplayer Recovery Integration Infrastructure

This module provides the orchestration framework for multiplayer session recovery. It coordinates recovery across exploration, save state, and lobby components.

### Files

- **`IRecoveryStrategy.cs`**: Contract for recovery phase handlers
  - Defines `IRecoveryStrategy` interface
  - Enum `RecoveryPhase` with 6 phases (Connection → Completed)
  - `RecoveryContext`, `RecoveryResult`, `RecoveryOutcome` types

- **`MultiplayerRecoveryOrchestrator.cs`**: Pipeline coordinator
  - Manages strategy registration and execution
  - Transitions between recovery phases
  - Tracks session recovery state
  - Handles error and retry logic

### Recovery Pipeline

```
Connection → Validation → Lobby → Exploration → SaveState → Completed
```

Each phase is handled by an implementation of `IRecoveryStrategy`. The orchestrator
executes them sequentially, advancing based on phase requirements.

### Integration Status

| Phase | Status | Source |
|-------|--------|--------|
| Connection | Framework ready | #94 |
| Validation | Framework ready | #94 |
| Lobby | Implemented | #91 (LobbyRecovery) |
| Exploration | Pending | #92 |
| SaveState | Pending | #93 |

### Usage Example

```csharp
// Prepare orchestrator with strategies
var orchestrator = new MultiplayerRecoveryOrchestrator(
    new LobbyRecoveryStrategy(),        // From #91
    new ExplorationRecoveryStrategy(),  // From #92 (pending)
    new SaveStateRecoveryStrategy()     // From #93 (pending)
);

// Initialize recovery context
var context = orchestrator.InitializeRecovery(
    sessionId: 12345,
    originalPlayers: new[] { 1UL, 2UL, 3UL },
    connectedPlayers: new[] { 1UL, 3UL },
    checkpointData: checkpoint
);

// Execute recovery pipeline
var outcome = await orchestrator.ExecuteRecoveryAsync(context);

if (outcome.Success)
{
    // Continue gameplay
}
else if (outcome.CanRetry)
{
    // Retry recovery
}
else
{
    // Handle permanent failure
}
```

### Degradation Strategy

Supports partial recovery when not all players reconnect:
- Mark offline players while preserving their state
- Allow remaining players to continue
- Enable resume-with-full-roster option later

### Testing

See `src/Tests/Integration/RecoveryPipelineStubTests.cs` for test scenarios:
1. One connected player reaches Completed (stub strategies)
2. Full roster connected reaches Completed (stub strategies)
3. Partial roster connected reaches Completed (stub strategies)

Test implementations pending completion of #92 and #93.
