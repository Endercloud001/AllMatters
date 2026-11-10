using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

/// <summary>Why a recovery query or initialization did not succeed.</summary>
public enum RecoveryFailure
{
    None,
    /// <summary>Initialization has not run, or ran and failed (see the reason).</summary>
    NotInitialized,
    /// <summary>Initialization was already completed; the existing snapshot is kept.</summary>
    AlreadyInitialized,
    /// <summary>The run carries no jigsaw checkpoint at all.</summary>
    MissingCheckpoint,
    /// <summary>A checkpoint exists but its map is null.</summary>
    MissingMap,
    /// <summary>The map is present but cannot be read or fails <c>JigsawSaveValidation</c>.</summary>
    CorruptMap,
    /// <summary>The player is not tracked, or is not part of the original roster.</summary>
    UnknownPlayer,
    /// <summary>The player disconnected longer than the reconnection window ago.</summary>
    TimedOut,
}

/// <summary>Explicit success or failure for recovery queries; a failure never carries a fake empty value.</summary>
public readonly record struct RecoveryResult<T>(RecoveryFailure Failure, string Reason, T? Value)
{
    public bool Success => Failure == RecoveryFailure.None;
    public static RecoveryResult<T> Ok(T value) => new(RecoveryFailure.None, "", value);
    public static RecoveryResult<T> Fail(RecoveryFailure failure, string reason) => new(failure, reason, default);
}

/// <summary>
/// Handles multiplayer game state recovery for save files.
/// Tracks the checkpoint-derived progress (Act, Budget, Steps, Phase, roster) and per-player
/// resources and inventory across disconnects/reconnects.
/// The native card/relic/potion inventory lives on the native Player and is verified in the
/// batch 3 in-game acceptance; this class covers what the checkpoint and its callers can express.
/// </summary>
public sealed class SaveStateRecovery
{
    /// <summary>Reconnection window: a disconnected player stays recoverable for this long.</summary>
    public const long PlayerTimeoutMs = 30000;

    // Kept in a nested type so that loading SaveStateRecovery does not load RunState
    // (the xUnit project exercises this class through the internal checkpoint-reader seam).
    private static class Registry
    {
        public static readonly ConditionalWeakTable<RunState, SaveStateRecovery> Recoveries = new();
    }

    /// <summary>Get or create save state recovery for a native run session.</summary>
    /// <param name="clock">Time source used when the instance is created; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public static SaveStateRecovery? GetOrCreate(RunState native, Func<DateTimeOffset>? clock = null)
    {
        if (!Registry.Recoveries.TryGetValue(native, out var recovery))
        {
            recovery = new SaveStateRecovery(NativeReader(native), clock);
            Registry.Recoveries.Add(native, recovery);
        }
        return recovery;
    }

    private static CheckpointReader NativeReader(RunState native) => (out JigsawCheckpointData? data) =>
    {
        bool found = JigsawCheckpoint.Slot.TryGet(native, out var value);
        data = value;
        return found;
    };

    /// <summary>Reads the jigsaw checkpoint of a run; returns false when there is none.</summary>
    internal delegate bool CheckpointReader(out JigsawCheckpointData? data);

    /// <summary>Test seam: build a recovery over an arbitrary checkpoint source and clock.</summary>
    internal static SaveStateRecovery CreateForTest(CheckpointReader reader, Func<DateTimeOffset>? clock = null) =>
        new(reader, clock);

