extends SceneTree
func scan(p:String):
 for n in DirAccess.get_files_at(p):
  if p.contains("common_ui") or p.contains("font") or n.contains("panel") or n.contains("frame") or n.contains("banner"):
   print(p+"/"+n)
 for d in DirAccess.get_directories_at(p): scan(p+"/"+d)
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck", false)
 scan("res://images")
 scan("res://fonts")
 quit()
