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

    private LobbyRecovery(RunState native)
    {
        nativeRun = native;
    }

    /// <summary>Initialize lobby recovery from checkpoint save.</summary>
    /// <param name="checkpoint">The original save checkpoint to recover from.</param>
    /// <returns>True if recovery initialization succeeded.</returns>
    public bool InitializeFromCheckpoint(SerializableRun checkpoint)
    {
        if (recovered)
            return false;

        // Check if this is a jigsaw save with checkpoint data
        if (!JigsawSaveSource.IsJigsaw(checkpoint))
        {
            Console.WriteLine("[LobbyRecovery] Not a jigsaw save");
            return false;
        }

        // TODO (#91-Alpha): Extract original player list from checkpoint RitsuLib data
        // For now, initialize with empty player list (to be populated by #92)
        Console.WriteLine("[LobbyRecovery] Initializing from jigsaw checkpoint");
        recovered = true;
        return true;
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

    /// <summary>Get current lobby state snapshot.</summary>
    public LobbyState GetLobbyState()
    {
        return new LobbyState
        {
            OriginalPlayerCount = originalPlayers.Count,
            ConnectedPlayerCount = connectedPlayerIds.Count,
            IsRecovered = recovered,
            ConnectedPlayerIds = [.. connectedPlayerIds]
        };
    }

    /// <summary>Snapshot of current lobby state for sync.</summary>
    public sealed class LobbyState
    {
        public int OriginalPlayerCount { get; init; }
        public int ConnectedPlayerCount { get; init; }
        public bool IsRecovered { get; init; }
        public ulong[] ConnectedPlayerIds { get; init; } = [];
    }
}

/// <summary>Patch to initialize lobby recovery when loading multiplayer saves.</summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class InitializeLobbyRecoveryPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Postfix(SerializableRun serializedRun, RunState __result)
    {
        // Only initialize for jigsaw saves with checkpoint data
        if (!JigsawSaveSource.IsJigsaw(serializedRun))
            return;

        var recovery = LobbyRecovery.GetOrCreate(__result);
        if (recovery != null)
        {
            recovery.InitializeFromCheckpoint(serializedRun);
        }
    }
}
