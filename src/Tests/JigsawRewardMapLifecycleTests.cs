using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

internal static class JigsawRewardMapLifecycleTests
{
    internal const string Name = "Jigsaw native reward map pairs overlay lifecycle across repeated open and Escape";

    private static MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.IScreenContext? active;
    private static bool CurrentScreen(ref MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.IScreenContext? __result)
    { __result = active; return false; }

    internal static async Task Run()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        var native = MegaCrit.Sts2.Core.Runs.RunState.CreateForTest(
            [MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
                MegaCrit.Sts2.Core.Models.ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),
                MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1)],
            acts: [MegaCrit.Sts2.Core.Models.ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Overgrowth>()], seed: "REWARD_MAP_LIFECYCLE");
        var manager = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
        manager.SetUpTest(native, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
        using var host = new JigsawNativeMapUiHost();
        var mapScreen = new NMapScreen();
        var addedActions = MegaCrit.Sts2.Core.ControllerInput.MegaInput.AllInputs
            .Append(MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewMap.ToString()).Distinct()
            .Where(action => !InputMap.HasAction(action)).ToArray();
        HeadlessUiHost.EnsureInputActions();
        foreach (var action in addedActions)
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        var context = new Harmony("AllMatters.Tests.RewardMapContext");
        NOverlayStack? ownedOverlays = null;
        try
        {
            manager.GenerateRooms(); await manager.GenerateMap();
            var map = JigsawRun.Create(74);
            map.Take(1, 0); map.Drop(1, new(3, 0)); map.AddEdge(1, 0, 1);
            map.ChooseEnd(1, EndChoice.Manual); map.Inspect(); map.CompleteAncient(1); map.Continue(1);
            var attempt = map.BeginEnter(1)!; map.ConfirmEnter(attempt.Id);
            JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            var view = await host.Open(session);
            session.HideMap();
            var global = NRun.Instance!.GlobalUi;
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(global, mapScreen);
            mapScreen.Hide();
            var overlays = new NOverlayStack();
            ownedOverlays = overlays;
            var backstop = new Control { Name = "OverlayBackstop" };
            overlays.AddChild(backstop);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(global, overlays);
            global.AddChild(overlays);
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var reward = new JigsawRewardOverlayProbe();
            active = reward;
            context.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.ActiveScreenContext), "GetCurrentScreen"),
                prefix: new HarmonyMethod(typeof(JigsawRewardMapLifecycleTests), nameof(CurrentScreen)));
            overlays.Push(reward);
            await tree.ToSignal(tree.CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            int shown = reward.Shown;
            Check(backstop.Modulate.A > .99f, "The native reward overlay initially owns its visible shared backstop.");
            mapScreen.Open(true);
            Check(backstop.Modulate.A == 0 && reward.Hidden == 1,
                "Opening the puzzle must notify the native reward overlay and remove its dark backstop.");
            session.ShowMap();
            Check(reward.Hidden == 1, "Refreshing an already visible map does not hide its owner twice.");
            mapScreen.Open(true);
            Check(!view.Visible && reward.Shown == shown + 1 && backstop.Modulate.A == 1,
                "The next map key restores the same native reward overlay exactly once.");
            session.HideMap();
            Check(reward.Shown == shown + 1, "Repeated hide cannot restore a reward screen twice.");
            mapScreen.Open(true);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(!view.Visible && reward.Hidden == 2 && reward.Shown == shown + 2,
                "Engine-routed Escape closes the map and pairs the second native reward restoration.");
        }
        finally
        {
            ownedOverlays?.Clear();
            manager.CleanUp(false);
            context.UnpatchAll(context.Id); active = null;
            mapScreen.Free();
            foreach (var action in addedActions) InputMap.EraseAction(action);
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

internal partial class JigsawRewardOverlayProbe : Control, IOverlayScreen
{
    internal int Shown, Hidden;
    public NetScreenType ScreenType => NetScreenType.Rewards;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => this;
    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() { }
    public void AfterOverlayShown() { Shown++; Show(); }
    public void AfterOverlayHidden() { Hidden++; Hide(); }
}
