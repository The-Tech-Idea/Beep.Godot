extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var scene = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn").instantiate()
	var args := OS.get_cmdline_user_args()
	if "--no-water" in args: scene.get_node("World/Iso").set("WaterShaderPath", "")
	if "--no-features" in args:
		scene.get_node("WorldBuilder").set("IsometricFeaturesPath", NodePath())
		# The feature renderer also subscribes to SurfaceRebuilt on entering the tree.
		scene.get_node("World/IsoFeatures").free()
	if "--no-build" in args: scene.get_node("WorldBuilder").set("BuildOnReady", false)
	root.add_child(scene)
	for frame in 5: await process_frame
	if not "--no-diagnostics" in args:
		print(scene.get_node("World/Iso").call("GetLayerDiagnostics"))
	scene.queue_free()
	await process_frame
	await process_frame
	assert(not is_instance_valid(scene))
	print("[terrain-iso-shutdown] unloaded: ", args)
	print("[terrain-iso-shutdown] OK")
	quit()
