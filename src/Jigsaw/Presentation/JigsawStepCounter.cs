using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.Jigsaw.Presentation;

public partial class JigsawStepCounter : HBoxContainer
{
 private Control anchor=null!;
 private Label? floorNumber;
 private Control? potionRow;
 private bool hovering;
 private Func<JigsawRun?> current=()=>JigsawNativeSession.Current?.Map;
 private readonly Label number=new() {VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
 internal static JigsawStepCounter Attach(Control bar,Control boss,Func<JigsawRun?>? source=null,Label? floor=null,Control? potions=null)
 {
  if(bar.GetNodeOrNull<JigsawStepCounter>("AllMattersSteps") is {} existing)return existing;
  var counter=new JigsawStepCounter {Name="AllMattersSteps",anchor=boss,floorNumber=floor,potionRow=potions,MouseFilter=MouseFilterEnum.Stop,ProcessMode=ProcessModeEnum.Always};
  if(source!=null)counter.current=source;
  counter.AddThemeConstantOverride("separation",6);
  counter.AddChild(new TextureRect {Texture=ModelDb.Relic<WingedBoots>().Icon,CustomMinimumSize=new(36,36),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore});
  counter.number.AddThemeFontSizeOverride("font_size",22);counter.AddChild(counter.number);
  counter.MouseEntered+=counter.ShowTip;counter.MouseExited+=counter.HideTip;counter.TreeExiting+=counter.HideTip;
  bar.AddChild(counter);counter.UpdateCounter();return counter;
 }
 private void UpdateCounter()
 {
  var map=current();Visible=map?.Phase is JigsawPhase.Ancient or JigsawPhase.Exploring;
  if(!Visible) {if(hovering)HideTip();return;}
  string value=map!.RemainingSteps.ToString();bool changed=number.Text!=value;number.Text=value;
  if(changed&&hovering)ShowTip();
  MatchFloorText();
  var bar=(Control)GetParent();var rect=VisibleBounds(anchor);
  float right=rect.End.X;
  if(potionRow is {} potions)right=Math.Max(right,VisibleBounds(potions).End.X);
  Position=bar.GetGlobalTransformWithCanvas().AffineInverse()*new Vector2(right,anchor.GetGlobalRect().Position.Y)+new Vector2(12,0);
  Size=new(Math.Max(72,GetCombinedMinimumSize().X),Math.Max(36,anchor.Size.Y));
 }
 private static Rect2 VisibleBounds(Control control)
 {
  var bounds=control.GetGlobalRect();
  foreach(var child in control.FindChildren("*","Control",true,false).OfType<Control>())
   if(child.IsVisibleInTree())bounds=bounds.Merge(child.GetGlobalRect());
  return bounds;
 }
 private void MatchFloorText()
 {
  if(floorNumber is not {} floor)return;
  var font=floor.GetThemeFont("font");
  if(number.GetThemeFont("font")!=font)number.AddThemeFontOverride("font",font);
  int size=floor.GetThemeFontSize("font_size");
  if(number.GetThemeFontSize("font_size")!=size)number.AddThemeFontSizeOverride("font_size",size);
  int outline=floor.GetThemeConstant("outline_size");
  if(number.GetThemeConstant("outline_size")!=outline)number.AddThemeConstantOverride("outline_size",outline);
  foreach(var key in new[]{"font_color","font_outline_color"})
   if(number.GetThemeColor(key)!=floor.GetThemeColor(key))number.AddThemeColorOverride(key,floor.GetThemeColor(key));
 }
 private void ShowTip()
 {
  var map=current();if(!Visible||map==null)return;
  hovering=true;NHoverTipSet.Remove(this);
  var description=new LocString("static_hover_tips","AM_JIGSAW_STEPS.description");description.Add("Remaining",map.RemainingSteps);description.Add("Budget",map.Budget);
  var tip=new HoverTip(new LocString("static_hover_tips","AM_JIGSAW_STEPS.title"),description);
  NHoverTipSet.CreateAndShow(this,tip)?.SetGlobalPosition(GlobalPosition+new Vector2(0,Size.Y+20));
 }
 private void HideTip() {hovering=false;NHoverTipSet.Remove(this);}
 public override void _Process(double delta)=>UpdateCounter();
}

[HarmonyPatch(typeof(NTopBar),nameof(NTopBar._Ready))]
internal static class JigsawTopBarPatch
{
 private static void Postfix(NTopBar __instance)=>JigsawStepCounter.Attach(__instance,__instance.BossIcon,floor:__instance.FloorIcon?.GetNodeOrNull<Label>("FloorNumLabel"),potions:__instance.PotionContainer);
}
