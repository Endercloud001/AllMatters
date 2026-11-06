using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
internal static class JigsawPresentationTests
{
 public static readonly (string Name, Func<Task> Test)[] Cases = [
  ("Jigsaw presentation approved editor controls and help wheel isolation", ApprovedControls),
  ("Jigsaw presentation keyboard hints follow the actual device", KeyboardHints),
  ("Jigsaw presentation native art and paused input ownership", NativeArtAndPause),
  ("Jigsaw presentation first sentence is complete and profile resumes", FirstSentence),
  ("Jigsaw presentation actual input does not leak and help cancels preserves votes", InputHelp),
  ("Jigsaw presentation focus reaches isolated dense nodes and responsive layouts", NavigationLayout),
  ("Jigsaw presentation manual errors retain both five second segments", FailureTiming),
  ("Jigsaw presentation routed input is isolated and mode action toggles", RoutedInput),
  ("Jigsaw presentation entrance stays visible with native root sizing", NativeEntranceLayout),
  ("Jigsaw presentation exploring removes editing surface", ExploringSurface),
  ("Jigsaw presentation pending entry retry receives engine input", RetryInput),
  ("Jigsaw presentation pending entry abandon receives engine input", ()=>EntryActionInput(true)),
  ("Jigsaw presentation rejected take does not add a holding prompt", RejectedTake),
  ("Jigsaw presentation failed endpoint interrupts consecutive blank clicks", EndpointBlanks),
  ("Jigsaw presentation recent notice ends with the puzzle", NoticeLifetime),
  ("Jigsaw presentation ending explanation covers direct region entry and mouse", EndingExplanation),
  ("Jigsaw presentation error priority does not consume unseen teaching", DeferredTeaching)];
 private static JigsawScreen View(JigsawRun map,MemoryJigsawTutorialProfile profile,Action? pause=null)
 {
  HeadlessUiHost.EnsureInputActions();
  return JigsawScreen.Create(map,1,()=>{},()=>Task.CompletedTask,()=>Task.CompletedTask,_=>Task.CompletedTask,pause??(()=>{}),profile);
 }
 private static async Task ApprovedControls()
 {
  var view=View(JigsawRun.Create(74),new(){Sentence=6});
  var tree=(SceneTree)Engine.GetMainLoop();var originalSize=tree.Root.Size;tree.Root.Size=new(1280,720);
  try
  {
   await HeadlessUiHost.Mount(view);
   Vector2 Spacing()=>view.GridToCanvas(new(1,0))-view.GridToCanvas(new(0,0));
   void Wheel()=>tree.Root.PushInput(new InputEventMouseButton {Position=view.MapControl.GetGlobalRect().GetCenter(),GlobalPosition=view.MapControl.GetGlobalRect().GetCenter(),ButtonIndex=MouseButton.WheelDown,Pressed=true},true);
   var before=Spacing(); Wheel();
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(Spacing()!=before,"Preparation: engine wheel input must reach the production map camera.");
   var spacing=Spacing();
   view.OpenHelp();
   Wheel();
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(Spacing()==spacing,"Help wheel must leave the underlying map camera unchanged.");
   view.Back();
   var cards=view.PoolControl.GetChild<Control>(0);
   Require(cards.GetChild<Button>(0).Text=="归还碎块","Return remains at the top of the pool after refresh.");
   Require(!view.DecisionControl.GetChildren().OfType<Button>().Any(b=>b.Text=="暂停"),"Native pause remains available through input without a redundant button.");
   Require(!view.ControllerHints.Visible&&!view.MouseHints.Visible,"The operation strip is removed for both device modes.");
   Require(!cards.GetChildren().OfType<Button>().SelectMany(b=>b.GetChildren()).OfType<Label>().Any(),"Pool cards have no shape letters.");
   var wide=JigsawRun.Create(74);Require(wide.Take(1,0).Success&&wide.Drop(1,new(100,0)).Success,"Prepare a widely spread editable map.");
   view.SetMap(wide);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   var safe=new Rect2(view.MapControl.Size*new Vector2(.12f,.24f),view.MapControl.Size*new Vector2(.74f,.61f));
   foreach(var node in wide.Nodes)Require(safe.HasPoint(view.GridToCanvas(node.Center)),"Every node of a widely spread map must fit inside the paper safe area.");
  }
  finally {view.Free();tree.Root.Size=originalSize;}
 }
 private static void Require(bool ok,string message) { if(!ok)throw new Exception(message); }
 private static async Task KeyboardHints()
 {
  var view=View(JigsawRun.Create(74),new(){Sentence=6});
  try {
  await HeadlessUiHost.Mount(view);
  Require(Input.GetConnectedJoypads().Count==0,"This regression requires the keyboard/mouse host, without an emulated controller.");
  Require(!view.ControllerHints.Visible && !view.MouseHints.Visible,"The approved UI removes the operation strip for keyboard and controller modes.");
  Require(new JigsawPreferencesData().Enabled,"New global settings enable the mode by default.");
  } finally {view.Free();}
 }
 private static async Task NativeArtAndPause()
 {
  using var atlases=HeadlessTestSetup.LoadAtlases("ui_atlas","compressed");
  var native=MegaCrit.Sts2.Core.Runs.RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
   MegaCrit.Sts2.Core.Models.ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],
   acts:[MegaCrit.Sts2.Core.Models.ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Overgrowth>()],seed:"JIGSAW_UI_FEEDBACK");
  var manager=MegaCrit.Sts2.Core.Runs.RunManager.Instance;
  manager.SetUpTest(native,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
  JigsawScreen? view=null;var probe=new JigsawUnhandledInputProbe();
  var viewport=(SceneTree)Engine.GetMainLoop();var originalViewportSize=viewport.Root.Size;viewport.Root.Size=new(1280,720);
  try
  {
   manager.GenerateRooms();await manager.GenerateMap();
   var session=JigsawNativeSession.Enable(native);await session.StartAct();
   var art=new JigsawNativeArt(native.Act);
   Require(native.Act.MapTopBg!=null && native.Act.MapMidBg!=null && native.Act.MapBotBg!=null,"All three parchment sections come from the active native act.");
   foreach(var room in Enum.GetValues<JigsawRoom>())Require(art.Icon(room)!=null,"Every room uses a loadable native icon: "+room);
   var boss=art.Boss(JigsawRoom.Boss);try {Require(boss is { } && boss.GetChildCount()>0,"The production boss renders its native visual subtree.");}finally{boss?.Free();}
   view=session.CreateScreen(new MemoryJigsawTutorialProfile{Sentence=6});await HeadlessUiHost.Mount(probe,view);
   session.SuspendForNativePause();
   Require(!view.IsProcessingInput(),"Opening the native pause menu must relinquish editor input ownership.");
   int before=probe.Events;
   Input.ParseInputEvent(new InputEventAction{Action=MegaInput.select,Pressed=true});
   var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(probe.Events>before,"Native menu confirmation can pass through the suspended overlay.");
   session.ResumeInput();Require(view.IsProcessingInput(),"Closing native menus restores editor input ownership.");
   var explored=JigsawRun.Create(74);explored.Take(1,0);explored.Drop(1,new(3,0));explored.AddEdge(1,0,1);explored.ChooseEnd(1,EndChoice.Manual);explored.Inspect();explored.CompleteAncient(1);explored.Continue(1);
   int bossId=explored.Nodes.Single(n=>n.Room==JigsawRoom.Boss).Id;
   while(!explored.AvailableDestinations.Contains(bossId))
   {
    int destination=explored.AvailableDestinations.First();var attempt=explored.BeginEnter(destination)!;
    Require(explored.ConfirmEnter(attempt.Id).Success && explored.CompleteRoom(attempt.Id).Success,"Prepare an available Boss through completed logical rooms.");
   }
   view.SetMap(explored);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   var renderedBoss=view.MapControl.GetChildren().OfType<Node2D>().Single();
   var scales=new List<float>();
   for(int i=0;i<3;i++) {await tree.ToSignal(tree.CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);scales.Add(renderedBoss.Scale.X);
    var bounds=JigsawNativeArt.BossBounds(renderedBoss);
    float cell=view.GridToCanvas(new(1,0)).DistanceTo(view.GridToCanvas(new(0,0)));
    Require(Math.Max(bounds.Size.X,bounds.Size.Y)*renderedBoss.Scale.X<=3*cell,"Animated Boss including pulse stays within its assigned three-grid square.");}
   Require(scales.Max()-scales.Min()>.0001f,"The actual native Boss visual must pulse while it is an available destination.");
  }
  finally {view?.Free();probe.Free();manager.CleanUp(false);viewport.Root.Size=originalViewportSize;}
 }
 private static Task FirstSentence()
 {
  var profile=new MemoryJigsawTutorialProfile(); var view=View(JigsawRun.Create(74),profile);
  try {
   Require(view.DialogText=="先古之民们决定赐予你更多的帮助。","First sentence immediately complete");
   view.AdvanceDialog(); view.AdvanceDialog(); Require(profile.Sentence==2,"Current sentence persists");
  } finally {view.Free();}
  view=View(JigsawRun.Create(75),profile);
  try {
   Require(view.DialogText=="从左侧可用的碎块中构建你的心愿，交付给我们。","New run resumes current sentence");
   for(int i=0;i<4;i++)view.AdvanceDialog(); Require(profile.Sentence==6&&!view.Reading,"Last confirmation completes tutorial");
  } finally {view.Free();}
  return Task.CompletedTask;
 }
 private static async Task InputHelp()
 {
  var tree=(SceneTree)Engine.GetMainLoop(); var map=JigsawRun.Create(74,players:[1UL,2UL]); var profile=new MemoryJigsawTutorialProfile(); int pauses=0;
  var view=View(map,profile,()=>pauses++);
  try {
   await HeadlessUiHost.Mount(view);
   view._Input(new InputEventAction {Action=MegaInput.select,Pressed=true});
   Require(profile.Sentence==1&&map.Placements.Count==0&&map.Holdings.Count==0,"Advance consumes entire confirmation");
   while(profile.Sentence<6)view.AdvanceDialog();
   map.Take(1,map.Pool[0].Id); view.OpenHelp(); Require(map.Holdings.Count==0,"Help releases held piece");
   view.Back(); map.ChooseEnd(1,EndChoice.Manual); view.OpenHelp(); Require(map.Choices[1]==EndChoice.Manual,"Help preserves vote");
   view._Input(new InputEventAction{Action=MegaInput.cancel,Pressed=true}); Require(!view.Reading&&pauses==0,"First return closes help");
   view._Input(new InputEventAction{Action=MegaInput.cancel,Pressed=true}); Require(pauses==1&&map.Choices[1]==EndChoice.Manual,"Empty return opens native pause and retains vote");
  } finally {view.Free();}
 }
 private static async Task NavigationLayout()
 {
  var tree=(SceneTree)Engine.GetMainLoop(); var map=JigsawRun.Create(74);
  foreach(var p in map.Pool.Take(5)) { Require(map.Take(1,p.Id).Success,"Take fixture"); Require(map.Drop(1,new GridPoint(p.Id*14,7)).Success,"Place isolated fixture"); }
  var view=View(map,new(){Sentence=6});
  try {
   await HeadlessUiHost.Mount(view);
   view.SwitchZone(1); var visited=new HashSet<int>();
   for(int i=0;i<map.Nodes.Count;i++) {visited.Add(view.FocusedNodeId!.Value); view._Input(new InputEventAction{Action=MegaInput.right,Pressed=true});}
   Require(visited.SetEquals(map.Nodes.Select(n=>n.Id)),"Every isolated and same-direction node must be reachable");
   foreach(var size in new[]{new Vector2(1280,720),new Vector2(3840,2160),new Vector2(1280,800),new Vector2(1680,720)})
   {
    tree.Root.Size=(Vector2I)size; view.Size=size;
    await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame); await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
    Require(view.PoolControl.Size.X>100&&view.MapControl.Size.X>size.X*.4f,"Pool/map usable at supported size");
    Require(view.PoolControl.GetGlobalRect().Position.Y>=size.Y*.09f,"The editor must leave the native top HUD clear.");
    Require(view.DialogControl.GetGlobalRect().End.Y<=size.Y+1&&view.DecisionControl.GetGlobalRect().End.Y<=size.Y+1,"Footer must fit supported viewport");
    view.ShowNotice(new string('长',500)); await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
    Require(view.DialogControl.Size.X<size.X&&view.DecisionControl.GetGlobalRect().End.X<=size.X+1,"Long text must remain scrollable without obscuring decisions");
   }
  } finally {view.Free();tree.Root.Size=new(1280,720);}
 }
 private static Task FailureTiming()
 {
  var map=JigsawRun.Create(74); var view=View(map,new(){Sentence=6});
  try {
   view.ShowResult(JigsawResult.Reject(JigsawFailure.BossUnreachable),true);
   Require(view.DialogText=="你的愿景触犯了尖塔的规则，我不能实现……","Manual failure first exact segment");
   view.AdvanceTime(4.9); Require(view.DialogText=="你的愿景触犯了尖塔的规则，我不能实现……","First segment lasts 5 seconds");
   view.AdvanceTime(.1); Require(view.DialogText=="（提示：从先古之民不可到达Boss）","Manual failure second exact segment");
   map.Take(1,map.Pool[0].Id); view.Refresh(); Require(view.DialogText=="（提示：从先古之民不可到达Boss）","Editing cannot replace manual failure");
   view.AdvanceTime(5); Require(view.DialogText!="（提示：从先古之民不可到达Boss）","Second segment lasts 5 seconds");
  } finally {view.Free();}
  return Task.CompletedTask;
 }
 private static async Task ExploringSurface()
 {
  var map=JigsawRun.Create(74);
  map.Take(1,0); map.Drop(1,new(3,0)); map.AddEdge(1,0,1); map.ChooseEnd(1,EndChoice.Manual); map.Inspect();map.CompleteAncient(1);map.Continue(1);
  var view=View(map,new(){Sentence=6});try {await HeadlessUiHost.Mount(view);
  Require(!view.PoolControl.Visible,"The exploration map must not retain the editor piece pool.");
  } finally {view.Free();}
 }
 private static Task RetryInput()=>EntryActionInput(false);
 private static async Task EntryActionInput(bool abandon)
 {
  var map=JigsawRun.Create(74);
  Require(map.Take(1,0).Success && map.Drop(1,new(3,0)).Success && map.AddEdge(1,0,1).Success && map.ChooseEnd(1,EndChoice.Manual).Success && map.Inspect().Success,"Prepare accepted layout.");
  map.CompleteAncient(1); map.Continue(1); map.BeginEnter(1);
  HeadlessUiHost.EnsureInputActions(); int retries=0, abandons=0;
  var view=JigsawScreen.Create(map,1,()=>{},()=>Task.CompletedTask,()=>Task.CompletedTask,node=>{Require(node==1,"Retry preserves destination.");retries++;return Task.CompletedTask;},()=>{},new MemoryJigsawTutorialProfile{Sentence=6},abandonRequested:()=>{Require(map.AbandonEntry(map.PendingEntry!.Id).Success,"The domain accepts abandon.");abandons++;return Task.CompletedTask;});
  var probe=new JigsawUnhandledInputProbe(); var tree=(SceneTree)Engine.GetMainLoop();
  try
  {
   await HeadlessUiHost.Mount(probe,view);
   var button=view.DecisionControl.GetChildren().OfType<Button>().SingleOrDefault(b=>b.Text==(abandon?"放弃进入":"重试进入"));
   Require(button is { Visible:true, Disabled:false },"A pending entry exposes the formal retry action.");
   view.SwitchZone(1); view.SwitchZone(1); button!.GrabFocus();
   Input.ParseInputEvent(new InputEventAction{Action=MegaInput.select,Pressed=true});
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(probe.Events==0 && (abandon ? abandons==1 && retries==0 && map.PendingEntry==null : retries==1 && abandons==0 && map.PendingEntry?.NodeId==1),"Engine input reaches only the chosen entry action once, without leaking.");
  }
  finally {view.Free();probe.Free();}
 }

 private static async Task NativeEntranceLayout()
 {
  var state=ResourceLoader.Load<PackedScene>("res://scenes/screens/character_select_screen.tscn").GetState();
  float right=0,bottom=0;
  for(int i=0;i<state.GetNodePropertyCount(0);i++)
  {
   string key=state.GetNodePropertyName(0,i);
   if(key=="offset_right")right=(float)state.GetNodePropertyValue(0,i).AsDouble();
   if(key=="offset_bottom")bottom=(float)state.GetNodePropertyValue(0,i).AsDouble();
  }
  Console.WriteLine($"NATIVE_ENTRANCE root offsets={right},{bottom}");
  using var host=new Control {Size=new(right,bottom)};
  var choice=JigsawModeEntry.Attach(host); HeadlessUiHost.EnsureInputActions();
  await HeadlessUiHost.Mount(host);
  var tree=(SceneTree)Engine.GetMainLoop();
  var rect=choice.GetGlobalRect();
  Require(rect.Position.Y>=0 && rect.End.Y<=tree.Root.Size.Y && rect.Size.Y>0,"The production entrance must appear inside the viewport even when native screen root has zero size.");
  bool initial=choice.ButtonPressed;
  Input.ParseInputEvent(new InputEventAction{Action=MegaInput.viewDrawPile,Pressed=true});
  await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
  Require(choice.ButtonPressed!=initial && JigsawNativeSession.SelectedForNewRun==choice.ButtonPressed && JigsawPreferences.Enabled==choice.ButtonPressed,"Visible entrance receives engine input and persists the selected production run mode.");
  JigsawPreferences.Enabled=true;
  JigsawNativeSession.SelectedForNewRun=false;
 }

 private static async Task RoutedInput()
 {
  var tree=(SceneTree)Engine.GetMainLoop();
  var profile=new MemoryJigsawTutorialProfile();
  var view=View(JigsawRun.Create(74),profile);
  var probe=new JigsawUnhandledInputProbe();
  try {
   await HeadlessUiHost.Mount(probe,view);
   Input.ParseInputEvent(new InputEventAction{Action=MegaInput.select,Pressed=true});
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(profile.Sentence==1 && probe.Events==0,$"Godot routes confirmation to tutorial and consumes it before underlying input (sentence={profile.Sentence}, unhandled={probe.Events}).");
   view.Hide();
   using var choice=new JigsawModeChoice();
   await HeadlessUiHost.Mount(choice);
   Input.ParseInputEvent(new InputEventAction{Action=MegaInput.viewDrawPile,Pressed=true});
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(choice.ButtonPressed,"Bound mode action toggles the actual entrance control.");
   choice.Hide();
   Input.ParseInputEvent(new InputEventAction{Action=MegaInput.viewDrawPile,Pressed=true});
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(choice.ButtonPressed,"Hidden mode entrance cannot consume or toggle another screen's input.");
  } finally {view.Free();probe.Free();}
 }
 private static Task RejectedTake()
 {
  var map=JigsawRun.Create(74,players:[1UL,2UL]);
  map.ChooseEnd(1,EndChoice.Manual);
  var view=View(map,new(){Sentence=6});
  try {
   string before=view.DialogText;
   view.PoolControl.GetChild<VBoxContainer>(0).GetChildren().OfType<Button>().First().EmitSignal(Button.SignalName.Pressed);
   Require(map.Holdings.Count==0 && view.DialogText==before,"A voted player's rejected take must remain silent.");
  } finally {view.Free();}
  return Task.CompletedTask;
 }
 private static async Task EndpointBlanks()
 {
  var tree=(SceneTree)Engine.GetMainLoop(); var map=JigsawRun.Create(74);
  map.Take(1,0); map.Drop(1,new(3,0));
  var view=View(map,new(){Sentence=6}); tree.Root.CallDeferred(Node.MethodName.AddChild,view);
  await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame); await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
  void Click(GridPoint point)=>view._Input(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=true,Position=view.MapControl.GlobalPosition+view.GridToCanvas(point)});
  try {
   Click(new(0,0)); Click(new(0,5)); Click(new(6,0)); Click(new(0,5));
   Require(map.SelectedEndpoints.GetValueOrDefault(1UL,-1)==0,"An invalid second endpoint interrupts two consecutive blanks and preserves the first endpoint.");
   Click(new(0,5)); Require(!map.SelectedEndpoints.ContainsKey(1),"Only consecutive double blanks cancel the first endpoint.");
  } finally {view.Free();}
 }
 private static Task NoticeLifetime()
 {
  var view=View(JigsawRun.Create(74),new(){Sentence=6});
  try {
   view.ShowResult(JigsawResult.Reject(JigsawFailure.BossUnreachable),true);
   view.SetMap(JigsawRun.Create(75)); view.OpenHelp();
   var panel=view.GetChildren().OfType<PanelContainer>().Single();
   var content=panel.GetChild<VBoxContainer>(0);
   content.GetChildren().OfType<Button>().ElementAt(2).EmitSignal(Button.SignalName.Pressed);
   Require(content.GetChild<ScrollContainer>(3).GetChild<Label>(0).Text=="尚无提示。","A new puzzle cannot display the previous puzzle's recent notice.");
   view.Back(); view.AdvanceTime(5);
   Require(!view.DialogText.Contains("Boss"),"The previous puzzle's second error segment must not resume.");
  } finally {view.Free();}
  return Task.CompletedTask;
 }
 private static async Task EndingExplanation()
 {
  var tree=(SceneTree)Engine.GetMainLoop();var view=View(JigsawRun.Create(74),new(){Sentence=6});
  tree.Root.CallDeferred(Node.MethodName.AddChild,view);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
  try {
   view.SwitchZone(-1);
   Require(view.DialogText.Contains("审视")&&view.DialogText.Contains("自动")&&view.DialogText.Contains("禁编辑")&&view.DialogText.Contains("全员同选"),"Direct entry to ending actions explains both choices before activation.");
   view.SetMap(JigsawRun.Create(75));
   view.DecisionControl.GetChildren().OfType<Button>().First().EmitSignal(Control.SignalName.MouseEntered);
   Require(view.DialogText.Contains("审视")&&view.DialogText.Contains("自动"),"Mouse approach also explains both ending choices.");
  } finally {view.Free();}
 }
 private static Task DeferredTeaching()
 {
  var view=View(JigsawRun.Create(74),new(){Sentence=6});
  try {
   var button=view.DecisionControl.GetChildren().OfType<Button>().First();
   view.ShowResult(JigsawResult.Reject(JigsawFailure.BossUnreachable));
   button.EmitSignal(Control.SignalName.MouseEntered);
   Require(!view.DialogText.Contains("手动审视会"),"Error keeps priority over teaching.");
   view.AdvanceTime(5);
   button.EmitSignal(Control.SignalName.MouseEntered);
   Require(view.DialogText.Contains("手动审视会")&&view.DialogText.Contains("全员同选"),"Unseen teaching must still appear after the error expires.");
  } finally {view.Free();}
  return Task.CompletedTask;
 }
}

internal partial class JigsawUnhandledInputProbe : Node
{
 internal int Events;
 public override void _UnhandledInput(InputEvent input) { if(input.IsActionPressed(MegaInput.select))Events++; }
}
