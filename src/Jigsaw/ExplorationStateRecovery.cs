using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

/// <summary>
/// Handles exploration state recovery for multiplayer sessions.
/// Tracks visited nodes, completed rooms, and player positions across disconnects/reconnects.
/// </summary>
public sealed class ExplorationStateRecovery
{
    private static readonly ConditionalWeakTable<RunState, ExplorationStateRecovery> recoveries = new();

    /// <summary>Get or create exploration recovery for a native run session.</summary>
    public static ExplorationStateRecovery? GetOrCreate(RunState native)
        => recoveries.GetValue(native, state => new ExplorationStateRecovery(state));

    private readonly RunState nativeRun;
    private ExplorationSnapshot? snapshot;

    /// <summary>Whether exploration recovery has been initialized from checkpoint.</summary>
    public bool IsInitialized => snapshot != null || JigsawNativeSession.TryGet(nativeRun)?.Map != null;

    private ExplorationStateRecovery(RunState native)
    {
        nativeRun = native;
    }

    /// <summary>Initialize exploration state from checkpoint save.</summary>
    /// <returns>True if recovery initialization succeeded.</returns>
    public bool InitializeFromCheckpoint()
    {
        if (snapshot != null)
            return false;

        // Extract jigsaw checkpoint containing exploration state
        if (JigsawCheckpoint.Slot.TryGet(nativeRun, out var checkpointData) &&
            checkpointData?.Map is { } mapSave)
        {
            snapshot = new ExplorationSnapshot
            {
                VisitedNodes = [.. mapSave.Visited],
                CompletedNodes = [.. mapSave.Completed],
                CurrentNode = mapSave.CurrentNode,
                Steps = mapSave.Steps,
                FirstProgress = mapSave.FirstProgress,
                Phase = mapSave.Phase,
                Budget = mapSave.Budget
            };

            Console.WriteLine($"[ExplorationStateRecovery] Loaded checkpoint: " +
                $"Visited={snapshot.VisitedNodes.Count}, " +
                $"Completed={snapshot.CompletedNodes.Count}, " +
                $"Steps={snapshot.Steps}/{snapshot.Budget}, " +
                $"Phase={snapshot.Phase}");
            return true;
        }

        Console.WriteLine("[ExplorationStateRecovery] Checkpoint found but no exploration state");
        return false;
    }

    /// <summary>Get the current exploration state snapshot.</summary>
    public ExplorationSnapshot? GetSnapshot()
    {
        if (JigsawNativeSession.TryGet(nativeRun)?.Map is { } map)
            return new ExplorationSnapshot
            {
                VisitedNodes = [.. map.Visited],
                CompletedNodes = [.. map.Completed],
                CurrentNode = map.CurrentNode,
                Steps = map.Steps,
                FirstProgress = map.FirstProgress,
                Phase = map.Phase,
                Budget = map.Budget
            };

        // The native deserialization postfix can run before the checkpoint slot is attached.
        if (snapshot == null)
            InitializeFromCheckpoint();
        return snapshot;
    }

    /// <summary>Check if a node has been visited in the recovered state.</summary>
    public bool IsNodeVisited(int nodeId)
    {
        return GetSnapshot()?.VisitedNodes.Contains(nodeId) ?? false;
    }

    /// <summary>Check if a node has been completed in the recovered state.</summary>
    public bool IsNodeCompleted(int nodeId)
    {
        return GetSnapshot()?.CompletedNodes.Contains(nodeId) ?? false;
    }

    /// <summary>Get remaining steps before hunting phase.</summary>
    public int GetRemainingSteps()
    {
        var current = GetSnapshot();
        if (current == null)
            return 0;
        return Math.Max(0, current.Budget - current.Steps);
    }

    /// <summary>Check if exploration is in hunting phase (budget exceeded).</summary>
    public bool IsHunting()
    {
        var current = GetSnapshot();
        if (current == null)
            return false;
        return current.Steps > current.Budget;
    }

    /// <summary>Snapshot of exploration state for recovery.</summary>
    public sealed class ExplorationSnapshot
    {
        public required HashSet<int> VisitedNodes { get; init; }
        public required HashSet<int> CompletedNodes { get; init; }
        public required int? CurrentNode { get; init; }
        public required int Steps { get; init; }
        public required int FirstProgress { get; init; }
        public required JigsawPhase Phase { get; init; }
        public required int Budget { get; init; }
    }
}

/// <summary>Patch to initialize exploration recovery when loading multiplayer saves.</summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class InitializeExplorationRecoveryPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Postfix(RunState __result)
    {
        // Only initialize for jigsaw saves
        var recovery = ExplorationStateRecovery.GetOrCreate(__result);
        if (recovery != null)
        {
            recovery.InitializeFromCheckpoint();
        }
    }
}
