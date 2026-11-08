extends SceneTree
func save(p:String):
 var t=load(p) as Texture2D
 if t==null: print("MISS ",p);return
 var im:Image
 if t is AtlasTexture:
  im=t.atlas.get_image()
  if im.is_compressed():im.decompress()
  im=im.get_region(t.region)
 else:im=t.get_image()
 if im.is_compressed():im.decompress()
 var out="res://../assets/"+p.get_file().get_basename().replace(".png","")+".png"
 im.save_png(out);print(p," ",im.get_size()," used=",im.get_used_rect())
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck",false)
 for i in 5: save("res://images/atlases/compressed.sprites/map/map_circle_%d.tres"%i)
 save("res://images/atlases/compressed.sprites/map/map_monster_outline.tres")
 for c in ["ironclad","silent","defect","regent","necrobinder"]:
  save("res://images/ui/top_panel/character_icon_%s.png"%c)
  save("res://images/ui/top_panel/character_icon_%s_outline.png"%c)
 print(FileAccess.get_file_as_string("res://scenes/ui/selection_reticle.tscn").substr(0,1800))
 print(FileAccess.get_file_as_string("res://scenes/ui/multiplayer_vote_container.tscn").substr(0,900))
 quit()
