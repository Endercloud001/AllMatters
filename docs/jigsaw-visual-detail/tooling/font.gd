extends SceneTree
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck", false)
 for n in ["SourceHanSerifSC-Bold.otf","SourceHanSerifSC-Medium.otf"]:
  var f=load("res://fonts/zhs/"+n) as FontFile
  if f:
   var o=FileAccess.open("res://../assets/"+n,FileAccess.WRITE)
   o.store_buffer(f.data)
   print(n," ",f.data.size())
 quit()
