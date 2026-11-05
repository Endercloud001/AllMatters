using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.Jigsaw.Presentation;
internal static class JigsawInputPresentation
{
    internal static bool UsingController => Godot.Input.GetConnectedJoypads().Count > 0 &&
#if STS2_PUBLIC
        NControllerManager.Instance?.IsUsingController == true;
#else
        NInputManager.Instance?.ControllerManager.InputType == MegaCrit.Sts2.Core.ControllerInput.InputType.Controller;
#endif
}
