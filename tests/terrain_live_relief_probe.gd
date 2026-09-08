extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const ROCKS := "res://addons/beep_game_builder_cs/textures/rocks/rock_debris.png"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	for key in properties:
		node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells := make("grid/GridCellDataComponent", host)
	var replacement := make("grid/GridCellDataComponent", host)
	var layer := TileMapLayer.new()
	layer.tile_set = TileSet.new()
	layer.tile_set.tile_size = Vector2i(96, 48)
	layer.position = Vector2(140, -51)
	layer.rotation = 0.2
	host.add_child(layer)
	var grid := make("grid/GridProjectionComponent", host, {
		"TileMapLayerPath": layer.get_path(), "DrawGrid": false, "TrackMouseCell": false})
	var relief := make("terrain/TerrainReliefRendererComponent", host, {
		"RefreshOnReady": false, "CellDataPath": cells.get_path(), "GridPath": grid.get_path(),
		"BoundsOrigin": Vector2i(-3, 7), "BoundsSize": Vector2i(4, 4),
		"HillsSheetPath": ROCKS, "MountainsSheetPath": ROCKS})
	relief.position = Vector2(-81, 36)
	relief.rotation = -0.1
	relief.scale = Vector2(1.2, 0.8)
	var at := Vector2i(-2, 8)
	cells.call("SetMetadata", at, "terrain_relief", 1)
	relief.call("Rebuild")
	check(relief.get("StampCount") == 2, "Live hill at a nonzero origin was not drawn")
	check(relief.to_global(relief.call("CellPosition", at)).distance_to(grid.call("CellToWorld", at)) < 0.001,
		"Relief and gameplay disagree on transformed rectangular cell centre")
	var captured: Image
	if "--capture" in OS.get_cmdline_user_args():
		var camera := Camera2D.new()
		host.add_child(camera)
		camera.global_position = grid.call("CellToWorld", at)
		camera.zoom = Vector2(4, 4)
		camera.make_current()
		await process_frame
		await RenderingServer.frame_post_draw
		captured = root.get_texture().get_image()
		DirAccess.make_dir_recursive_absolute("res://tests/output/live_relief")
		captured.save_png("res://tests/output/live_relief/hill.png")
	var saved = cells.call("GetCells")
	relief.hide()
	cells.call("SetMetadata", at, "terrain_relief", 0)
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 2, "Hidden relief rebuilt")
	relief.show()
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 0, "Flattened hill stayed visible")
	if captured != null:
		await RenderingServer.frame_post_draw
		var flattened := root.get_texture().get_image()
		flattened.save_png("res://tests/output/live_relief/flattened.png")
		check(captured.get_data() != flattened.get_data(), "Flattening did not change rendered pixels")
	cells.call("LoadCells", saved, true)
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 2, "Restored live relief was not drawn")
	host.remove_child(relief)
	host.add_child(relief)
	await process_frame
	await process_frame
	cells.call("SetTerrainKind", at, "water")
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 0, "Flooded cell kept mountain stamps")
	cells.call("SetTerrainKind", at, "grass")
	cells.call("SetMetadata", at, "terrain_relief", 2)
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 1, "Live mountain change did not replace hill stamps")
	relief.set("CellDataPath", replacement.get_path())
	relief.call("Rebuild")
	check(relief.get("StampCount") == 0, "Source replacement retained the old world's relief")
	replacement.call("SetMetadata", at, "terrain_relief", 1)
	await process_frame
	await process_frame
	check(relief.get("StampCount") == 2, "Replacement cell source was not subscribed")
	relief.set("HillsSheetPath", "")
	relief.call("Rebuild")
	check(relief.get("StampCount") == 0, "Removed relief sheet retained a cached texture")
	relief.set("HillsSheetPath", ROCKS)
	relief.call("Rebuild")
	check(relief.get("StampCount") == 2, "Restored relief sheet did not reload")
	relief.set("CellDataPath", NodePath("../Missing"))
	relief.call("Rebuild")
	check(relief.get("StampCount") == 0, "Missing explicit source retained stale relief")
	# Last elevated cell has no right/down neighbour. Its own top-face corners remain valid.
	var iso := make("terrain/TerrainIsometricRendererComponent", host, {
		"RefreshOnReady": false, "CellDataPath": replacement.get_path(),
		"BoundsOrigin": Vector2i(-3, 7), "BoundsSize": Vector2i(4, 4),
		"BlockSheetPath": "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png",
		"SheetColumns": 8, "SheetRows": 7, "CellSize": Vector2i(111, 64)})
	replacement.call("ClearCells")
	var edge := Vector2i(0, 10)
	replacement.call("SetMetadata", edge, "terrain_relief", 1)
	iso.call("Rebuild")
	grid.set("TileMapLayerPath", NodePath())
	grid.set("ElevatedTerrainPath", iso.get_path())
	grid.call("NotifyGeometryChanged")
	relief.set("CellDataPath", replacement.get_path())
	relief.call("Rebuild")
	check(relief.get("StampCount") == 2, "Elevated boundary hill lost stamps through missing neighbours")
	host.free()
	print("[terrain-live-relief] OK" if failures.is_empty() else "[terrain-live-relief] FAILED")
	quit(0 if failures.is_empty() else 1)
