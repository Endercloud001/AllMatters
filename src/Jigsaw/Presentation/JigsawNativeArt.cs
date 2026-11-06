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
 internal static void DrawPath(CanvasItem target,Vector2 start,Vector2 end,float cell,Color color)
 {
  var delta=end-start;
  if(delta.Length()<1)return;
  var texture=Load("atlases/compressed.sprites/map/map_dot.tres");
  int count=Math.Max(2,(int)Math.Ceiling(delta.Length()/(cell*.26f)));
  var size=new Vector2(cell*.14f,cell*.25f);
  for(int i=1;i<count;i++)
  {
   target.DrawSetTransform(start.Lerp(end,(float)i/count),delta.Angle()-Mathf.Pi/2);
   target.DrawTextureRect(texture,new Rect2(-size/2,size),false,color);
  }
  target.DrawSetTransform(Vector2.Zero);
 }
 internal static Rect2 BossBounds(Node2D visual)
 {
  var sprite=visual.GetChild<Node2D>(0);
  return sprite is Sprite2D image ? image.GetRect() : new MegaSprite(sprite).GetSkeleton().GetBounds();
 }
 internal static void FitBoss(Node2D visual,Vector2 center,float side)
 {
  var bounds=BossBounds(visual);
  float extent=Math.Max(bounds.Size.X,bounds.Size.Y);
  if(extent<=0) {visual.Hide();return;}
  visual.Show();
  float scale=side*.86f/extent;
  visual.Scale=Vector2.One*scale;
  visual.Position=center-bounds.GetCenter()*scale;
 }
 internal void DrawPaper(CanvasItem target,Vector2 size)
 {
  if(act==null)return;
  float cap=size.Y*.38f;
  target.DrawTextureRect(act.MapTopBg,new Rect2(0,0,size.X,cap),false);
  target.DrawTextureRect(act.MapMidBg,new Rect2(0,cap,size.X,size.Y*.24f),false);
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
