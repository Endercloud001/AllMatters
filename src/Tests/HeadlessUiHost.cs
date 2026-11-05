using Godot;
using MegaCrit.Sts2.Core.ControllerInput;

// Supplies the game's input action names and waits for the real Godot tree.
// It does not call _Input, substitute event dispatch, or install production callbacks.
internal static class HeadlessUiHost
{
    internal static void EnsureInputActions()
    {
        foreach (var action in MegaInput.AllInputs)
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
    }

    internal static async Task Mount(params Node[] nodes)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        foreach (var node in nodes) tree.Root.CallDeferred(Node.MethodName.AddChild, node);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (nodes.Any(node => !node.IsInsideTree()))
            throw new InvalidOperationException("UI host preparation failed: a node did not enter the real scene tree.");
    }
}
