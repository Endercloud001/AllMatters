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
 private Func<int,Task> enter = null!;
 private Func<Task>? automaticRequested;
 private Func<ulong,Texture2D?>? avatar;
 private readonly HBoxContainer manualVoters=new(), autoVoters=new();
 private int helpIndex;
 private readonly Theme adaptiveTheme=new();
 private IJigsawTutorialProfile profile = null!;
 private readonly Label dialog = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
 private readonly Label steps = new();
 private readonly VBoxContainer cards = new();
 private readonly ScrollContainer pool = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
 private readonly ScrollContainer dialogScroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
 private readonly HBoxContainer root = new();
 private readonly VBoxContainer main = new(), footer = new(), decisions = new();
 private readonly HBoxContainer bottom = new(), top = new();
 private readonly Button next = new(), manual = new(), automatic = new(), resume = new(), help = new();
 private readonly PanelContainer helpPanel = new();
 private readonly VBoxContainer helpContent = new();
 private readonly Label helpText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
 private readonly List<Button> poolButtons = [], actionButtons = [];
 private readonly List<(TextureRect Icon,string Action)> inputIcons = [];
 private JigsawCanvas canvas = null!;
 private int? replay;
 private bool helpOpen, tutorialPaused;
 private JigsawPhase previousPhase;
 private enum FocusRegion { Pool, Map, Actions }
 private FocusRegion zone;
 private int poolIndex, nodeIndex, actionIndex;
 private GridPoint cursor = new(4,0);
 private Vector2 pan;
 private float zoom = 1;
 private bool dragging;
 private double errorRemaining;
 private string recent = "", secondError = "";
 private ColorRect? generationCover;
 public bool IsGenerating => generationCover is { Visible:true };
 public void SetGenerating(bool active)
 {
  if(generationCover==null)
  {
   generationCover=new ColorRect { Color=Colors.Black, MouseFilter=MouseFilterEnum.Stop };
   AddChild(generationCover);generationCover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
   var caption=new Label { Text=L("先古之民的意志正在影响高塔"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore };
   generationCover.AddChild(caption);caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
  }
  generationCover.Visible=active;
  Refresh();
 }
 public void ShowAutomaticFailure(string reason)
 {
  ShowNotice("已保留原布局，可再次手动调整拼图。");
  var popup=NErrorPopup.Create(L("自动拼图失败"),L(reason),false);
  if(popup!=null) { popup.TreeExiting+=RestoreFocus;NModalContainer.Instance?.Add(popup); }
 }

 private readonly HashSet<string> tips = [];
 public string DialogText => dialog.Text;
 public bool Reading => (!tutorialPaused && profile.Sentence < Opening.Length) || replay != null || helpOpen;
 public IReadOnlyList<int> NavigableNodeIds => map.Nodes.OrderBy(n=>n.Center.Y).ThenBy(n=>n.Center.X).ThenBy(n=>n.Id).Select(n=>n.Id).ToArray();
 public int? FocusedNodeId => NavigableNodeIds.Count == 0 ? null : NavigableNodeIds[Math.Clamp(nodeIndex,0,NavigableNodeIds.Count-1)];
 public Control MapControl => canvas;
 public Control PoolControl => pool;
 public Control DialogControl => dialogScroll;
 public Control DecisionControl => decisions;
 public static JigsawScreen Create(JigsawRun map, ulong player, Action changed, Func<Task> inspected, Func<Task> continued, Func<int,Task> enter, Action pause, IJigsawTutorialProfile? profile = null, Func<Task>? automaticRequested = null, Func<ulong,Texture2D?>? avatar = null)
 {
  var view = new JigsawScreen { map=map, player=player, changed=changed, inspected=inspected, continued=continued, enter=enter, pause=pause, profile=profile ?? new JigsawTutorialProfile(), previousPhase=map.Phase, automaticRequested=automaticRequested, avatar=avatar };
  view.Build(); view.Refresh(); return view;
 }
 private static string L(string text) => TranslationServer.Translate(text);
 private Button MakeButton(string text, Action pressed)
 {
  var b=new Button { Text=L(text), AutowrapMode=TextServer.AutowrapMode.WordSmart, CustomMinimumSize=new(0,42), SizeFlagsHorizontal=SizeFlags.ExpandFill };
  b.Pressed += pressed; return b;
 }
 private void Build()
 {
  Theme=adaptiveTheme; Name="AllMattersJigsaw"; MouseFilter=MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
  var background=new ColorRect { Color=new Color("17202d"), MouseFilter=MouseFilterEnum.Ignore }; AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
  AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddThemeConstantOverride("separation",16);
  root.AddChild(pool); pool.AddChild(cards); cards.SizeFlagsHorizontal=SizeFlags.ExpandFill;
  root.AddChild(main); main.SizeFlagsHorizontal=SizeFlags.ExpandFill; main.SizeFlagsVertical=SizeFlags.ExpandFill;
  main.AddChild(top); top.AddChild(steps); steps.SizeFlagsHorizontal=SizeFlags.ExpandFill;
  help.Text=L("帮助"); help.Pressed += OpenHelp; top.AddChild(help);
  canvas=new JigsawCanvas { OwnerScreen=this, FocusMode=FocusModeEnum.All, SizeFlagsHorizontal=SizeFlags.ExpandFill, SizeFlagsVertical=SizeFlags.ExpandFill, ClipContents=true, MouseFilter=MouseFilterEnum.Stop }; main.AddChild(canvas);
  main.AddChild(bottom); bottom.AddChild(footer); footer.SizeFlagsHorizontal=SizeFlags.ExpandFill; footer.AddChild(dialogScroll); dialogScroll.SizeFlagsVertical=SizeFlags.ExpandFill; dialogScroll.AddChild(dialog);
  next.Text=L("继续"); next.Pressed += AdvanceDialog; footer.AddChild(next);
  bottom.AddChild(decisions);
  manual.Text=L("由先古之民审视"); manual.AutowrapMode=TextServer.AutowrapMode.WordSmart; manual.Pressed += ()=>Choose(EndChoice.Manual);
  automatic.Text=L("令先古之民决定"); automatic.AutowrapMode=TextServer.AutowrapMode.WordSmart; automatic.Pressed += ()=>Choose(EndChoice.Automatic);
  manual.MouseEntered += ExplainEnding; automatic.MouseEntered += ExplainEnding;
  manual.FocusEntered += ExplainEnding; automatic.FocusEntered += ExplainEnding;
  resume.Text=L("继续前行"); resume.Pressed += ()=>RunAsync(continued());
  decisions.AddChild(manualVoters); decisions.AddChild(manual); decisions.AddChild(autoVoters); decisions.AddChild(automatic); decisions.AddChild(resume);
  var back=MakeButton("暂停",Back); decisions.AddChild(back);
  actionButtons.AddRange([manual,automatic,resume,help,back]);
  var returnButton=MakeButton("归还碎块",ReturnHeld); cards.AddChild(returnButton);
  var hints=new HBoxContainer(); footer.AddChild(hints);
  foreach(var pair in new[]{(MegaInput.select,"拿放"),(MegaInput.cancel,"返回"),(MegaInput.viewDiscardPile,"旋转／连拆线"),(MegaInput.viewDrawPile,"归还"),(MegaInput.viewDeckAndTabLeft,"切换区域"),(MegaInput.pauseAndBack,"帮助")})
  {
   var icon=new TextureRect { CustomMinimumSize=new(22,22), ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered };
   hints.AddChild(icon); inputIcons.Add((icon,pair.Item1)); hints.AddChild(new Label{Text=L(pair.Item2)});
  }
  AddChild(helpPanel); helpPanel.AddChild(helpContent); helpPanel.Visible=false; helpPanel.ZIndex=5;
  helpContent.AddChild(MakeButton("重看开场对白",()=>{ replay=0; helpOpen=false; helpPanel.Hide(); Refresh(); }));
  helpContent.AddChild(MakeButton("操作与规则",()=>helpText.Text=L(Rules)));
  helpContent.AddChild(MakeButton("最近一次提示",()=>helpText.Text=recent.Length==0?L("尚无提示。"):recent));
  var helpScroll=new ScrollContainer { SizeFlagsVertical=SizeFlags.ExpandFill, HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; helpContent.AddChild(helpScroll); helpScroll.AddChild(helpText); helpText.SizeFlagsHorizontal=SizeFlags.ExpandFill;
  helpContent.AddChild(MakeButton("返回",Back));
  Resized += AdaptLayout;
 }
 private const string Rules="左键拿取或放置碎块；持有时右键逆时针旋转90度，空手时右键选两端连线或拆线。Esc先关闭帮助，再取消持有或待连端点，空手时打开暂停。中键拖动地图，滚轮缩放，池底归还碎块。\n\n手柄切换池、地图、操作区；方向输入逐卡、逐节点或逐格移动；拿放、旋转／连拆线、归还、返回均使用下方当前输入图标。地图逐节点导航包括孤立碎块。\n\n从先古之民出发，在本幕步数内到达Boss。已完成区域可免费中转；商店仅在实际可前往时可重访，重访也消耗一步，不刷新库存。普通房不能重进。\n\n两种结束按钮须全员同选，选后本人不可编辑，激活同一按钮撤票。持有或待连时先完成操作。手动审视先撤回孤立块；自动决定将重塑尖塔的一部分。\n\n双Boss以第一场抵达时判断额度；第二场不重判。最后一步进入非Boss房间时本房及内部战斗安全，全队完成后发放面具，之后进入受罚；战胜最后地图Boss解除追猎。帮助不会暂停队友，也不增加共同确认。";
 public void SetMap(JigsawRun value)
 {
  if(!ReferenceEquals(map,value)) { ClearRecent(); tips.Clear(); }
  map=value; Refresh();
 }
 public void ClearRecent() { recent=""; secondError=""; errorRemaining=0; }
 public void Refresh()
 {
  if(previousPhase!=map.Phase) { if(map.Phase!=JigsawPhase.Editing)ClearRecent(); replay=null; helpOpen=false; helpPanel.Hide(); previousPhase=map.Phase; zone=FocusRegion.Map; nodeIndex=0; }
  foreach(var b in poolButtons) { cards.RemoveChild(b); b.QueueFree(); } poolButtons.Clear();
  foreach(var p in map.Pool.Where(p=>!map.Placements.Any(x=>x.PieceId==p.Id)))
  {
   var holders=map.Holdings.Where(h=>h.Value.PieceId==p.Id).Select(h=>h.Key).ToArray();
   var b=MakeButton("",()=>Take(p.Id)); b.CustomMinimumSize=new(0,100*Math.Max(1,Size.Y/720));
   var preview=new JigsawPiecePreview { Piece=p, Position=new(12,8), Size=new(100,80), MouseFilter=MouseFilterEnum.Ignore }; b.AddChild(preview);
   b.Resized += ()=>preview.Size=new Vector2(Math.Max(100,b.Size.X-24),Math.Max(80,b.Size.Y-16));
   var shape=new Label {Text=p.Shape.ToString(), MouseFilter=MouseFilterEnum.Ignore}; b.AddChild(shape);
   shape.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight); shape.OffsetLeft=-32; shape.OffsetRight=-8; shape.OffsetTop=8; shape.OffsetBottom=32;
   foreach(var holder in holders) { var portrait=Portrait(holder); portrait.Position=new(135,16); b.AddChild(portrait); } cards.AddChild(b); cards.MoveChild(b,poolButtons.Count); poolButtons.Add(b);
   if(holders.Length>0) b.Modulate=new Color(1,1,1,.55f);
  }
  poolIndex=Math.Clamp(poolIndex,0,Math.Max(0,poolButtons.Count-1)); nodeIndex=Math.Clamp(nodeIndex,0,Math.Max(0,NavigableNodeIds.Count-1));
  steps.Text=L("剩余步数")+"  "+map.RemainingSteps+" / "+map.Budget;
  manual.Visible=automatic.Visible=map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating;
  manual.Disabled=automatic.Disabled=Reading || map.Phase!=JigsawPhase.Editing;
  manual.Text=L("由先古之民审视"); automatic.Text=L("令先古之民决定"); UpdateVoters(manualVoters,EndChoice.Manual); UpdateVoters(autoVoters,EndChoice.Automatic);
  resume.Visible=map.Phase==JigsawPhase.Ancient; resume.Disabled=Reading;
  next.Visible=(!tutorialPaused && profile.Sentence<Opening.Length)||replay!=null;
  if(next.Visible) dialog.Text=L(Opening[replay??Math.Clamp(profile.Sentence,0,5)]);
  else if(errorRemaining<=0) dialog.Text=CurrentInstruction();
  AdaptLayout(); canvas.QueueRedraw(); RestoreFocus();
 }
 private TextureRect Portrait(ulong id)=>new() { Texture=avatar?.Invoke(id), CustomMinimumSize=new(32,32), Size=new(32,32), ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter=MouseFilterEnum.Ignore };
 private void UpdateVoters(HBoxContainer row,EndChoice choice)
 {
  foreach(var child in row.GetChildren()) { row.RemoveChild(child); child.QueueFree(); }
  foreach(var voter in map.Choices.Where(x=>x.Value==choice))row.AddChild(Portrait(voter.Key));
  row.Visible=map.Choices.Any(x=>x.Value==choice);
 }
 private string CurrentInstruction()
 {
  if(map.Phase==JigsawPhase.Generating) return L("先古之民的意志正在影响高塔");
  if(map.Phase==JigsawPhase.Ancient) return L("你的愿望实现了...");
  if(map.Phase==JigsawPhase.Exploring) return L(map.RemainingSteps==1?"最后一步，请尽快前往Boss处。":$"还可前行{map.RemainingSteps}步。");
  return L(map.Holdings.ContainsKey(player)?"放置碎块，旋转，返回取消，或归还碎块。":"从左侧拿取碎块，或选择两端连线／拆线。");
 }
 public void AdvanceDialog()
 {
  if(replay is int r) { replay=r<5?r+1:null; }
  else if(profile.Sentence<6) profile.Sentence++;
  Refresh();
 }
 public void OpenHelp()
 {
  map.Cancel(player); changed(); helpOpen=true; helpPanel.Show(); helpText.Text=L(Rules); Refresh(); helpPanel.Show();
  helpIndex=0; if(IsInsideTree())helpContent.GetChild<Button>(0).GrabFocus();
 }
 public void Back()
 {
  if(helpOpen) { helpOpen=false; helpPanel.Hide(); Refresh(); return; }
  if(replay!=null) { replay=null; Refresh(); return; }
  if(map.Holdings.ContainsKey(player)||map.SelectedEndpoints.ContainsKey(player)) { Apply(map.Cancel(player)); return; }
  tutorialPaused=true; SetProcessInput(false); pause();
 }
 public void ResumeFromPause() { tutorialPaused=false; SetProcessInput(true); Refresh(); }
 private void Apply(JigsawResult result,bool silent=false)
 {
  if(!result.Success && !silent) ShowResult(result); changed(); Refresh();
 }
 private void Take(int piece)
 {
  if(Reading)return;
  var result=map.Take(player,piece);
  if(!result.Success)return;
  Apply(result,true); Tip("holding", "放置碎块，旋转，返回取消，或归还碎块。");
 }
 private void Tip(string id,string text) { if(errorRemaining<=0&&tips.Add(id))ShowNotice(text); }
 private void ExplainEnding()
 {
  if(map.Phase==JigsawPhase.Editing&&!Reading)
   Tip("end", "手动审视会检查你的拼图；自动决定将重新安排布局。两种结束按钮须全员同选，选择后本人禁编辑；激活同一按钮撤票，另一按钮改选。");
 }
 private void ReturnHeld() { if(!Reading && map.Holdings.TryGetValue(player,out var h))Apply(map.ReturnToPool(player,h.PieceId)); }
 private void Choose(EndChoice choice)
 {
  if(Reading)return;
  if(choice==EndChoice.Automatic && automaticRequested!=null) { RunAsync(automaticRequested()); return; }
  var result=map.ChooseEnd(player,choice); Apply(result);
  if(result.Success && map.Phase==JigsawPhase.Inspecting)RunAsync(inspected());
  if(result.Success && map.Phase==JigsawPhase.Generating)ShowNotice("将重塑尖塔的一部分");
 }
 private async void RunAsync(Task task)
 {
  try { await task; Refresh(); }
  catch(Exception e)
  {
   if(map.Phase==JigsawPhase.Editing && map.AutomaticOperation!=null)ShowNotice("已保留原布局，可再次手动调整拼图。");
   else if(map.Phase==JigsawPhase.Ancient && map.AutomaticOperation!=null)ShowNotice("布局已保留，保存未完成。请再次继续以重试。");
   else ShowResult(JigsawResult.Reject(e.Message));
  }
 }
 public void ShowNotice(string text) { recent=L(text); dialog.Text=recent; errorRemaining=5; secondError=""; }
 public void ShowResult(JigsawResult result,bool manual=false)
 {
  if(result.Success) { ShowNotice("你的愿望实现了..."); return; }
  string reason=Reason(result.Failure);
  secondError=manual?L("（提示："+reason+"）"):"";
  dialog.Text=L(manual?"你的愿景触犯了尖塔的规则，我不能实现……":"你的愿景触犯了尖塔的规则...")+(!manual?L("（提示："+reason+"）"):"");
  recent=dialog.Text+(manual?"\n"+secondError:""); errorRemaining=5;
 }
 private static string Reason(JigsawFailure reason)=>reason switch
 {
  JigsawFailure.InsufficientClearance=>"碎块之间的距离不足",
  JigsawFailure.BossUnreachable=>"从先古之民不可到达Boss",
  JigsawFailure.BossOverBudget=>"到达Boss的最短路线所需步数超出本幕限额",
  JigsawFailure.MissingEndpoint=>"连接端点已不存在",
  JigsawFailure.MisalignedPath=>"通路必须沿网格横向或纵向对齐",
  JigsawFailure.InvalidInternalPath=>"内部通路必须属于同一碎块",
  JigsawFailure.InvalidExternalEndpoint=>"跨碎块通路不能连接同一碎块或Boss",
  JigsawFailure.InvalidExternalGap=>"节点实际占地之间须恰好留一个空格",
  JigsawFailure.OccupiedPort=>"连接位置占用",
  JigsawFailure.IntersectingPaths=>"通路交叉或重合",
  JigsawFailure.PathThroughNode=>"通路穿过节点",
  JigsawFailure.TooManyPairPaths=>"同对碎块最多两条通路",
  JigsawFailure.ReservedPath=>"相关碎块正被持有",
  JigsawFailure.InternalPathFixed=>"内部通路不可编辑",
  JigsawFailure.EditIncomplete=>"请先完成当前拿放或连线",
  JigsawFailure.DisconnectedLayout=>"所有提交节点必须连接先古之民",
  JigsawFailure.BossNotTerminal=>"Boss必须位于固定内部通路终端",
  JigsawFailure.BossBlocksOrdinaryRooms=>"普通房间的路线不能穿过Boss",
  _=>"此操作不符合节点、通路或当前阶段的规则"
 };
 public override void _Process(double delta)
 {
  AdvanceTime(delta);
  foreach(var (icon,action) in inputIcons)
  {
#if STS2_PUBLIC
   icon.Texture=NControllerManager.Instance?.GetHotkeyIcon(action);
#else
   icon.Texture=NInputManager.Instance?.GetHotkeyIcon(action);
#endif
  }
  canvas.QueueRedraw();
 }
 public void AdvanceTime(double seconds)
 {
  if(errorRemaining<=0)return; errorRemaining-=seconds;
  if(Math.Abs(errorRemaining)<.000001)errorRemaining=0;
  if(errorRemaining<=0 && secondError.Length>0) { dialog.Text=secondError; secondError=""; errorRemaining+=5; }
  if(errorRemaining<=0 && !Reading) dialog.Text=CurrentInstruction();
 }
 private void AdaptLayout()
 {
  if(Size.X<=0)return;
  float scale=Math.Clamp(Size.Y/720f,1,3); adaptiveTheme.DefaultFontSize=(int)(18*scale);
  pool.CustomMinimumSize=new(Size.X*.23f,0); decisions.CustomMinimumSize=new(Size.X*.26f,0); bottom.CustomMinimumSize=new(0,Size.Y*.26f);
  dialog.CustomMinimumSize=new(Math.Max(100,Size.X*.44f),0);
  helpPanel.Position=Size*new Vector2(.16f,.08f); helpPanel.Size=Size*new Vector2(.68f,.84f); helpText.CustomMinimumSize=new(Size.X*.63f,0);
 }
 private void RestoreFocus()
 {
  if(!IsInsideTree()||helpOpen)return;
  if(next.Visible) { next.GrabFocus(); return; }
  if(zone==FocusRegion.Pool && poolButtons.Count>0) { poolButtons[poolIndex].GrabFocus(); pool.EnsureControlVisible(poolButtons[poolIndex]); }
  if(zone==FocusRegion.Map)canvas.GrabFocus();
  if(zone==FocusRegion.Actions) { var buttons=actionButtons.Where(b=>b.Visible).ToArray(); if(buttons.Length>0)buttons[actionIndex%buttons.Length].GrabFocus(); }
 }
 public void Navigate(int direction)
 {
  if(Reading)return;
  if(zone==FocusRegion.Pool)poolIndex=Wrap(poolIndex+direction,poolButtons.Count);
  else if(zone==FocusRegion.Map)
  {
   if(map.Holdings.ContainsKey(player))cursor+=new GridPoint(direction,0);
   else { nodeIndex=Wrap(nodeIndex+direction,NavigableNodeIds.Count); FollowFocused(); }
  }
  else { actionIndex=Wrap(actionIndex+direction,actionButtons.Count(b=>b.Visible)); ExplainEnding(); }
  RestoreFocus(); canvas.QueueRedraw();
 }
 private static int Wrap(int value,int count)=>count==0?0:(value%count+count)%count;
 public void SwitchZone(int direction) { if(Reading)return; zone=(FocusRegion)Wrap((int)zone+direction,3); if(zone==FocusRegion.Actions)ExplainEnding(); RestoreFocus(); canvas.QueueRedraw(); }
 private void FollowFocused()
 {
  var n=map.Nodes.FirstOrDefault(n=>n.Id==FocusedNodeId); if(n==null)return;
  pan=-new Vector2(n.Center.X,-n.Center.Y)*Cell;
 }
 private float Cell=>26*Math.Max(1,Size.Y/720)*zoom;
 public Vector2 GridToCanvas(GridPoint p)=>canvas.Size/2+pan+new Vector2(p.X,-p.Y)*Cell;
 private GridPoint CanvasToGrid(Vector2 p)=>GridPoint.Snap((p.X-canvas.Size.X/2-pan.X)/Cell,-(p.Y-canvas.Size.Y/2-pan.Y)/Cell);
 private JigsawNode? Hit(Vector2 p)=>map.Nodes.OrderBy(n=>(GridToCanvas(n.Center)-p).LengthSquared()).FirstOrDefault(n=>Math.Abs(GridToCanvas(n.Center).X-p.X)<=Cell*(n.Radius+.55f)&&Math.Abs(GridToCanvas(n.Center).Y-p.Y)<=Cell*(n.Radius+.55f));
 private void ActivateMap(int? id,GridPoint point,bool secondary)
 {
  if(Reading)return;
  if(map.Phase==JigsawPhase.Exploring) { if(id!=null && !secondary)RunAsync(enter(id.Value)); return; }
  if(secondary)
  {
   if(map.Holdings.ContainsKey(player))Apply(map.Rotate(player));
   else Apply(map.SelectEndpoint(player,id));
  }
  else if(map.Holdings.ContainsKey(player))Apply(map.Drop(player,point));
  else if(id!=null && map.Nodes.First(n=>n.Id==id).PieceId is int piece)Take(piece);
 }
 public override void _Input(InputEvent ev)
 {
  if(!IsVisibleInTree())return;
  if(NModalContainer.Instance?.OpenModal!=null)return;
  if(IsGenerating) { GetViewport()?.SetInputAsHandled();return; }
  // This dedicated screen consumes map drawing/erasing before native unhandled input.
  if(ev is InputEventMouseButton mouse)
  {
   var p=canvas.GetGlobalTransformWithCanvas().AffineInverse()*mouse.Position; bool inMap=new Rect2(Vector2.Zero,canvas.Size).HasPoint(p);
   if(mouse.ButtonIndex==MouseButton.Middle)dragging=mouse.Pressed && inMap && !Reading;
   if(inMap && mouse.Pressed)
   {
    if(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)zoom=Math.Clamp(zoom*(mouse.ButtonIndex==MouseButton.WheelUp?1.12f:1/1.12f),.25f,3);
    else if(mouse.ButtonIndex is MouseButton.Left or MouseButton.Right) { cursor=CanvasToGrid(p); ActivateMap(Hit(p)?.Id,cursor,mouse.ButtonIndex==MouseButton.Right); GetViewport()?.SetInputAsHandled(); }
   }
   if(mouse.ButtonIndex is MouseButton.Right or MouseButton.Middle) GetViewport()?.SetInputAsHandled();
   return;
  }
  if(ev is InputEventMouseMotion motion) { if(dragging) { pan+=motion.Relative; GetViewport()?.SetInputAsHandled(); } else if(map.Holdings.ContainsKey(player))cursor=CanvasToGrid(canvas.GetLocalMousePosition()); return; }
  if(ev is InputEventKey {Pressed:true,Keycode:Key.Escape} || ev.IsActionPressed(MegaInput.cancel)) { Back(); GetViewport()?.SetInputAsHandled(); return; }
  if(ev.IsActionPressed(MegaInput.pauseAndBack)) { OpenHelp(); GetViewport()?.SetInputAsHandled(); return; }
  if(ev.IsActionPressed(MegaInput.select))
  {
   if(helpOpen) { GetViewport()?.GuiGetFocusOwner()?.EmitSignal(Button.SignalName.Pressed); }
   else if(next.Visible)AdvanceDialog();
   else if(zone==FocusRegion.Pool && poolButtons.Count>0)poolButtons[poolIndex].EmitSignal(Button.SignalName.Pressed);
   else if(zone==FocusRegion.Map)ActivateMap(FocusedNodeId,cursor,false);
   else if(zone==FocusRegion.Actions)GetViewport()?.GuiGetFocusOwner()?.EmitSignal(Button.SignalName.Pressed);
   GetViewport()?.SetInputAsHandled(); return;
  }
  if(Reading)
  {
   if(helpOpen&&(ev.IsActionPressed(MegaInput.up)||ev.IsActionPressed(MegaInput.down)||ev.IsActionPressed(MegaInput.left)||ev.IsActionPressed(MegaInput.right)))
   {
    int delta=ev.IsActionPressed(MegaInput.up)||ev.IsActionPressed(MegaInput.left)?-1:1;
    helpIndex=Wrap(helpIndex+delta,4); var buttons=helpContent.GetChildren().OfType<Button>().ToArray(); buttons[helpIndex].GrabFocus();
   }
   GetViewport()?.SetInputAsHandled(); return;
  }
  if(ev.IsActionPressed(MegaInput.viewDeckAndTabLeft))SwitchZone(-1);
  else if(ev.IsActionPressed(MegaInput.viewExhaustPileAndTabRight))SwitchZone(1);
  else if(ev.IsActionPressed(MegaInput.viewDiscardPile))ActivateMap(FocusedNodeId,cursor,true);
  else if(ev.IsActionPressed(MegaInput.viewDrawPile))ReturnHeld();
  else if(ev.IsActionPressed(MegaInput.left)||ev.IsActionPressed(MegaInput.up)) { if(zone==FocusRegion.Map&&map.Holdings.ContainsKey(player)&&ev.IsActionPressed(MegaInput.up))cursor+=new GridPoint(0,1); else Navigate(-1); }
  else if(ev.IsActionPressed(MegaInput.right)||ev.IsActionPressed(MegaInput.down)) { if(zone==FocusRegion.Map&&map.Holdings.ContainsKey(player)&&ev.IsActionPressed(MegaInput.down))cursor+=new GridPoint(0,-1); else Navigate(1); }
  else if(ev is InputEventJoypadMotion axis)
  {
   if(axis.Axis==JoyAxis.RightX)pan.X-=axis.AxisValue*18;
   if(axis.Axis==JoyAxis.RightY)pan.Y-=axis.AxisValue*18;
   if(axis.Axis==JoyAxis.TriggerLeft)zoom=Math.Clamp(zoom-axis.AxisValue*.04f,.25f,3);
   if(axis.Axis==JoyAxis.TriggerRight)zoom=Math.Clamp(zoom+axis.AxisValue*.04f,.25f,3);
  }
  else return;
  GetViewport()?.SetInputAsHandled(); canvas.QueueRedraw();
 }
 internal void DrawMap(JigsawCanvas target)
 {
  foreach(var e in map.Edges)
  {
   var a=map.Nodes.First(n=>n.Id==e.First);var b=map.Nodes.First(n=>n.Id==e.Second);
   target.DrawLine(GridToCanvas(a.Center),GridToCanvas(b.Center),e.Internal?new Color("a6b6c5"):new Color("f4d79a"),3,true);
  }
  foreach(var n in map.Nodes)DrawNode(target,n,GridToCanvas(n.Center),false);
  if(map.Holdings.TryGetValue(player,out var held))
  {
   var p=map.Pool.First(p=>p.Id==held.PieceId);
   foreach(var n in p.Nodes) { var point=n.Offset; for(int r=0;r<held.Rotation;r++)point=point.Rotate(); DrawNode(target,new(n.Id,p.Id,n.Room,point+cursor),GridToCanvas(point+cursor),true); }
  }
 }
 private void DrawNode(JigsawCanvas target,JigsawNode n,Vector2 at,bool preview)
 {
  float size=Cell*(n.Radius*2+1); var rect=new Rect2(at-new Vector2(size,size)/2,new(size,size));
  bool available=map.AvailableDestinations.Contains(n.Id);
  float pulse=available?1+.04f*(float)Math.Sin(Time.GetTicksMsec()/260d):1;
  rect=new Rect2(rect.GetCenter()-rect.Size*pulse/2,rect.Size*pulse);
  var fill=new Color(preview?"577d98":"334b62");
  if(preview||map.Holdings.Values.Any(h=>h.PieceId==n.PieceId))fill.A=.5f;
  target.DrawRect(rect,fill); target.DrawRect(rect,new Color("c9d7e4"),false,2);
  if(map.Visited.Contains(n.Id)&&n.Room is not (JigsawRoom.Shop or JigsawRoom.Boss or JigsawRoom.SecondBoss or JigsawRoom.Ancient))target.DrawArc(at,size*.65f,0,Mathf.Tau,32,new Color("c8c8af"),2,true);
  if(zone==FocusRegion.Map&&FocusedNodeId==n.Id&&!preview)target.DrawRect(rect.Grow(5),Colors.White,false,3);
  if(map.SelectedEndpoints.GetValueOrDefault(player,-1)==n.Id)target.DrawRect(rect.Grow(9),new Color("f4d79a"),false,2);
  string glyph=RoomGlyph(n.Room);
  target.DrawString(ThemeDB.FallbackFont,at+new Vector2(-size*.23f,size*.22f),glyph,HorizontalAlignment.Left,-1,(int)(Cell*.65f),Colors.White);
  foreach(var h in map.Holdings.Where(h=>h.Value.PieceId==n.PieceId)) { var texture=avatar?.Invoke(h.Key); target.DrawRect(rect.Grow(3),new Color("f4d79a"),false,3); if(texture!=null)target.DrawTextureRect(texture,new Rect2(at+new Vector2(size/2,-size/2-22),new Vector2(24,24)),false); }
 }
 internal static string RoomGlyph(JigsawRoom room)=>room switch {JigsawRoom.Ancient=>"◎",JigsawRoom.Boss or JigsawRoom.SecondBoss=>"♛",JigsawRoom.Combat=>"⚔",JigsawRoom.Shop=>"$",JigsawRoom.Rest=>"R",JigsawRoom.Elite=>"E",JigsawRoom.Treasure=>"◇",_=>"?"};
}
public partial class JigsawCanvas : Control
{
 public JigsawScreen OwnerScreen { get; set; }=null!;
 public override void _Draw()=>OwnerScreen.DrawMap(this);
}

