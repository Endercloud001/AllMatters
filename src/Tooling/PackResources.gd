extends SceneTree

# Texture imports remain native Godot resources; scenes and Spine atlas/skeleton
# files remain raw so the game's Spine extension can load them at runtime.
func _initialize():
    var packer = PCKPacker.new()
    var result = packer.pck_start(OS.get_cmdline_user_args()[0])
    if result == OK:
        result = add_directory(packer, "res://AllMatters")
    if result == OK:
        result = add_directory(packer, "res://.godot/imported")
    if result == OK:
        result = packer.flush()
    if result != OK:
        push_error("AllMatters resource pack failed: %s" % result)
    quit(0 if result == OK else 1)

func add_directory(packer: PCKPacker, path: String) -> Error:
    var directory = DirAccess.open(path)
    if directory == null:
        return DirAccess.get_open_error()
    for file in directory.get_files():
        if file.ends_with(".md5") or file.ends_with(".uid"):
            continue
        var source = path.path_join(file)
        var result = packer.add_file(source, source)
        if result != OK:
            push_error("Cannot pack %s" % source)
            return result
    for child in directory.get_directories():
        var result = add_directory(packer, path.path_join(child))
        if result != OK:
            return result
    return OK
