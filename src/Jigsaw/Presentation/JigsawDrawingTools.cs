using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace AllMatters.Jigsaw.Presentation;

internal sealed class JigsawDrawingTools : IDisposable
{
    private static readonly ConditionalWeakTable<NMapScreen, JigsawDrawingTools> maps = new();
    private static readonly ConditionalWeakTable<NMapDrawings, JigsawDrawingTools> layers = new();
    [ThreadStatic] private static int moving;
    private readonly JigsawScreen screen;
    private readonly Control canvas;
    private readonly Control paper;
    private readonly NMapScreen native;
    private readonly NMapDrawings drawings;
    private readonly Control tools;
    private readonly NMapDrawButton drawButton;
    private readonly NMapEraseButton eraseButton;
    private readonly NMapClearButton clearButton;
    private readonly Node drawingsParent;
    private readonly Node toolsParent;
    private readonly Vector2 toolsPosition;
    private readonly Control share;
    private readonly Node? shareParent;
    private readonly List<(Node Node, Node? Owner)> borrowedOwners = [];
    private NMapDrawingInput? input;
    private Rect2 bounds;
    private bool sharing;
    private Vector2 oldPaperSize, oldPaperPosition, oldNativeSize;
    private float oldLeft, oldTop, oldRight, oldBottom;

    internal bool IsDrawing => input != null;
    internal bool IsSharing => sharing;
    internal static bool IsMoving(NMapDrawings layer) => moving > 0 && layers.TryGetValue(layer, out _);
    private static void Move(Node node, Node parent)
    {
        moving++;
        try
        { node.Reparent(parent, false); }
        finally { moving--; }
    }

    internal static JigsawDrawingTools? Attach(JigsawScreen screen, Control canvas, Control paper, NMapScreen native)
    {
        if (maps.TryGetValue(native, out var existing))
            return existing;
        if (native.Drawings == null || native.GetNodeOrNull<Control>("%DrawingTools") is not { } tools)
            return null;
        return new(screen, canvas, paper, native, tools);
    }

    private JigsawDrawingTools(JigsawScreen screen, Control canvas, Control paper, NMapScreen native, Control tools)
    {
        this.screen = screen;
        this.canvas = canvas;
        this.paper = paper;
        this.native = native;
        this.tools = tools;
        drawings = native.Drawings;
        drawButton = native.GetNode<NMapDrawButton>("%DrawButton");
        eraseButton = native.GetNode<NMapEraseButton>("%EraseButton");
        clearButton = native.GetNode<NMapClearButton>("%ClearButton");
        drawingsParent = drawings.GetParent();
        toolsParent = tools.GetParent();
        toolsPosition = tools.Position;
        maps.Add(native, this);
        layers.Add(drawings, this);
        RememberOwners(drawings);
        RememberOwners(tools);
        Move(drawings, canvas);
        drawings.MouseFilter = Control.MouseFilterEnum.Ignore;
        tools.Reparent(screen, false);
#if !STS2_PUBLIC
        var button = native.GetNode<NMapShareButton>("%ShareButton");
        RememberOwners(button);
        share = button;
        shareParent = button.GetParent();
        button.Reparent(screen, false);
        button.Initialize(native, paper, paper);
#else
        var button = new Button { Text = MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language == "zhs" ? "分享地图" : "Share map", CustomMinimumSize = new(130, 48) };
        share = button;
        screen.AddChild(button);
        button.Pressed += () => MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(SharePublic());
#endif
        screen.TreeExiting += Dispose;
        screen.VisibilityChanged += Stop;
        Update(false);
    }

    internal void Update(bool exploring)
    {
        tools.Visible = share.Visible = exploring;
        drawings.Visible = exploring;
        if (!exploring)
        { Stop(); return; }
        PositionToolbar();
        share.Position = new(screen.Size.X - share.Size.X - 24, screen.Size.Y - share.Size.Y - 24);
        var nextBounds = screen.DrawingBounds.Grow(2);
        if (bounds != nextBounds)
        {
            bounds = nextBounds;
            ResizeDrawingCoordinates(bounds.Size * 64);
            ResizeLayers();
        }
        drawings.Scale = Vector2.One * screen.DrawingCell / 64;
        drawings.Position = screen.DrawingOrigin + bounds.Position * screen.DrawingCell;
    }

