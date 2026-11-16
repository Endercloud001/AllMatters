using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.addons.mega_text;

internal sealed class HuntingLayoutNativeHost : IDisposable
{
    // Native layout cases run serially against the game's singleton UI/run context.
    internal static NCombatRoom? CombatRoom { get; private set; }
    private static bool SkipLifecycle() => false;
    private static bool SkipAudio() => false;
    private static bool CreatureNode(Creature __instance, ref NCreature? __result)
    {
        __result = CombatRoom?.GetCreatureNode(__instance);
        return false;
    }

    internal static async Task Frames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    internal static async Task Until(Func<bool> ready, string failure)
    {
        ulong deadline = Time.GetTicksMsec() + 10000;
        while (!ready() && Time.GetTicksMsec() < deadline) await Frames(1);
        NativeEventTests.Check(ready(), failure);
    }

    internal readonly Window Window = ((SceneTree)Engine.GetMainLoop()).Root;
    private readonly Vector2I size;
    private readonly Vector2I content;
    private readonly Window.ContentScaleModeEnum mode;
    private readonly Window.ContentScaleAspectEnum aspect;
    private readonly bool testing = TestMode.IsOn;
    private readonly Input.MouseModeEnum mouseMode = Input.MouseMode;
    private readonly Func<bool> autoSlayer = NonInteractiveMode.AutoSlayerCheck;
    private readonly System.Reflection.FieldInfo consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
    private readonly object? previousConsole;
    private readonly Harmony patches = new("AllMatters.Tests.HuntingNativeLayout");
    private readonly List<StringName> addedActions = [];
    private readonly List<Node> owned = [];
    private JigsawNativeMapUiHost? ui;
    private IDisposable? atlases;
    internal NTargetManager Target = null!;
    internal NControllerManager Controller = null!;

    internal HuntingLayoutNativeHost()
    {
        size = Window.Size; content = Window.ContentScaleSize; mode = Window.ContentScaleMode; aspect = Window.ContentScaleAspect;
        previousConsole = consoleField.GetValue(null);
    }

