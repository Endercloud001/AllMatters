using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace AllMatters.Jigsaw;

public enum JigsawEditorOperation { Take, Rotate, Drop, Cancel, ReturnToPool, SelectEndpoint, ChooseEnd, CompleteAncient, ContinueAncient, Reset, FinalizeAutomatic, Disconnect, CompleteRoom, VoteDestination, FinalizeDestination, EntryReady, EntryFailed, ConfirmEntry, RetryEntry, AbandonEntry, RecoverEntry }
public sealed record JigsawEditorIntent(int Act, JigsawEditorOperation Operation, int Piece = 0, GridPoint Point = default, int? NodeId = null, EndChoice? Choice = null, long Sequence = 0, long EditRevision = 0, int? FirstEndpoint = null, bool? DesiredAdd = null, ulong? CompletedOwner = null, JigsawLayout? Layout = null, string? AutomaticFailure = null, long EntryRevision = 0, long EntryRound = 0, string? EntryCheckpoint = null);

internal static class JigsawEditorAction
{
    private sealed class InputState
    {
        internal long Next;
        internal long Revision;
        internal readonly Dictionary<long, TaskCompletionSource<JigsawResult>> Resets = [];
        internal readonly Dictionary<ulong, long> Accepted = [];
        internal bool Pending;
        internal int? Endpoint;
        internal bool Blank;
    }
    private static readonly ConditionalWeakTable<JigsawRun, InputState> inputs = new();
    private static readonly RitsuLibManagedNetActionDescriptor<JigsawEditorIntent> descriptor = new(
        "AllMatters", "jigsaw-editor",
        message => JsonSerializer.SerializeToUtf8Bytes(message),
        bytes => JsonSerializer.Deserialize<JigsawEditorIntent>(bytes) ?? throw new InvalidDataException("Missing editor intent."),
        async context =>
        {
            var session = JigsawNativeSession.Current;
            if (session?.Map is { } map && session.Native.CurrentActIndex == context.Message.Act && ReferenceEquals(session.Native, context.Player.RunState))
            {
                var input = inputs.GetValue(map, _ => new());
                var message = context.Message;
                if (message.EntryRound != session.EntryRound)
                    return;
                if (message.Sequence <= input.Accepted.GetValueOrDefault(context.Player.NetId))
                    return;
                input.Accepted[context.Player.NetId] = message.Sequence;
                bool entry = message.Operation is JigsawEditorOperation.EntryReady or JigsawEditorOperation.EntryFailed or JigsawEditorOperation.ConfirmEntry or JigsawEditorOperation.RetryEntry or JigsawEditorOperation.AbandonEntry or JigsawEditorOperation.RecoverEntry;
                bool exploration = entry || message.Operation is JigsawEditorOperation.CompleteRoom or JigsawEditorOperation.VoteDestination or JigsawEditorOperation.FinalizeDestination;
                var result = message.Operation != JigsawEditorOperation.Disconnect && (message.EditRevision != map.EditRevision || exploration && message.EntryRevision != map.CreateSave().NextEntry) ? JigsawResult.Reject("拼图阶段已变化，请重试。") : context.Message.Operation switch
                {
                    JigsawEditorOperation.FinalizeAutomatic => IsHost(context.Player.NetId) ? session.ApplyAutomatic(message.Layout, message.AutomaticFailure) : JigsawResult.Reject("只有 Host 可以提交自动拼图。"),
                    JigsawEditorOperation.Disconnect => IsHost(context.Player.NetId) && message.CompletedOwner is { } departed ? session.ApplyDisconnect(departed) : JigsawResult.Reject("Only Host can disconnect."),
                    JigsawEditorOperation.Reset => IsHost(context.Player.NetId) ? map.ResetLayout() : JigsawResult.Reject("只有 Host 可以重置拼图。"),
                    JigsawEditorOperation.ReturnToPool => map.ReturnToPool(context.Player.NetId, context.Message.Piece),
                    JigsawEditorOperation.Take => map.Take(context.Player.NetId, context.Message.Piece),
                    JigsawEditorOperation.Rotate => map.Rotate(context.Player.NetId),
                    JigsawEditorOperation.Cancel => map.Cancel(context.Player.NetId),
                    JigsawEditorOperation.Drop => map.Drop(context.Player.NetId, context.Message.Point),
                    JigsawEditorOperation.SelectEndpoint => map.SelectedEndpoints.GetValueOrDefault(context.Player.NetId, -1) == (message.FirstEndpoint ?? -1) && (message.FirstEndpoint == null || message.NodeId == null || message.FirstEndpoint == message.NodeId || message.DesiredAdd != null) ? map.SelectEndpoint(context.Player.NetId, message.NodeId, message.DesiredAdd) : JigsawResult.Reject("待连接端点已变化。"),
                    JigsawEditorOperation.ChooseEnd => map.ChooseEnd(context.Player.NetId, context.Message.Choice),
                    JigsawEditorOperation.CompleteAncient => IsHost(context.Player.NetId) && message.CompletedOwner is { } owner && (RunManager.Instance.NetService.NetId != context.Player.NetId || session.NativeAncientFinished(owner)) ? map.RecordNativeAncientCompletion(owner) : JigsawResult.Reject("Ancient reward unfinished."),
                    JigsawEditorOperation.ContinueAncient => map.Continue(context.Player.NetId),
                    JigsawEditorOperation.CompleteRoom => message.CompletedOwner == context.Player.NetId && JigsawNativeExploration.NativeFlowFinished(session, context.Player.NetId) ? map.RecordRoomCompletion(context.Player.NetId) : JigsawResult.Reject("Room flow unfinished."),
                    JigsawEditorOperation.VoteDestination => message.NodeId is { } voted ? map.VoteDestination(context.Player.NetId, voted) : JigsawResult.Reject(JigsawFailure.UnknownNode),
                    JigsawEditorOperation.FinalizeDestination => IsHost(context.Player.NetId) && message.NodeId is { } selected ? map.FinalizeDestination(selected) : JigsawResult.Reject("Only Host can finalize a destination."),
                    JigsawEditorOperation.EntryReady => message.CompletedOwner == context.Player.NetId ? session.AcceptEntryReady(context.Player.NetId) : JigsawResult.Reject("Only the native room owner can report readiness."),
                    JigsawEditorOperation.EntryFailed => message.CompletedOwner == context.Player.NetId ? session.AcceptEntryFailure(context.Player.NetId) : JigsawResult.Reject("Only the native room owner can report failure."),
                    JigsawEditorOperation.ConfirmEntry => IsHost(context.Player.NetId) ? session.ConfirmSharedEntry() : JigsawResult.Reject("Only Host can confirm entry."),
                    JigsawEditorOperation.RetryEntry or JigsawEditorOperation.AbandonEntry => IsHost(context.Player.NetId) ? session.RequestEntryRecovery(message.Operation == JigsawEditorOperation.AbandonEntry) : JigsawResult.Reject("Only Host can choose entry recovery."),
                    JigsawEditorOperation.RecoverEntry => IsHost(context.Player.NetId) && message.EntryCheckpoint != null ? session.AcceptEntryRecovery(message.EntryCheckpoint) : JigsawResult.Reject("Only Host can restore entry."),
                    _ => JigsawResult.Reject("未知拼图操作。")
                };
                if (context.Player.NetId == RunManager.Instance.NetService.NetId && message.Sequence >= input.Next)
                {
                    input.Next = message.Sequence;
                    input.Pending = false;
                    input.Endpoint = map.SelectedEndpoints.TryGetValue(context.Player.NetId, out var endpoint) ? endpoint : null;
                    if (!result.Success || message.Operation != JigsawEditorOperation.SelectEndpoint || input.Endpoint == null)
                        input.Blank = false;
                }
                if (result.Success && context.Message.Operation == JigsawEditorOperation.CompleteAncient)
                    session.OnAncientCompleted();
                if (result.Success && !entry)
                    session.OnEdited();
                if (result.Success && context.Message.Operation is JigsawEditorOperation.ChooseEnd or JigsawEditorOperation.Disconnect)
                    session.OnOrderedConsensus();
                if (result.Success && message.Operation is JigsawEditorOperation.VoteDestination or JigsawEditorOperation.CompleteRoom or JigsawEditorOperation.Disconnect)
                    session.OnDestinationBallotChanged();
                if (result.Success && message.Operation == JigsawEditorOperation.FinalizeDestination && message.NodeId is { } destination)
                    session.OnDestinationFinalized(destination);
                if (!result.Success && message.Operation is JigsawEditorOperation.RecoverEntry or JigsawEditorOperation.ConfirmEntry)
                    Console.Error.WriteLine($"Jigsaw entry {message.Operation} rejected: {result.Reason}");
                if (message.Operation == JigsawEditorOperation.Reset && context.Player.NetId == RunManager.Instance.NetService.NetId && input.Resets.TryGetValue(message.Sequence, out var completion))
                {
                    try
                    { if (result.Success) await session.WaitForSave(); completion.TrySetResult(JigsawNativeSession.Current == session ? result : JigsawResult.Reject("会话已结束。")); }
                    catch (Exception error) { completion.TrySetException(error); }
                    finally { input.Resets.Remove(message.Sequence); }
                }
                if (message.Operation != JigsawEditorOperation.Reset && !entry)
                    session.EditorResult(context.Player.NetId, context.Message.Operation, result);
                session.RefreshEditor();
            }
            return;
        }, GameActionType.NonCombat);
    // Entry readiness and disconnects must execute inside CombatRoom; NonCombat waits for combat to end.
    private static readonly RitsuLibManagedNetActionDescriptor<JigsawEditorIntent> entryDescriptor =
        descriptor with { ActionKey = "jigsaw-entry", ActionType = GameActionType.Any };

