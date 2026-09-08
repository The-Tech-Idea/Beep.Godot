@tool
extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	print("[terrain-art-import] waiting for editor filesystem")
	assert(Engine.is_editor_hint(), "Run with --headless --editor --script")
	var filesystem := EditorInterface.get_resource_filesystem()
	assert(filesystem != null)
	while filesystem.is_scanning() or filesystem.is_importing():
		await process_frame
	var paths := OS.get_cmdline_user_args()
	if paths.is_empty():
		paths = PackedStringArray(["res://addons/beep_game_builder_cs/textures/iso/voxel_tops_seamless.png"])
	filesystem.reimport_files(paths)
	for path in paths:
		assert(ResourceLoader.load(path) is Texture2D, "Terrain texture was not imported: " + path)
	print("[terrain-art-import] OK")
	quit()
