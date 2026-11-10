using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AllMatters.Jigsaw;

/// <summary>Lobby recovery for multiplayer sessions. Handles reconnection and roster sync.</summary>
public sealed class LobbyRecovery
{
    /// <summary>Outcome of a <see cref="OnPlayerConnected"/> call.</summary>
    public enum ConnectionResult
    {
        /// <summary>Player was newly admitted to the connected set.</summary>
        Admitted,
        /// <summary>Player was already in the connected set; no change.</summary>
        Duplicate,
        /// <summary>Player is not in the original checkpoint roster; rejected.</summary>
        UnknownPlayer,
        /// <summary>Recovery has not been initialized from a checkpoint yet.</summary>
        NotInitialized,
    }

    private static readonly ConditionalWeakTable<RunState, LobbyRecovery> recoveries = new();

    /// <summary>Get or create lobby recovery for a native run session.</summary>
    public static LobbyRecovery? GetOrCreate(RunState native)
    {
        if (!recoveries.TryGetValue(native, out var recovery))
        {
            recovery = new LobbyRecovery(native);
            recoveries.Add(native, recovery);
        }
        return recovery;
    }

    private readonly RunState nativeRun;
    // Ordered list from checkpoint; never mutated after InitializeFromCheckpoint returns true.
    private readonly List<ulong> originalPlayers = [];
    // Concurrent-safe connected set.
    private readonly ConcurrentDictionary<ulong, bool> connectedSet = new();
    private volatile bool recovered;

    /// <summary>Whether lobby recovery has been initialized.</summary>
    public bool IsRecovered => recovered;

    private LobbyRecovery(RunState native)
    {
        nativeRun = native;
    }

    /// <summary>Initialize lobby recovery from checkpoint save.</summary>
    /// <returns>True if recovery initialization succeeded.</returns>
    public bool InitializeFromCheckpoint()
    {
        if (recovered)
            return false;

        // Extract jigsaw checkpoint containing original player list
        if (JigsawCheckpoint.Slot.TryGet(nativeRun, out var checkpointData) &&
            checkpointData?.Map is { } mapSave && mapSave.OriginalPlayers is { } originalPlayerList)
        {
            // Store original players from checkpoint in checkpoint order.
            originalPlayers.Clear();
            originalPlayers.AddRange(originalPlayerList);
            Console.WriteLine($"[LobbyRecovery] Loaded checkpoint with {originalPlayers.Count} original players (Act {checkpointData.Act})");
            recovered = true;
            return true;
        }

        Console.WriteLine("[LobbyRecovery] Checkpoint found but no original player list");
        return false;
    }

    /// <summary>
    /// Sync player connection state to lobby.
    /// Thread-safe: may be called concurrently from multiple connection events.
    /// </summary>
    /// <param name="playerId">The network ID of the connecting player.</param>
    /// <returns>
    /// <see cref="ConnectionResult.Admitted"/> on first join,
    /// <see cref="ConnectionResult.Duplicate"/> when already connected,
    /// <see cref="ConnectionResult.UnknownPlayer"/> when not in the original roster,
    /// <see cref="ConnectionResult.NotInitialized"/> when called before checkpoint load.
    /// </returns>
    public ConnectionResult OnPlayerConnected(ulong playerId)
    {
        if (!recovered)
            return ConnectionResult.NotInitialized;

        if (!originalPlayers.Contains(playerId))
        {
            Console.WriteLine($"[LobbyRecovery] Rejected unknown player: NetId={playerId}");
            return ConnectionResult.UnknownPlayer;
        }

        bool added = connectedSet.TryAdd(playerId, true);
        if (added)
        {
            Console.WriteLine($"[LobbyRecovery] Player connected: NetId={playerId}");
            return ConnectionResult.Admitted;
        }
        return ConnectionResult.Duplicate;
    }

    /// <summary>Handle player disconnection during recovery.</summary>
    public void OnPlayerDisconnected(ulong playerId)
    {
        if (!recovered)
            return;

        if (connectedSet.TryRemove(playerId, out _))
        {
            Console.WriteLine($"[LobbyRecovery] Player disconnected: NetId={playerId}");
        }
    }

