using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.Jigsaw.Presentation;

public partial class JigsawStepCounter : HBoxContainer
{
 private Control anchor=null!;
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
  bar.AddChild(counter);counter.UpdateCounter();return counter;
 }
 private void UpdateCounter()
 {
  var map=current();Visible=map?.Phase is JigsawPhase.Ancient or JigsawPhase.Exploring;
  if(!Visible)return;
  number.Text=map!.RemainingSteps.ToString();TooltipText=$"剩余步数 {map.RemainingSteps}/{map.Budget}";
  var bar=(Control)GetParent();var rect=anchor.GetGlobalRect();
  Position=bar.GetGlobalTransformWithCanvas().AffineInverse()*(rect.Position+new Vector2(rect.Size.X+12,0));
  Size=new(Math.Max(72,GetCombinedMinimumSize().X),Math.Max(36,anchor.Size.Y));
 }
 public override void _Process(double delta)=>UpdateCounter();
}

[HarmonyPatch(typeof(NTopBar),nameof(NTopBar._Ready))]
internal static class JigsawTopBarPatch
{
 private static void Postfix(NTopBar __instance)=>JigsawStepCounter.Attach(__instance,__instance.BossIcon);
}
