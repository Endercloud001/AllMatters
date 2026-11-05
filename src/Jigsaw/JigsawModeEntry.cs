using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace AllMatters.Jigsaw;

/// <summary>The standard solo character selection retains its native launch path.</summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer))]
internal static class JigsawModeEntry
{
    private static void Postfix(NCharacterSelectScreen __instance)
        => Attach(__instance);
    internal static JigsawModeChoice Attach(Control __instance)
    {
        JigsawNativeSession.SelectedForNewRun = false;
        var choice = __instance.GetNodeOrNull<JigsawModeChoice>("AllMattersJigsawMode");
        if (choice == null)
        {
            choice = new JigsawModeChoice
            {
                Name = "AllMattersJigsawMode", Text = "拼图模式",
                TopLevel = true, ZIndex = 100, Size = new(300, 64),
                FocusMode = Control.FocusModeEnum.All
            };
            choice.Toggled += selected => JigsawNativeSession.SelectedForNewRun = selected;
            __instance.AddChild(choice);
        }
        choice.ButtonPressed = false;
        choice.Show();
        return choice;
    }
}

/// <summary>The native select button keeps its embark hotkey; this has a distinct bound action.</summary>
public partial class JigsawModeChoice : CheckButton
{
    private readonly TextureRect hotkey = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore,
        Position = new(250, 12), Size = new(32, 32)
    };
    public override void _Ready() { AddChild(hotkey); PositionEntrance(); }
    private void PositionEntrance()
    {
        if (TopLevel) Position = new Vector2(40, Math.Max(0, GetViewportRect().Size.Y - 190));
    }
    public override void _Process(double delta)
    {
        PositionEntrance();
#if STS2_PUBLIC
        hotkey.Texture = MegaCrit.Sts2.Core.Nodes.CommonUi.NControllerManager.Instance?.GetHotkeyIcon(MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewDrawPile);
#else
        hotkey.Texture = MegaCrit.Sts2.Core.Nodes.CommonUi.NInputManager.Instance?.GetHotkeyIcon(MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewDrawPile);
#endif
    }
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || input.IsEcho() || !input.IsActionPressed(MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewDrawPile)) return;
        ButtonPressed = !ButtonPressed;
        GetViewport().SetInputAsHandled();
    }
}

[HarmonyPatch]
internal static class JigsawHideNonSoloEntry
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeMultiplayerAsHost));
        yield return AccessTools.Method(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeMultiplayerAsClient));
    }
    private static void Prefix(NCharacterSelectScreen __instance)
    {
        JigsawNativeSession.SelectedForNewRun = false;
        __instance.GetNodeOrNull<CheckButton>("AllMattersJigsawMode")?.Hide();
    }
}
