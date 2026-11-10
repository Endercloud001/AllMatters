using System.Collections.Generic;
using System.Linq;
using Xunit;
using AllMatters.Jigsaw;

namespace AllMatters.Tests;

/// <summary>
/// Save state recovery over a generated, validated jigsaw checkpoint.
/// Multiplayer is simulated inside one process, not over a real network.
/// The native card/relic/potion inventory lives on the native Player and is checked in the
/// batch 3 in-game acceptance; these tests cover the lists the checkpoint and its callers can express.
/// </summary>
public static class SaveStateRecoveryTests
{
    private static void Check(bool value, string text) => Assert.True(value, text);

    private static void Check(JigsawResult result, string text) => Assert.True(result.Success, text + " " + result.Reason);

    /// <summary>A real generated run, solved and restored, so the save passes JigsawSaveValidation.</summary>
    private static JigsawSave GeneratedSave(int seed, params ulong[] players)
    {
        var run = JigsawRun.Create(seed, players: players);
        foreach (var player in players)
            Check(run.ChooseEnd(player, EndChoice.Automatic), "Automatic layout must be authorized.");
        var solved = JigsawAutomatic.Solve(run, 0);
        Check(solved != null, "Zero expansions must yield a layout.");
        Check(run.FinishAutomatic(run.AutomaticOperation!, solved), "The solved layout must commit.");
        foreach (var player in players)
            Check(run.CompleteAncient(player), "Ancient phase must complete for every player.");
        foreach (var player in players)
            Check(run.Continue(player), "Every player must continue into exploration.");
        return JigsawRun.Restore(run.CreateSave()).CreateSave();
    }

    private static SaveStateRecovery Recover(JigsawCheckpointData? data, Func<DateTimeOffset>? clock = null) =>
        SaveStateRecovery.CreateForTest((out JigsawCheckpointData? read) => { read = data; return data != null; }, clock);

    private static SaveStateRecovery Initialized(int seed, ulong[] players, Func<DateTimeOffset>? clock = null)
    {
        var recovery = Recover(new JigsawCheckpointData { Version = 1, Act = 1, Map = GeneratedSave(seed, players) }, clock);
        Check(recovery.InitializeFromCheckpoint(), "A generated checkpoint must initialize recovery.");
        return recovery;
    }

    [Fact(DisplayName = "Save state recovery initializes from a generated checkpoint")]
    public static void InitializeFromCheckpoint()
    {
        ulong[] players = [1UL, 2UL];
        var save = GeneratedSave(12345, players);
        var recovery = Recover(new JigsawCheckpointData { Version = 1, Act = 2, Map = save });

        var result = recovery.TryInitialize();

        Check(result.Success && recovery.IsInitialized, "Initialization must succeed.");
        var snapshot = recovery.GetSnapshot()!;
        Check(snapshot.Act == 2 && snapshot.Budget == save.Budget && snapshot.Steps == save.Steps && snapshot.Phase == save.Phase,
            "The snapshot must copy the checkpoint act and the map progress.");
        Check(snapshot.OriginalPlayers.SequenceEqual(players), "The snapshot must keep the original roster.");
        Check(recovery.GetConnectedPlayerCount() == 2, "Both checkpoint players start connected.");
        Check(!recovery.InitializeFromCheckpoint(), "A second initialization must not replace the snapshot.");
    }

    [Fact(DisplayName = "Save state recovery tracks connections and rejects an unknown player")]
    public static void PlayerConnectionTracking()
    {
        var recovery = Initialized(99999, [10UL, 20UL]);
        recovery.OnPlayerDisconnected(20UL);

        Check(recovery.GetPlayerState(10UL) is { IsConnected: true }, "The untouched player stays connected.");
        Check(recovery.GetPlayerState(20UL) is { IsConnected: false }, "The departed player is tracked as disconnected.");
        Check(recovery.GetConnectedPlayerCount() == 1, "Only the connected player counts.");

        recovery.OnPlayerConnected(99UL);
        Check(recovery.GetPlayerState(99UL) == null, "A player outside the original roster is rejected.");
    }

    [Fact(DisplayName = "Save state recovery reconnects inside the timeout window (in-process simulation, not a real network)")]
    public static void ReconnectionWithinTimeout()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var recovery = Initialized(88888, [5UL, 6UL], () => now);
        recovery.OnPlayerDisconnected(6UL);
        now = now.AddSeconds(29);
        recovery.OnPlayerConnected(6UL);

