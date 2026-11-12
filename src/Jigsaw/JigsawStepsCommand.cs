using System.Globalization;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

public sealed class JigsawStepsCommand : AbstractConsoleCmd
{
    public override string CmdName => "am_steps";
    public override string Args => "N (0..2147483647)";
    public override string Description => "Host 将本幕剩余步数设为 N；仅稳定的非战斗探索阶段可用，不解除追猎。";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int remaining))
            return new(false, "用法：am_steps N；N 必须为0至2147483647的整数。");
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return new(false, "当前不在拼图模式。");
        if (issuingPlayer == null || !ReferenceEquals(issuingPlayer.RunState, session.Native) ||
            issuingPlayer.NetId != RunManager.Instance.NetService.NetId || !JigsawEditorAction.IsHost(issuingPlayer.NetId))
            return new(false, "只有 Host 可以设置剩余步数。");
        if (!session.CanSetRemainingSteps)
            return new(false, "只能在稳定的非战斗探索阶段设置剩余步数。");
        async Task Complete()
        {
            var result = await JigsawEditorAction.SetRemainingSteps(session, remaining);
            if (!result.Success)
                throw new InvalidOperationException(result.Reason);
            MegaCrit.Sts2.Core.Logging.Log.Info($"本幕剩余步数已设为 {remaining}；沿用下一正常完整保存点。");
        }
        return new(Complete(), true, "已提交 Host 剩余步数请求，等待原生顺序接受。");
    }
}
