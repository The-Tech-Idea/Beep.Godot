extends SceneTree

const RENDERER = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainIsometricAutotileRendererComponent.cs")
const CELLS = preload("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs")
const NAV = preload("res://addons/beep_game_builder_cs/ecs/grid/GridNavigationComponent.cs")
const TRANSITIONS = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainTransitionLayerComponent.cs")
const TILE_VIEW = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainTileRendererComponent.cs")
const PAINTED = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs")
const FEATURES = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainFeatureRendererComponent.cs")
const GENERATOR = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		print("[terrain-live] FAIL: " + message)

func make_tiles() -> TileSet:
	var tiles := TileSet.new()
	tiles.tile_size = Vector2i(16, 16)
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tiles.add_terrain_set()
	tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_SIDES)
	tiles.add_terrain(0)
	tiles.add_terrain(0)
	var texture := GradientTexture2D.new()
	texture.width = 2592
	texture.height = 16
	texture.gradient = Gradient.new()
	var atlas := TileSetAtlasSource.new()
	atlas.texture = texture
	atlas.texture_region_size = Vector2i(16, 16)
	tiles.add_source(atlas, 0)
	for index in range(162):
		atlas.create_tile(Vector2i(index, 0))
		var data = atlas.get_tile_data(Vector2i(index, 0), 0)
		data.terrain_set = 0
		data.terrain = index / 81
		var pattern := index % 81
		for neighbor in range(16):
			if data.is_valid_terrain_peering_bit(neighbor):
				data.set_terrain_peering_bit(neighbor, pattern % 3 - 1)
				pattern /= 3
	return tiles

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	await verify_autotile_authoring(host)
	var cells = CELLS.new()
	cells.name = "Cells"
	host.add_child(cells)
	var nav = NAV.new()
	nav.set("CellDataPath", NodePath("../Cells"))
	nav.set("BoundsSize", Vector2i(3, 3))
	host.add_child(nav)
	var renderer = RENDERER.new()
	renderer.set("RefreshOnReady", false)
	renderer.set("CellDataPath", NodePath("../Cells"))
	renderer.set("BoundsSize", Vector2i(3, 3))
	renderer.set("Tiles", make_tiles())
	renderer.set("TerrainBindings", PackedStringArray(["grass=0", "water=1"]))
	host.add_child(renderer)
	renderer.call("Rebuild")
	var layer = renderer.call("GetTerrainLayer")
	check(layer.get_used_cells().size() == 9, "Native autotile painted extra cells beyond the requested map: " + str(layer.get_used_cells()))
	var cell := Vector2i(1, 1)
	check(layer.get_cell_tile_data(cell).terrain == 0, "Initial live grass was not drawn")
	cells.call("SetTerrainKind", cell, " WATER ")
	check(nav.call("IsBlocked", cell), "Navigation did not read edited water")
	await process_frame
	await process_frame
	var changed = layer.get_cell_tile_data(cell)
	check(changed != null and changed.terrain == 1, "Live water edit did not repaint")
	cells.call("ClearCells")
	await process_frame
	await process_frame
	check(layer.get_cell_tile_data(cell).terrain == 0 and not nav.call("IsBlocked", cell), "Bulk reset disagrees with navigation")
	var replacement = CELLS.new()
	replacement.name = "Replacement"
	replacement.set("DefaultTerrainKind", "water")
	host.add_child(replacement)
	renderer.set("CellDataPath", NodePath("../Replacement"))
	nav.set("CellDataPath", NodePath("../Replacement"))
	renderer.call("Rebuild")
	check(layer.get_cell_tile_data(cell).terrain == 1 and nav.call("IsBlocked", cell), "Source replacement kept the old live map")
	replacement.call("SetTerrainKind", cell, "grass")
	await process_frame
	await process_frame
	check(layer.get_cell_tile_data(cell).terrain == 0 and not nav.call("IsBlocked", cell), "Replacement map edits were not observed")
	check(renderer.call("GetPaintDiagnostics").valid, "Complete native fixture reported incomplete coverage")
	var shifted_origin := Vector2i(-10, 20)
	renderer.set("BoundsOrigin", shifted_origin)
	replacement.call("SetTerrainKind", shifted_origin + cell, "grass")
	renderer.call("Rebuild")
	check(layer.get_cell_tile_data(shifted_origin + cell).terrain == 0, "Shifted autotile ignored live grass")
	check(layer.get_cell_tile_data(shifted_origin).terrain == 1, "Shifted autotile ignored live water")
	check(layer.get_cell_tile_data(cell) == null, "Autotile retained old zero-origin cells")
	check(renderer.call("GetPaintDiagnostics").valid, "Shifted complete fixture reported incomplete coverage")
	replacement.call("SetTerrainKind", shifted_origin + cell, "water")
	await process_frame
	await process_frame
	check(layer.get_cell_tile_data(shifted_origin + cell).terrain == 1, "Shifted live edit was not repainted")
	renderer.set("BoundsOrigin", Vector2i.ZERO)
	renderer.set("TerrainBindings", PackedStringArray(["grass=0"]))
	renderer.call("Rebuild")
	check(renderer.call("GetPaintDiagnostics").unmapped == 8, "Unbound water cells were not reported")
	check(not renderer.call("GetPaintDiagnostics").valid, "Partial terrain coverage was reported as complete")
	check(layer.get_used_cells() == [cell], "Native terrain matching painted unbound cells: " + str(layer.get_used_cells()))
	renderer.set("Tiles", load("res://addons/beep_game_builder_cs/textures/iso/grassland_tileset.tres"))
	renderer.call("Rebuild")
	check(layer.get_used_cells().is_empty(), "Invalid TileSet retained stale terrain from previous render")
	check(not renderer.call("GetPaintDiagnostics").valid and renderer.call("GetPaintDiagnostics").has("reason"), "Unauthored TileSet lacked an explicit failure diagnostic")
	var dual := TileMapLayer.new()
	dual.name = "Dual"
	var sheet := GradientTexture2D.new()
	sheet.width = 64
	sheet.height = 64
	sheet.gradient = Gradient.new()
	var atlas := TileSetAtlasSource.new()
	atlas.texture = sheet
	atlas.texture_region_size = Vector2i(16, 16)
	var dual_tiles := TileSet.new()
	dual_tiles.tile_size = Vector2i(16, 16)
	dual_tiles.add_source(atlas, 0)
	for y in range(4):
		for x in range(4):
			atlas.create_tile(Vector2i(x, y))
	dual.tile_set = dual_tiles
	host.add_child(dual)
	var transition = TRANSITIONS.new()
	transition.set("RefreshOnReady", false)
	transition.set("UseTileSetTerrains", false)
	transition.set("TransitionTerrainKind", "grass")
	transition.set("CellDataPath", NodePath("../Replacement"))
	transition.set("DisplayLayerPath", NodePath("../Dual"))
	transition.set("BoundsSize", Vector2i(3, 3))
	host.add_child(transition)
	transition.call("RefreshTransitions")
	check(dual.get_used_cells().size() == 4, "Single live grass cell must affect four dual tiles")
	dual.set_cell(Vector2i(99, 99), 0, Vector2i.ZERO)
	replacement.call("SetTerrainKind", cell, "water")
	await process_frame
	await process_frame
	check(dual.get_used_cells() == [Vector2i(99, 99)], "Incremental dual refresh did not erase exactly the affected tiles")
	dual.hide()
	replacement.call("SetTerrainKind", cell, "grass")
	await process_frame
	await process_frame
	check(dual.get_used_cells() == [Vector2i(99, 99)], "Standalone hidden transitions repainted")
	dual.show()
	await process_frame
	await process_frame
	check(dual.get_used_cells().size() == 5, "Standalone transitions did not catch up when shown")
	host.remove_child(transition)
	host.add_child(transition)
	await process_frame
	await process_frame
	replacement.call("SetTerrainKind", cell, "water")
	await process_frame
	await process_frame
	check(dual.get_used_cells() == [Vector2i(99, 99)], "Reattached transition lost live subscription")
	transition.free()
	var map_cells = CELLS.new()
	map_cells.name = "MapCells"
	host.add_child(map_cells)
	var view = TILE_VIEW.new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../MapCells"))
	view.set("BoundsSize", Vector2i(5, 5))
	view.set("CoastDetail", 1)
	view.set("GrassAtlasPath", "res://addons/beep_game_builder_cs/textures/tiles/grass_15piece.png")
	view.set("WaterAtlasPath", "res://addons/beep_game_builder_cs/textures/tiles/water_15piece.png")
	view.set("WaterShaderPath", "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader")
	host.add_child(view)
	var authored_tiles := TileMapLayer.new()
	authored_tiles.name = "RoadTiles"
	authored_tiles.tile_set = dual_tiles
	view.add_child(authored_tiles)
	authored_tiles.set_cell(Vector2i(10, 10), 0, Vector2i.ZERO)
	view.call("Rebuild")
	check(is_instance_valid(authored_tiles) and not authored_tiles.is_queued_for_deletion() and authored_tiles.get_parent() == view, "First rebuild removed authored RoadTiles")
	var painted = PAINTED.new()
	painted.set("RefreshOnReady", false)
	painted.set("CellDataPath", NodePath("../MapCells"))
	painted.set("BoundsSize", Vector2i(5, 5))
	painted.set("CoastDetail", 1)
	host.add_child(painted)
	painted.call("Rebuild")
	var splat = painted.get_node("SplatSurface")
	var surface = view.get_node("TileWater")
	var coast = surface.material.get_shader_parameter("coast_map").get_image()
	check(coast.get_pixel(2, 2).r < 0.5, "Dry live map has wet shader mask")
	map_cells.call("SetTerrainKind", Vector2i(2, 2), "water")
	await process_frame
	await process_frame
	coast = surface.material.get_shader_parameter("coast_map").get_image()
	check(coast.get_pixel(2, 2).r > 0.5 and coast.get_pixel(2, 2).g < 0.5, "Enclosed live lake is missing or has ocean surf")
	var painted_ids = splat.material.get_shader_parameter("id_map").get_image()
	check(roundi(painted_ids.get_pixel(2, 2).r * 255) == 12, "Painted live water fell back to grass")
	check(splat.material.get_shader_parameter("coast_map").get_image().get_data() == coast.get_data(), "Painted and tiled coast masks disagree")
	check(view.get_node("WaterTiles").get_used_cells().size() == 4, "Parent did not propagate newly introduced water to dual tiles")
	map_cells.call("SetTerrainKind", Vector2i(2, 1), "water")
	map_cells.call("SetTerrainKind", Vector2i(2, 0), "water")
	await process_frame
	await process_frame
	coast = surface.material.get_shader_parameter("coast_map").get_image()
	check(coast.get_pixel(2, 2).g > 0.5, "Boundary-connected water did not become ocean")
	view.hide()
	map_cells.call("ClearCells")
	await process_frame
	await process_frame
	check(not view.get_node("WaterTiles").get_used_cells().is_empty(), "Hidden tile transitions repainted")
	check(surface.material.get_shader_parameter("coast_map").get_image().get_data() == coast.get_data(), "Hidden tile coastline rebuilt")
	view.show()
	await process_frame
	await process_frame
	coast = surface.material.get_shader_parameter("coast_map").get_image()
	check(coast.get_pixel(2, 2).r < 0.5 and view.get_node("WaterTiles").get_used_cells().is_empty(), "Reset left stale water tiles or shader")
	painted_ids = splat.material.get_shader_parameter("id_map").get_image()
	check(roundi(painted_ids.get_pixel(2, 2).r * 255) == 0, "Painted bulk reset kept stale water IDs")
	check(splat.material.get_shader_parameter("coast_map").get_image().get_data() == coast.get_data(), "Painted reset kept stale coast")
	map_cells.call("SetMetadata", Vector2i(2, 2), "terrain_feature", "woods")
	var features = FEATURES.new()
	# Test a four-stamp live feature explicitly, independent of presentation defaults.
	features.set("SpritesPerTile", 4)
	features.set("RefreshOnReady", false)
	features.set("CellDataPath", NodePath("../MapCells"))
	features.set("BoundsSize", Vector2i(5, 5))
	features.set("WoodsSheetPath", "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png")
	host.add_child(features)
	features.call("Rebuild")
	check(features.get("StampCount") == 4, "Live woods metadata did not create props")
	map_cells.call("SetFlags", Vector2i(2, 2), 2)
	await process_frame
	await process_frame
	check(features.get("StampCount") == 0, "Cleared cell kept trees")
	var saved_cells = map_cells.call("GetCells")
	map_cells.call("ClearCells")
	map_cells.call("LoadCells", saved_cells, true)
	await process_frame
	await process_frame
	check(features.get("StampCount") == 0, "Restored cleared cell regrew trees")
	map_cells.call("SetFlags", Vector2i(2, 2), 0)
	map_cells.call("SetTerrainKind", Vector2i(2, 2), "water")
	await process_frame
	await process_frame
	check(features.get("StampCount") == 0, "Water cell kept land props")
	map_cells.call("SetTerrainKind", Vector2i(2, 2), "grass")
	await process_frame
	await process_frame
	check(features.get("StampCount") == 4, "Live feature did not return on suitable ground")
	var generator = GENERATOR.new()
	generator.set("GenerateOnReady", false)
	generator.set("CellDataPath", NodePath("../MapCells"))
	generator.set("BoundsSize", Vector2i(5, 5))
	generator.set("Mode", 0)
	host.add_child(generator)
	generator.call("GenerateTerrain")
	check(map_cells.call("GetMetadata", Vector2i(2, 2), "terrain_feature") == generator.call("FeatureAt", Vector2i(2, 2)), "Generator did not hand feature metadata to live map")
	check(typeof(map_cells.call("GetMetadata", Vector2i(2, 2), "terrain_relief")) == TYPE_INT, "Generated relief missing from live map")
	check(typeof(map_cells.call("GetMetadata", Vector2i(2, 2), "terrain_shade")) == TYPE_FLOAT, "Generated shade missing from live map")
	await process_frame
	await process_frame
	# Same-path replacement must rebuild the transition components' source bindings too.
	map_cells.name = "OldMapCells"
	var new_map = CELLS.new()
	new_map.name = "MapCells"
	new_map.set("DefaultTerrainKind", "water")
	host.add_child(new_map)
	view.call("Rebuild")
	check(view.get_node("GrassTiles").get_used_cells().is_empty() and not view.get_node("WaterTiles").get_used_cells().is_empty(), "Tile transitions retained replaced cell source")
	view.set("TerrainGeneratorPath", view.get_path_to(generator))
	view.set("CellDataPath", NodePath("../MissingMap"))
	new_map.call("SetTerrainKind", Vector2i(2, 2), "grass")
	await process_frame
	await process_frame
	check(not view.has_node("GrassTiles") and not view.has_node("WaterTiles") and surface.get_used_cells().is_empty(), "Missing explicit source retained stale terrain or generated sea")
	view.set("CellDataPath", NodePath("../MapCells"))
	view.call("Rebuild")
	check(not view.get_node("WaterTiles").get_used_cells().is_empty() and not surface.get_used_cells().is_empty(), "Restored source did not redraw tile view")
	view.set("WaterShaderPath", "")
	view.call("Rebuild")
	check(surface.get_used_cells().is_empty(), "Removing water shader left the old overlay visible")
	view.set("GrassAtlasPath", "")
	view.call("Rebuild")
	check(not view.has_node("GrassTiles"), "Atlas removal kept the owned grass layer")
	check(is_instance_valid(authored_tiles) and not authored_tiles.is_queued_for_deletion() and authored_tiles.get_parent() == view and authored_tiles.get_used_cells() == [Vector2i(10, 10)], "Reconfiguration or source reset modified authored RoadTiles")
	renderer.free()
	cells.call("SetTerrainKind", cell, "water")
	host.free()
	print("[terrain-live] OK" if failures.is_empty() else "[terrain-live] FAILED")
	quit(0 if failures.is_empty() else 1)

