extends SceneTree

const IMPORT_DIR = "res://.godot/imported"
const EVENT_PORTRAIT_SOURCES = [
    "res://AllMatters/images/events/augmenter.png",
    "res://AllMatters/images/events/cleric.png",
    "res://AllMatters/images/events/colosseum.png",
    "res://AllMatters/images/events/cursed_tome.png",
    "res://AllMatters/images/events/mind_bloom.png",
    "res://AllMatters/images/events/old_beggar.png",
    "res://AllMatters/images/events/the_divine_fountain.png",
    "res://AllMatters/images/events/the_library.png",
    "res://AllMatters/images/events/tomb_of_lord_red_mask.png",
    "res://AllMatters/images/events/wheel_of_change.png",
]

# Texture imports remain native Godot resources; scenes and Spine atlas/skeleton
# files remain raw so the game's Spine extension can load them at runtime.
func _initialize():
    var packer = PCKPacker.new()
    var result = validate_event_portrait_imports()
    if result == OK:
        result = packer.pck_start(OS.get_cmdline_user_args()[0])
    if result == OK:
        result = add_directory(packer, "res://AllMatters")
    if result == OK:
        result = add_directory(packer, IMPORT_DIR)
    if result == OK:
        result = packer.flush()
    if result != OK:
        push_error("AllMatters resource pack failed: %s" % result)
    quit(0 if result == OK else 1)

# Validate before pck_start opens the output. The shipped .import remaps
# each original resource path to its compiled texture.
func validate_event_portrait_imports() -> Error:
    for source in EVENT_PORTRAIT_SOURCES:
        var import_path = source + ".import"
        var config = ConfigFile.new()
        var result = config.load(import_path)
        if result != OK:
            push_error("Cannot read event portrait import %s: %s" % [import_path, error_string(result)])
            return result
        var target = config.get_value("remap", "path", "")
        var destinations = config.get_value("deps", "dest_files", [])
        if (
            not target is String
            or target.get_base_dir() != IMPORT_DIR
            or not target.ends_with(".ctex")
            or config.get_value("deps", "source_file", "") != source
            or not destinations is Array
            or target not in destinations
        ):
            push_error("Invalid event portrait remap in %s: %s" % [import_path, target])
            return ERR_INVALID_DATA
        if not FileAccess.file_exists(target):
            push_error("Missing event portrait texture for %s: %s" % [import_path, target])
            return ERR_FILE_NOT_FOUND
    return OK

func add_directory(packer: PCKPacker, path: String) -> Error:
    var directory = DirAccess.open(path)
    if directory == null:
        return DirAccess.get_open_error()
    for file in directory.get_files():
        if file.ends_with(".md5") or file.ends_with(".uid"):
            continue
        var source = path.path_join(file)
        if source in EVENT_PORTRAIT_SOURCES:
            continue
        var result = packer.add_file(source, source)
        if result != OK:
            push_error("Cannot pack %s" % source)
            return result
    for child in directory.get_directories():
        var result = add_directory(packer, path.path_join(child))
        if result != OK:
            return result
    return OK
