extends SceneTree

# Live resources on the map have ONE drawer, TerrainResourceRendererComponent. The map
# overlay used to draw the same live subtree as category discs and was checked here in
# step with the icons; it no longer draws surface resources (VIEW-13).

const BASE := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func make(path: String, parent: Node, values: Dictionary) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	for key in values:
		node.set(key, values[key])
	parent.add_child(node)
	return node

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func settle() -> void:
	await process_frame
	await process_frame

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var deposits := Node2D.new()
	host.add_child(deposits)
	var icons := make("terrain/TerrainResourceRendererComponent", host, {
		"RefreshOnReady": false, "BoundsSize": Vector2i(8, 8), "ResourceRootPath": deposits.get_path()})
	icons.call("Rebuild")
	var resource := make("grid/GridResourceNodeComponent", deposits, {
		"UseExplicitCell": true, "Cell": Vector2i(2, 3), "ResourceId": "stone", "Amount": 1})
	make("grid/GridResourceNodeComponent", host, {
		"UseExplicitCell": true, "Cell": Vector2i(3, 3), "ResourceId": "stone", "Amount": 1})
	await settle()
	check(icons.get("IconCount") == 1, "Icons missed added resource or included another subtree")
	var saved = resource.call("CaptureState")
	icons.hide()
	resource.call("Gather")
	await settle()
	check(icons.get("IconCount") == 1, "Hidden resource view rebuilt")
	icons.show()
	await settle()
	check(icons.get("IconCount") == 0, "Depleted resource stayed visible")
	host.remove_child(icons)
	host.add_child(icons)
	await settle()
	resource.call("RestoreState", saved)
	await settle()
	check(icons.get("IconCount") == 1, "Restored resource stayed hidden")
	var layer := TileMapLayer.new()
	layer.tile_set = TileSet.new()
	layer.tile_set.tile_size = Vector2i(96, 48)
	layer.position = Vector2(70, -20)
	layer.rotation = 0.2
	host.add_child(layer)
	var grid := make("grid/GridProjectionComponent", host, {
		"TileMapLayerPath": layer.get_path(), "DrawGrid": false, "TrackMouseCell": false})
	icons.position = Vector2(-50, 30)
	icons.rotation = -0.1
	icons.scale = Vector2(1.2, 0.8)
	icons.set("GridPath", grid.get_path())
	icons.set("BoundsOrigin", Vector2i(-3, -1))
	icons.set("VerticalOffset", 0)
	resource.call("RestoreState", {"cell": Vector2i(-1, 2), "amount": 1})
	for shape in [TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_SHAPE_ISOMETRIC]:
		layer.tile_set.tile_shape = shape
		icons.call("Rebuild")
		var centers = icons.call("GetIconCenters")
		check(centers.size() == 1, "Native icon binding lost a negative-origin resource")
		if centers.size() == 1:
			check(icons.to_global(centers[0]).distance_to(grid.call("CellToWorld", Vector2i(-1, 2))) < 0.001,
				"Baked icon center differs from native grid")
	layer.position += Vector2(80, -30)
	grid.call("NotifyGeometryChanged")
	await settle()
	check(icons.to_global(icons.call("GetIconCenters")[0]).distance_to(grid.call("CellToWorld", Vector2i(-1, 2))) < 0.001,
		"Geometry notification left cached icon position stale")
	icons.set("IconSource", 1)
	icons.set("IconSheetPath", "res://addons/beep_game_builder_cs/textures/resources/resources_historical_5x5.png")
	icons.set("IconOrder", PackedStringArray(["unknown"]))
	icons.call("Rebuild")
	check(icons.get("IconCount") == 0, "Changed custom mapping retained cached frames")
	icons.set("IconOrder", PackedStringArray(["stone"]))
	icons.call("Rebuild")
	check(icons.get("IconCount") == 1, "Changed custom mapping did not apply")
	resource.queue_free()
	await settle()
	await settle()
	check(icons.get("IconCount") == 0, "Removed resource stayed visible")
	icons.set("ResourceRootPath", NodePath("../Missing"))
	icons.call("Rebuild")
	check(icons.get("IconCount") == 0, "Missing explicit root fell back to generation")
	icons.set("ResourceRootPath", deposits.get_path())
	make("grid/GridResourceNodeComponent", deposits, {
		"UseExplicitCell": true, "Cell": Vector2i(-1, 2), "ResourceId": "stone", "Amount": 1})
	icons.call("Rebuild")
	check(icons.get("IconCount") == 1, "Generator-free live icons did not recover after the root was restored")
	host.free()
	print("[terrain-live-resource-view] OK" if failures.is_empty() else "[terrain-live-resource-view] FAILED")
	quit(0 if failures.is_empty() else 1)
