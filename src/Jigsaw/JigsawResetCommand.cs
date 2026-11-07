using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AllMatters.Jigsaw;

public sealed class JigsawResetCommand : AbstractConsoleCmd
{
    public override string CmdName => "am_jigsaw_reset";
    public override string Args => "";
    public override string Description => "在可编辑单人拼图中归还全部碎块，保留本幕原池。";
    public override bool IsNetworked => false;
    // Keep AbstractConsoleCmd.DebugOnly and the native debug/mod/achievement checks.
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 0) return new(false, "用法：am_jigsaw_reset（无参数）。");
        if (JigsawNativeSession.Current is not { Map: not null } session || session.Native.Players.Count != 1)
            return new(false, "当前不在单人拼图模式。");
        if (!session.CanResetLayout)
            return new(false, "当前不在可编辑拼图界面，不能重置拼图。");
        var result = session.Map.ResetLayout();
        if (!result.Success) return new(false, "当前已离开可编辑阶段，不能重置拼图。");
        session.OnEdited(); session.ShowMap();
        return new(session.WaitForSave(), true, "已归还全部碎块并清理临时操作；本幕原池保持不变。");
    }
}
