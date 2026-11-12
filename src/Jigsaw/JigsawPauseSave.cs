using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.TestSupport;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(NPauseMenu), "CloseToMenu")]
internal static class JigsawWaitForSafeExit
{
    [ThreadStatic] private static bool invoking;
    private static bool Prefix(NPauseMenu __instance, ref Task __result)
    {
        if (invoking || JigsawNativeSession.Current is not { } session)
            return true;
        __result = WaitAndExit(session, __instance);
        return false;
    }

    private static async Task WaitAndExit(JigsawNativeSession session, NPauseMenu menu)
    {
        try
        {
            await session.WaitForSave();
            await JigsawTreasureExitDrain.Wait(session, menu);
        }
        catch (Exception)
        {
            var error = new JigsawSaveErrorDialog
            {
                Title = "保存失败",
                DialogText = "拼图尚未成功保存，已保留本局。请返回拼图重试保存后再退出。",
                Exclusive = true
            };
            error.Confirmed += error.QueueFree;
            error.CloseRequested += error.QueueFree;
            menu.AddChild(error);
            error.PopupCentered(new Vector2I(600, 180));
            return;
        }
        Task original;
        invoking = true;
        try
        { original = (Task)AccessTools.Method(typeof(NPauseMenu), "CloseToMenu").Invoke(menu, null)!; }
        finally { invoking = false; }
        await original;
    }
}

internal static class JigsawTreasureExitDrain
{
    internal static async Task Wait(JigsawNativeSession session, Node? parent)
    {
        if (!JigsawNativeExploration.HasUnsettledTreasureTasks(session))
            return;
        JigsawNativeExploration.StopNewTreasureTasks(session);
        try
        {
            if (MegaCrit.Sts2.Core.Runs.RunManager.Instance.ShouldSave)
            {
                await MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SaveRun(null);
                await session.WaitForSave();
            }
        }
        catch { JigsawNativeExploration.ResumeTreasureTasksAfterFailedExit(session); throw; }
        AcceptDialog? notice = null;
        if (!TestMode.IsOn && GodotObject.IsInstanceValid(parent))
        {
            notice = new AcceptDialog
            {
                Title = "等待宝箱任务结束",
                DialogText = "宝箱任务仍在完成。正在保留当前会话，任务结束后继续退出；关闭此提示不会取消等待。未完成时读档将返回开箱前的完整存档点。",
                Exclusive = true
            };
            parent!.AddChild(notice);
            notice.PopupCentered(new Vector2I(660, 200));
        }
        try
        { await JigsawNativeExploration.DrainTreasureTasks(session); }
        finally { if (GodotObject.IsInstanceValid(notice)) notice!.QueueFree(); }
    }
}

// Both native disconnect/error return paths reach this method before CleanUp
// destroys the run. SaveSession still identifies it even after disconnect closed it.
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.NGame), nameof(MegaCrit.Sts2.Core.Nodes.NGame.ReturnToMainMenu))]
internal static class JigsawDrainTreasureBeforeMenu
{
    [ThreadStatic] private static bool invoking;
    private static bool Prefix(MegaCrit.Sts2.Core.Nodes.NGame __instance, ref Task __result)
    {
        if (invoking || JigsawNativeSession.SaveSession is not { } session ||
            !JigsawNativeExploration.HasUnsettledTreasureTasks(session))
            return true;
        __result = Return(session, __instance);
        return false;
    }

    private static async Task Return(JigsawNativeSession session, MegaCrit.Sts2.Core.Nodes.NGame game)
    {
        await JigsawTreasureExitDrain.Wait(session, game);
        Task original;
        invoking = true;
        try
        { original = game.ReturnToMainMenu(); }
        finally { invoking = false; }
        await original;
    }
}

public partial class JigsawSaveErrorDialog : AcceptDialog
{
    public override void _Input(InputEvent input)
    {
        if (input.IsActionPressed(MegaCrit.Sts2.Core.ControllerInput.MegaInput.select) ||
            input.IsActionPressed(MegaCrit.Sts2.Core.ControllerInput.MegaInput.cancel))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuClosed))]
internal static class JigsawResumeInput
{
    private static void Postfix() { if (JigsawNativeSession.Current is { } session) _ = AfterFrame(session); }
    private static async Task AfterFrame(JigsawNativeSession session)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (JigsawNativeSession.Current == session && MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.SubmenuStack.Stack?.SubmenusOpen != true)
            session.ResumeInput();
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuOpened))]
internal static class JigsawSuspendNativePause
{
    private static void Postfix() => JigsawNativeSession.Current?.SuspendForNativePause();
}
