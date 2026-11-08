extends SceneTree
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck", false)
 var paths = ["images/atlases/ui_atlas.sprites/map/placeholder/knowledge_demon_boss_icon.tres","images/atlases/ui_atlas.sprites/map/ancients/ancient_node_darv.tres"]
 for p in paths:
  var t=load("res://"+p) as Texture2D
  if t:
   var im:Image
   if t is AtlasTexture:
    im=t.atlas.get_image()
    if im.is_compressed():im.decompress()
    im=im.get_region(t.region)
   else:im=t.get_image()
   if im.is_compressed():im.decompress()
   im.save_png("res://../assets/"+p.get_file().get_basename()+".png")
   print(p," ",im.get_size())
 quit()

