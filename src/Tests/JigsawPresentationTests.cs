using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
internal static class JigsawPresentationTests
{
 public static readonly (string Name, Func<Task> Test)[] Cases = [
  ("Jigsaw presentation first sentence is complete and profile resumes", FirstSentence),
  ("Jigsaw presentation actual input does not leak and help cancels preserves votes", InputHelp),
  ("Jigsaw presentation focus reaches isolated dense nodes and responsive layouts", NavigationLayout),
  ("Jigsaw presentation manual errors retain both five second segments", FailureTiming),
  ("Jigsaw presentation routed input is isolated and mode action toggles", RoutedInput),
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
 private static void Require(bool ok,string message) { if(!ok)throw new Exception(message); }
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