        Check(recovery.GetPlayerState(6UL) is { IsConnected: true }, "Reconnecting inside the window restores the connection.");
        Check(recovery.IsPlayerRecoverable(6UL), "The reconnected player is recoverable.");
        Check(recovery.GetRecoverablePlayers().Order().SequenceEqual(new ulong[] { 5, 6 }), "Both players are recoverable again.");
    }

    [Fact(DisplayName = "Save state recovery detects a timeout by advancing the injected clock (in-process simulation, not a real network)")]
    public static void TimeoutDetection()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var recovery = Initialized(77777, [7UL, 8UL], () => now);
        recovery.OnPlayerDisconnected(8UL);

        now = now.AddMilliseconds(SaveStateRecovery.PlayerTimeoutMs);
        Check(recovery.IsPlayerRecoverable(8UL), "Exactly at the window boundary the player is still recoverable.");

        now = now.AddMilliseconds(1);
        var query = recovery.QueryPlayerState(8UL);
        Check(!query.Success && query.Failure == RecoveryFailure.TimedOut, "Past the window the query reports a timeout, not a missing player.");
        Check(!recovery.IsPlayerRecoverable(8UL), "A timed out player is not recoverable.");
        Check(recovery.GetRecoverablePlayers().Single() == 7UL, "Only the player who never left remains.");
    }

    [Fact(DisplayName = "Save state recovery resolves a conflict in favor of the server state (in-process simulation, not a real network)")]
    public static void StateConflictResolution()
    {
        var recovery = Initialized(66666, [9UL]);
        Check(recovery.UpdatePlayerState(9UL, gold: 90, health: 75), "The local state must be recorded.");
        recovery.OnPlayerDisconnected(9UL);

        var server = new SaveStateRecovery.PlayerStateSnapshot
        {
            PlayerId = 9UL,
            IsConnected = true,
            Gold = 100,
            Health = 80,
            RelicIds = ["ANCHOR"],
            CardIds = ["STRIKE"],
        };
        Check(recovery.ResolveStateConflict(9UL, server), "The conflict must resolve.");

        var state = recovery.GetPlayerState(9UL)!;
        Check(state.Gold == 100 && state.Health == 80, "Server gold and health replace the local values.");
        Check(state.RelicIds!.SequenceEqual(new[] { "ANCHOR" }) && state.CardIds!.SequenceEqual(new[] { "STRIKE" }),
            "Server inventory lists replace the local ones.");
        Check(!state.IsConnected, "Resolving the conflict must not pretend the player reconnected.");
        Check(!recovery.ResolveStateConflict(99UL, server), "A player outside the roster cannot be written by conflict resolution.");
    }

    [Fact(DisplayName = "Save state recovery reconnects several players (in-process simulation, not a real network)")]
    public static void MultiplePlayerReconnection()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var recovery = Initialized(55555, [11UL, 12UL, 13UL], () => now);
        recovery.OnPlayerDisconnected(12UL);
        recovery.OnPlayerDisconnected(13UL);
        now = now.AddSeconds(10);
        recovery.OnPlayerConnected(12UL);
        recovery.OnPlayerConnected(13UL);

        Check(recovery.GetConnectedPlayerCount() == 3, "All three players are connected again.");
        var snapshot = recovery.GetSnapshot()!;
        Check(snapshot.Steps == 0 && snapshot.Phase == JigsawPhase.Exploring, "The generated checkpoint's exploration progress is unchanged.");
    }

    [Fact(DisplayName = "Save state recovery fails on a missing checkpoint without an empty snapshot")]
    public static void MissingCheckpoint()
    {
        var recovery = SaveStateRecovery.CreateForTest((out JigsawCheckpointData? read) => { read = null; return false; });

        var result = recovery.TryInitialize();

        Check(!result.Success && result.Failure == RecoveryFailure.MissingCheckpoint, "A missing checkpoint fails initialization.");
        Check(!recovery.IsInitialized && recovery.GetSnapshot() == null, "No snapshot is stored.");
        var query = recovery.QuerySnapshot();
        Check(!query.Success && query.Failure == RecoveryFailure.MissingCheckpoint, "Querying reports the failure instead of an empty snapshot.");
        var recipients = recovery.GetRewardRecipients();
        Check(!recipients.Success, "Reward recipients cannot be decided without a checkpoint.");
    }

    [Fact(DisplayName = "Save state recovery fails on a checkpoint with no map")]
    public static void MissingMap()
    {
        var recovery = Recover(new JigsawCheckpointData { Version = 1, Act = 1, Map = null });

        var result = recovery.TryInitialize();

        Check(!result.Success && result.Failure == RecoveryFailure.MissingMap, "A checkpoint without a map fails initialization.");
        Check(!recovery.IsInitialized, "The failure leaves recovery uninitialized.");
    }

    [Fact(DisplayName = "Save state recovery rejects a corrupt map and keeps no state")]
    public static void CorruptMap()
    {
        var save = GeneratedSave(45678, 40UL, 41UL);
        var corrupt = save with { Pool = save.Pool[..13] };
        var recovery = Recover(new JigsawCheckpointData { Version = 1, Act = 1, Map = corrupt });

        var result = recovery.TryInitialize();

        Check(!result.Success && result.Failure == RecoveryFailure.CorruptMap, "A map that fails validation is rejected.");
        Check(!recovery.IsInitialized && recovery.GetSnapshot() == null, "Nothing is written as recovered state.");
        Check(!recovery.UpdatePlayerState(40UL, gold: 10), "Player state cannot be written against a rejected checkpoint.");
    }

    [Fact(DisplayName = "Save state recovery keeps known fields and marks the missing ones unknown")]
    public static void PartialPlayerState()
    {
        var recovery = Initialized(23456, [20UL, 21UL]);

        Check(recovery.UpdatePlayerState(20UL, gold: 150), "A partial update must be accepted.");

        var state = recovery.GetPlayerState(20UL)!;
        Check(state.Gold == 150, "The provided gold is restored.");
        Check(state.Health == null && state.MaxHealth == null, "Fields never provided stay unknown instead of zero.");
        Check(state.UnknownFields.Contains(nameof(state.Health)) && state.UnknownFields.Contains(nameof(state.MaxHealth)),
            "The missing fields are reported as unknown.");
        Check(recovery.GetPlayerState(21UL)!.Gold == null, "The other player's state is untouched.");
    }

    [Fact(DisplayName = "Save state recovery gives a reward only to connected recoverable players (in-process simulation, not a real network)")]
    public static void RewardRecipients()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var recovery = Initialized(34567, [30UL, 31UL, 32UL], () => now);
        recovery.OnPlayerDisconnected(32UL);
        now = now.AddSeconds(31);

        var recipients = recovery.GetRewardRecipients();

        Check(recipients.Success, "Recipients can be decided once recovery is initialized.");
        Check(recipients.Value!.SequenceEqual(new ulong[] { 30, 31 }),
            "Only the two connected, recoverable players receive the reward; the absent one does not.");
    }

    [Fact(DisplayName = "Save state recovery stores relic and card lists and flags an incomplete inventory")]
    public static void InventoryLists()
    {
        var recovery = Initialized(22222, [17UL]);
        var before = recovery.GetPlayerState(17UL)!;
        Check(before.RelicIds == null && before.CardIds == null && !before.InventoryComplete,
            "Right after loading, no inventory list exists, so the inventory is not claimed complete.");

        Check(recovery.UpdatePlayerState(17UL, relicsCount: 2, cardsCount: 2,
            relicIds: ["BURNING_BLOOD", "ANCHOR"], cardIds: ["STRIKE", "DEFEND"]), "The lists must be stored.");
        var complete = recovery.GetPlayerState(17UL)!;
        Check(complete.RelicIds!.SequenceEqual(new[] { "BURNING_BLOOD", "ANCHOR" }), "Relic ids are kept, not just counted.");
        Check(complete.CardIds!.SequenceEqual(new[] { "STRIKE", "DEFEND" }), "Card ids are kept, not just counted.");
        Check(complete.InventoryComplete, "Lists that agree with their counts are complete.");

        Check(recovery.UpdatePlayerState(17UL, cardsCount: 5), "A later count update must be accepted.");
        var mismatch = recovery.GetPlayerState(17UL)!;
        Check(mismatch.CardIds!.Count == 2 && mismatch.CardsCount == 5 && !mismatch.InventoryComplete,
            "A list shorter than its count is kept but flagged incomplete.");
    }
}
