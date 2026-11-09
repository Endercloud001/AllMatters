using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

/// <summary>
/// Handles multiplayer game state recovery for save files.
/// Tracks player inventory (cards, relics, potions), progress (Act, Floor, defeated bosses),
/// and resources (gold, health) across disconnects/reconnects.
/// </summary>
public sealed class SaveStateRecovery
{
    private static readonly ConditionalWeakTable<RunState, SaveStateRecovery> recoveries = new();

    /// <summary>Get or create save state recovery for a native run session.</summary>
    public static SaveStateRecovery? GetOrCreate(RunState native)
    {
        if (!recoveries.TryGetValue(native, out var recovery))
        {
            recovery = new SaveStateRecovery(native);
            recoveries.Add(native, recovery);
        }
        return recovery;
    }

    private readonly RunState nativeRun;
    private SaveStateSnapshot? snapshot;
    private readonly Dictionary<ulong, PlayerStateSnapshot> playerStates = [];
    private readonly Dictionary<ulong, long> playerLastUpdateTime = [];
    private const long PLAYER_TIMEOUT_MS = 30000; // 30 seconds reconnection timeout

    /// <summary>Whether save state recovery has been initialized from checkpoint.</summary>
    public bool IsInitialized => snapshot != null;

    private SaveStateRecovery(RunState native)
    {
        nativeRun = native;
    }

