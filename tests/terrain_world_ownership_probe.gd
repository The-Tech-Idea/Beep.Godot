extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var builds := 0

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, parent: Node, node_name: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	node.name = node_name
	for key in properties:
		node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var a := Node2D.new()
	a.name = "A"
	host.add_child(a)
	var b := Node2D.new()
	b.name = "B"
	host.add_child(b)
	var cells_a := make("grid/GridCellDataComponent", a, "Cells")
	var cells_b := make("grid/GridCellDataComponent", b, "Cells")
	cells_a.call("SetTerrainKind", Vector2i.ZERO, "grass")
	cells_b.call("SetTerrainKind", Vector2i.ZERO, "desert")
	var object_a := make("grid/GridObjectComponent", a, "Object", {"ObjectId": "a"})
	var object_b := make("grid/GridObjectComponent", b, "Object", {"ObjectId": "b"})
	var snapshot := make("grid/GridWorldStateComponent", host, "Snapshot", {
		"ParticipatesInSave": false, "ObjectsRootPath": NodePath("../A")})
	var saved_a: Dictionary = snapshot.call("CaptureState")
	check(saved_a.grid_objects.size() == 1, "Snapshot captured another world's objects")
	snapshot.set("ObjectsRootPath", NodePath("../B"))
	var saved_b: Dictionary = snapshot.call("CaptureState")
	check(saved_b.grid_objects.size() == 1 and saved_b.grid_objects[0].state.object_id == "b",
		"Snapshot retained the previous root")
	check(saved_a.cell_chunks != saved_b.cell_chunks, "Automatic cell lookup ignored the snapshot root")
	object_a.set("ObjectId", "a-edited")
	var restore_state := saved_b.duplicate(true)
	restore_state.grid_objects = saved_a.grid_objects
	var restored := [false]
	snapshot.connect("StateRestored", func(): restored[0] = true)
	snapshot.call("RestoreState", restore_state)
	check(restored[0], "World restore did not complete")
	check(object_a.get("ObjectId") == "a-edited", "Restore modified an object outside its root")
	snapshot.set("ObjectsRootPath", NodePath("../Missing"))
	check(snapshot.call("CaptureState").grid_objects.is_empty(), "Missing root captured all scene objects")
	snapshot.set("CellDataPath", NodePath("../A/Cells"))
	var first: Dictionary = snapshot.call("CaptureState")
	snapshot.set("CellDataPath", NodePath("../B/Cells"))
	var second: Dictionary = snapshot.call("CaptureState")
	check(first.cell_chunks != second.cell_chunks, "Explicit cell path retained a live stale reference")
	check(object_b.get("ObjectId") == "b", "World B identity changed unexpectedly")

	make("terrain/TerrainGeneratorComponent", host, "Generator", {"GenerateOnReady": false})
	var world := make("terrain/TerrainWorldComponent", host, "World", {
		"GeneratorPath": NodePath("../Generator"), "MapSize": 0, "ParticipatesInSave": false})
	world.connect("WorldBuilt", func(_size: Vector2i): builds += 1)
	var label := Label.new()
	label.name = "Status"
	host.add_child(label)
	var lab := make("terrain/TerrainLabComponent", host, "Lab", {
		"WorldPath": NodePath("../World"), "StatusPath": NodePath("../Status")})
	await process_frame
	await process_frame
	check(builds == 1, "World and lab generated the same map twice at startup")
	check(label.text == world.call("StatusLine"), "Lab missed the world's startup report")
	lab.free()
	lab = make("terrain/TerrainLabComponent", host, "Lab", {"WorldPath": NodePath("../World")})
	await process_frame
	check(builds == 1, "Opening a lab regenerated an already-built world")
	var status := make("terrain/TerrainWorldStatusComponent", host, "Reporter", {
		"WorldPath": NodePath("../World"), "LabelPath": NodePath("../Status")})
	check(label.text == world.call("StatusLine"), "Late status helper stayed on generating")
	var unbuilt := make("terrain/TerrainWorldComponent", host, "Unbuilt", {
		"BuildOnReady": false, "ParticipatesInSave": false})
	status.set("WorldPath", NodePath("../Unbuilt"))
	check(label.text == status.get("PendingText"), "Status did not rebind to another live world")
	label.text = "sentinel"
	world.emit_signal("WorldBuilt", Vector2i(32, 32))
	check(label.text == "sentinel", "Status stayed subscribed to its previous world")
	unbuilt.emit_signal("WorldBuilt", Vector2i.ZERO)
	check(label.text == status.get("PendingText"), "Status missed its replacement world's signal")
	# A scene may explicitly build after adding a BuildOnReady world, before its deferred call.
	var explicit_world := make("terrain/TerrainWorldComponent", host, "ExplicitWorld", {
		"GeneratorPath": NodePath("../Generator"), "MapSize": 0, "ParticipatesInSave": false})
	var explicit_builds := [0]
	explicit_world.connect("WorldBuilt", func(_size: Vector2i): explicit_builds[0] += 1)
	explicit_world.call("NewWorld")
	await process_frame
	await process_frame
	check(explicit_builds[0] == 1, "Deferred startup regenerated an explicitly built world")
	explicit_world.call("NewWorld")
	check(explicit_builds[0] == 2, "Explicit regeneration stopped working after startup")
	await check_saved_tile_view()
	host.free()
	print("[terrain-world-ownership] OK" if failures.is_empty() else "[terrain-world-ownership] FAILED")
	quit(0 if failures.is_empty() else 1)

func check_saved_tile_view() -> void:
	var scene := Node2D.new()
	scene.name = "SavedTileWorld"
	root.add_child(scene)
	var cells := make("grid/GridCellDataComponent", scene, "Cells")
	cells.owner = scene
	var view := make("terrain/TerrainTileRendererComponent", scene, "View", {
		"RefreshOnReady": false, "CellDataPath": NodePath("../Cells"),
		"BoundsSize": Vector2i(3, 3),
		"GrassAtlasPath": "res://addons/beep_game_builder_cs/textures/tiles/grass_15piece.png"})
	view.owner = scene
	# An exact generated-name collision must remain an authored, untouched layer.
	var authored := TileMapLayer.new()
	authored.name = "GrassTiles"
	view.add_child(authored)
	authored.owner = scene
	authored.position = Vector2(123, 456)
	view.call("Rebuild")
	for cycle in range(3):
		var packed := PackedScene.new()
		check(packed.pack(scene) == OK, "Could not pack tile world")
		scene.free()
		scene = packed.instantiate()
		root.add_child(scene)
		view = scene.get_node("View")
		view.call("Rebuild")
		await process_frame
		await process_frame
		var generated := []
		for child in view.get_children():
			if child.get_meta("_terrain_tile_biome_display", false):
				generated.append(child)
		check(generated.size() == 1, "Saved tile world duplicated or lost generated displays")
		if generated.size() == 1:
			check(generated[0].get_child_count() == 1, "Saved display duplicated transition components")
			check(not generated[0].get_used_cells().is_empty(), "Saved display did not redraw live cells")
			check(generated[0].owner == scene, "Rebuilt display no longer belongs to saved scene")
		authored = view.get_node("GrassTiles")
		check(authored.position == Vector2(123, 456) and authored.get_child_count() == 0,
			"Generated-name collision modified authored layer")
	view.set("GrassAtlasPath", "")
	view.call("Rebuild")
	await process_frame
	check(view.get_child_count() == 1 and view.get_child(0) == authored,
		"Removing saved biome left stale displays or removed authored layer")
	scene.free()
