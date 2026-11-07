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
 private bool hovering;
 private Func<JigsawRun?> current=()=>JigsawNativeSession.Current?.Map;
 private readonly Label number=new() {VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
 internal static JigsawStepCounter Attach(Control bar,Control boss,Func<JigsawRun?>? source=null)
 {
  if(bar.GetNodeOrNull<JigsawStepCounter>("AllMattersSteps") is {} existing)return existing;
  var counter=new JigsawStepCounter {Name="AllMattersSteps",anchor=boss,MouseFilter=MouseFilterEnum.Stop,ProcessMode=ProcessModeEnum.Always};
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
  var bar=(Control)GetParent();var rect=anchor.GetGlobalRect();
  Position=bar.GetGlobalTransformWithCanvas().AffineInverse()*(rect.Position+new Vector2(rect.Size.X+12,0));
  Size=new(Math.Max(72,GetCombinedMinimumSize().X),Math.Max(36,anchor.Size.Y));
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
 private static void Postfix(NTopBar __instance)=>JigsawStepCounter.Attach(__instance,__instance.BossIcon);
}
