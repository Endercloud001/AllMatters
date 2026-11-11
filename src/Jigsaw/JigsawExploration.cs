using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

public sealed partial class JigsawRun
{
    private readonly Dictionary<ulong, int> destinationVotes = [];
    private readonly HashSet<ulong> roomCompleted = [];
    private readonly Dictionary<int, JigsawRoom> resolvedRooms = [];
    private ulong destinationRandomState = 1;

    public IReadOnlyDictionary<ulong, int> DestinationVotes => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong, int>(destinationVotes);
    public IReadOnlyCollection<ulong> RoomCompletedPlayers => roomCompleted.ToArray();
    public JigsawRoom ResolvedRoom(int node) => resolvedRooms.GetValueOrDefault(node, Nodes.Single(n => n.Id == node).Room);

    public JigsawResult RecordResolvedRoom(int node, JigsawRoom room)
    {
        if (CurrentNode != node || Phase != JigsawPhase.Exploring || room == JigsawRoom.Ancient || !Enum.IsDefined(room))
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        if (resolvedRooms.TryGetValue(node, out var previous) && previous != room)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        resolvedRooms[node] = room;
        return JigsawResult.Ok;
    }

    public IReadOnlyCollection<int> VotingDestinations
    {
        get
        {
            if (Phase != JigsawPhase.Exploring || HasDeferredAncient || PendingEntry != null)
                return [];
            if (CurrentNode == null)
                return AvailableDestinations;

            return DestinationsAfter(completed.Append(CurrentNode.Value).ToHashSet());
        }
    }

    private IReadOnlyCollection<int> DestinationsAfter(IReadOnlySet<int> completedNodes)
    {
        var candidates = Edges.SelectMany(e => new[] { (From: e.First, To: e.Second), (From: e.Second, To: e.First) })
            .Where(e => completedNodes.Contains(e.From) && (!visited.Contains(e.To) || ResolvedRoom(e.To) == JigsawRoom.Shop) && e.To != 0)
            .Select(e => e.To).Distinct().ToArray();
        var secondBoss = Nodes.FirstOrDefault(n => n.Room == JigsawRoom.SecondBoss);
        var firstBoss = Nodes.FirstOrDefault(n => n.Room == JigsawRoom.Boss);
        if (secondBoss != null && firstBoss != null && !completedNodes.Contains(firstBoss.Id))
            candidates = candidates.Where(id => id != secondBoss.Id).ToArray();
        return candidates;
    }
    public JigsawResult RecordRoomCompletion(ulong player)
    {
        if (Phase != JigsawPhase.Exploring || HasDeferredAncient || CurrentNode == null || !players.Contains(player))
            return JigsawResult.Reject(JigsawFailure.StaleCompletion);
        roomCompleted.Add(player);
        return JigsawResult.Ok;
    }

    public JigsawResult VoteDestination(ulong player, int node)
    {
        if (!players.Contains(player))
            return JigsawResult.Reject(JigsawFailure.UnknownPlayer);
        if (CurrentNode != null && !roomCompleted.Contains(player))
            return JigsawResult.Reject(JigsawFailure.StaleCompletion);
        if (!VotingDestinations.Contains(node))
            return JigsawResult.Reject(JigsawFailure.UnknownNode);
        destinationVotes[player] = node;
        return JigsawResult.Ok;
    }

    public int? ProposedDestination
    {
        get
        {
            if (players.Count == 0 || players.Any(player => !destinationVotes.ContainsKey(player)) ||
                CurrentNode != null && players.Any(player => !roomCompleted.Contains(player)))
                return null;
            var ballots = players.Order().Select(player => destinationVotes[player]).ToArray();
            return ballots.All(node => node == ballots[0]) ? ballots[0] : null;
        }
    }

    public JigsawResult FinalizeDestination(int node)
    {
        if (ProposedDestination != node || !VotingDestinations.Contains(node) || PendingEntry != null)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        if (CurrentNode != null && !CompleteRoom(currentEntry!.Value).Success)
            return JigsawResult.Reject(JigsawFailure.StaleCompletion);
        destinationRandomState = NextDestinationState(destinationRandomState);
        return BeginEnter(node) == null ? JigsawResult.Reject(JigsawFailure.StaleEntry) : JigsawResult.Ok;
    }

    private static ulong NextDestinationState(ulong state)
    {
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state;
    }
}

internal static class JigsawNativeExploration
{
    private sealed class ExitState
    {
        internal long MerchantExit;
        internal long TreasureOpen;
        internal long LocalExit;
        internal readonly Dictionary<ulong, long> TerminalRewards = [];
        internal readonly Dictionary<(long Entry, ulong Player), HashSet<int>> RewardSets = [];
        internal readonly Dictionary<(long Entry, ulong Player), List<Task>> RewardSelections = [];
    }

    private static readonly ConditionalWeakTable<JigsawNativeSession, ExitState> exits = new();
    private static ExitState State(JigsawNativeSession session) => exits.GetValue(session, _ => new());
    private static long Entry(JigsawNativeSession session) => session.Map?.CreateSave().NextEntry ?? 0;

    internal static void MerchantExited(JigsawNativeSession session) => State(session).MerchantExit = Entry(session);
    internal static void TreasureOpened(JigsawNativeSession session) => State(session).TreasureOpen = Entry(session);
    internal static void TerminalRewardsFinished(JigsawNativeSession session)
    {
        // Native terminal Proceed leaves unclaimed rewards on the stack until room
        // exit. Exploration needs that same local exit before it can vote to leave.
        var synchronizer = RunManager.Instance.RewardsSetSynchronizer;
        var player = session.Native.Players.Single(owner => owner.NetId == RunManager.Instance.NetService.NetId);
        if (State(session).RewardSets.TryGetValue((Entry(session), player.NetId), out var ids))
            foreach (int id in ids.OrderDescending())
                if (!synchronizer.IsRewardsSetCompleted(player, id))
                    synchronizer.SkipLocalRewardsSet();
        State(session).TerminalRewards[RunManager.Instance.NetService.NetId] = Entry(session);
    }

