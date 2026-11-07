using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace AllMatters.Jigsaw;

public enum JigsawEditorOperation { Take, Rotate, Drop, Cancel, ReturnToPool, SelectEndpoint, ChooseEnd, CompleteAncient, ContinueAncient }
public sealed record JigsawEditorIntent(int Act, JigsawEditorOperation Operation, int Piece=0,GridPoint Point=default,int? NodeId=null,EndChoice? Choice=null);

internal static class JigsawEditorAction
{
    private static readonly RitsuLibManagedNetActionDescriptor<JigsawEditorIntent> descriptor = new(
        "AllMatters", "jigsaw-editor",
        message => JsonSerializer.SerializeToUtf8Bytes(message),
        bytes => JsonSerializer.Deserialize<JigsawEditorIntent>(bytes) ?? throw new InvalidDataException("Missing editor intent."),
        context =>
        {
            var session=JigsawNativeSession.Current;
            if(session?.Map is {} map && session.Native.CurrentActIndex==context.Message.Act)
            {
                var result=context.Message.Operation switch
                {
                    JigsawEditorOperation.ReturnToPool=>map.ReturnToPool(context.Player.NetId,context.Message.Piece),
                    JigsawEditorOperation.Take=>map.Take(context.Player.NetId,context.Message.Piece),
                    JigsawEditorOperation.Rotate=>map.Rotate(context.Player.NetId),
                    JigsawEditorOperation.Cancel=>map.Cancel(context.Player.NetId),
                    JigsawEditorOperation.Drop=>map.Drop(context.Player.NetId,context.Message.Point),
                    JigsawEditorOperation.SelectEndpoint=>map.SelectEndpoint(context.Player.NetId,context.Message.NodeId),
                    JigsawEditorOperation.ChooseEnd=>map.ChooseEnd(context.Player.NetId,context.Message.Choice),
                    JigsawEditorOperation.CompleteAncient=>map.CompleteAncient(context.Player.NetId),
                    JigsawEditorOperation.ContinueAncient=>map.Continue(context.Player.NetId),
                    _=>JigsawResult.Reject("未知拼图操作。")
                };
                if(result.Success)session.OnEdited();
                if(result.Success && context.Message.Operation==JigsawEditorOperation.CompleteAncient)session.OnAncientCompleted();
                if(result.Success && context.Message.Operation==JigsawEditorOperation.ChooseEnd)session.OnOrderedConsensus();
                session.EditorResult(context.Player.NetId,context.Message.Operation,result);
                session.RefreshEditor();
            }
            return Task.CompletedTask;
        },GameActionType.NonCombat);

    internal static void Initialize() => RitsuLibManagedNetActions.Register(descriptor);

    internal static JigsawResult Submit(JigsawNativeSession session,JigsawEditorOperation operation,int piece=0,GridPoint point=default,int? nodeId=null,EndChoice? choice=null) =>
        RitsuLibManagedNetActions.Request(RunManager.Instance,descriptor,new(session.Native.CurrentActIndex,operation,piece,point,nodeId,choice))
            ? JigsawResult.Ok : JigsawResult.Reject("无法向当前原生会话提交拼图操作。");
}