func verify_autotile_authoring(host: Node) -> void:
	var cells = CELLS.new()
	cells.name = "AuthoringCells"
	host.add_child(cells)
	var view = RENDERER.new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../MissingAuthoringCells"))
	view.set("BoundsSize", Vector2i(2, 2))
	view.set("Tiles", make_tiles())
	view.set("TerrainBindings", PackedStringArray(["grass=0", "water=1"]))
	var authored := TileMapLayer.new()
	authored.name = "IsoTerrain"
	authored.tile_set = view.get("Tiles")
	authored.set_cell(Vector2i.ZERO, 0, Vector2i.ZERO)
	view.add_child(authored)
	host.add_child(view)
	view.call("Rebuild")
	check(authored.get_used_cells().is_empty(), "First invalid rebuild kept stale authored terrain")
	view.set("CellDataPath", NodePath("../AuthoringCells"))
	for bindings in [
		PackedStringArray(["grass=0", "GRASS=1"]),
		PackedStringArray(["grass=0", "bad binding"]),
		PackedStringArray(["grass=0", "water=-1"]),
		PackedStringArray(["grass=0", "water=2"])
	]:
		view.set("TerrainBindings", bindings)
		view.call("Rebuild")
		var diagnostics: Dictionary = view.call("GetPaintDiagnostics")
		check(not diagnostics.valid and diagnostics.has("reason"), "Invalid bindings accepted: " + str(bindings))
		check(authored.get_used_cells().is_empty(), "Invalid bindings painted a misleading partial map")
	view.set("TerrainBindings", PackedStringArray(["grass=0", "water=1"]))
	view.call("Rebuild")
	check(view.call("GetPaintDiagnostics").valid, "Valid bindings did not recover after authoring errors")
	host.remove_child(view)
	cells.call("SetTerrainKind", Vector2i.ZERO, "water")
	host.add_child(view)
	await process_frame
	await process_frame
	check(authored.get_cell_tile_data(Vector2i.ZERO).terrain == 1, "Reattached isometric autotile retained detached-time terrain")
	cells.call("SetTerrainKind", Vector2i.ONE, "water")
	await process_frame
	await process_frame
	check(authored.get_cell_tile_data(Vector2i.ONE).terrain == 1, "Reattached isometric autotile lost live edits")
	view.free()
	cells.free()
