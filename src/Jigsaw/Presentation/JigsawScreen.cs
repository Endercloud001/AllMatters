using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
namespace AllMatters.Jigsaw.Presentation;

/// <summary>A local input/read surface. All accepted map decisions belong to JigsawRun.</summary>
public partial class JigsawScreen : Control
{
    public static readonly string[] Opening = ["先古之民们决定赐予你更多的帮助。", "你有权利决定自己的来路...以及自己的去向。", "从左侧可用的碎块中构建你的心愿，交付给我们。", "你必须从先古之民出发，在一定步数内到达本幕的Boss。", "若你在同一幕内停留得太久，建筑师的诅咒将降临于你。", "当然，若你自身举棋不定，也可交由我们决定。"];
    private JigsawRun map = null!;
    private ulong player;
    private Action changed = null!, pause = null!;
    private Func<Task> inspected = null!, continued = null!;
    private Func<int, Task> enter = null!;
    private Func<Task>? automaticRequested;
    private Func<Task>? abandonRequested;
    internal Func<JigsawResult>? CancelRequested { get; set; }
    internal Func<int, JigsawResult>? ReturnRequested { get; set; }
    internal Func<int, JigsawResult>? TakeRequested { get; set; }
    internal Func<JigsawResult>? RotateRequested { get; set; }
    internal Func<GridPoint, JigsawResult>? DropRequested { get; set; }
    internal Func<int?, JigsawResult>? EndpointRequested { get; set; }
    internal Func<EndChoice, JigsawResult>? ChoiceRequested { get; set; }
    internal Func<int, JigsawResult>? VoteRequested { get; set; }
    private Func<ulong, Texture2D?>? avatar;
    private Func<ulong, Color>? playerColor;
    private readonly HBoxContainer manualVoters = new(), autoVoters = new();
    private int helpIndex;
    private readonly Theme adaptiveTheme = new();
    private JigsawNativeArt art = null!;
    private readonly Dictionary<int, Node2D> bossVisuals = [];
    private readonly Dictionary<int, JigsawRoomPulse> roomVisuals = [];
    private IJigsawTutorialProfile profile = null!;
    private readonly Label dialog = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Label steps = new();
    private readonly Label poolTitle = new() { Text = "可用碎块", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Label explorationNotice = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly ColorRect helpShade = new() { Name = "JigsawHelpShade", Color = new Color(0, 0, 0, .65f), ZIndex = 4, MouseFilter = MouseFilterEnum.Stop };
    private readonly VBoxContainer cards = new();
    private readonly PanelContainer poolFrame = new();
    private readonly ScrollContainer pool = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly ScrollContainer dialogScroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly HBoxContainer root = new();
    private readonly VBoxContainer main = new(), footer = new();
    private readonly HBoxContainer decisions = new();
    private readonly HBoxContainer bottom = new(), top = new();
    private Action? returnToRoom;
    private MegaCrit.Sts2.Core.Models.ActModel? nativeAct;
    private JigsawPaper paper = null!;
    public bool CanReturnToRoom => returnToRoom != null;
    public void SetRoomPreview(Action? close) { returnToRoom = close; Refresh(); }
    private readonly Button next = new(), manual = new(), automatic = new(), resume = new(), abandon = new(), help = new();
    private readonly PanelContainer helpPanel = new();
    private readonly VBoxContainer helpContent = new();
    private readonly Label helpText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly List<Button> poolButtons = [], actionButtons = [];
    private readonly List<(TextureRect Icon, string Action)> inputIcons = [];
    private readonly HBoxContainer hints = new();
    private readonly Label mouseHints = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private JigsawCanvas canvas = null!;
    private JigsawOverlay overlay = null!;
    private int? replay;
    private bool helpOpen, tutorialPaused;
    private JigsawPhase previousPhase;
    private enum FocusRegion { Pool, Map, Actions }
    private FocusRegion zone;
    private int poolIndex, nodeIndex, actionIndex;
    private GridPoint cursor = new(4, 0);
    private Vector2 pan;
    private float zoom = 1;
    private bool dragging;
    private double errorRemaining;
    private string recent = "", secondError = "";
    private ColorRect? generationCover;
    public bool IsGenerating => generationCover is { Visible: true };
    public void SetGenerating(bool active)
    {
        if (generationCover == null)
        {
            generationCover = new ColorRect { Color = Colors.Black, ZIndex = 20, MouseFilter = MouseFilterEnum.Stop };
            AddChild(generationCover);
            generationCover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var caption = new Label { Text = L("先古之民的意志正在影响高塔"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
            generationCover.AddChild(caption);
            caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        generationCover.Visible = active;
        Refresh();
    }
    public void ShowAutomaticFailure(string reason)
    {
        ShowNotice("已保留原布局，可再次手动调整拼图。");
        var popup = NErrorPopup.Create(L("自动拼图失败"), L(reason), false);
        if (popup != null)
        { popup.TreeExiting += RestoreFocus; NModalContainer.Instance?.Add(popup); }
    }

    private readonly HashSet<string> tips = [];
    public string DialogText => dialog.Text;
    public bool Reading => (!tutorialPaused && profile.Sentence < Opening.Length) || replay != null || helpOpen;
    public IReadOnlyList<int> NavigableNodeIds => map.Nodes.OrderBy(n => n.Center.Y).ThenBy(n => n.Center.X).ThenBy(n => n.Id).Select(n => n.Id).ToArray();
    public int? FocusedNodeId => NavigableNodeIds.Count == 0 ? null : NavigableNodeIds[Math.Clamp(nodeIndex, 0, NavigableNodeIds.Count - 1)];
    public Control MapControl => canvas;
    public Control PoolControl => pool;
    public Control DialogControl => dialogScroll;
    public Control DecisionControl => decisions;
    internal Control ControllerHints => hints;
    internal Control MouseHints => mouseHints;
    public static JigsawScreen Create(JigsawRun map, ulong player, Action changed, Func<Task> inspected, Func<Task> continued, Func<int, Task> enter, Action pause, IJigsawTutorialProfile? profile = null, Func<Task>? automaticRequested = null, Func<ulong, Texture2D?>? avatar = null, Func<Task>? abandonRequested = null, MegaCrit.Sts2.Core.Models.ActModel? nativeAct = null, Func<ulong, Color>? playerColor = null)
    {
        var view = new JigsawScreen { map = map, player = player, changed = changed, inspected = inspected, continued = continued, enter = enter, pause = pause, profile = profile ?? new JigsawTutorialProfile(), previousPhase = map.Phase, automaticRequested = automaticRequested, avatar = avatar, abandonRequested = abandonRequested };
        view.nativeAct = nativeAct;
        view.playerColor = playerColor;
        view.art = new JigsawNativeArt(nativeAct);
        view.Build();
        view.Refresh();
        return view;
    }
    private static string L(string text) => TranslationServer.Translate(text);
    private Button MakeButton(string text, Action pressed)
    {
        var b = new Button { Text = L(text), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        b.Pressed += pressed;
        return b;
    }
    private void Build()
    {
        adaptiveTheme.DefaultFont = ResourceLoader.Load<Font>("res://fonts/zhs/SourceHanSerifSC-Medium.otf");
        adaptiveTheme.SetFont("font", "Button", ResourceLoader.Load<Font>("res://fonts/zhs/SourceHanSerifSC-Bold.otf"));
        ProcessMode = ProcessModeEnum.Always;
        Theme = adaptiveTheme;
        Name = "AllMattersJigsaw";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var buttonStyle = JigsawNativeArt.Panel("packed/common_ui/event_button.png", 20);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            adaptiveTheme.SetStylebox(state, "Button", buttonStyle);
        adaptiveTheme.SetStylebox("focus", "Button", JigsawNativeArt.Panel("packed/common_ui/event_button_outline.png", 20));
        var pressedStyle = (StyleBoxTexture)buttonStyle.Duplicate();
        pressedStyle.ModulateColor = new Color(.8f, .8f, .8f);
        adaptiveTheme.SetStylebox("pressed", "Button", pressedStyle);
        var disabledStyle = (StyleBoxTexture)buttonStyle.Duplicate();
        disabledStyle.ModulateColor = new Color(.65f, .65f, .65f);
        adaptiveTheme.SetStylebox("disabled", "Button", disabledStyle);
        adaptiveTheme.SetColor("font_color", "Button", new Color("f4dfad"));
        adaptiveTheme.SetColor("font_hover_color", "Button", Colors.White);
        adaptiveTheme.SetColor("font_disabled_color", "Button", new Color("8f897c"));
        poolFrame.AddThemeStyleboxOverride("panel", JigsawNativeArt.Panel("ui/reward_screen/reward_panel.png", 30));
        dialog.AddThemeColorOverride("font_color", new Color("f4eee2"));
        var background = new ColorRect { Color = new Color("17202d"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 16);
        root.AddChild(poolFrame);
        var poolColumn = new VBoxContainer();
        poolFrame.AddChild(poolColumn);
        var banner = new TextureRect { Texture = JigsawNativeArt.Load("ui/reward_screen/reward_banner.png"), CustomMinimumSize = new(0, 55), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        poolColumn.AddChild(banner);
        poolTitle.Text = L("可用碎块");
        banner.AddChild(poolTitle);
        poolTitle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        poolColumn.AddChild(pool);
        pool.SizeFlagsVertical = SizeFlags.ExpandFill;
        pool.AddChild(cards);
        cards.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(main);
        main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.SizeFlagsVertical = SizeFlags.ExpandFill;
        main.AddChild(top);
        top.Alignment = BoxContainer.AlignmentMode.End;
        top.AddChild(steps);
        steps.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.AddChild(explorationNotice);
        help.Text = L("帮助");
        help.Pressed += OpenHelp;
        top.AddChild(help);
        paper = new JigsawPaper { Art = art, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        main.AddChild(paper);
        canvas = new JigsawCanvas { OwnerScreen = this, FocusMode = FocusModeEnum.All, ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        paper.AddChild(canvas);
        canvas.AnchorLeft = .12f;
        canvas.AnchorTop = .18f;
        canvas.AnchorRight = .86f;
        canvas.AnchorBottom = .85f;
        overlay = new JigsawOverlay { OwnerScreen = this, ZIndex = 1, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        main.AddChild(bottom);
        bottom.AddChild(footer);
        footer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var dialogueRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        footer.AddChild(dialogueRow);
        dialogueRow.AddThemeConstantOverride("separation", -12);
        var tail = new TextureRect { Texture = JigsawNativeArt.Load("ui/dialogue_tail.png"), Modulate = new Color("28454f"), CustomMinimumSize = new(28, 0), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        dialogueRow.AddChild(tail);
        var tailShadow = new TextureRect { Texture = tail.Texture, Modulate = new Color(0, 0, 0, .25f), ShowBehindParent = true, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        tail.AddChild(tailShadow);
        tailShadow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tailShadow.OffsetLeft = tailShadow.OffsetRight = 2;
        tailShadow.OffsetTop = tailShadow.OffsetBottom = 5;
        var bubble = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        dialogueRow.AddChild(bubble);
        NinePatchRect Bubble(Color color) => new() { Texture = JigsawNativeArt.Load("ui/dialogue_nine_patch.png"), RegionRect = new Rect2(0, 0, 116, 85), PatchMarginLeft = 27, PatchMarginRight = 27, PatchMarginTop = 28, PatchMarginBottom = 28, AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile, AxisStretchVertical = NinePatchRect.AxisStretchMode.Tile, Modulate = color, MouseFilter = MouseFilterEnum.Ignore };
        var shadow = Bubble(new Color(0, 0, 0, .25f));
        bubble.AddChild(shadow);
        shadow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shadow.OffsetLeft = shadow.OffsetRight = 2;
        shadow.OffsetTop = shadow.OffsetBottom = 5;
        var frame = Bubble(new Color("28454f"));
        bubble.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bubble.AddChild(dialogScroll);
        dialogScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialogScroll.OffsetLeft = 20;
        dialogScroll.OffsetRight = -18;
        dialogScroll.OffsetTop = 10;
        dialogScroll.OffsetBottom = -12;
        dialogScroll.AddChild(dialog);

        next.Text = L("继续");
        next.Pressed += AdvanceDialog;
        footer.AddChild(next);
        bottom.AddChild(decisions);
        var confirmTexture = JigsawNativeArt.Load("atlases/ui_atlas.sprites/confirm_button.tres");
        var sourceAtlas = (AtlasTexture)confirmTexture;
        var confirmHalf = new AtlasTexture { Atlas = sourceAtlas.Atlas, Region = new Rect2(sourceAtlas.Region.Position, new Vector2(150, sourceAtlas.Region.Size.Y)) };
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            automatic.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 12, ContentMarginBottom = 12 });
        var confirmArt = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        automatic.AddChild(confirmArt);
        confirmArt.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        confirmArt.AddThemeConstantOverride("separation", 0);
        foreach (bool mirrored in new[] { false, true })
            confirmArt.AddChild(new TextureRect { Texture = confirmHalf, FlipH = mirrored, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        manual.Text = L("由先古之民审视");
        manual.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        manual.Pressed += () => Choose(EndChoice.Manual);
        automatic.Text = L("令先古之民决定");
        automatic.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        automatic.Pressed += () => Choose(EndChoice.Automatic);
        manual.MouseEntered += ExplainEnding;
        automatic.MouseEntered += ExplainEnding;
        manual.FocusEntered += ExplainEnding;
        automatic.FocusEntered += ExplainEnding;
        resume.Text = L("继续前行");
        resume.Pressed += () => RunAsync(map.PendingEntry is { } pending ? enter(pending.NodeId) : continued());
        decisions.AddChild(manualVoters);
        decisions.AddChild(manual);
        decisions.AddChild(autoVoters);
        decisions.AddChild(automatic);
        decisions.AddChild(resume);
        abandon.Text = L("放弃进入");
        abandon.Pressed += () => { if (abandonRequested != null) RunAsync(abandonRequested()); };
        decisions.AddChild(abandon);
        actionButtons.AddRange([manual, automatic, resume, abandon, help]);
        var returnButton = MakeButton("归还碎块", ReturnHeld);
        cards.AddChild(returnButton);
        footer.AddChild(hints);
        footer.AddChild(mouseHints);
        foreach (var pair in new[] { (MegaInput.select, "拿放"), (MegaInput.cancel, "返回"), (MegaInput.viewDiscardPile, "旋转／连拆线"), (MegaInput.viewDrawPile, "归还"), (MegaInput.viewDeckAndTabLeft, "切换区域"), (MegaInput.pauseAndBack, "帮助") })
        {
            var icon = new TextureRect { CustomMinimumSize = new(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            hints.AddChild(icon);
            inputIcons.Add((icon, pair.Item1));
            hints.AddChild(new Label { Text = L(pair.Item2) });
        }
        AddChild(helpShade);
        helpShade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        helpShade.Hide();
        AddChild(helpPanel);
        helpPanel.AddThemeStyleboxOverride("panel", JigsawNativeArt.Panel("atlases/ui_atlas.sprites/popup_vertical.tres", 40));
        var popupMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader") };
        popupMaterial.SetShaderParameter("h", .505f);
        popupMaterial.SetShaderParameter("s", 1f);
        popupMaterial.SetShaderParameter("v", .75f);
        helpPanel.Material = popupMaterial;
        helpPanel.AddChild(helpContent);
        helpPanel.Visible = false;
        helpPanel.ZIndex = 5;
        var helpTitle = new Label { Text = L("帮助"), HorizontalAlignment = HorizontalAlignment.Center };
        helpTitle.AddThemeColorOverride("font_color", new Color("efc851"));
        helpContent.AddChild(helpTitle);
        helpContent.AddChild(MakeButton("操作与规则", () => helpText.Text = L(Rules)));
        var helpScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        helpContent.AddChild(helpScroll);
        helpScroll.AddChild(helpText);
        helpText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        helpContent.AddChild(MakeButton("返回", Back));
        Resized += AdaptLayout;
    }
    private const string Rules = "鼠标左键拿取、放置碎块；\n\n持有碎块时右键旋转碎块；\n\n持有碎块时左键点击\"归还碎块\"可放回；\n\n空手连续右键两个碎块连线，重复操作拆除连线；\n\n按住中键可拖动地图，滚轮缩放；\n\n目的是存在至少一条规定步数内从先古之民出发到达Boss的路线，当然玩家也可以选择随机生成。";
    public void SetMap(JigsawRun value, MegaCrit.Sts2.Core.Models.ActModel? currentAct = null)
    {
        if (currentAct != null && !ReferenceEquals(nativeAct, currentAct))
        {
            nativeAct = currentAct;
            art = new JigsawNativeArt(currentAct);
            paper.Art = art;
            paper.QueueRedraw();
            foreach (var visual in bossVisuals.Values)
            { visual.GetParent()?.RemoveChild(visual); visual.QueueFree(); }
            bossVisuals.Clear();
        }
        if (!ReferenceEquals(map, value))
        { ClearRecent(); tips.Clear(); zoom = 1; pan = Vector2.Zero; }
        map = value;
        Refresh();
    }
    public void ClearRecent() { recent = ""; secondError = ""; errorRemaining = 0; }
    public void Refresh()
    {
        foreach (var id in bossVisuals.Keys.Where(id => !map.Nodes.Any(n => n.Id == id)).ToArray())
        { bossVisuals[id].QueueFree(); bossVisuals.Remove(id); }
        poolFrame.Visible = pool.Visible = map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating;
        help.Visible = pool.Visible;
        hints.Visible = false;
        mouseHints.Visible = false;
        mouseHints.Text = L(pool.Visible ? "左键 拿放 · 右键 旋转／连拆线 · 中键 平移 · 滚轮 缩放 · Esc 取消／暂停" : "左键 选择目的地 · 中键 平移 · 滚轮 缩放 · Esc 暂停");
        if (previousPhase != map.Phase)
        { if (map.Phase != JigsawPhase.Editing) ClearRecent(); replay = null; helpOpen = false; helpShade.Hide(); helpPanel.Hide(); previousPhase = map.Phase; zone = FocusRegion.Map; nodeIndex = 0; }
        foreach (var b in poolButtons)
        { cards.RemoveChild(b); b.QueueFree(); }
        poolButtons.Clear();
        foreach (var p in map.Pool.Where(p => !map.Placements.Any(x => x.PieceId == p.Id)))
        {
            var holders = map.Holdings.Where(h => h.Value.PieceId == p.Id).Select(h => h.Key).ToArray();
            var b = MakeButton("", () => Take(p.Id));
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
                b.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            var cardFrame = new JigsawCardFrame { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
            b.AddChild(cardFrame);
            cardFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            b.CustomMinimumSize = new(0, 100 * Math.Max(1, Size.Y / 720));
            var preview = new JigsawPiecePreview { Art = art, Piece = p, Position = new(12, 8), Size = new(100, 80), MouseFilter = MouseFilterEnum.Ignore };
            b.AddChild(preview);
            b.Resized += () => preview.Size = new Vector2(Math.Max(100, b.Size.X - 24), Math.Max(80, b.Size.Y - 16));
            foreach (var holder in holders)
            { var portrait = Portrait(holder); portrait.Position = new(135, 16); b.AddChild(portrait); }
            cards.AddChild(b);
            cards.MoveChild(b, poolButtons.Count + 1);
            poolButtons.Add(b);
            if (holders.Length > 0)
                b.Modulate = new Color(1, 1, 1, .55f);
        }
        poolIndex = Math.Clamp(poolIndex, 0, Math.Max(0, poolButtons.Count - 1));
        nodeIndex = Math.Clamp(nodeIndex, 0, Math.Max(0, NavigableNodeIds.Count - 1));
        steps.Text = L("剩余步数") + "  " + map.RemainingSteps + " / " + map.Budget;
        steps.Visible = false;
        bottom.Visible = !CanReturnToRoom && (map.Phase != JigsawPhase.Exploring || map.PendingEntry != null);
        manual.Visible = automatic.Visible = map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating;
        manual.Disabled = automatic.Disabled = Reading || map.Phase != JigsawPhase.Editing;
        manual.Text = L("由先古之民审视");
        automatic.Text = L("令先古之民决定");
        UpdateVoters(manualVoters, EndChoice.Manual);
        UpdateVoters(autoVoters, EndChoice.Automatic);
        resume.Visible = map.Phase == JigsawPhase.Ancient || map.PendingEntry != null;
        resume.Disabled = Reading;
        resume.Text = L(map.PendingEntry != null ? "重试进入" : "继续前行");
        abandon.Visible = map.PendingEntry != null && abandonRequested != null;
        abandon.Disabled = Reading;
        next.Visible = (!tutorialPaused && profile.Sentence < Opening.Length) || replay != null;
        if (next.Visible)
            dialog.Text = L(Opening[replay ?? Math.Clamp(profile.Sentence, 0, 5)]);
        else if (errorRemaining <= 0)
            dialog.Text = CurrentInstruction();
        AdaptLayout();
        canvas.QueueRedraw();
        RestoreFocus();
    }
    private TextureRect Portrait(ulong id) => new() { Texture = avatar?.Invoke(id), CustomMinimumSize = new(32, 32), Size = new(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
    private void UpdateVoters(HBoxContainer row, EndChoice choice)
    {
        foreach (var child in row.GetChildren())
        { row.RemoveChild(child); child.QueueFree(); }
        foreach (var voter in map.Choices.Where(x => x.Value == choice))
            row.AddChild(Portrait(voter.Key));
        row.Visible = map.Choices.Any(x => x.Value == choice);
    }
    private string CurrentInstruction()
    {
        if (map.Phase == JigsawPhase.Generating)
            return L("先古之民的意志正在影响高塔");
        if (map.Phase == JigsawPhase.Ancient)
            return L("你的愿望实现了...");
        if (map.Phase == JigsawPhase.Exploring)
            return L(map.RemainingSteps == 1 ? "最后一步，请尽快前往Boss处。" : $"还可前行{map.RemainingSteps}步。");
        return L(map.Holdings.ContainsKey(player) ? "放置碎块，旋转，返回取消，或归还碎块。" : "从左侧拿取碎块，或选择两端连线／拆线。");
    }
    public void AdvanceDialog()
    {
        if (replay is int r)
        { replay = r < 5 ? r + 1 : null; }
        else if (profile.Sentence < 6)
            profile.Sentence++;
        Refresh();
    }
    public void OpenHelp()
    {
        if (!help.Visible)
            return;
        var cancelled = CancelRequested?.Invoke() ?? map.Cancel(player);
        dragging = false;
        changed();
        helpOpen = true;
        helpShade.Show();
        helpPanel.Show();
        helpText.Text = L(Rules);
        Refresh();
        helpPanel.Show();
        helpIndex = 0;
        if (IsInsideTree())
            helpContent.GetChildren().OfType<Button>().First().GrabFocus();
    }
    public void Back()
    {
        if (helpOpen)
        { helpOpen = false; helpShade.Hide(); helpPanel.Hide(); Refresh(); return; }
        if (replay != null)
        { replay = null; Refresh(); return; }
        if (map.Holdings.ContainsKey(player) || map.SelectedEndpoints.ContainsKey(player))
        { Apply(CancelRequested?.Invoke() ?? map.Cancel(player)); return; }
        if (returnToRoom != null)
        { returnToRoom(); return; }
        tutorialPaused = true;
        SetProcessInput(false);
        pause();
    }
    public void ResumeFromPause() { tutorialPaused = false; SetProcessInput(true); Refresh(); }
    private void Apply(JigsawResult result, bool silent = false)
    {
        if (!result.Success && !silent)
            ShowResult(result);
        changed();
        Refresh();
    }
    private void Take(int piece)
    {
        if (Reading)
            return;
        var result = TakeRequested?.Invoke(piece) ?? map.Take(player, piece);
        if (!result.Success || TakeRequested != null)
            return;
        Apply(result, true);
        Tip("holding", "放置碎块，旋转，返回取消，或归还碎块。");
    }
    internal void EditorResult(JigsawEditorOperation operation, JigsawResult result)
    {
        if (operation == JigsawEditorOperation.Take)
        { if (result.Success) Tip("holding", "放置碎块，旋转，返回取消，或归还碎块。"); }
        else if (!result.Success)
            ShowResult(result);
        Refresh();
    }
    private void Tip(string id, string text) { if (errorRemaining <= 0 && tips.Add(id)) ShowNotice(text); }
    private void ExplainEnding()
    {
        if (map.Phase == JigsawPhase.Editing && !Reading)
            Tip("end", "手动审视会检查你的拼图；自动决定将重新安排布局。两种结束按钮须全员同选，选择后本人禁编辑；激活同一按钮撤票，另一按钮改选。");
    }
    private void ReturnHeld() { if (!Reading && map.Holdings.TryGetValue(player, out var h)) Apply(ReturnRequested?.Invoke(h.PieceId) ?? map.ReturnToPool(player, h.PieceId)); }
    private void Choose(EndChoice choice)
    {
        if (Reading)
            return;
        if (choice == EndChoice.Automatic && automaticRequested != null)
        { RunAsync(automaticRequested()); return; }
        if (ChoiceRequested != null)
        { Apply(ChoiceRequested(choice)); return; }
        var result = map.ChooseEnd(player, choice);
        Apply(result);
        if (result.Success && map.Phase == JigsawPhase.Inspecting)
            RunAsync(inspected());
        if (result.Success && map.Phase == JigsawPhase.Generating)
            ShowNotice("将重塑尖塔的一部分");
    }
    private async void RunAsync(Task task)
    {
        try
        { await task; if (GodotObject.IsInstanceValid(this)) Refresh(); }
        catch (Exception e)
        {
            if (!GodotObject.IsInstanceValid(this))
                return;
            if (map.Phase == JigsawPhase.Editing && map.AutomaticOperation != null)
                ShowNotice("已保留原布局，可再次手动调整拼图。");
            else if (map.Phase == JigsawPhase.Ancient && map.AutomaticOperation != null)
                ShowNotice("布局已保留，保存未完成。请再次继续以重试。");
            else if (map.PendingEntry != null)
                ShowNotice("进入未完成，目的地已保留。可重试进入，或放弃进入后重新选择。");
            else
                ShowResult(JigsawResult.Reject(e.Message));
        }
    }
    public void ShowNotice(string text) { recent = L(text); dialog.Text = recent; errorRemaining = 5; secondError = ""; }
    public void ShowResult(JigsawResult result, bool manual = false)
    {
        if (result.Success)
        { ShowNotice("你的愿望实现了..."); return; }
        string reason = Reason(result.Failure);
        secondError = manual ? L("（提示：" + reason + "）") : "";
        dialog.Text = L(manual ? "你的愿景触犯了尖塔的规则，我不能实现……" : "你的愿景触犯了尖塔的规则...") + (!manual ? L("（提示：" + reason + "）") : "");
        recent = dialog.Text + (manual ? "\n" + secondError : "");
        errorRemaining = 5;
    }
    private static string Reason(JigsawFailure reason) => reason switch
    {
        JigsawFailure.InsufficientClearance => "碎块之间的距离不足",
        JigsawFailure.BossUnreachable => "从先古之民不可到达Boss",
        JigsawFailure.BossOverBudget => "到达Boss的最短路线所需步数超出本幕限额",
        JigsawFailure.MissingEndpoint => "连接端点已不存在",
        JigsawFailure.MisalignedPath => "通路必须沿网格横向或纵向对齐",
        JigsawFailure.InvalidInternalPath => "内部通路必须属于同一碎块",
        JigsawFailure.InvalidExternalEndpoint => "跨碎块通路不能连接同一碎块或Boss",
        JigsawFailure.InvalidExternalGap => "节点实际占地之间须恰好留一个空格",
        JigsawFailure.OccupiedPort => "连接位置占用",
        JigsawFailure.IntersectingPaths => "通路交叉或重合",
        JigsawFailure.PathThroughNode => "通路穿过节点",
        JigsawFailure.TooManyPairPaths => "同对碎块最多两条通路",
        JigsawFailure.ReservedPath => "相关碎块正被持有",
        JigsawFailure.InternalPathFixed => "内部通路不可编辑",
        JigsawFailure.EditIncomplete => "请先完成当前拿放或连线",
        JigsawFailure.DisconnectedLayout => "所有提交节点必须连接先古之民",
        JigsawFailure.BossNotTerminal => "Boss必须位于固定内部通路终端",
        JigsawFailure.BossBlocksOrdinaryRooms => "普通房间的路线不能穿过Boss",
        _ => "此操作不符合节点、通路或当前阶段的规则"
    };
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || tutorialPaused)
            return;
        AdvanceTime(delta);
        explorationNotice.Visible = map.Phase == JigsawPhase.Exploring;
        explorationNotice.Text = errorRemaining > 0 ? dialog.Text : "";
        hints.Visible = false;
        mouseHints.Visible = false;
        foreach (var (icon, action) in inputIcons)
        {
#if STS2_PUBLIC
   icon.Texture=NControllerManager.Instance?.GetHotkeyIcon(action);
#else
            icon.Texture = NInputManager.Instance?.GetHotkeyIcon(action);
#endif
        }
        canvas.QueueRedraw();
        overlay.QueueRedraw();
    }
    public void AdvanceTime(double seconds)
    {
        if (errorRemaining <= 0)
            return;
        errorRemaining -= seconds;
        if (Math.Abs(errorRemaining) < .000001)
            errorRemaining = 0;
        if (errorRemaining <= 0 && secondError.Length > 0)
        { dialog.Text = secondError; secondError = ""; errorRemaining += 5; }
        if (errorRemaining <= 0 && !Reading)
            dialog.Text = CurrentInstruction();
    }
    private void AdaptLayout()
    {
        if (Size.X <= 0)
            return;
        float scale = Math.Max(.75f, Size.Y / 1080f);
        poolTitle.OffsetTop = poolTitle.OffsetBottom = -6 * scale;
        adaptiveTheme.DefaultFontSize = (int)(22 * scale);
        root.OffsetTop = Size.Y * .12f;
        root.OffsetLeft = 20 * scale;
        root.OffsetRight = -20 * scale;
        root.OffsetBottom = -20 * scale;
        float poolWidth = Size.Y <= 800 ? 240 : 300 * scale, buttonWidth = Size.Y <= 800 ? 225 : 300 * scale;
        poolFrame.CustomMinimumSize = new(poolWidth, 0);
        foreach (var card in poolButtons)
            card.CustomMinimumSize = new(0, Math.Max(105, 126 * scale));
        decisions.CustomMinimumSize = new(buttonWidth * 2 + 12 * scale, 0);
        decisions.AddThemeConstantOverride("separation", (int)(12 * scale));
        foreach (var button in new[] { manual, automatic, resume, abandon })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            button.CustomMinimumSize = new(buttonWidth, 88 * scale);
        }
        bottom.CustomMinimumSize = new(0, Size.Y <= 800 ? 112 : 140 * scale);
        bottom.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        dialog.CustomMinimumSize = Vector2.Zero;
        help.CustomMinimumSize = new(80 * scale, 48 * scale);
        help.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        helpPanel.Size = new Vector2(Math.Min(Size.X - 40, 522 * scale), Math.Min(Size.Y * .8f, 600 * scale));
        helpPanel.Position = (Size - helpPanel.Size) / 2;
        helpText.CustomMinimumSize = new(Math.Max(100, helpPanel.Size.X - 100), 0);

    }
    private void RestoreFocus()
    {
        if (!IsInsideTree() || helpOpen)
            return;
        if (next.Visible)
        { next.GrabFocus(); return; }
        if (zone == FocusRegion.Pool && poolButtons.Count > 0)
        { poolButtons[poolIndex].GrabFocus(); pool.EnsureControlVisible(poolButtons[poolIndex]); }
        if (zone == FocusRegion.Map)
            canvas.GrabFocus();
        if (zone == FocusRegion.Actions)
        { var buttons = actionButtons.Where(b => b.Visible).ToArray(); if (buttons.Length > 0) buttons[actionIndex % buttons.Length].GrabFocus(); }
    }
    public void Navigate(int direction)
    {
        if (Reading)
            return;
        if (zone == FocusRegion.Pool)
            poolIndex = Wrap(poolIndex + direction, poolButtons.Count);
        else if (zone == FocusRegion.Map)
        {
            if (map.Holdings.ContainsKey(player))
                cursor += new GridPoint(direction, 0);
            else
            { nodeIndex = Wrap(nodeIndex + direction, NavigableNodeIds.Count); FollowFocused(); }
        }
        else
        { actionIndex = Wrap(actionIndex + direction, actionButtons.Count(b => b.Visible)); ExplainEnding(); }
        RestoreFocus();
        canvas.QueueRedraw();
    }
    private static int Wrap(int value, int count) => count == 0 ? 0 : (value % count + count) % count;
    public void SwitchZone(int direction) { if (Reading) return; do { zone = (FocusRegion)Wrap((int)zone + direction, 3); } while (zone == FocusRegion.Pool && !pool.Visible); if (zone == FocusRegion.Actions) ExplainEnding(); RestoreFocus(); canvas.QueueRedraw(); }
    private void FollowFocused()
    {
        var n = map.Nodes.FirstOrDefault(n => n.Id == FocusedNodeId);
        if (n == null)
            return;
        pan = (ContentBounds.GetCenter() - new Vector2(n.Center.X, -n.Center.Y)) * Cell;
    }
    private Rect2 ContentBounds
    {
        get
        {
            var nodes = map.Nodes;
            var min = new Vector2(nodes.Min(n => n.Center.X - n.Radius - 1.6f), nodes.Min(n => -n.Center.Y - n.Radius - 1.6f));
            var max = new Vector2(nodes.Max(n => n.Center.X + n.Radius + 1.6f), nodes.Max(n => -n.Center.Y + n.Radius + 1.6f));
            return new Rect2(min, max - min);
        }
    }
    private float Cell => Math.Max(40 * Math.Max(.75f, Size.Y / 1080f), Math.Min(26 * Math.Max(1, Size.Y / 720), Math.Min(Math.Max(1, canvas.Size.X - 48) / ContentBounds.Size.X, Math.Max(1, canvas.Size.Y - 48) / ContentBounds.Size.Y))) * zoom;
    private Vector2 MapOrigin => canvas.Size / 2 + pan - ContentBounds.GetCenter() * Cell;
    public Vector2 GridToCanvas(GridPoint p) => MapOrigin + new Vector2(p.X, -p.Y) * Cell;
    private GridPoint CanvasToGrid(Vector2 p) => GridPoint.Snap((p.X - MapOrigin.X) / Cell, -(p.Y - MapOrigin.Y) / Cell);
    private JigsawNode? Hit(Vector2 p) => map.Nodes.OrderBy(n => (GridToCanvas(n.Center) - p).LengthSquared()).FirstOrDefault(n => Math.Abs(GridToCanvas(n.Center).X - p.X) <= Cell * (n.Radius + .55f) && Math.Abs(GridToCanvas(n.Center).Y - p.Y) <= Cell * (n.Radius + .55f));
    private void ActivateMap(int? id, GridPoint point, bool secondary)
    {
        if (Reading)
            return;
        if (CanReturnToRoom && !(map.Phase == JigsawPhase.Exploring && map.RoomCompletedPlayers.Contains(player)))
            return;
        if (map.Phase == JigsawPhase.Exploring)
        {
            if (id != null && !secondary)
            {
                if (VoteRequested != null)
                    Apply(VoteRequested(id.Value));
                else
                    RunAsync(enter(id.Value));
            }
            return;
        }
        if (secondary)
        {
            if (map.Holdings.ContainsKey(player))
                Apply(RotateRequested?.Invoke() ?? map.Rotate(player));
            else
                Apply(EndpointRequested?.Invoke(id) ?? map.SelectEndpoint(player, id));
        }
        else if (map.Holdings.ContainsKey(player))
            Apply(DropRequested?.Invoke(point) ?? map.Drop(player, point));
        else if (id != null && map.Nodes.First(n => n.Id == id).PieceId is int piece)
            Take(piece);
    }
    public override bool _HasPoint(Vector2 point) => new Rect2(0, Size.Y * .12f, Size.X, Size.Y * .88f).HasPoint(point);
    public override void _Input(InputEvent ev)
    {
        if (!IsVisibleInTree())
            return;
        if (MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.SubmenuStack.Stack?.SubmenusOpen == true)
            return;
        if (NModalContainer.Instance?.OpenModal != null)
            return;
        if (CanReturnToRoom && ev.IsActionPressed(MegaInput.viewMap))
        { returnToRoom!(); GetViewport()?.SetInputAsHandled(); return; }
        if (IsGenerating)
        { GetViewport()?.SetInputAsHandled(); return; }
        // This dedicated screen consumes map drawing/erasing before native unhandled input.
        if (ev is InputEventMouseButton mouse)
        {
            if (helpOpen)
            { dragging = false; return; } // Leave wheel/click events to the Help GUI, never the map camera.
            var p = canvas.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
            bool inMap = new Rect2(Vector2.Zero, canvas.Size).HasPoint(p);
            if (mouse.ButtonIndex == MouseButton.Middle)
                dragging = mouse.Pressed && inMap && !Reading;
            if (inMap && mouse.Pressed)
            {
                if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                    zoom = Math.Clamp(zoom * (mouse.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1 / 1.12f), .25f, 3);
                else if (mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
                { cursor = CanvasToGrid(p); ActivateMap(Hit(p)?.Id, cursor, mouse.ButtonIndex == MouseButton.Right); GetViewport()?.SetInputAsHandled(); }
            }
            if (mouse.ButtonIndex is MouseButton.Right or MouseButton.Middle)
                GetViewport()?.SetInputAsHandled();
            return;
        }
        if (ev is InputEventMouseMotion motion)
        { if (helpOpen) return; if (dragging) { pan += motion.Relative; GetViewport()?.SetInputAsHandled(); } else if (map.Holdings.ContainsKey(player)) cursor = CanvasToGrid(canvas.GetLocalMousePosition()); return; }
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape } or InputEventKey { Pressed: true, PhysicalKeycode: Key.Escape } || ev.IsActionPressed(MegaInput.cancel) || ev.IsActionPressed(MegaInput.pauseAndBack))
        { Back(); GetViewport()?.SetInputAsHandled(); return; }
        if (ev.IsActionPressed(MegaInput.select))
        {
            if (helpOpen)
            { GetViewport()?.GuiGetFocusOwner()?.EmitSignal(Button.SignalName.Pressed); }
            else if (next.Visible)
                AdvanceDialog();
            else if (zone == FocusRegion.Pool && poolButtons.Count > 0)
                poolButtons[poolIndex].EmitSignal(Button.SignalName.Pressed);
            else if (zone == FocusRegion.Map)
                ActivateMap(FocusedNodeId, cursor, false);
            else if (zone == FocusRegion.Actions)
                GetViewport()?.GuiGetFocusOwner()?.EmitSignal(Button.SignalName.Pressed);
            GetViewport()?.SetInputAsHandled();
            return;
        }
        if (Reading)
        {
            if (helpOpen && (ev.IsActionPressed(MegaInput.up) || ev.IsActionPressed(MegaInput.down) || ev.IsActionPressed(MegaInput.left) || ev.IsActionPressed(MegaInput.right)))
            {
                int delta = ev.IsActionPressed(MegaInput.up) || ev.IsActionPressed(MegaInput.left) ? -1 : 1;
                var buttons = helpContent.GetChildren().OfType<Button>().ToArray();
                helpIndex = Wrap(helpIndex + delta, buttons.Length);
                buttons[helpIndex].GrabFocus();
            }
            GetViewport()?.SetInputAsHandled();
            return;
        }
        if (ev.IsActionPressed(MegaInput.viewDeckAndTabLeft))
            SwitchZone(-1);
        else if (ev.IsActionPressed(MegaInput.viewExhaustPileAndTabRight))
            SwitchZone(1);
        else if (ev.IsActionPressed(MegaInput.viewDiscardPile))
            ActivateMap(FocusedNodeId, cursor, true);
        else if (ev.IsActionPressed(MegaInput.viewDrawPile))
            ReturnHeld();
        else if (ev.IsActionPressed(MegaInput.left) || ev.IsActionPressed(MegaInput.up))
        { if (zone == FocusRegion.Map && map.Holdings.ContainsKey(player) && ev.IsActionPressed(MegaInput.up)) cursor += new GridPoint(0, 1); else Navigate(-1); }
        else if (ev.IsActionPressed(MegaInput.right) || ev.IsActionPressed(MegaInput.down))
        { if (zone == FocusRegion.Map && map.Holdings.ContainsKey(player) && ev.IsActionPressed(MegaInput.down)) cursor += new GridPoint(0, -1); else Navigate(1); }
        else if (ev is InputEventJoypadMotion axis)
        {
            if (axis.Axis == JoyAxis.RightX)
                pan.X -= axis.AxisValue * 18;
            if (axis.Axis == JoyAxis.RightY)
                pan.Y -= axis.AxisValue * 18;
            if (axis.Axis == JoyAxis.TriggerLeft)
                zoom = Math.Clamp(zoom - axis.AxisValue * .04f, .25f, 3);
            if (axis.Axis == JoyAxis.TriggerRight)
                zoom = Math.Clamp(zoom + axis.AxisValue * .04f, .25f, 3);
        }
        else
            return;
        GetViewport()?.SetInputAsHandled();
        canvas.QueueRedraw();
    }
    internal void DrawMap(JigsawCanvas target)
    {
        foreach (var visual in roomVisuals.Values)
            visual.Hide();
        foreach (var e in map.Edges)
        {
            var a = map.Nodes.First(n => n.Id == e.First);
            var b = map.Nodes.First(n => n.Id == e.Second);
            var start = GridToCanvas(a.Center);
            var end = GridToCanvas(b.Center);
            JigsawNativeArt.DrawPath(target, start, end, Cell, new Color("554637"));
        }
        foreach (var n in map.Nodes)
            DrawNode(target, n, GridToCanvas(n.Center), false);
        foreach (var node in HeldPreview())
            DrawNode(target, node, GridToCanvas(node.Center), true);
    }
    private JigsawNode[] HeldPreview()
    {
        if (!map.Holdings.TryGetValue(player, out var held))
            return [];
        return map.Pool.First(p => p.Id == held.PieceId).Nodes.Select(n => { var point = n.Offset; for (int r = 0; r < held.Rotation; r++) point = point.Rotate(); return new JigsawNode(n.Id, held.PieceId, n.Room, point + cursor); }).ToArray();
    }
    internal void DrawMarkers(CanvasItem target)
    {
        foreach (var n in map.Nodes)
        {
            float size = Cell * (n.Radius * 2 + 1);
            var at = GridToCanvas(n.Center);
            var rect = new Rect2(at - Vector2.One * size / 2, Vector2.One * size);
            DrawFocus(target, n, rect);
        }
        DrawHolding(target, HeldPreview(), player);
        foreach (var holding in map.Holdings.Where(h => h.Key != player))
            DrawHolding(target, map.Nodes.Where(n => n.PieceId == holding.Value.PieceId).ToArray(), holding.Key);
    }
    private void DrawHolding(CanvasItem target, JigsawNode[] nodes, ulong holder)
    {
        if (nodes.Length == 0)
            return;
        var points = nodes.Select(n => (Point: GridToCanvas(n.Center), Radius: n.Radius + .5f)).ToArray();
        var min = new Vector2(points.Min(n => n.Point.X - n.Radius * Cell), points.Min(n => n.Point.Y - n.Radius * Cell));
        var max = new Vector2(points.Max(n => n.Point.X + n.Radius * Cell), points.Max(n => n.Point.Y + n.Radius * Cell));
        target.DrawRect(new Rect2(min, max - min).Grow(3), playerColor?.Invoke(holder) ?? new Color("f4d79a"), false, 3);
        if (avatar?.Invoke(holder) is { } portrait)
            target.DrawTextureRect(portrait, new Rect2(new Vector2(max.X, min.Y) - new Vector2(24, 24), new Vector2(24, 24)), false);
    }
    private void DrawFocus(CanvasItem target, JigsawNode n, Rect2 rect)
    {
        if (JigsawInputPresentation.UsingController && zone == FocusRegion.Map && FocusedNodeId == n.Id)
        {
            var focus = rect.Grow(5);
            float arm = Math.Min(focus.Size.X, focus.Size.Y) * .22f;
            foreach (var corner in new[] { focus.Position, new Vector2(focus.End.X, focus.Position.Y), focus.End, new Vector2(focus.Position.X, focus.End.Y) })
            {
                var inward = new Vector2(corner.X == focus.Position.X ? 1 : -1, corner.Y == focus.Position.Y ? 1 : -1);
                target.DrawLine(corner, corner + new Vector2(inward.X * arm, 0), Colors.White, 3);
                target.DrawLine(corner, corner + new Vector2(0, inward.Y * arm), Colors.White, 3);
            }
        }
        if (map.Phase == JigsawPhase.Editing && map.SelectedEndpoints.GetValueOrDefault(player, -1) == n.Id)
            target.DrawRect(rect.Grow(9), new Color("f4d79a"), false, 2);
    }
    private void DrawNode(JigsawCanvas target, JigsawNode n, Vector2 at, bool preview)
    {
        float size = Cell * (n.Radius * 2 + 1);
        var rect = new Rect2(at - new Vector2(size, size) / 2, new(size, size));
        bool available = (map.CurrentNode != null && map.RoomCompletedPlayers.Contains(player) ? map.VotingDestinations : map.AvailableDestinations).Contains(n.Id);
        float pulse = available ? 1 + .12f * (float)Math.Sin(Time.GetTicksMsec() / 260d) : 1;
        rect = new Rect2(rect.GetCenter() - rect.Size * pulse / 2, rect.Size * pulse);


        if (n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss)
        {
            if (!bossVisuals.TryGetValue(n.Id, out var visual) && art.Boss(n.Room) is { } created)
            { bossVisuals[n.Id] = visual = created; visual.Name = $"JigsawBoss{n.Id}"; target.AddChild(visual); }
            if (visual != null)
            { JigsawNativeArt.FitBoss(visual, at, size * pulse); visual.Modulate = new Color(1, 1, 1, preview ? .55f : 1); }
            else
                target.DrawTextureRect(art.Icon(n.Room), rect, false, new Color("433427"));
        }
        else
        {
            if (!roomVisuals.TryGetValue(n.Id, out var icon))
            {
                icon = new JigsawRoomPulse { Name = $"Room{n.Id}" };
                icon.AddChild(new Sprite2D { Name = "Visited", Texture = JigsawNativeArt.Load("atlases/compressed.sprites/map/map_circle_4.tres"), ShowBehindParent = true, Modulate = Colors.Black });
                roomVisuals[n.Id] = icon;
                target.AddChild(icon);
            }
            icon.Show();
            icon.Texture = art.Icon(n.Room);
            icon.Position = at;
            icon.BaseScale = size / Math.Max(icon.Texture.GetWidth(), icon.Texture.GetHeight());
            icon.Available = available;
            icon.Modulate = new Color(1, 1, 1, preview || map.Holdings.Values.Any(h => h.PieceId == n.PieceId) ? .55f : 1);
            var ring = icon.GetChild<Sprite2D>(0);
            ring.Visible = !preview && map.Visited.Contains(n.Id) && n.Room is not (JigsawRoom.Shop or JigsawRoom.Ancient);
            ring.Scale = Vector2.One * (Math.Max(icon.Texture.GetWidth(), icon.Texture.GetHeight()) * 1.4f / Math.Max(ring.Texture.GetWidth(), ring.Texture.GetHeight()));
        }

    }
}
public partial class JigsawCanvas : Control
{
    public JigsawScreen OwnerScreen { get; set; } = null!;
    public override void _Draw() => OwnerScreen.DrawMap(this);
}

public partial class JigsawPiecePreview : Control
{
    internal JigsawNativeArt Art { get; set; } = new(null);
    private readonly Dictionary<int, Node2D> bosses = [];
    public JigsawPiece Piece { get; set; } = null!;
    public override void _Draw()
    {
        var min = new Vector2(Piece.Nodes.Min(n => n.Offset.X), Piece.Nodes.Min(n => n.Offset.Y));
        var max = new Vector2(Piece.Nodes.Max(n => n.Offset.X), Piece.Nodes.Max(n => n.Offset.Y));
        float unit = Math.Min(Size.X / (max.X - min.X + 4), Size.Y / (max.Y - min.Y + 4));
        Vector2 Point(GridPoint p) => Size / 2 + new Vector2(p.X - (min.X + max.X) / 2, -p.Y + (min.Y + max.Y) / 2) * unit;
        foreach (var edge in Piece.InternalEdges)
            JigsawNativeArt.DrawPath(this, Point(Piece.Nodes.First(n => n.Id == edge.First).Offset), Point(Piece.Nodes.First(n => n.Id == edge.Second).Offset), unit, new Color("b6bdc6"));
        foreach (var node in Piece.Nodes)
        {
            float side = unit * (node.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss ? 3 : 1);
            DrawRect(new Rect2(Point(node.Offset) - new Vector2(side, side) / 2, new(side, side)), new Color("e4d5b6"));
            DrawRect(new Rect2(Point(node.Offset) - new Vector2(side, side) / 2, new(side, side)), new Color("806947"), false, 1);
            if (node.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss)
            {
                if (!bosses.TryGetValue(node.Id, out var boss) && Art.Boss(node.Room) is { } created)
                { bosses[node.Id] = boss = created; AddChild(boss); }
                if (boss != null)
                { JigsawNativeArt.FitBoss(boss, Point(node.Offset), side); continue; }
            }
            DrawTextureRect(Art.Icon(node.Room), new Rect2(Point(node.Offset) - Vector2.One * side / 2, Vector2.One * side), false);
        }
    }
}

public partial class JigsawPaper : Control
{
    internal JigsawNativeArt Art { get; set; } = null!;
    public override void _Draw() => Art.DrawPaper(this, Size);
}

public partial class JigsawCardFrame : Control
{
    public override void _Draw()
    {
        var texture = JigsawNativeArt.Load("packed/common_ui/event_button.png");
        DrawRect(new Rect2(Vector2.One * 10, Size - Vector2.One * 20), new Color("0a171b"));
        float[] sx = [0, 28, texture.GetWidth() - 28, texture.GetWidth()], sy = [0, 28, texture.GetHeight() - 28, texture.GetHeight()];
        float[] dx = [0, 10, Size.X - 10, Size.X], dy = [0, 10, Size.Y - 10, Size.Y];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            {
                if (x == 1 && y == 1)
                    continue;
                DrawTextureRectRegion(texture, new Rect2(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]), new Rect2(sx[x], sy[y], sx[x + 1] - sx[x], sy[y + 1] - sy[y]));
            }
    }
}

public partial class JigsawOverlay : Control
{
    public JigsawScreen OwnerScreen { get; set; } = null!;
    public override void _Draw() => OwnerScreen.DrawMarkers(this);
}

public partial class JigsawRoomPulse : Sprite2D
{
    public float BaseScale { get; set; } = 1;
    public bool Available { get; set; }
    public JigsawRoomPulse() { ProcessMode = ProcessModeEnum.Always; }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;
        float pulse = Available ? 1.2f + .25f * (float)Math.Sin(Time.GetTicksMsec() / 250d) : 1;
        Scale = Vector2.One * BaseScale * pulse;
    }
}
