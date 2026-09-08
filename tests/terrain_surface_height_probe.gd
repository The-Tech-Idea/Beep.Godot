extends SceneTree
const GENERATOR = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const ISO = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainIsometricRendererComponent.cs")
const CELLS = preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const PROPS = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainIsometricFeatureRendererComponent.cs")
const GRID = preload("res://addons/beep_game_builder_cs/ecs/grid/GridProjectionComponent.cs")
var failures := 0

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures += 1
		print("[surface-height] FAIL: ", message)

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var generator = GENERATOR.new()
	generator.name = "Generator"
	generator.set("BoundsSize", Vector2i(32, 32))
	generator.set("MountainsFraction", 0.4)
	host.add_child(generator)
	var renderer = ISO.new()
	renderer.set("RefreshOnReady", false)
	renderer.set("WaterShaderPath", "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	renderer.set("TerrainGeneratorPath", NodePath("../Generator"))
	renderer.set("BoundsSize", Vector2i(32, 32))
	renderer.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png")
	renderer.set("SheetColumns", 8)
	renderer.set("SheetRows", 7)
	renderer.set("CellSize", Vector2i(111, 64))
	host.add_child(renderer)
	renderer.call("Rebuild")
	var water = renderer.get_node("IsoWater")
	check(water is Polygon2D and water.polygon.size() == 4, "Water should use one continuous four-vertex surface")
	check(water.material.get_shader_parameter("tile_offset") == Vector2.ZERO, "Continuous surface retained tiled offset")
	var highest: Dictionary = {}
	var maximum := 0
	for child in renderer.get_children():
		if child is TileMapLayer and str(child.name).begins_with("IsoLevel"):
			var level = int(str(child.name).trim_prefix("IsoLevel"))
			for cell in child.get_used_cells():
				if not highest.has(cell) or highest[cell][0] < level:
					highest[cell] = [level, child]
				maximum = maxi(maximum, level)
	check(maximum >= 4, "Fixture did not generate summit tiles")
	var extent: Rect2 = renderer.get("SurfaceExtent")
	for cell in highest:
		for corner in renderer.call("SurfaceCorners", cell):
			check(extent.grow(0.001).has_point(corner), "Built extent cropped a native summit corner")
		var level = highest[cell][0]
		var layer = highest[cell][1]
		if renderer.call("SurfaceLevel", cell) != level and failures == 0:
			print("height mismatch ", cell, " drawn=", level, " query=", renderer.call("SurfaceLevel", cell), " terrain=", generator.call("TerrainKindAt", cell))
		check(renderer.call("SurfaceLevel", cell) == level, "Surface query differs from highest drawn level")
		var expected = layer.transform * layer.map_to_local(cell)
		check(renderer.call("SurfacePosition", cell).distance_to(expected) < 0.001, "Surface position is below drawn tile")
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	var origin := Vector2i(-20, 40)
	var at := origin + Vector2i(1, 1)
	cells.call("SetMetadata", at, "terrain_relief", 1)
	cells.call("SetMetadata", at, "terrain_feature", "woods")
	renderer.name = "Iso"
	renderer.set("CellDataPath", NodePath("../Cells"))
	renderer.set("BoundsSize", Vector2i(3, 3))
	renderer.set("BoundsOrigin", origin)
	var props = PROPS.new()
	props.set("RefreshOnReady", false)
	props.set("IsometricRendererPath", NodePath("../Iso"))
	props.set("BoundsSize", Vector2i(3, 3))
	props.set("WoodsSheetPath", "res://addons/beep_game_builder_cs/textures/iso/iso_trees_8x1.png")
	props.set("WoodsColumns", 8)
	props.set("WoodsRows", 1)
	host.add_child(props)
	renderer.call("Rebuild")
	check(renderer.get_node("IsoLevel1").get_used_rect() == Rect2i(origin, Vector2i(3, 3)), "Isometric tiles lost absolute bounds")
	check(not renderer.call("ContainsSurfaceCell", Vector2i.ZERO), "Shifted surface still contains zero")
	check(water.position.distance_to(renderer.get("OriginPosition")) < 0.001, "Water did not move with isometric origin")
	check(renderer.call("SurfaceLevel", at) == 2, "Live hill relief was ignored")
	check(props.call("GetLayerDiagnostics")[1].cells == 2, "Props did not follow live hill level")
	var original_anchors: Array[Vector2] = []
	for anchor in props.call("GetStampAnchors"):
		original_anchors.append(renderer.to_local(props.to_global(anchor)))
	var grid = GRID.new()
	grid.set("DrawGrid", false)
	grid.set("TrackMouseCell", false)
	grid.set("ElevatedTerrainPath", NodePath("../Iso"))
	host.add_child(grid)
	grid.name = "Grid"
	var stationary := Node2D.new()
	host.add_child(stationary)
	var identity = load("res://addons/beep_game_builder_cs/ecs/grid/GridObjectComponent.cs").new()
	identity.set("GridPath", NodePath("../../Grid"))
	identity.set("Cell", at)
	stationary.add_child(identity)
	host.position = Vector2(73, -29)
	host.rotation = 0.16
	renderer.position = Vector2(18, 35)
	renderer.rotation = -0.21
	renderer.scale = Vector2(1.2, 0.8)
	props.position = Vector2(-63, 28)
	props.rotation = 0.37
	props.scale = Vector2(0.75, 1.3)
	props.call("Rebuild")
	var transformed_anchors = props.call("GetStampAnchors")
	check(transformed_anchors.size() == original_anchors.size(), "Transform changed prop count")
	for anchor in transformed_anchors:
		var actual: Vector2 = renderer.to_local(props.to_global(anchor))
		var closest := INF
		for expected in original_anchors: closest = minf(closest, expected.distance_to(actual))
		check(closest < 0.01, "Prop jitter did not follow native transformed surface")
		check(Geometry2D.is_point_in_polygon(actual, renderer.call("SurfaceCorners", at)), "Trunk scattered outside its hill")
	props.set("WoodsSheetPath", "")
	props.call("Rebuild")
	check(props.call("GetStampAnchors").is_empty(), "Removed sheet retained stale stamps")
	props.set("WoodsSheetPath", "res://addons/beep_game_builder_cs/textures/iso/iso_trees_8x1.png")
	props.call("Rebuild")
	check(props.call("GetStampAnchors").size() == 2, "Restored sheet did not recover stamps")
	grid.position = Vector2(-41, 17)
	grid.rotation = 0.32
	var hill_center: Vector2 = renderer.to_global(renderer.call("SurfacePosition", at))
	check(grid.call("CellToWorld", at).distance_to(hill_center) < 0.001, "Grid center did not follow transformed hill")
	check(grid.call("WorldToCell", hill_center) == at, "Picking missed raised hill")
	var corners = grid.call("CellCorners", at)
	var center := Vector2.ZERO
	for corner in corners:
		center += grid.to_global(corner)
	check(corners.size() == 4 and (center / 4).distance_to(hill_center) < 0.001, "Grid outline is below hill")
	var outside := renderer.to_global(Vector2(-100000, -100000))
	check(grid.call("WorldToCell", outside) == Vector2i(-2147483648, -2147483648), "Outside map selected a cell")
	check(grid.call("SnapWorld", outside) == outside, "Invalid surface pick moved snap target")
	props.hide()
	cells.call("SetMetadata", at, "terrain_relief", 0)
	await process_frame
	await process_frame
	check(props.call("GetLayerDiagnostics")[1].cells == 2, "Hidden isometric props rebuilt on surface event")
	props.show()
	await process_frame
	await process_frame
	check(renderer.call("SurfaceLevel", at) == 1, "Live flattening kept old height")
	var flat_center: Vector2 = renderer.to_global(renderer.call("SurfacePosition", at))
	check(grid.call("WorldToCell", flat_center) == at, "Picking kept old hill height after edit")
	check(grid.call("CellToWorld", at).distance_to(flat_center) < 0.001, "Grid kept old hill height after edit")
	check(stationary.global_position.distance_to(flat_center) < 0.001, "Stationary object did not follow live flattening")
	check(props.call("GetLayerDiagnostics")[0].cells == 2 and props.call("GetLayerDiagnostics")[1].cells == 0, "Props remained on old elevation")
	host.remove_child(props)
	host.add_child(props)
	await process_frame
	await process_frame
	cells.call("SetTerrainKind", at, "water")
	await process_frame
	await process_frame
	check(not renderer.call("IsLandCell", at), "Flooded cell stayed land")
	var water_center: Vector2 = renderer.to_global(renderer.call("SurfacePosition", at))
	check(grid.call("WorldToCell", water_center) == at, "Picking missed flooded surface")
	check(stationary.global_position.distance_to(water_center) < 0.001, "Stationary object did not follow live flooded surface")
	for row in props.call("GetLayerDiagnostics"):
		check(row.cells == 0, "Flooded terrain kept props")
	grid.set("ElevatedTerrainPath", NodePath("../MissingTerrain"))
	var notifications := [0]
	grid.connect("GeometryChanged", func(): notifications[0] += 1)
	renderer.call("Rebuild")
	check(notifications[0] == 0, "Detached terrain still notified grid")
	check(grid.call("WorldToCell", water_center) == Vector2i(-2147483648, -2147483648), "Missing elevated source fell back to flat picking")
	check(grid.call("SnapWorld", water_center) == water_center, "Missing elevated source moved snap target")
	renderer.set("CellDataPath", NodePath("../MissingCells"))
	renderer.call("Rebuild")
	check(not renderer.get("HasSurface"), "Failed rebuild retained a queryable surface")
	check(renderer.get("SurfaceExtent").size == Vector2.ZERO, "Failed rebuild retained camera extent")
	check(not water.visible, "Failed rebuild retained stale ocean")
	for child in renderer.get_children():
		if child is TileMapLayer: check(child.get_used_cells().is_empty(), "Failed rebuild retained native blocks")
	check(props.call("GetStampAnchors").is_empty(), "Failed surface retained feature stamps")
	renderer.set("CellDataPath", NodePath("../Cells"))
	renderer.call("Rebuild")
	check(renderer.get("HasSurface") and renderer.get("SurfaceExtent").size.x > 0, "Restored source did not recover surface bounds")
	var ground: TileMapLayer = renderer.get_node("IsoLevel1")
	var cached_set := ground.tile_set
	renderer.call("Rebuild")
	check(ground.tile_set == cached_set, "Unchanged atlas settings rebuilt the TileSet")
	renderer.set("CellSize", Vector2i(128, 72))
	renderer.set("BlockLift", 23)
	renderer.set("GrassFrame", 1)
	renderer.call("Rebuild")
	check(ground.tile_set != cached_set and ground.tile_set.tile_size == Vector2i(128, 72), "Cell-size edit retained old native geometry")
	check(ground.get_cell_atlas_coords(origin) == Vector2i(1, 0), "Frame edit retained old mapping")
	check(ground.get_cell_tile_data(origin).texture_origin == Vector2i(0, -23), "Lift edit retained old texture offset")
	var variants := PackedStringArray(["grass=2"])
	renderer.set("TerrainVariants", variants)
	renderer.call("Rebuild")
	check(ground.get_cell_atlas_coords(origin) == Vector2i(2, 0), "Variant edit retained old tile choices")
	renderer.set("BlockSheetPath", "")
	renderer.call("Rebuild")
	check(not renderer.get("HasSurface") and ground.get_used_cells().is_empty(), "Removed block sheet retained old surface")
	renderer.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png")
	renderer.set("SheetColumns", 100000)
	renderer.call("Rebuild")
	check(not renderer.get("HasSurface"), "Empty atlas frames produced a surface")
	renderer.set("SheetColumns", 8)
	renderer.call("Rebuild")
	check(renderer.get("HasSurface"), "Valid atlas settings did not recover after failure")
	host.free()
	print("[surface-height] OK" if failures == 0 else "[surface-height] FAILED")
	quit(0 if failures == 0 else 1)
