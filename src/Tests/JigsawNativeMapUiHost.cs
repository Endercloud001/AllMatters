using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.TestSupport;

// Owns native scene lookup and containers, without running NGame's asset/audio startup.
// Input, native parent ordering and production ShowMap/CreateScreen remain real.
internal sealed class JigsawNativeMapUiHost : IDisposable
{
    private readonly Harmony harmony = new("AllMatters.Tests.NativeMapUi");
    private readonly NGame game = new();
    private readonly NSceneContainer scenes = new();
    private readonly NRun run = new();
    private readonly NGlobalUi global = new();
    private readonly NCapstoneSubmenuStack submenu = new();
    private readonly NInputManager input = new();
    private readonly NGame? previous = NGame.Instance;
    private static bool SkipReady() => false;
    internal async Task<JigsawScreen> Open(JigsawNativeSession session)
    {
        foreach (var type in new[] { typeof(NRun), typeof(NGlobalUi), typeof(NCapstoneSubmenuStack) })
            harmony.Patch(AccessTools.Method(type, "_Ready"), prefix: new HarmonyMethod(typeof(JigsawNativeMapUiHost), nameof(SkipReady)));
        run.ProcessMode = Node.ProcessModeEnum.Disabled;
        global.ProcessMode = Node.ProcessModeEnum.Always;
        AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, game);
        AccessTools.Property(typeof(NGame), nameof(NGame.RootSceneContainer)).SetValue(game, scenes);
        AccessTools.Property(typeof(NGame), nameof(NGame.InputManager)).SetValue(game, input);
        AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(scenes, run);
        AccessTools.Property(typeof(NRun), nameof(NRun.GlobalUi)).SetValue(run, global);
        AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.SubmenuStack)).SetValue(global, submenu);
        run.AddChild(global);
        global.AddChild(submenu);
        await HeadlessUiHost.Mount(run);
        run.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        global.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bool testing = TestMode.IsOn;
        try { TestMode.IsOn = false; session.ShowMap(); }
        finally { TestMode.IsOn = testing; }
        var screen = global.GetNode<JigsawScreen>("AllMattersJigsaw");
        var profile = new MemoryJigsawTutorialProfile { Sentence = 6 };
        AccessTools.Field(typeof(JigsawScreen), "profile").SetValue(screen, profile);
        screen.Refresh();
        await ((SceneTree)Engine.GetMainLoop()).ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        return screen;
    }
    internal void ReleaseSceneOwnership() => AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, previous);
    public void Dispose()
    {
        AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, previous);
        run.Free(); scenes.Free(); input.Free(); game.Free();
        harmony.UnpatchAll(harmony.Id);
    }
}
