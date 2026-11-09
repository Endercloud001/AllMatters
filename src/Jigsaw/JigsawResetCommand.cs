using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AllMatters.Jigsaw;

public sealed class JigsawResetCommand : AbstractConsoleCmd
{
    public override string CmdName => "am_jigsaw_reset";
    public override string Args => "";
    public override string Description => "由 Host 在可编辑拼图中归还全部碎块，保留本幕原池。";
    public override bool IsNetworked => false;
    // Keep AbstractConsoleCmd.DebugOnly and the native debug/mod/achievement checks.
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 0)
            return new(false, "用法：am_jigsaw_reset（无参数）。");
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return new(false, "当前不在拼图模式。");
        if (issuingPlayer == null || !ReferenceEquals(issuingPlayer.RunState, session.Native) || issuingPlayer.NetId != MegaCrit.Sts2.Core.Runs.RunManager.Instance.NetService.NetId || !JigsawEditorAction.IsHost(issuingPlayer.NetId))
            return new(false, "只有 Host 可以重置拼图。");
        if (!session.CanResetLayout)
            return new(false, "当前不在可编辑拼图界面，不能重置拼图。");
        async Task Complete()
        {
            // TODO(completion): Reset async completion depends on managed action handler resolving
            // the TaskCompletionSource. If session closes between Reset() storing the completion
            // and handler executing, the completion may be orphaned. Consider adding explicit
            // test verifying handler completes the task, or document timeout behavior.
            var result = await JigsawEditorAction.Reset(session);
            if (!result.Success)
                throw new InvalidOperationException(string.IsNullOrEmpty(result.Reason) ? result.Failure.ToString() : result.Reason);
            MegaCrit.Sts2.Core.Logging.Log.Info("已归还全部碎块并保存；本幕原池保持不变。");
        }
        return new(Complete(), true, "已提交 Host 重置请求，等待原生顺序接受并保存。");
    }
}