    private void PositionToolbar()
    {
        // The native toolbar can center its children around a zero-width holder.
        // Include the holders because the native hover tips are anchored to them.
        var bounds = new Rect2(tools.GlobalPosition, Vector2.Zero);
        foreach (var button in new Control[] { drawButton, eraseButton, clearButton })
        {
            bounds = bounds.Merge(button.GetGlobalRect());
            if (button.GetParent() is Control holder)
                bounds = bounds.Expand(holder.GlobalPosition);
        }
        var origin = tools.GlobalPosition - bounds.Position;
        tools.GlobalPosition = screen.GlobalPosition + origin + new Vector2(32, screen.Size.Y - bounds.Size.Y - 20);
    }

    private void ResizeDrawingCoordinates(Vector2 size)
    {
        // Native save/load normalizes against the current layer dimensions.
        // Reproject lines loaded by NGame before this puzzle screen was opened.
        var toNet = AccessTools.Method(typeof(NMapDrawings), "ToNetPosition");
        var fromNet = AccessTools.Method(typeof(NMapDrawings), "FromNetPosition");
        var lines = drawings.GetChildren().OfType<Control>()
            .Select(owner => owner.GetNodeOrNull<SubViewport>("DrawViewport"))
            .Where(viewport => viewport != null)
            .SelectMany(viewport => viewport!.GetChildren().OfType<Line2D>())
            .Select(line => (Line: line, Points: line.Points.Select(point =>
                (Vector2)toNet.Invoke(drawings, [point])!).ToArray())).ToArray();
        drawings.Size = size;
        foreach (var (line, points) in lines)
            line.Points = points.Select(point => (Vector2)fromNet.Invoke(drawings, [point])!).ToArray();
    }