    private static bool UsesEntryQueue(JigsawEditorOperation operation) =>
        operation is JigsawEditorOperation.Disconnect or JigsawEditorOperation.EntryReady or
            JigsawEditorOperation.EntryFailed or JigsawEditorOperation.ConfirmEntry or
            JigsawEditorOperation.RetryEntry or JigsawEditorOperation.AbandonEntry or
            JigsawEditorOperation.RecoverEntry;

    internal static bool IsHost(ulong issuer) => RunManager.Instance.NetService switch
    {
        MegaCrit.Sts2.Core.Multiplayer.NetHostGameService host => issuer == host.NetId,
        MegaCrit.Sts2.Core.Multiplayer.NetClientGameService client => issuer == client.HostNetId,
        MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService single => issuer == single.NetId,
        _ => false
    };

    internal static Task<JigsawResult> Reset(JigsawNativeSession session)
    {
        if (session.Map is not { } map)
            return Task.FromResult(JigsawResult.Reject("当前没有拼图。"));
        var input = inputs.GetValue(map, _ => new());
        var sequence = input.Next + 1;
        var completion = new TaskCompletionSource<JigsawResult>();
        input.Resets[sequence] = completion;
        var submitted = Submit(session, JigsawEditorOperation.Reset);
        if (!submitted.Success)
        { input.Resets.Remove(sequence); completion.TrySetResult(submitted); }
        return completion.Task;
    }
    internal static void Initialize()
    {
        RitsuLibManagedNetActions.Register(descriptor);
        RitsuLibManagedNetActions.Register(entryDescriptor);
    }
    internal static void Stop(JigsawRun map)
    {
        if (!inputs.TryGetValue(map, out var input))
            return;
        foreach (var completion in input.Resets.Values)
            completion.TrySetResult(JigsawResult.Reject("会话已结束。"));
        input.Resets.Clear();
        input.Pending = false;
    }

