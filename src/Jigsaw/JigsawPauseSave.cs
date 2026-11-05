using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(NPauseMenu), "CloseToMenu")]
internal static class JigsawWaitForSafeExit
{
    [ThreadStatic] private static bool invoking;
    private static bool Prefix(NPauseMenu __instance, ref Task __result)
    {
        if (invoking || JigsawNativeSession.Current is not { } session) return true;
        __result = WaitAndExit(session, __instance);
        return false;
    }

    private static async Task WaitAndExit(JigsawNativeSession session, NPauseMenu menu)
    {
        try { await session.WaitForSave(); }
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
        try { original = (Task)AccessTools.Method(typeof(NPauseMenu), "CloseToMenu").Invoke(menu, null)!; }
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
    private static void Postfix() { if(JigsawNativeSession.Current is {} session) _=AfterFrame(session); }
    private static async Task AfterFrame(JigsawNativeSession session)
    {
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        if(JigsawNativeSession.Current==session && MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.SubmenuStack.Stack?.SubmenusOpen!=true)session.ResumeInput();
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuOpened))]
internal static class JigsawSuspendNativePause
{
    private static void Postfix()=>JigsawNativeSession.Current?.SuspendForNativePause();
}