    private void ResizeLayers()
    {
        foreach (Control owner in drawings.GetChildren().OfType<Control>())
        {
            owner.Size = drawings.Size;
            if (owner.GetNodeOrNull<SubViewport>("DrawViewport") is { } viewport)
                viewport.Size = new Vector2I(Math.Max(1, (int)(drawings.Size.X / 2)), Math.Max(1, (int)(drawings.Size.Y / 2)));
            if (owner.GetNodeOrNull<TextureRect>("DrawViewportTextureRect") is { } texture)
            {
                texture.Position = Vector2.Zero;
                texture.Scale = Vector2.One;
                texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                texture.Size = drawings.Size;
                texture.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
        }
    }

    internal static void OnPlayerLayer(NMapDrawings drawings)
    {
        if (layers.TryGetValue(drawings, out var adapter))
            adapter.ResizeLayers();
    }

    internal void Toggle(DrawingMode mode)
    {
        bool wasActive = input?.DrawingMode == mode;
        Stop();
        if (!wasActive)
        {
            input = NMapDrawingInput.Create(drawings, mode);
            input.Finished += () => { input = null; UpdateButtons(); };
            screen.AddChild(input);
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        drawButton.SetIsDrawing(input?.DrawingMode == DrawingMode.Drawing);
        eraseButton.SetIsErasing(input?.DrawingMode == DrawingMode.Erasing);
    }

    internal void Stop()
    {
        if (input != null && GodotObject.IsInstanceValid(input))
            input.StopDrawing();
        input = null;
    }

    internal void Clear()
    {
        Stop();
        drawings.ClearDrawnLinesLocal();
        SfxCmd.Play("event:/sfx/ui/map/map_erase");
    }

    internal static JigsawDrawingTools? For(NMapScreen map) => maps.TryGetValue(map, out var adapter) ? adapter : null;

    private void RememberOwners(Node node)
    {
        borrowedOwners.Add((node, node.Owner));
        foreach (var child in node.GetChildren())
            RememberOwners(child);
    }

    internal void BeginShare()
    {
        Stop();
        sharing = true;
        oldPaperSize = paper.Size;
        oldPaperPosition = paper.Position;
        oldNativeSize = native.Size;
        oldLeft = canvas.AnchorLeft;
        oldTop = canvas.AnchorTop;
        oldRight = canvas.AnchorRight;
        oldBottom = canvas.AnchorBottom;
        screen.BeginDrawingCapture();
        var size = screen.DrawingBounds.Grow(2).Size * 48;
        size = new Vector2(Math.Max(1280, size.X), Math.Max(720, size.Y));
        paper.Position = Vector2.Zero;
        paper.Size = size;
        native.Size = size;
        canvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Update(true);
        paper.QueueRedraw();
        canvas.QueueRedraw();
    }

    internal void EndShare()
    {
        if (!sharing)
            return;
        sharing = false;
        screen.EndDrawingCapture();
        if (GodotObject.IsInstanceValid(paper))
        {
            paper.Position = oldPaperPosition;
            paper.Size = oldPaperSize;
            native.Size = oldNativeSize;
            canvas.AnchorLeft = oldLeft;
            canvas.AnchorTop = oldTop;
            canvas.AnchorRight = oldRight;
            canvas.AnchorBottom = oldBottom;
            canvas.OffsetLeft = canvas.OffsetTop = canvas.OffsetRight = canvas.OffsetBottom = 0;
            Update(true);
        }
    }

#if STS2_PUBLIC
    private async Task SharePublic()
    {
        if (sharing) return;
        using var image = await CapturePanorama();
        string path = "user://map_screenshot_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        if (image.SavePng(path) != Error.Ok)
            throw new IOException("无法保存地图图片。");
        screen.ShowNotice(ProjectSettings.GlobalizePath(path));
    }

    internal async Task<Image> CapturePanorama()
    {
        var parent = paper.GetParent(); int index = paper.GetIndex();
        var viewport = new SubViewport { RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
        BeginShare();
        viewport.Size = new((int)paper.Size.X, (int)paper.Size.Y);
        try
        {
            screen.AddChild(viewport);
            Move(paper, viewport);
            await screen.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            return viewport.GetTexture().GetImage();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(paper) && GodotObject.IsInstanceValid(parent))
            { Move(paper, parent); parent.MoveChild(paper, index); }
            EndShare();
            if (GodotObject.IsInstanceValid(viewport)) viewport.QueueFree();
        }
    }
#endif

    public void Dispose()
    {
        Stop();
        screen.TreeExiting -= Dispose;
        screen.VisibilityChanged -= Stop;
        if (GodotObject.IsInstanceValid(drawings) && GodotObject.IsInstanceValid(drawingsParent))
            Move(drawings, drawingsParent);
        if (GodotObject.IsInstanceValid(tools) && GodotObject.IsInstanceValid(toolsParent))
        { tools.Reparent(toolsParent, false); tools.Position = toolsPosition; }
        if (GodotObject.IsInstanceValid(share))
        {
            if (GodotObject.IsInstanceValid(shareParent))
                share.Reparent(shareParent, false);
            else
                share.QueueFree();
        }
        foreach (var (node, owner) in borrowedOwners)
            if (GodotObject.IsInstanceValid(node) && GodotObject.IsInstanceValid(owner) && owner!.IsAncestorOf(node))
                node.Owner = owner;
        layers.Remove(drawings);
        maps.Remove(native);
    }
}

[HarmonyPatch]
internal static class JigsawNativeDrawingButtons
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        new[] { "OnMapDrawingButtonPressed", "OnMapErasingButtonPressed", "OnClearMapDrawingButtonPressed" }
            .Select(name => (MethodBase)AccessTools.Method(typeof(NMapScreen), name));
    private static bool Prefix(NMapScreen __instance, MethodBase __originalMethod)
    {
        if (JigsawDrawingTools.For(__instance) is not { } adapter)
            return true;
        if (__originalMethod.Name == "OnClearMapDrawingButtonPressed")
            adapter.Clear();
        else
            adapter.Toggle(__originalMethod.Name == "OnMapDrawingButtonPressed" ? DrawingMode.Drawing : DrawingMode.Erasing);
        return false;
    }
}

[HarmonyPatch(typeof(NMapDrawings), "GetDrawingStateForPlayer")]
internal static class JigsawResizeNativeDrawing
{
    private static void Postfix(NMapDrawings __instance) => JigsawDrawingTools.OnPlayerLayer(__instance);
}

#if STS2_PUBLIC
[HarmonyPatch(typeof(NMapDrawings), "_ExitTree")]
internal static class JigsawKeepBorrowedDrawingSubscription
{
    private static bool Prefix(NMapDrawings __instance) => !JigsawDrawingTools.IsMoving(__instance);
}
#else
[HarmonyPatch(typeof(NMapShareButton), "CopyMapScreenshot")]
internal static class JigsawNativeMapShare
{
    private static void Prefix(NMapScreen ____mapScreen, out JigsawDrawingTools? __state)
    {
        __state = JigsawDrawingTools.For(____mapScreen);
        __state?.BeginShare();
    }
    private static void Postfix(ref Task __result, JigsawDrawingTools? __state)
    {
        if (__state != null)
            __result = Finish(__result, __state);
    }
    private static async Task Finish(Task original, JigsawDrawingTools adapter)
    {
        try
        { await original; }
        finally { adapter.EndShare(); }
    }
}
#endif
