extends SceneTree
func scan(p:String):
 for n in DirAccess.get_files_at(p):
  var l=n.to_lower()
  if (l.contains("character_icon") or l.contains("char_select") or l.contains("portrait") or l.contains("head")) and (l.ends_with(".tres") or l.ends_with(".png.import")):print(p+"/"+n)
 for d in DirAccess.get_directories_at(p):scan(p+"/"+d)
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck",false)
 scan("res://images/atlases/ui_atlas.sprites")
 scan("res://images/ui")
 for s in ["res://scenes/ui/multiplayer_vote_icon.tscn","res://scenes/vfx/map_circle_vfx.tscn","res://scenes/ui/normal_map_point.tscn"]:
  print("=== ",s);print(FileAccess.get_file_as_string(s).substr(0,2500))
 quit()