    private readonly CheckpointReader readCheckpoint;
    private readonly Func<DateTimeOffset> clock;
    private SaveStateSnapshot? snapshot;
    private RecoveryResult<SaveStateSnapshot> lastInitialization =
        RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.NotInitialized, "Initialization has not been attempted.");
    private readonly Dictionary<ulong, PlayerStateSnapshot> playerStates = [];
    private readonly Dictionary<ulong, long> playerLastUpdateTime = [];

    /// <summary>Whether save state recovery has been initialized from checkpoint.</summary>
    public bool IsInitialized => snapshot != null;

    /// <summary>Outcome of the most recent initialization attempt.</summary>
    public RecoveryResult<SaveStateSnapshot> LastInitialization => lastInitialization;

    private SaveStateRecovery(CheckpointReader reader, Func<DateTimeOffset>? clock)
    {
        readCheckpoint = reader;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    private long NowMs() => clock().ToUnixTimeMilliseconds();

    /// <summary>Initialize save state from checkpoint.</summary>
    /// <returns>True if recovery initialization succeeded. Use <see cref="TryInitialize"/> for the failure reason.</returns>
    public bool InitializeFromCheckpoint() => TryInitialize().Success;

    /// <summary>
    /// Initialize from the checkpoint and report why it failed.
    /// Missing checkpoint, missing map and invalid map each fail without writing any recovered state.
    /// </summary>
    public RecoveryResult<SaveStateSnapshot> TryInitialize()
    {
        if (snapshot != null)
            return RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.AlreadyInitialized, "Save state recovery is already initialized.");

        var result = Load();
        lastInitialization = result;
        return result;
    }

    private RecoveryResult<SaveStateSnapshot> Load()
    {
        JigsawCheckpointData? checkpointData;
        try
        {
            if (!readCheckpoint(out checkpointData))
            {
                Console.WriteLine("[SaveStateRecovery] No checkpoint found");
                return RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.MissingCheckpoint, "The run has no jigsaw checkpoint.");
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"[SaveStateRecovery] Checkpoint unreadable: {error.Message}");
            return RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.CorruptMap, "The checkpoint could not be read: " + error.Message);
        }

        if (checkpointData?.Map is not { } mapSave)
        {
            Console.WriteLine("[SaveStateRecovery] Checkpoint found but no save state");
            return RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.MissingMap, "The checkpoint has no map.");
        }

        SaveStateSnapshot loaded;
        ulong[] players;
        try
        {
            // Restore runs JigsawSaveValidation plus the geometry and automatic-witness checks.
            JigsawRun.Restore(mapSave);
            players = [.. (mapSave.Players ?? []).Distinct()];
            loaded = new SaveStateSnapshot
            {
                Act = checkpointData.Act,
                Budget = mapSave.Budget,
                Steps = mapSave.Steps,
                OriginalPlayers = [.. mapSave.OriginalPlayers ?? mapSave.Players ?? []],
                Phase = mapSave.Phase
            };
        }
        catch (Exception error)
        {
            Console.WriteLine($"[SaveStateRecovery] Checkpoint map rejected: {error.Message}");
            return RecoveryResult<SaveStateSnapshot>.Fail(RecoveryFailure.CorruptMap, "The checkpoint map is invalid: " + error.Message);
        }

        // Commit only after everything above succeeded.
        long now = NowMs();
        snapshot = loaded;
        playerStates.Clear();
        playerLastUpdateTime.Clear();
        foreach (var playerId in players)
        {
            // Resource and inventory fields stay unknown (null): the checkpoint does not carry them.
            playerStates[playerId] = new PlayerStateSnapshot
            {
                PlayerId = playerId,
                IsConnected = true,
                LastHeartbeat = now
            };
            playerLastUpdateTime[playerId] = now;
        }

        Console.WriteLine($"[SaveStateRecovery] Initialized: " +
            $"Act={loaded.Act}, " +
            $"Budget={loaded.Budget}, " +
            $"Steps={loaded.Steps}, " +
            $"Players={loaded.OriginalPlayers.Length}");
        return RecoveryResult<SaveStateSnapshot>.Ok(loaded);
    }

    /// <summary>Record player connection event and update heartbeat.</summary>
    public void OnPlayerConnected(ulong playerId)
    {
        if (snapshot == null)
            return;

        var now = NowMs();
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
            state.DisconnectedTime = NowMs();
            Console.WriteLine($"[SaveStateRecovery] Player disconnected: NetId={playerId}");
        }
    }

    /// <summary>
    /// Update player game state (inventory, resources). A null argument means "not provided":
    /// the previously known value is kept, and a field never provided stays unknown.
    /// </summary>
    /// <param name="playerId">The network ID of the player.</param>
    /// <param name="gold">Current gold amount.</param>
    /// <param name="health">Current health.</param>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="cardsCount">Number of cards in deck.</param>
    /// <param name="relicsCount">Number of relics owned.</param>
    /// <param name="relicIds">Relic model ids owned (the list, not just the count).</param>
    /// <param name="cardIds">Card model ids in the deck (the list, not just the count).</param>
    /// <returns>False when recovery is uninitialized or the player is not in the original roster.</returns>
    public bool UpdatePlayerState(ulong playerId, int? gold = null, int? health = null, int? maxHealth = null,
        int? cardsCount = null, int? relicsCount = null,
        IReadOnlyList<string>? relicIds = null, IReadOnlyList<string>? cardIds = null)
    {
        if (snapshot == null || !snapshot.OriginalPlayers.Contains(playerId))
            return false;

        if (!playerStates.TryGetValue(playerId, out var state))
        {
            state = new PlayerStateSnapshot { PlayerId = playerId };
            playerStates[playerId] = state;
        }

        state.Gold = gold ?? state.Gold;
        state.Health = health ?? state.Health;
        state.MaxHealth = maxHealth ?? state.MaxHealth;
        state.CardsCount = cardsCount ?? state.CardsCount;
        state.RelicsCount = relicsCount ?? state.RelicsCount;
        if (relicIds != null)
            state.RelicIds = [.. relicIds];
        if (cardIds != null)
            state.CardIds = [.. cardIds];
        state.LastHeartbeat = NowMs();
        playerLastUpdateTime[playerId] = state.LastHeartbeat.Value;
        return true;
    }

    /// <summary>Get current save state snapshot; null when recovery is not initialized.</summary>
    public SaveStateSnapshot? GetSnapshot() => snapshot;

    /// <summary>Get the snapshot, or an explicit failure saying why there is none.</summary>
    public RecoveryResult<SaveStateSnapshot> QuerySnapshot()
    {
        if (snapshot != null)
            return RecoveryResult<SaveStateSnapshot>.Ok(snapshot);
        var failure = lastInitialization.Failure is RecoveryFailure.None or RecoveryFailure.AlreadyInitialized
            ? RecoveryFailure.NotInitialized
            : lastInitialization.Failure;
        return RecoveryResult<SaveStateSnapshot>.Fail(failure, lastInitialization.Reason);
    }

    /// <summary>Get player state if connected or within timeout window.</summary>
    public PlayerStateSnapshot? GetPlayerState(ulong playerId)
    {
        if (!playerStates.TryGetValue(playerId, out var state))
            return null;

        // If player is disconnected and beyond timeout, return null
        if (IsTimedOut(state))
        {
            Console.WriteLine($"[SaveStateRecovery] Player timeout exceeded: NetId={playerId}");
            return null;
        }

        return state;
    }

    /// <summary>Get player state, or an explicit failure (not initialized, unknown player, timed out).</summary>
    public RecoveryResult<PlayerStateSnapshot> QueryPlayerState(ulong playerId)
    {
        if (snapshot == null)
            return RecoveryResult<PlayerStateSnapshot>.Fail(QuerySnapshot().Failure, lastInitialization.Reason);
        if (!playerStates.TryGetValue(playerId, out var state))
            return RecoveryResult<PlayerStateSnapshot>.Fail(RecoveryFailure.UnknownPlayer, $"Player {playerId} has no tracked state.");
        if (IsTimedOut(state))
            return RecoveryResult<PlayerStateSnapshot>.Fail(RecoveryFailure.TimedOut, $"Player {playerId} exceeded the {PlayerTimeoutMs} ms reconnection window.");
        return RecoveryResult<PlayerStateSnapshot>.Ok(state);
    }

    private bool IsTimedOut(PlayerStateSnapshot state) =>
        !state.IsConnected && state.DisconnectedTime.HasValue && NowMs() - state.DisconnectedTime.Value > PlayerTimeoutMs;

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
            return !IsTimedOut(state);

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

    /// <summary>
    /// Players that receive a reward granted now: connected and recoverable. Absent players
    /// (not in the loaded roster, disconnected, or timed out) are excluded. Ordered as in the
    /// checkpoint's original roster. This only decides recipients; granting is game logic.
    /// </summary>
    public RecoveryResult<IReadOnlyList<ulong>> GetRewardRecipients()
    {
        if (snapshot == null)
            return RecoveryResult<IReadOnlyList<ulong>>.Fail(QuerySnapshot().Failure, lastInitialization.Reason);
        IReadOnlyList<ulong> recipients = [.. snapshot.OriginalPlayers
            .Where(id => playerStates.TryGetValue(id, out var s) && s.IsConnected && IsPlayerRecoverable(id))];
        return RecoveryResult<IReadOnlyList<ulong>>.Ok(recipients);
    }

    /// <summary>
    /// Resolve state conflict when player reconnects with server state: the server's resource and
    /// inventory fields replace the local ones; the local connection tracking is kept.
    /// </summary>
    /// <returns>True if conflict resolved. False when uninitialized or the player is not in the original roster.</returns>
    public bool ResolveStateConflict(ulong playerId, PlayerStateSnapshot serverState)
    {
        if (snapshot == null || !snapshot.OriginalPlayers.Contains(playerId))
            return false;

        if (!playerStates.TryGetValue(playerId, out var localState))
        {
            playerStates[playerId] = serverState;
            return true;
        }

        // Server state takes precedence
        Console.WriteLine($"[SaveStateRecovery] Resolving conflict for player {playerId}: " +
            $"Local(Gold={localState.Gold}, Health={localState.Health}) -> " +
            $"Server(Gold={serverState.Gold}, Health={serverState.Health})");

        playerStates[playerId] = new PlayerStateSnapshot
        {
            PlayerId = playerId,
            IsConnected = localState.IsConnected,
            LastHeartbeat = localState.LastHeartbeat,
            DisconnectedTime = localState.DisconnectedTime,
            Gold = serverState.Gold,
            Health = serverState.Health,
            MaxHealth = serverState.MaxHealth,
            CardsCount = serverState.CardsCount,
            RelicsCount = serverState.RelicsCount,
            RelicIds = serverState.RelicIds,
            CardIds = serverState.CardIds,
        };
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

    /// <summary>
    /// Snapshot of individual player state for recovery. A null resource or list means unknown,
    /// never zero or empty.
    /// </summary>
    public sealed class PlayerStateSnapshot
    {
        public required ulong PlayerId { get; init; }
        public bool IsConnected { get; set; }
        public long? LastHeartbeat { get; set; }
        public long? DisconnectedTime { get; set; }

        // Resource state; null = unknown.
        public int? Gold { get; set; }
        public int? Health { get; set; }
        public int? MaxHealth { get; set; }
        public int? CardsCount { get; set; }
        public int? RelicsCount { get; set; }

        // Inventory lists; null = unknown, not "empty".
        public IReadOnlyList<string>? RelicIds { get; set; }
        public IReadOnlyList<string>? CardIds { get; set; }

        /// <summary>
        /// True only when both lists are known and agree with the counts. An empty list counts as
        /// complete only when its count is known to be zero; a list that disagrees with a known
        /// count, or a count without a list, is incomplete.
        /// </summary>
        public bool InventoryComplete =>
            ListComplete(RelicIds, RelicsCount) && ListComplete(CardIds, CardsCount);

        private static bool ListComplete(IReadOnlyList<string>? ids, int? count) =>
            ids != null && (ids.Count == 0 ? count == 0 : count == null || count == ids.Count);

        /// <summary>Names of the resource and inventory fields that are still unknown.</summary>
        public IReadOnlyList<string> UnknownFields
        {
            get
            {
                var unknown = new List<string>();
                if (Gold == null)
                    unknown.Add(nameof(Gold));
                if (Health == null)
                    unknown.Add(nameof(Health));
                if (MaxHealth == null)
                    unknown.Add(nameof(MaxHealth));
                if (CardsCount == null)
                    unknown.Add(nameof(CardsCount));
                if (RelicsCount == null)
                    unknown.Add(nameof(RelicsCount));
                if (RelicIds == null)
                    unknown.Add(nameof(RelicIds));
                if (CardIds == null)
                    unknown.Add(nameof(CardIds));
                return unknown;
            }
        }
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
