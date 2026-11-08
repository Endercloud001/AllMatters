extends SceneTree
func _initialize():
 ProjectSettings.load_resource_pack("E:/SteamLibrary/steamapps/common/Slay the Spire 2/SlayTheSpire2.pck", false)
 var paths = ["images/packed/common_ui/ancient_event_option_button.png","images/packed/common_ui/event_button.png","images/packed/common_ui/submenu_panel.png","images/packed/common_ui/submenu_panel_short.png","images/packed/inspect_relic_screen/relic_inspect_frame.png","images/ui/reward_screen/reward_panel.png","images/ui/reward_screen/reward_banner.png","images/packed/run_history/banner.png","images/atlases/ui_atlas.sprites/card/card_banner.tres","images/packed/map/map_bgs/overgrowth/map_top_overgrowth.png","images/packed/map/map_bgs/overgrowth/map_middle_overgrowth.png","images/packed/map/map_bgs/overgrowth/map_bottom_overgrowth.png"]
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