    internal static JigsawResult Submit(JigsawNativeSession session, JigsawEditorOperation operation, int piece = 0, GridPoint point = default, int? nodeId = null, EndChoice? choice = null, ulong? completedOwner = null, JigsawLayout? layout = null, string? automaticFailure = null, string? entryCheckpoint = null)
    {
        if (!ReferenceEquals(session, JigsawNativeSession.Current))
            return JigsawResult.Reject("拼图会话已结束。");
        if (session.Map is not { } map)
            return JigsawResult.Reject("当前没有拼图。");
        if (operation == JigsawEditorOperation.CompleteRoom && (completedOwner != RunManager.Instance.NetService.NetId || !JigsawNativeExploration.LocalCompletionWasObserved(session, completedOwner.Value)))
            return JigsawResult.Reject("Room flow unfinished.");
        var input = inputs.GetValue(map, _ => new());
        var player = RunManager.Instance.NetService.NetId;
        if (!input.Pending || input.Revision != map.EditRevision)
        {
            input.Endpoint = map.SelectedEndpoints.TryGetValue(player, out var endpoint) ? endpoint : null;
            input.Blank = false;
        }
        input.Revision = map.EditRevision;
        int? first = input.Endpoint;
        bool? add = null;
        if (operation == JigsawEditorOperation.SelectEndpoint)
        {
            if (nodeId == null)
            {
                if (input.Blank)
                    input.Endpoint = null;
                input.Blank = !input.Blank;
            }
            else
            {
                input.Blank = false;
                if (first == null)
                    input.Endpoint = nodeId;
                else
                {
                    if (first != nodeId)
                        add = !map.Edges.Any(e => e.Connects(first.Value, nodeId.Value));
                    input.Endpoint = null;
                }
            }
        }
        input.Pending = true;
        var message = new JigsawEditorIntent(session.Native.CurrentActIndex, operation, piece, point, nodeId, choice, ++input.Next, map.EditRevision, first, add, completedOwner, layout, automaticFailure, map.CreateSave().NextEntry, session.EntryRound, entryCheckpoint);
        if (RitsuLibManagedNetActions.Request(RunManager.Instance,
            UsesEntryQueue(operation) ? entryDescriptor : descriptor, message))
            return JigsawResult.Ok;
        input.Pending = false;
        return JigsawResult.Reject("无法向当前原生会话提交拼图操作。");
    }
}
