using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
internal static class JigsawPresentationTests
{
 public static readonly (string Name, Func<Task> Test)[] Cases = [
  ..JigsawRecoveryPresentationTests.Cases,
  ..JigsawFeedbackPresentationTests.Cases,
  ("Jigsaw presentation top bar step badge and hover", StepBadge),
  ("Jigsaw presentation exploration visit ring and visible pulse", ExplorationVisuals),
  ("Jigsaw presentation real viewport buttons and Escape", ViewportAndEscape),
  ("Jigsaw presentation approved editor controls and help wheel isolation", ApprovedControls),
  ("Jigsaw presentation keyboard hints follow the actual device", KeyboardHints),
  ("Jigsaw presentation native art and paused input ownership", NativeArtAndPause),
  ("Jigsaw presentation first sentence is complete and profile resumes", FirstSentence),
  ("Jigsaw presentation actual input does not leak and help cancels preserves votes", InputHelp),
  ("Jigsaw presentation focus reaches isolated dense nodes and responsive layouts", NavigationLayout),
  ("Jigsaw presentation manual errors retain both five second segments", FailureTiming),
  ("Jigsaw presentation routed tutorial input is isolated", RoutedInput),
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
 private static HoverTip? capturedStepTip;
 private static bool CaptureStepTip(Control owner,IHoverTip hoverTip,ref NHoverTipSet? __result)
 {
  if(owner is not JigsawStepCounter)return true;
  capturedStepTip=(HoverTip)hoverTip;__result=null;return false;
 }
 private static async Task StepBadge()
 {
  using var atlases=HeadlessTestSetup.LoadAtlases("relic_atlas");
  var tree=(SceneTree)Engine.GetMainLoop();var original=tree.Root.Size;tree.Root.Size=new(1280,720);
  var map=JigsawRun.Create(74);map.Take(1,0);map.Drop(1,new(3,0));map.AddEdge(1,0,1);map.ChooseEnd(1,EndChoice.Manual);map.Inspect();map.CompleteAncient(1);map.Continue(1);
  var bar=new Control {Size=new(1280,80)};var boss=new Control {Position=new(500,8),Size=new(48,48)};bar.AddChild(boss);
  var secondBoss=new TextureRect {Position=new(34,0),Size=new(46,48),MouseFilter=Control.MouseFilterEnum.Ignore};boss.AddChild(secondBoss);
  var floor=new Label {Text="11"};floor.AddThemeFontOverride("font",new SystemFont {FontNames=["Arial"]});floor.AddThemeFontSizeOverride("font_size",32);floor.AddThemeConstantOverride("outline_size",3);bar.AddChild(floor);
  var potions=new Control {Position=new(180,8),Size=new(128,48)};bar.AddChild(potions);
  var badge=JigsawStepCounter.Attach(bar,boss,()=>map,floor,potions);var view=View(map,new(){Sentence=6});
  var hoverFactory=typeof(NHoverTipSet).GetMethods().Single(m=>m.Name=="CreateAndShow"&&m.GetParameters()[1].ParameterType==typeof(IHoverTip));
  var tipHost=new Harmony("AllMatters.StepTipHost");capturedStepTip=null;
  tipHost.Patch(hoverFactory,prefix:new HarmonyMethod(typeof(JigsawPresentationTests),nameof(CaptureStepTip)));
  try
  {
   var nativeReady=HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar),"_Ready");
   var hostPatch=new HarmonyLib.Harmony("AllMatters.StepCounterHost");
   var nativeBar=new MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar {Name="JigsawCounterHost"};
   var nativeBoss=new MegaCrit.sts2.Core.Nodes.TopBar.NTopBarBossIcon();
   HarmonyLib.AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar),"BossIcon").SetValue(nativeBar,nativeBoss);
   try
   {
    hostPatch.Patch(nativeReady,prefix:new HarmonyLib.HarmonyMethod(typeof(JigsawPresentationTests),nameof(SkipNativeTopBarSetup)));
    await HeadlessUiHost.Mount(nativeBar);
    Require(nativeBar.GetNodeOrNull<JigsawStepCounter>("AllMattersSteps")!=null,"The engine's actual native Ready callback invokes the production top-bar hook.");
   }
   finally {hostPatch.Unpatch(nativeReady,HarmonyLib.HarmonyPatchType.Prefix,"AllMatters.StepCounterHost");nativeBar.Free();nativeBoss.Free();}
   await HeadlessUiHost.Mount(bar,view);
   foreach(int slots in new[]{2,4,6})
   {
    potions.Size=new(slots*64,48);boss.Position=new(500+(slots-2)*64,8);
    foreach(bool dualBoss in new[]{false,true})
    {
     secondBoss.Visible=dualBoss;
     await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
     float bossRight=dualBoss?secondBoss.GetGlobalRect().End.X:boss.GetGlobalRect().End.X;
     Require(badge.GetGlobalRect().Position.X>=Math.Max(bossRight,potions.GetGlobalRect().End.X)+10,"The step badge clears all visible Boss artwork after potion expansion.");
    }
   }
   potions.Size=new(900,48);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(badge.GetGlobalRect().Position.X>=potions.GetGlobalRect().End.X+10,"A larger potion row cannot extend beneath the step badge.");
   potions.Size=new(128,48);boss.Position=new(500,8);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   var stepNumber=badge.GetChild<Label>(1);
   Require(stepNumber.GetThemeFont("font")==floor.GetThemeFont("font")&&stepNumber.GetThemeFontSize("font_size")==floor.GetThemeFontSize("font_size")&&stepNumber.GetThemeConstant("outline_size")==floor.GetThemeConstant("outline_size"),"The step number matches the floor number's resolved font, size and outline.");
   Require(badge.Visible&&badge.TooltipText.Length==0,"The native top-bar adapter shows the current budget tooltip.");
   Require(badge.GetChild<TextureRect>(0).Texture==MegaCrit.Sts2.Core.Models.ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.WingedBoots>().Icon,"Step icon is the native Winged Boots art.");
   tree.Root.PushInput(new InputEventMouseMotion {Position=badge.GetGlobalRect().GetCenter(),GlobalPosition=badge.GetGlobalRect().GetCenter()},true);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(tree.Root.GuiGetHoveredControl()==badge,"The map overlay leaves native top-bar hover reachable.");
   Require(capturedStepTip!=null,"Engine hover invokes the same native hover-tip factory used by the Boss icon.");
   var attempt=map.BeginEnter(map.AvailableDestinations.First())!;map.ConfirmEnter(attempt.Id);map.CompleteRoom(attempt.Id);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(badge.GetChild<Label>(1).Text=="14"&&capturedStepTip?.Description.Contains("14/15")==true,"Step number and hover text update together after confirmed entry.");
   if(System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} capture)
   {
    tree.Root.MoveChild(bar,tree.Root.GetChildCount()-1); // Native run.tscn also draws TopBar after the map screen.
    await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    tree.Root.GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(capture,"topbar.png"));
   }
  }
  finally {view.Free();bar.Free();tipHost.Unpatch(hoverFactory,HarmonyPatchType.Prefix,"AllMatters.StepTipHost");capturedStepTip=null;tree.Root.Size=original;}
 }
 private static bool SkipNativeTopBarSetup(MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar __instance)=>__instance.Name!="JigsawCounterHost";
 private static async Task ExplorationVisuals()
 {
  using var atlases=HeadlessTestSetup.LoadAtlases("ui_atlas","compressed");
  var tree=(SceneTree)Engine.GetMainLoop();var original=tree.Root.Size;tree.Root.Size=new(1280,720);
  var map=JigsawRun.Create(74);map.Take(1,0);map.Drop(1,new(3,0));map.AddEdge(1,0,1);map.ChooseEnd(1,EndChoice.Manual);map.Inspect();map.CompleteAncient(1);map.Continue(1);
  var view=View(map,new(){Sentence=6});var originalPause=tree.Paused;
  try
  {
   await HeadlessUiHost.Mount(view);
   Require(!view.GetChildren().SelectMany(n=>n is Control c?c.FindChildren("*","Button",true,false):new Godot.Collections.Array<Node>()).OfType<Button>().Any(b=>b.Text=="帮助"&&b.IsVisibleInTree()),"Exploration hides the redundant Help entry.");
   int entered=map.AvailableDestinations.First();var attempt=map.BeginEnter(entered)!;Require(map.ConfirmEnter(attempt.Id).Success&&map.CompleteRoom(attempt.Id).Success,"Complete one actual logical destination.");view.Refresh();
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(view.MapControl.GetNodeOrNull<Sprite2D>($"Room{entered}/Visited") is {Visible:true,Texture:not null},"A completed ordinary destination has a visible native visit ring in the production map.");
   int available=map.AvailableDestinations.First(id=>map.Nodes.First(n=>n.Id==id).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss));
   var icon=view.MapControl.GetNodeOrNull<Sprite2D>($"Room{available}");Require(icon!=null,"Available ordinary room has a rendered icon.");
   Require(view.MapControl.GetChildren().OfType<JigsawOverlay>().Single().ZIndex>icon!.ZIndex,"Focus and holding markers render above icons and visit rings.");
   tree.Paused=true;view.SetProcess(false);
   var scales=new List<float>();for(int i=0;i<12;i++){await tree.ToSignal(tree.CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);scales.Add(icon!.Scale.X);}
   view.SetProcess(true);
   Require(scales.Max()-scales.Min()>.02f,"The available ordinary icon visibly pulses across rendered frames.");
   if(System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} exploreCapture)
   {
    await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    System.GC.Collect();System.GC.WaitForPendingFinalizers();
    await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    var frame=tree.Root.GetTexture().GetImage();var sample=view.MapControl.GetGlobalRect().Position+view.MapControl.Size*.1f;
    var paperColor=frame.GetPixel((int)sample.X,(int)sample.Y);
    Require(paperColor.R>paperColor.B+.1f,"Parchment remains warm textured art after resource collection, rather than white missing-texture output.");
    frame.SavePng(System.IO.Path.ChangeExtension(exploreCapture,"explore.png"));
   }
  }
  finally {tree.Paused=originalPause;view.Free();tree.Root.Size=original;}
 }
 private static async Task ViewportAndEscape()
 {
  var tree=(SceneTree)Engine.GetMainLoop();var original=tree.Root.Size;tree.Root.Size=new(1920,1080);
  int pauses=0;var example=JigsawRun.Create(74);example.Take(1,0);example.Drop(1,new(3,0));example.AddEdge(1,0,1);var view=View(example,new(){Sentence=6},()=>pauses++);
  try
  {
   await HeadlessUiHost.Mount(view);
   if(System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} capture)
   {
    await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    tree.Root.GetTexture().GetImage().SavePng(capture);
    foreach(var button in view.DecisionControl.GetChildren().OfType<Button>().Where(b=>b.Visible))
    {
     Console.WriteLine($"CAPTURE {button.Text}: {button.Size}");
     foreach(var child in button.GetChildren().OfType<Control>())Console.WriteLine($"CAPTURE child {child.GetType().Name}: {child.Size}");
    }
   }
   foreach(var button in view.DecisionControl.GetChildren().OfType<Button>().Where(b=>b.Visible))
    Require(button.Size.X>=220 && button.Size.Y<=160,"Ending buttons must have readable horizontal width and bounded height at 1080p.");
   view.OpenHelp();
   var markerLayer=view.MapControl.GetChildren().OfType<JigsawOverlay>().Single();
   var shade=view.GetNode<ColorRect>("JigsawHelpShade");var panel=view.GetChildren().OfType<PanelContainer>().Single();
   Require(shade.ZIndex>markerLayer.ZIndex&&panel.ZIndex>shade.ZIndex,"Help shade and panel render above map markers.");
   if(System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} helpCapture)
   {
    await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
    tree.Root.GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(helpCapture,"help.png"));
   }
   tree.Root.PushInput(new InputEventAction {Action=MegaInput.pauseAndBack,Pressed=true},true);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(!view.Reading && pauses==0,"Native Escape action closes Help before opening pause.");
   tree.Root.PushInput(new InputEventKey {PhysicalKeycode=Key.Escape,Pressed=true},true);
   await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
   Require(pauses==1,"A second physical Escape reaches native pause for save/exit.");
  }
  finally {view.Free();tree.Root.Size=original;}
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
   Require(view.GridToCanvas(new(1,0)).DistanceTo(view.GridToCanvas(new(0,0)))>=30,"Wide maps keep readable nodes; panning replaces forced tiny whole-map fitting.");
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
   var renderedBoss=view.MapControl.GetChildren().OfType<Node2D>().Single(n=>n.Name.ToString().StartsWith("JigsawBoss"));
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
    Require(view.PoolControl.GetChild<VBoxContainer>(0).GetChildren().OfType<Button>().Skip(1).All(b=>b.Size.Y>=Math.Max(100,100*size.Y/1080)),"Pool preview rows scale with the viewport rather than retaining their initial tiny height.");
    Require(view.PoolControl.GetGlobalRect().Position.Y>=size.Y*.09f,"The editor must leave the native top HUD clear.");
    Require(view.DialogControl.GetGlobalRect().End.Y<=size.Y+1&&view.DecisionControl.GetGlobalRect().End.Y<=size.Y+1,"Footer must fit supported viewport");
    view.ShowNotice(new string('长',500)); await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
    Require(view.DialogControl.Size.X<size.X&&view.DecisionControl.GetGlobalRect().End.X<=size.X+1,"Long text must remain scrollable without obscuring decisions");
    if(System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} layoutCapture)
    {
     await tree.ToSignal(tree.CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
     tree.Root.GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(layoutCapture,$"{size.X}x{size.Y}.png"));
    }
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
  Require(!view.FindChildren("*","Button",true,false).OfType<Button>().Any(b=>b.Text=="继续前行"&&b.IsVisibleInTree()),"Exploration has no redundant Continue button.");
  Require(!view.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text.Contains("你的愿望实现了")&&l.IsVisibleInTree()),"Exploration hides the completed Ancient dialogue.");
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
   Click(new(0,0)); Click(new(0,2)); Click(new(6,0)); Click(new(0,2));
   Require(map.SelectedEndpoints.GetValueOrDefault(1UL,-1)==0,"An invalid second endpoint interrupts two consecutive blanks and preserves the first endpoint.");
   Click(new(0,2)); Require(!map.SelectedEndpoints.ContainsKey(1),"Only consecutive double blanks cancel the first endpoint.");
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
   Require(content.GetChildren().OfType<Button>().Select(b=>b.Text).SequenceEqual(new[]{"操作与规则","返回"}),"Help contains only the requested rules and return controls.");
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