    private T Own<T>(T node) where T : Node { owned.Add(node); return node; }
    internal async Task Mount(RunState runState, CombatState combat, Func<Task> prepareEncounter, Action<NCombatRoom>? prepareRoom = null)
    {
        Window.ContentScaleSize = new Vector2I(1920, 1080);
        Window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        Window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        Window.Size = new Vector2I(1280, 720);
        foreach (string name in MegaInput.AllInputs.Concat(MegaCrit.Sts2.Core.ControllerInput.Controller.AllControllerInputs.Select(action => action.ToString())).Concat(new[] { "debug_hide_intents", "debug_hide_combat_ui", "debug_hide_play_container", "debug_hide_hand",
            "debug_hide_hp_bars", "debug_hide_text_vfx", "debug_hide_targeting_ui", "debug_slow_rewards", "debug_hide_mp_targeting", "debug_hide_mp_intents" }
            .Concat(Enumerable.Range(1, 10).Select(index => "mega_select_card_" + index))
            .Concat(new[] { DebugHotkey.speedUp.ToString(), DebugHotkey.speedDown.ToString(), DebugHotkey.hideProceedButton.ToString(), DebugHotkey.hideHoverTips.ToString() })))
        {
            StringName action = name;
            if (!InputMap.HasAction(action)) { InputMap.AddAction(action); addedActions.Add(action); }
        }
        atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        JigsawHuntingNativeTests.RegisterScripts();
        ui = new JigsawNativeMapUiHost();
        RunManager.Instance.GenerateRooms();
        await prepareEncounter();
        var session = JigsawNativeSession.TryGet(runState)!;
        await ui.Open(session); session.HideMap();
        var game = NGame.Instance!;
        var run = NRun.Instance!;
        run.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
        var hotkeys = Own(new NHotkeyManager());
        AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
        await HeadlessUiHost.Mount(hotkeys);
        Controller = Own(new NControllerManager { ProcessMode = Node.ProcessModeEnum.Always });
        // Reproduce the original game.tscn label atom. Init's remaining
        // work starts Steam; native detection and notification stay real.
        var label = new MegaLabel { Name = "Label", AutoSizeEnabled = false, Modulate = Colors.Transparent,
            AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -133, OffsetTop = -74, OffsetRight = -41, OffsetBottom = -28,
            GrowHorizontal = Control.GrowDirection.Begin, GrowVertical = Control.GrowDirection.Begin };
        label.AddThemeColorOverride("font_color", new Color(0.34902f, 1, 1, 1));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 10);
        label.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        label.AddThemeFontSizeOverride("font_size", 36);
        Controller.AddChild(label);
        AccessTools.Field(typeof(NControllerManager), "_label").SetValue(Controller, label);
        AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, Controller);
        AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, Own(new NCursorManager()));
        Target = Own(new NTargetManager { ProcessMode = Node.ProcessModeEnum.Always });
        foreach (string path in NTargetingArrow.AssetPaths) PreloadManager.Cache.GetTexture2D(path);
        var arrow = PreloadManager.Cache.GetScene("res://scenes/combat/targeting_arrow.tscn").Instantiate<NTargetingArrow>();
        arrow.Name = "TargetingArrow"; Target.AddChild(arrow);
        var players = Own(new NMultiplayerPlayerStateContainer());
        players.Initialize(runState);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(run.GlobalUi, Target);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(run.GlobalUi, players);
        consoleField.SetValue(null, Own(new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false }));
        AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, Own(new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake()));
        foreach (string method in new[] { "_EnterTree", "_Ready", "_ExitTree" })
            patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(HuntingLayoutNativeHost), nameof(SkipLifecycle)));
        patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]), prefix: new HarmonyMethod(typeof(HuntingLayoutNativeHost), nameof(SkipAudio)));
        foreach (string method in new[] { "_Ready", "_Process" })
            patches.Patch(AccessTools.Method(typeof(NMapScreen), method), prefix: new HarmonyMethod(typeof(HuntingLayoutNativeHost), nameof(SkipLifecycle)));
        game.MouseFilter = Control.MouseFilterEnum.Ignore;
        var tips = Own(new Node { Name = "HoverTipsContainer" }); game.AddChild(tips);
        AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, tips);
        var map = Own(new NMapScreen());
        var capstone = Own(new NCapstoneContainer());
        var overlays = Own(new NOverlayStack { ProcessMode = Node.ProcessModeEnum.Always, MouseFilter = Control.MouseFilterEnum.Ignore });
        var backstop = new Control { Name = "OverlayBackstop" }; overlays.AddChild(backstop);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(run.GlobalUi, map);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(run.GlobalUi, capstone);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(run.GlobalUi, overlays);
        AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(run, new ScreenStateTracker(map, capstone, overlays));
        run.GlobalUi.AddChild(map); map.Hide(); map.SetProcess(false);
        run.GlobalUi.AddChild(overlays); overlays.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        run.GlobalUi.AddChild(Target);
        var rooms = Own(new NSceneContainer { ProcessMode = Node.ProcessModeEnum.Always });
        AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(run, rooms);
        run.AddChild(rooms); rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await HeadlessUiHost.Mount(game);
        await HeadlessUiHost.Mount(Controller);
        game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        run.MoveChild(run.GlobalUi, run.GetChildCount() - 1);
        TestMode.IsOn = false; NonInteractiveMode.AutoSlayerCheck = () => false;
        CombatRoom = NCombatRoom.Create((CombatRoom)runState.CurrentRoom!, CombatRoomMode.ActiveCombat)!;
        AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(rooms, CombatRoom);
        rooms.AddChild(CombatRoom); CombatRoom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        prepareRoom?.Invoke(CombatRoom);
        CombatRoom.Ui.Activate(combat);
        patches.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.GetCreatureNode)), prefix: new HarmonyMethod(typeof(HuntingLayoutNativeHost), nameof(CreatureNode)));
        ActiveScreenContext.Instance.Update();
        await Frames(4);
    }

    public void Dispose()
    {
        try
        {
            if (Target != null && GodotObject.IsInstanceValid(Target)) Target.CancelTargeting();
            if (CombatRoom != null && GodotObject.IsInstanceValid(CombatRoom)) CombatRoom.Free();
            for (int index = owned.Count - 1; index >= 0; index--)
                if (GodotObject.IsInstanceValid(owned[index])) owned[index].Free();
        }
        finally
        {
            CombatRoom = null; consoleField.SetValue(null, previousConsole);
            try { ui?.Dispose(); }
            finally
            {
                try { patches.UnpatchAll(patches.Id); }
                finally
                {
                    atlases?.Dispose();
                    foreach (var action in addedActions) InputMap.EraseAction(action);
                    TestMode.IsOn = testing; NonInteractiveMode.AutoSlayerCheck = autoSlayer;
                    Input.MouseMode = mouseMode;
                    Window.ContentScaleSize = content; Window.ContentScaleMode = mode; Window.ContentScaleAspect = aspect; Window.Size = size;
                }
            }
        }
    }
}
