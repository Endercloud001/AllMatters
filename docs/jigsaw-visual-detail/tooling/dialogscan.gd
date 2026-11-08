extends SceneTree
func scan(p:String):
 for n in DirAccess.get_files_at(p):
  if n.ends_with("tscn"):
   var s=FileAccess.get_file_as_string(p+"/"+n)
   if s.contains("dialogue_nine_patch") or n.contains("ancient") or n.contains("confirm_button"):
    FileAccess.open("res://native_"+n,FileAccess.WRITE).store_string(s)
    print(p+"/"+n)
 for d in DirAccess.get_directories_at(p):scan(p+"/"+d)
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck",false)
 scan("res://scenes")
 quit()
