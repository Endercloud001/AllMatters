extends SceneTree
func scan(p:String):
 for n in DirAccess.get_files_at(p):
  if n.ends_with("tscn") and (n.contains("reward") or n.contains("map") or n.contains("event")):
   var s=FileAccess.get_file_as_string(p+"/"+n)
   if s.contains("reward_panel") or s.contains("event_button") or s.contains("map_top"):
    FileAccess.open("res://"+n,FileAccess.WRITE).store_string(s)
    print(p+"/"+n)
 for d in DirAccess.get_directories_at(p):scan(p+"/"+d)
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck",false)
 scan("res://scenes")
 quit()