    internal static void RewardSetBegan(JigsawNativeSession session, RewardsSet rewards)
    {
        if (session.Map?.Phase != JigsawPhase.Exploring ||
            session.Map.CurrentNode == null && session.Map.PendingEntry == null || rewards.Id < 0)
            return;
        var key = (Entry(session), rewards.Player.NetId);
        if (!State(session).RewardSets.TryGetValue(key, out var ids))
            State(session).RewardSets[key] = ids = [];
        ids.Add(rewards.Id);
    }

    internal static void RewardSelectionBegan(JigsawNativeSession session, Reward reward, Task selection)
    {
        if (session.Map?.Phase != JigsawPhase.Exploring || session.Map.CurrentNode == null && session.Map.PendingEntry == null)
            return;
        var key = (Entry(session), reward.Player.NetId);
        if (!State(session).RewardSelections.TryGetValue(key, out var selections))
            State(session).RewardSelections[key] = selections = [];
        selections.Add(selection);
    }

    internal static async Task<bool> WaitForStartedRewards(JigsawNativeSession session, ulong player)
    {
        var map = session.Map;
        int act = session.Native.CurrentActIndex;
        long entry = Entry(session);
        try
        {
            if (State(session).RewardSelections.TryGetValue((entry, player), out var selections))
                await Task.WhenAll(selections.Where(task => !task.IsCompleted).ToArray()).WaitAsync(session.ClosedToken);
        }
        catch (OperationCanceledException) when (session.ClosedToken.IsCancellationRequested) { return false; }
        return ReferenceEquals(JigsawNativeSession.Current, session) && ReferenceEquals(session.Map, map) &&
            session.Native.CurrentActIndex == act && Entry(session) == entry;
    }

    internal static async Task<bool> ReceiveRoomCompletion(JigsawNativeSession session, ulong player)
    {
        // A finished native room authenticates the boundary; only selections that
        // have already started on this replica may delay its ordered completion.
        if (!NativeFlowFinished(session, player, requireLocalSignals: false, checkRewards: false) ||
            !await WaitForStartedRewards(session, player))
            return false;
        return NativeFlowFinished(session, player);
    }

    internal static bool MarkLocalExit(JigsawNativeSession session, bool canTravel)
    {
        if (session.Map?.ActivePlayers.Count == 1 && !canTravel || !NativeFlowFinished(session, RunManager.Instance.NetService.NetId, requireLocalSignals: true))
            return false;
        State(session).LocalExit = Entry(session);
        return true;
    }

    internal static bool LocalCompletionWasObserved(JigsawNativeSession session, ulong player) =>
        player == RunManager.Instance.NetService.NetId && State(session).LocalExit == Entry(session) &&
        NativeFlowFinished(session, player, requireLocalSignals: true);

    internal static bool NativeFlowFinished(JigsawNativeSession session, ulong player) => NativeFlowFinished(session, player, requireLocalSignals: false);

    private static bool NativeFlowFinished(JigsawNativeSession session, ulong player, bool requireLocalSignals, bool checkRewards = true)
    {
        if (session.Map?.CurrentNode == null || session.Native.CurrentRoomCount > 1)
            return false;
        var nativePlayer = session.Native.Players.FirstOrDefault(candidate => candidate.NetId == player);
        if (nativePlayer == null)
            return false;
        if (checkRewards && State(session).RewardSets.TryGetValue((Entry(session), player), out var ids) &&
            ids.Any(id => !RunManager.Instance.RewardsSetSynchronizer.IsRewardsSetCompleted(nativePlayer, id)))
            return false;
        if (State(session).TerminalRewards.GetValueOrDefault(player) == Entry(session))
            return true;
        return session.Native.CurrentRoom switch
        {
            CombatRoom combat => combat.IsPreFinished,
            EventRoom => RunManager.Instance.EventSynchronizer.GetEventForPlayer(nativePlayer)?.IsFinished == true,
            RestSiteRoom => RunManager.Instance.RestSiteSynchronizer.GetChosenOptionIndex(player) != null,
            TreasureRoom => (!requireLocalSignals || State(session).TreasureOpen == Entry(session)) &&
                (RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.Count is not > 0 ||
                 RunManager.Instance.TreasureRoomRelicSynchronizer.GetPlayerVote(nativePlayer)?.voteReceived == true),
            MerchantRoom => !requireLocalSignals || State(session).MerchantExit == Entry(session),
            _ => false
        };
    }
}

[HarmonyPatch]
internal static class JigsawObserveRewardSelection
{
    private static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(RewardsSetSynchronizer))
        .Single(method => method.Name == "SelectRewardForPlayer" && method.GetParameters() is { Length: 2 } parameters && parameters[1].ParameterType == typeof(Reward));
    private static void Postfix(Reward reward, Task __result)
    {
        if (JigsawNativeSession.Current is { } session)
            JigsawNativeExploration.RewardSelectionBegan(session, reward, __result);
    }
}

[HarmonyPatch(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.BeginRewardsSet))]
internal static class JigsawObserveRoomRewards
{
    private static void Postfix(RewardsSet set)
    {
        if (JigsawNativeSession.Current is { } session)
            JigsawNativeExploration.RewardSetBegan(session, set);
    }
}
