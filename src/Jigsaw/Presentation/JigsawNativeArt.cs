using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace AllMatters.Jigsaw.Presentation;

// References game-owned resources; no extracted game art is shipped in the mod.
internal sealed class JigsawNativeArt(ActModel? act)
{
 internal Texture2D Icon(JigsawRoom room) => room==JigsawRoom.Ancient && act!=null ? act.Ancient.MapIcon : Load(room==JigsawRoom.Ancient
  ? "atlases/ui_atlas.sprites/map/ancients/ancient_node_neow.tres"
  : "atlases/ui_atlas.sprites/map/icons/"+(room switch {
   JigsawRoom.Combat=>"map_monster",JigsawRoom.Elite=>"map_elite",JigsawRoom.Shop=>"map_shop",JigsawRoom.Rest=>"map_rest",JigsawRoom.Treasure=>"map_chest",
   JigsawRoom.Boss or JigsawRoom.SecondBoss=>"map_chest_boss",_=>"map_unknown"})+".tres");
 internal static Texture2D Load(string path)=>ResourceLoader.Load<Texture2D>("res://images/"+path);
 internal static StyleBoxTexture Panel(string path,int margin=16)=>new() {
  Texture=Load(path),TextureMarginLeft=margin,TextureMarginRight=margin,TextureMarginTop=margin,TextureMarginBottom=margin,
  ContentMarginLeft=margin,ContentMarginRight=margin,ContentMarginTop=margin,ContentMarginBottom=margin };
 internal void DrawPaper(CanvasItem target,Vector2 size)
 {
  if(act==null)return;
  float cap=Math.Min(size.Y*.15f,80);
  target.DrawTextureRect(act.MapTopBg,new Rect2(0,0,size.X,cap),false);
  target.DrawTextureRect(act.MapMidBg,new Rect2(0,cap,size.X,size.Y-2*cap),false);
  target.DrawTextureRect(act.MapBotBg,new Rect2(0,size.Y-cap,size.X,cap),false);
 }
 internal Node2D? Boss(JigsawRoom room)
 {
  if(act==null)return null;
  var encounter=(room==JigsawRoom.SecondBoss?act.SecondBossEncounter:act.BossEncounter) ?? throw new InvalidOperationException("The active native act has no selected boss.");
  var visual=new Node2D();
  if(encounter.BossNodeSpineResource!=null)
  {
   // Copy the authored visual node, without instantiating the native vertical-map input controller.
   var state=ResourceLoader.Load<PackedScene>("res://scenes/ui/boss_map_point.tscn").GetState();
   int index=Enumerable.Range(0,state.GetNodeCount()).Single(i=>state.GetNodeName(i)=="SpineSprite");
   var sprite=(Node2D)ClassDB.Instantiate(state.GetNodeType(index)).AsGodotObject();
   for(int i=0;i<state.GetNodePropertyCount(index);i++)
   {
    var name=state.GetNodePropertyName(index,i);
    if(name!="script")sprite.Set(name,state.GetNodePropertyValue(index,i));
   }
   visual.AddChild(sprite);sprite.Position=Vector2.Zero;sprite.Show();
   var animation=new MegaSprite(sprite);animation.SetSkeletonDataRes(encounter.BossNodeSpineResource);animation.GetAnimationState().AddAnimation("animation");
  }
  else visual.AddChild(new Sprite2D {Texture=ResourceLoader.Load<Texture2D>(encounter.BossNodePath+".png")});
  return visual;
 }
}
