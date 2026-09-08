extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var native := TileMapLayer.new()
	native.name = "Native"
	native.tile_set = TileSet.new()
	native.tile_set.tile_size = Vector2i(80, 40)
	native.position = Vector2(110, 53)
	native.rotation = 0.2
	host.add_child(native)
	var grid: Node2D = load(BASE + "grid/GridProjectionComponent.cs").new()
	grid.name = "Grid"
	grid.set("DrawGrid", false)
	grid.set("TrackMouseCell", false)
	grid.set("TileMapLayerPath", NodePath("../Native"))
	host.add_child(grid)
	var view: Node2D = load(BASE + "terrain/TerrainFeatureRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("GridPath", NodePath("../Grid"))
	view.set("BoundsOrigin", Vector2i(-4, 7))
	view.set("BoundsSize", Vector2i(3, 3))
	view.set("SpritesPerTile", 1)
	view.set("PositionJitter", 0.0)
	view.set("ScaleJitter", 0.0)
	view.set("SpriteAnchor", Vector2(0.5, 0.5))
	view.position = Vector2(-20, 33)
	view.scale = Vector2(0.8, 1.4)
	var sheet := "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png"
	view.set("WoodsSheetPath", sheet)
	host.add_child(view)
	var cell := Vector2i(-3, 8)
	cells.call("SetTerrainKind", cell, "grass")
	cells.call("SetMetadata", cell, "terrain_feature", "woods")
	# A feature outside the renderer bounds must not appear.
	cells.call("SetMetadata", Vector2i.ZERO, "terrain_feature", "woods")
	view.call("Rebuild")
	assert(view.get("StampCount") == 1, "Shifted feature not read from its live cell")
	for offset in [Vector2.ZERO, Vector2(100, -20)]:
		native.position += offset
		grid.call("NotifyGeometryChanged")
		await process_frame
		await process_frame
		var centers: PackedVector2Array = view.call("GetStampCenters")
		var expected := native.to_global(native.map_to_local(cell))
		assert(view.to_global(centers[0]).distance_to(expected) < 0.001, "Feature disagrees with native grid geometry")
	# The same absolute cell must keep its anchor, frame size and scale when the
	# visible bounds change. A noncentral anchor makes scale changes observable.
	view.set("SpritesPerTile", 5)
	view.set("PositionJitter", 0.85)
	view.set("ScaleJitter", 0.18)
	view.set("SpriteAnchor", Vector2(0.5, 0.86))
	view.call("Rebuild")
	var stable_anchors: PackedVector2Array = view.call("GetStampAnchors")
	var stable_centers: PackedVector2Array = view.call("GetStampCenters")
	view.set("BoundsOrigin", Vector2i(-5, 6))
	view.set("BoundsSize", Vector2i(5, 5))
	view.call("Rebuild")
	assert(view.call("GetStampAnchors") == stable_anchors, "Changing view bounds reseeded the same grid cell")
	assert(view.call("GetStampCenters") == stable_centers, "Changing view bounds changed sprite size")
	view.set("Seed", 25)
	view.call("Rebuild")
	assert(view.call("GetStampAnchors") != stable_anchors, "Seed did not change feature scatter")
	view.set("Seed", 31415)
	view.call("Rebuild")
	assert(view.call("GetStampAnchors") == stable_anchors, "Restoring seed did not restore scatter")
	view.set("SpritesPerTile", 1)
	view.set("PositionJitter", 0.0)
	view.call("Rebuild")
	view.hide()
	cells.call("SetTerrainKind", cell, "water")
	await process_frame
	await process_frame
	assert(view.get("StampCount") == 1, "Hidden feature view rebuilt")
	view.show()
	await process_frame
	await process_frame
	assert(view.get("StampCount") == 0, "Flooding left a tree in water")
	cells.call("SetTerrainKind", cell, "grass")
	view.call("Rebuild")
	assert(view.get("StampCount") == 1)
	view.set("WoodsSheetPath", "")
	view.call("Rebuild")
	assert(view.get("StampCount") == 0, "Removed sheet was retained")
	view.set("WoodsSheetPath", sheet)
	view.call("Rebuild")
	assert(view.get("StampCount") == 1)
	host.remove_child(view)
	host.add_child(view)
	await process_frame
	await process_frame
	var before: PackedVector2Array = view.call("GetStampCenters")
	native.position += Vector2(33, -17)
	grid.call("NotifyGeometryChanged")
	await process_frame
	await process_frame
	assert(view.call("GetStampCenters") != before, "Reattached features lost grid subscription")
	cells.call("ClearLand", cell)
	await process_frame
	await process_frame
	assert(view.get("StampCount") == 0, "Cleared land retained vegetation")
	host.free()
	print("[terrain-feature-grid] OK")
	quit()
