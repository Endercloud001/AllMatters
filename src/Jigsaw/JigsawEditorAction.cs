using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace AllMatters.Jigsaw;

public enum JigsawEditorOperation { Take, Rotate, Drop, Cancel, ReturnToPool, SelectEndpoint, ChooseEnd, CompleteAncient, ContinueAncient, Reset, FinalizeAutomatic, Disconnect }
public sealed record JigsawEditorIntent(int Act, JigsawEditorOperation Operation, int Piece = 0, GridPoint Point = default, int? NodeId = null, EndChoice? Choice = null, long Sequence = 0, long EditRevision = 0, int? FirstEndpoint = null, bool? DesiredAdd = null, ulong? CompletedOwner = null, JigsawLayout? Layout = null, string? AutomaticFailure = null);

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
                if (message.Sequence <= input.Accepted.GetValueOrDefault(context.Player.NetId))
                    return;
                input.Accepted[context.Player.NetId] = message.Sequence;
                var result = message.Operation != JigsawEditorOperation.Disconnect && message.EditRevision != map.EditRevision ? JigsawResult.Reject("拼图编辑阶段已变化，请重试。") : context.Message.Operation switch
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
                if (result.Success)
                    session.OnEdited();
                if (result.Success && context.Message.Operation is JigsawEditorOperation.ChooseEnd or JigsawEditorOperation.Disconnect)
                    session.OnOrderedConsensus();
                if (message.Operation == JigsawEditorOperation.Reset && context.Player.NetId == RunManager.Instance.NetService.NetId && input.Resets.TryGetValue(message.Sequence, out var completion))
                {
                    try
                    { if (result.Success) await session.WaitForSave(); completion.TrySetResult(JigsawNativeSession.Current == session ? result : JigsawResult.Reject("会话已结束。")); }
                    catch (Exception error) { completion.TrySetException(error); }
                    finally { input.Resets.Remove(message.Sequence); }
                }
                if (message.Operation != JigsawEditorOperation.Reset)
                    session.EditorResult(context.Player.NetId, context.Message.Operation, result);
                session.RefreshEditor();
            }
            return;
        }, GameActionType.NonCombat);

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
    internal static void Initialize() => RitsuLibManagedNetActions.Register(descriptor);
    internal static void Stop(JigsawRun map)
    {
        if (!inputs.TryGetValue(map, out var input))
            return;
        foreach (var completion in input.Resets.Values)
            completion.TrySetResult(JigsawResult.Reject("会话已结束。"));
        input.Resets.Clear();
        input.Pending = false;
    }

    internal static JigsawResult Submit(JigsawNativeSession session, JigsawEditorOperation operation, int piece = 0, GridPoint point = default, int? nodeId = null, EndChoice? choice = null, ulong? completedOwner = null, JigsawLayout? layout = null, string? automaticFailure = null)
    {
        if (!ReferenceEquals(session, JigsawNativeSession.Current))
            return JigsawResult.Reject("拼图会话已结束。");
        if (session.Map is not { } map)
            return JigsawResult.Reject("当前没有拼图。");
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
        var message = new JigsawEditorIntent(session.Native.CurrentActIndex, operation, piece, point, nodeId, choice, ++input.Next, map.EditRevision, first, add, completedOwner, layout, automaticFailure);
        if (RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, message))
            return JigsawResult.Ok;
        input.Pending = false;
        return JigsawResult.Reject("无法向当前原生会话提交拼图操作。");
    }
}