    /// <summary>Initialize save state from checkpoint.</summary>
    /// <returns>True if recovery initialization succeeded.</returns>
    public bool InitializeFromCheckpoint()
    {
        if (snapshot != null)
            return false;

        // Extract jigsaw checkpoint containing save state
        if (JigsawCheckpoint.Slot.TryGet(nativeRun, out var checkpointData) &&
            checkpointData?.Map is { } mapSave)
        {
            snapshot = new SaveStateSnapshot
            {
                Act = checkpointData.Act,
                Budget = mapSave.Budget,
                Steps = mapSave.Steps,
                OriginalPlayers = [.. mapSave.OriginalPlayers ?? []],
                Phase = mapSave.Phase
            };

            // Initialize player states from native run
            foreach (var playerId in mapSave.Players ?? [])
            {
                playerStates[playerId] = new PlayerStateSnapshot
                {
                    PlayerId = playerId,
                    IsConnected = true,
                    LastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                playerLastUpdateTime[playerId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            Console.WriteLine($"[SaveStateRecovery] Initialized: " +
                $"Act={snapshot.Act}, " +
                $"Budget={snapshot.Budget}, " +
                $"Steps={snapshot.Steps}, " +
                $"Players={snapshot.OriginalPlayers.Length}");
            return true;
        }

        Console.WriteLine("[SaveStateRecovery] Checkpoint found but no save state");
        return false;
    }

    /// <summary>Record player connection event and update heartbeat.</summary>
    public void OnPlayerConnected(ulong playerId)
    {
        if (snapshot == null)
            return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!playerStates.TryGetValue(playerId, out var state))
        {
            // New connection - validate against original roster
            if (!snapshot.OriginalPlayers.Contains(playerId))
            {
                Console.WriteLine($"[SaveStateRecovery] Rejected unknown player: NetId={playerId}");
                return;
            }
            state = new PlayerStateSnapshot { PlayerId = playerId };
            playerStates[playerId] = state;
        }

        state.IsConnected = true;
        state.LastHeartbeat = now;
        playerLastUpdateTime[playerId] = now;
        Console.WriteLine($"[SaveStateRecovery] Player connected: NetId={playerId}");
    }

    /// <summary>Mark player as disconnected and start timeout tracking.</summary>
    public void OnPlayerDisconnected(ulong playerId)
    {
        if (snapshot == null)
            return;

        if (playerStates.TryGetValue(playerId, out var state))
        {
            state.IsConnected = false;
            state.DisconnectedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Console.WriteLine($"[SaveStateRecovery] Player disconnected: NetId={playerId}");
        }
    }

    /// <summary>Update player game state (inventory, resources, progress).</summary>
    /// <param name="playerId">The network ID of the player.</param>
    /// <param name="gold">Current gold amount.</param>
    /// <param name="health">Current health.</param>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="cardsCount">Number of cards in deck.</param>
    /// <param name="relicsCount">Number of relics owned.</param>
    public void UpdatePlayerState(ulong playerId, int gold, int health, int maxHealth, int cardsCount, int relicsCount)
    {
        if (snapshot == null)
            return;

        if (!playerStates.TryGetValue(playerId, out var state))
        {
            state = new PlayerStateSnapshot { PlayerId = playerId };
            playerStates[playerId] = state;
        }

        state.Gold = gold;
        state.Health = health;
        state.MaxHealth = maxHealth;
        state.CardsCount = cardsCount;
        state.RelicsCount = relicsCount;
        state.LastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        playerLastUpdateTime[playerId] = state.LastHeartbeat.Value;
    }

    /// <summary>Get current save state snapshot.</summary>
    public SaveStateSnapshot? GetSnapshot() => snapshot;

    /// <summary>Get player state if connected or within timeout window.</summary>
    public PlayerStateSnapshot? GetPlayerState(ulong playerId)
    {
        if (!playerStates.TryGetValue(playerId, out var state))
            return null;

        // If player is disconnected and beyond timeout, return null
        if (!state.IsConnected && state.DisconnectedTime.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - state.DisconnectedTime.Value > PLAYER_TIMEOUT_MS)
            {
                Console.WriteLine($"[SaveStateRecovery] Player timeout exceeded: NetId={playerId}");
                return null;
            }
        }

        return state;
    }

    /// <summary>Check if a player is within the timeout recovery window.</summary>
    public bool IsPlayerRecoverable(ulong playerId)
    {
        var state = GetPlayerState(playerId);
        if (state == null)
            return false;

        // Connected players are always recoverable
        if (state.IsConnected)
            return true;

        // Disconnected players are recoverable if within timeout
        if (state.DisconnectedTime.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now - state.DisconnectedTime.Value <= PLAYER_TIMEOUT_MS;
        }

        return true;
    }

    /// <summary>Get connected player count (excludes timed-out players).</summary>
    public int GetConnectedPlayerCount()
    {
        return playerStates.Values.Count(p => GetPlayerState(p.PlayerId) != null && p.IsConnected);
    }

    /// <summary>Get all players within recovery window (connected or reconnecting).</summary>
    public IEnumerable<ulong> GetRecoverablePlayers()
    {
        return playerStates.Keys.Where(IsPlayerRecoverable);
    }

    /// <summary>Resolve state conflict when player reconnects with server state.</summary>
    /// <returns>True if conflict resolved (server state takes precedence).</returns>
    public bool ResolveStateConflict(ulong playerId, PlayerStateSnapshot serverState)
    {
        if (!playerStates.TryGetValue(playerId, out var localState))
        {
            playerStates[playerId] = serverState;
            return true;
        }

        // Server state takes precedence
        Console.WriteLine($"[SaveStateRecovery] Resolving conflict for player {playerId}: " +
            $"Local(Gold={localState.Gold}, Health={localState.Health}) -> " +
            $"Server(Gold={serverState.Gold}, Health={serverState.Health})");

        playerStates[playerId] = serverState;
        return true;
    }

    /// <summary>Snapshot of overall save state for recovery.</summary>
    public sealed class SaveStateSnapshot
    {
        public required int Act { get; init; }
        public required int Budget { get; init; }
        public required int Steps { get; init; }
        public required JigsawPhase Phase { get; init; }
        public required ulong[] OriginalPlayers { get; init; }
    }

    /// <summary>Snapshot of individual player state for recovery.</summary>
    public sealed class PlayerStateSnapshot
    {
        public required ulong PlayerId { get; init; }
        public bool IsConnected { get; set; }
        public long? LastHeartbeat { get; set; }
        public long? DisconnectedTime { get; set; }

        // Inventory and resource state
        public int Gold { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int CardsCount { get; set; }
        public int RelicsCount { get; set; }
    }
}

/// <summary>Patch to initialize save state recovery when loading multiplayer saves.</summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class InitializeSaveStateRecoveryPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Postfix(RunState __result)
    {
        // Only initialize for jigsaw saves
        var recovery = SaveStateRecovery.GetOrCreate(__result);
        if (recovery != null)
        {
            recovery.InitializeFromCheckpoint();
        }
    }
}