public partial class JigsawPiecePreview : Control
{
 public JigsawPiece Piece { get; set; }=null!;
 public override void _Draw()
 {
  var min=new Vector2(Piece.Nodes.Min(n=>n.Offset.X),Piece.Nodes.Min(n=>n.Offset.Y));
  var max=new Vector2(Piece.Nodes.Max(n=>n.Offset.X),Piece.Nodes.Max(n=>n.Offset.Y));
  float unit=Math.Min(Size.X/(max.X-min.X+4),Size.Y/(max.Y-min.Y+4));
  Vector2 Point(GridPoint p)=>Size/2+new Vector2(p.X-(min.X+max.X)/2,-p.Y+(min.Y+max.Y)/2)*unit;
  foreach(var edge in Piece.InternalEdges)DrawLine(Point(Piece.Nodes.First(n=>n.Id==edge.First).Offset),Point(Piece.Nodes.First(n=>n.Id==edge.Second).Offset),new Color("b6bdc6"),2,true);
  foreach(var node in Piece.Nodes)
  {
   float side=unit*(node.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss?3:1);
   DrawRect(new Rect2(Point(node.Offset)-new Vector2(side,side)/2,new(side,side)),new Color("e4d5b6"),false,2);
   DrawString(ThemeDB.FallbackFont,Point(node.Offset)+new Vector2(-unit*.28f,unit*.25f),JigsawScreen.RoomGlyph(node.Room),HorizontalAlignment.Left,-1,Math.Max(10,(int)(unit*.8f)),Colors.White);
  }
 }
}
