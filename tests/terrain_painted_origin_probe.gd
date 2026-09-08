extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	host.position = Vector2(70, -30)
	host.rotation = 0.2
	host.scale = Vector2(1.2, 0.8)
	root.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.name = "Painted"
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(4, 3))
	view.set("TileSize", 32)
	host.add_child(view)
	var reference_coast := PackedByteArray()
	for origin in [Vector2i.ZERO, Vector2i(-7, 9), Vector2i(12, -5)]:
		cells.call("ClearCells")
		view.set("BoundsOrigin", origin)
		for y in range(3):
			for x in range(4):
				cells.call("SetTerrainKind", origin + Vector2i(x, y), "water" if x < 2 else "desert")
		view.call("Rebuild")
		var surface: TileMapLayer = view.get_node("SplatSurface")
		var logical: TileMapLayer = view.call("GetTerrainLayer")
		var material: ShaderMaterial = surface.material
		var ids: Image = material.get_shader_parameter("id_map").get_image()
		assert(roundi(ids.get_pixel(0, 0).r * 255) == 12, "Shifted water was read from zero")
		assert(roundi(ids.get_pixel(3, 2).r * 255) == 2, "Shifted desert was read from zero")
		var coast: PackedByteArray = material.get_shader_parameter("coast_map").get_image().get_data()
		if reference_coast.is_empty(): reference_coast = coast
		else: assert(coast == reference_coast, "Origin changed coastline geometry")
		assert(surface.get_used_rect() == Rect2i(0, 0, 4, 3), "Shader cells crossed into a negative quadrant")
		assert(logical.get_used_cells().is_empty(), "Logical grid duplicated rendered tiles")
		for y in range(3):
			for x in range(4):
				var local := Vector2i(x, y)
				var drawn := surface.to_global(surface.map_to_local(local))
				assert(drawn.distance_to(logical.to_global(logical.map_to_local(origin + local))) < 0.001)
				assert(logical.local_to_map(logical.to_local(drawn)) == origin + local)
		cells.call("SetTerrainKind", origin + Vector2i(3, 2), "water")
		await process_frame
		await process_frame
		ids = material.get_shader_parameter("id_map").get_image()
		assert(roundi(ids.get_pixel(3, 2).r * 255) == 12, "Shifted live edit did not repaint")
	var surface: TileMapLayer = view.get_node("SplatSurface")
	var previous_ids = surface.material.get_shader_parameter("id_map")
	view.hide()
	cells.call("ClearCells")
	await process_frame
	await process_frame
	assert(surface.material.get_shader_parameter("id_map") == previous_ids, "Hidden painted view rebuilt its map")
	view.show()
	await process_frame
	await process_frame
	assert(surface.material.get_shader_parameter("id_map") != previous_ids, "Shown painted view did not catch up")
	host.remove_child(view)
	host.add_child(view)
	await process_frame
	await process_frame
	cells.call("SetTerrainKind", view.get("BoundsOrigin"), "water")
	await process_frame
	await process_frame
	assert(roundi(surface.material.get_shader_parameter("id_map").get_image().get_pixel(0, 0).r * 255) == 12,
		"Reattached painted view lost live updates")
	# A saved material can be shared by separate scene instances. Only map uniforms must diverge.
	var other_cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	other_cells.name = "OtherCells"
	host.add_child(other_cells)
	var other: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	other.set("RefreshOnReady", false)
	other.set("CellDataPath", NodePath("../OtherCells"))
	other.set("BoundsSize", Vector2i(4, 3))
	var shared_surface := TileMapLayer.new()
	shared_surface.name = "SplatSurface"
	shared_surface.material = surface.material
	other.add_child(shared_surface)
	host.add_child(other)
	other.call("Rebuild")
	assert(shared_surface.material != surface.material, "Separate worlds shared mutable map uniforms")
	assert(roundi(surface.material.get_shader_parameter("id_map").get_image().get_pixel(0, 0).r * 255) == 12,
		"Second world overwrote the first world's water")
	assert(roundi(shared_surface.material.get_shader_parameter("id_map").get_image().get_pixel(0, 0).r * 255) == 0,
		"Second world did not draw its own grass")
	view.set("CellDataPath", NodePath("../Missing"))
	view.call("Rebuild")
	assert(surface.get_used_cells().is_empty(), "Missing live source retained painted terrain")
	view.set("CellDataPath", NodePath("../Cells"))
	view.call("Rebuild")
	assert(not surface.get_used_cells().is_empty(), "Source recovery did not restore painted terrain")
	# Shader-data refreshes must not invalidate an already correct tile surface.
	await process_frame
	await process_frame
	var geometry_changes: Array[int] = [0]
	var cached_coast: Texture2D = surface.material.get_shader_parameter("coast_map")
	surface.changed.connect(func(): geometry_changes[0] += 1)
	view.call("Rebuild")
	await process_frame
	await process_frame
	assert(geometry_changes[0] == 0, "Unchanged repaint rebuilt shader tile geometry")
	assert(surface.material.get_shader_parameter("coast_map") == cached_coast, "Unchanged water mask rebuilt the coast")
	var cached_id: Texture2D = surface.material.get_shader_parameter("id_map")
	var cached_shade: Texture2D = surface.material.get_shader_parameter("shade_map")
	var stored_cell: Vector2i = view.get("BoundsOrigin")
	cells.call("SetFlags", stored_cell, 2)
	cells.call("SetMetadata", stored_cell, "terrain_feature", "woods")
	cells.call("Till", stored_cell)
	cells.call("PlantCrop", stored_cell, "test_crop", 2, -1)
	cells.call("AdvanceDay", 1)
	await process_frame
	await process_frame
	assert(surface.material.get_shader_parameter("id_map") == cached_id, "Workflow state re-uploaded terrain IDs")
	assert(surface.material.get_shader_parameter("shade_map") == cached_shade, "Workflow state re-uploaded lighting")
	view.set("BlendSharpness", 6.0)
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("id_map") == cached_id, "Look settings rebuilt terrain data")
	assert(surface.material.get_shader_parameter("blend_sharpness") == 6.0, "Cached maps prevented look settings from updating")
	view.set("CoastDetail", 8)
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("coast_map").get_size() == Vector2(64, 48), "Detail change retained old reconstructed coast data")
	view.set("CoastDetail", 4)
	view.call("Rebuild")
	# New records freeze the default kind, even if the default changes back
	# before the renderer next sees it. A default-string-only cache misses this.
	other_cells.set("DefaultTerrainKind", "desert")
	other_cells.call("SetFlags", Vector2i.ZERO, 2)
	other_cells.set("DefaultTerrainKind", "grass")
	other.call("Rebuild")
	assert(roundi(shared_surface.material.get_shader_parameter("id_map").get_image().get_pixel(0, 0).r * 255) == 2,
		"Default-kind round trip hid a newly stored cell")
	other_cells.call("ClearCells")
	other.call("Rebuild")
	assert(roundi(shared_surface.material.get_shader_parameter("id_map").get_image().get_pixel(0, 0).r * 255) == 0,
		"Clearing cell records retained a cached terrain map")
	surface.erase_cell(Vector2i(1, 1))
	surface.set_cell(Vector2i(-1, 0), 0, Vector2i.ZERO)
	view.call("Rebuild")
	assert(surface.get_used_rect() == Rect2i(0, 0, 4, 3))
	assert(surface.get_used_cells().size() == 12, "Surface reconciliation did not repair a hole")
	# Bounds and cell count alone cannot detect a wrong atlas/alternative/source.
	var atlas: TileSetAtlasSource = surface.tile_set.get_source(0)
	var alternate := atlas.create_alternative_tile(Vector2i.ZERO)
	surface.set_cell(Vector2i(1, 1), 0, Vector2i.ZERO, alternate)
	view.call("Rebuild")
	assert(surface.get_cell_alternative_tile(Vector2i(1, 1)) == 0, "Fast fill accepted the wrong alternative")
	var wrong_source := TileSetAtlasSource.new()
	wrong_source.texture = atlas.texture
	wrong_source.texture_region_size = atlas.texture_region_size
	wrong_source.create_tile(Vector2i.ZERO)
	surface.tile_set.add_source(wrong_source, 1)
	surface.set_cell(Vector2i(1, 1), 1, Vector2i.ZERO)
	view.call("Rebuild")
	assert(surface.get_cell_source_id(Vector2i(1, 1)) == 0, "Fast fill accepted the wrong source")
	atlas.texture = ImageTexture.create_from_image(Image.create(atlas.texture_region_size.x * 2, atlas.texture_region_size.y, false, Image.FORMAT_RGBA8))
	atlas.create_tile(Vector2i(1, 0))
	surface.set_cell(Vector2i(1, 1), 0, Vector2i(1, 0))
	view.call("Rebuild")
	assert(surface.get_cell_atlas_coords(Vector2i(1, 1)) == Vector2i.ZERO, "Fast fill accepted the wrong atlas coordinate")
	view.set("BoundsSize", Vector2i(2, 2))
	view.call("Rebuild")
	assert(surface.get_used_rect() == Rect2i(0, 0, 2, 2) and surface.get_used_cells().size() == 4, "Shrinking left obsolete cells")
	view.set("BoundsSize", Vector2i(4, 3))
	view.call("Rebuild")
	assert(surface.get_used_cells().size() == 12, "Growing did not fill new cells")
	# Lighting is derived from live elevations, never the stale generation shade.
	var origin: Vector2i = view.get("BoundsOrigin")
	for y in range(3):
		for x in range(4):
			var at := origin + Vector2i(x, y)
			cells.call("SetTerrainKind", at, "grass")
			cells.call("SetMetadata", at, "terrain_elevation", 0.0)
			cells.call("SetMetadata", at, "terrain_shade", 0.7)
	view.call("Rebuild")
	var shade: Image = surface.material.get_shader_parameter("shade_map").get_image()
	cached_coast = surface.material.get_shader_parameter("coast_map")
	assert(absf(shade.get_pixel(1, 1).r * 2 - 1) < 0.01, "Stale stored shade darkened leveled ground")
	cells.call("SetMetadata", origin + Vector2i(2, 1), "terrain_elevation", 1.0)
	await process_frame
	await process_frame
	shade = surface.material.get_shader_parameter("shade_map").get_image()
	assert(shade.get_pixel(1, 1).r < 0.4, "Live uphill neighbour did not shade the slope")
	assert(shade.get_pixel(3, 1).r > 0.6, "Opposite slope did not face the light")
	assert(surface.material.get_shader_parameter("coast_map") == cached_coast, "Elevation-only edit rebuilt the coastline")
	cells.call("SetTerrainKind", origin + Vector2i(2, 1), "water")
	await process_frame
	await process_frame
	shade = surface.material.get_shader_parameter("shade_map").get_image()
	assert(surface.material.get_shader_parameter("coast_map") != cached_coast, "Flooding reused obsolete coastline")
	for x in range(4):
		assert(absf(shade.get_pixel(x, 1).r * 2 - 1) < 0.01, "Flooded elevation left obsolete hill lighting")
	cached_coast = surface.material.get_shader_parameter("coast_map")
	cells.call("SetTerrainKind", origin, "gravel")
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("coast_map") == cached_coast, "Dry terrain edit rebuilt the coast")
	view.set("CoastDetail", 2)
	view.call("Rebuild")
	var detailed: Texture2D = surface.material.get_shader_parameter("coast_map")
	assert(detailed != cached_coast and detailed.get_width() == 16, "Coast detail change reused wrong reconstructed resolution")
	view.set("CoastRangeTiles", 9.0)
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("coast_map") != detailed, "Coast range change reused wrong distance encoding")
	cached_coast = surface.material.get_shader_parameter("coast_map")
	cells.call("SetTerrainKind", origin + Vector2i(2, 1), "grass")
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("coast_map") != cached_coast, "Draining reused obsolete coastline")
	cached_coast = surface.material.get_shader_parameter("coast_map")
	other_cells.call("SetTerrainKind", origin, "water")
	view.set("CellDataPath", NodePath("../OtherCells"))
	view.call("Rebuild")
	assert(surface.material.get_shader_parameter("coast_map") != cached_coast, "Rebinding cells reused another source's coast")
	host.free()
	print("[terrain-painted-origin] OK")
	quit()
