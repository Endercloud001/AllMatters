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
    private readonly List<ulong> originalPlayers = [];
    private readonly HashSet<ulong> connectedPlayerIds = [];
    private bool recovered;

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
            // Store original players from checkpoint
            originalPlayers.Clear();
            originalPlayers.AddRange(originalPlayerList);
            Console.WriteLine($"[LobbyRecovery] Loaded checkpoint with {originalPlayers.Count} original players (Act {checkpointData.Act})");
            recovered = true;
            return true;
        }

        Console.WriteLine("[LobbyRecovery] Checkpoint found but no original player list");
        return false;
    }

    /// <summary>Sync player connection state to lobby.</summary>
    /// <param name="playerId">The network ID of the connecting player.</param>
    public void OnPlayerConnected(ulong playerId)
    {
        if (!recovered)
            return;

        if (connectedPlayerIds.Add(playerId))
        {
            Console.WriteLine($"[LobbyRecovery] Player connected: NetId={playerId}");

            // TODO: Validate player matches original roster
            // TODO: Broadcast updated lobby state to all connected players
        }
    }

    /// <summary>Handle player disconnection during recovery.</summary>
    public void OnPlayerDisconnected(ulong playerId)
    {
        if (!recovered)
            return;

        if (connectedPlayerIds.Remove(playerId))
        {
            Console.WriteLine($"[LobbyRecovery] Player disconnected: NetId={playerId}");

            // TODO: Update lobby state to reflect disconnection
            // TODO: Check if remaining players can continue (quorum logic)
        }
    }

    /// <summary>Check if lobby is ready to proceed (all expected players reconnected).</summary>
    public bool IsLobbyReady()
    {
        if (!recovered)
            return false;

        // TODO: Implement quorum check
        // - All original players reconnected? Or
        // - Minimum player count reached?
        return connectedPlayerIds.Count > 0;
    }

    /// <summary>Get current lobby state snapshot including save state info.</summary>
    public LobbyState GetLobbyState()
    {
        // Capture save state recovery info if available
        SaveStateRecovery.SaveStateSnapshot? saveState = null;
        if (SaveStateRecovery.GetOrCreate(nativeRun) is { } saveRecovery && saveRecovery.IsInitialized)
        {
            saveState = saveRecovery.GetSnapshot();
        }

        return new LobbyState
        {
            OriginalPlayerCount = originalPlayers.Count,
            ConnectedPlayerCount = connectedPlayerIds.Count,
            IsRecovered = recovered,
            ConnectedPlayerIds = [.. connectedPlayerIds],
            IsOriginalRosterReady = recovered && originalPlayers.Count > 0 &&
                connectedPlayerIds.Count == originalPlayers.Count &&
                originalPlayers.All(id => connectedPlayerIds.Contains(id)),
            SaveState = saveState != null ? new SaveStateInfo
            {
                Act = saveState.Act,
                Budget = saveState.Budget,
                Steps = saveState.Steps,
                Phase = saveState.Phase.ToString()
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
    }

    /// <summary>Snapshot of game save state for lobby sync.</summary>
    public sealed class SaveStateInfo
    {
        public required int Act { get; init; }
        public required int Budget { get; init; }
        public required int Steps { get; init; }
        public required string Phase { get; init; }
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
