extends SceneTree
func scan(p:String):
 for n in DirAccess.get_files_at(p):
  if n.contains("avatar") or n.contains("map_circle") or n.contains("map_monster") or n.contains("map_point") or n.contains("vote"):print(p+"/"+n)
 for d in DirAccess.get_directories_at(p):scan(p+"/"+d)
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck",false)
 scan("res://images")
 scan("res://scenes")
 quit()