    /// <summary>Check if lobby is ready to proceed (all original players reconnected).</summary>
    public bool IsLobbyReady()
    {
        if (!recovered)
            return false;

        return originalPlayers.Count > 0 &&
            originalPlayers.All(id => connectedSet.ContainsKey(id));
    }

    /// <summary>Get current lobby state snapshot with save and exploration state.</summary>
    public LobbyState GetLobbyState()
    {
        // Build connected IDs in original checkpoint order for deterministic snapshots.
        var orderedConnected = originalPlayers.Where(id => connectedSet.ContainsKey(id)).ToArray();

        // Capture save state recovery info if available
        SaveStateRecovery.SaveStateSnapshot? saveState = null;
        if (SaveStateRecovery.GetOrCreate(nativeRun) is { } saveRecovery && saveRecovery.IsInitialized)
        {
            saveState = saveRecovery.GetSnapshot();
        }

        // Capture exploration state recovery info if available
        var explorationRecovery = ExplorationStateRecovery.GetOrCreate(nativeRun);
        var explorationSnapshot = explorationRecovery?.GetSnapshot();

        return new LobbyState
        {
            OriginalPlayerCount = originalPlayers.Count,
            ConnectedPlayerCount = orderedConnected.Length,
            IsRecovered = recovered,
            ConnectedPlayerIds = orderedConnected,
            IsOriginalRosterReady = recovered && originalPlayers.Count > 0 &&
                orderedConnected.Length == originalPlayers.Count,
            SaveState = saveState != null ? new SaveStateInfo
            {
                Act = saveState.Act,
                Budget = saveState.Budget,
                Steps = saveState.Steps,
                Phase = saveState.Phase.ToString()
            } : null,
            ExplorationState = explorationSnapshot != null ? new ExplorationStateInfo
            {
                VisitedCount = explorationSnapshot.VisitedNodes.Count,
                CompletedCount = explorationSnapshot.CompletedNodes.Count,
                CurrentNode = explorationSnapshot.CurrentNode,
                Steps = explorationSnapshot.Steps,
                RemainingSteps = Math.Max(0, explorationSnapshot.Budget - explorationSnapshot.Steps),
                IsHunting = explorationSnapshot.Steps > explorationSnapshot.Budget,
                Phase = explorationSnapshot.Phase
            } : null
        };
    }

    /// <summary>Snapshot of current lobby state for sync.</summary>
    public sealed class LobbyState
    {
        public int OriginalPlayerCount { get; init; }
        public int ConnectedPlayerCount { get; init; }
        public bool IsRecovered { get; init; }
        public ulong[] ConnectedPlayerIds { get; init; } = [];
        public bool IsOriginalRosterReady { get; init; }
        public SaveStateInfo? SaveState { get; init; }
        public ExplorationStateInfo? ExplorationState { get; init; }
    }

    /// <summary>Snapshot of game save state for lobby sync.</summary>
    public sealed class SaveStateInfo
    {
        public required int Act { get; init; }
        public required int Budget { get; init; }
        public required int Steps { get; init; }
        public required string Phase { get; init; }
    }

    /// <summary>Exploration state information for lobby sync.</summary>
    public sealed class ExplorationStateInfo
    {
        public int VisitedCount { get; init; }
        public int CompletedCount { get; init; }
        public int? CurrentNode { get; init; }
        public int Steps { get; init; }
        public int RemainingSteps { get; init; }
        public bool IsHunting { get; init; }
        public JigsawPhase Phase { get; init; }
    }
}

/// <summary>Patch to initialize lobby recovery when loading multiplayer saves.</summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class InitializeLobbyRecoveryPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Postfix(RunState __result)
    {
        // Only initialize for jigsaw saves
        var recovery = LobbyRecovery.GetOrCreate(__result);
        if (recovery != null)
        {
            recovery.InitializeFromCheckpoint();
        }
    }
}
