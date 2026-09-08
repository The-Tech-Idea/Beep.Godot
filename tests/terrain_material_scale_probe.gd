extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless")
	var viewport := SubViewport.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	for y in 8:
		for x in 16:
			cells.call("SetTerrainKind", Vector2i(x, y), "grass" if x < 8 else "deep_water")
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(16, 8))
	view.set("TileSize", 32)
	view.set("ShallowTiles", 0.0)
	view.set("DeepTiles", 0.5)
	view.set("FoamStrength", 0.0)
	view.set("WaveIntensity", 0.0)
	viewport.add_child(view)
	assert(view.get("GroundTextureTiles") == 12.0 and view.get("WaterTextureTiles") == 6.0)
	for property in view.get_property_list(): assert(property.name != "TextureTiles", "Obsolete combined scale retained")
	view.call("Rebuild")
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	material.set_shader_parameter("wave_speed", 0.0)
	var pixels := Image.create(64, 64, true, Image.FORMAT_RGBA8)
	for y in 64:
		for x in 64:
			pixels.set_pixel(x, y, Color(0.2, 0.35, 0.45) if (x / 8 + y / 8) % 2 == 0 else Color(0.8, 0.7, 0.6))
	pixels.generate_mipmaps()
	var texture := ImageTexture.create_from_image(pixels)
	for slot in ["tex_grass", "tex_shallow", "tex_deep", "tex_sand"]:
		material.set_shader_parameter(slot, texture)
	var data: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]:
		data[slot] = material.get_shader_parameter(slot).get_image().get_data()
	for zoom in [0.5, 1.0, 2.0]:
		viewport.size = Vector2i(Vector2(512, 256) * zoom)
		view.scale = Vector2.ONE * zoom
		var images: Array[Image] = []
		for scales in [Vector2(6, 6), Vector2(12, 6), Vector2(12, 12)]:
			view.set("GroundTextureTiles", scales.x)
			view.set("WaterTextureTiles", scales.y)
			view.call("Rebuild")
			assert(material.get_shader_parameter("ground_texture_tiles") == scales.x)
			assert(material.get_shader_parameter("water_texture_tiles") == scales.y)
			for slot in data: assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot])
			await process_frame
			await RenderingServer.frame_post_draw
			images.append(viewport.get_texture().get_image())
		var ground := Rect2i(Vector2(32, 32) * zoom, Vector2(160, 160) * zoom)
		var water := Rect2i(Vector2(352, 32) * zoom, Vector2(96, 160) * zoom)
		assert(changed_pixels(images[0], images[1], ground) > ground.get_area() / 4, "Ground scale did not change rendered detail")
		assert(changed_pixels(images[0], images[1], water) == 0, "Ground detail resized the sea")
		assert(changed_pixels(images[1], images[2], ground) == 0, "Water scale resized the ground")
		assert(changed_pixels(images[1], images[2], water) > water.get_area() / 4, "Water scale did not change rendered detail")
		print("[terrain-material-scale] independent pixels at zoom ", zoom)
	viewport.free()
	await check_material_profiles()
	print("[terrain-material-scale] OK")
	quit()

func changed_pixels(a: Image, b: Image, area: Rect2i) -> int:
	var changed := 0
	for y in range(area.position.y, area.end.y):
		for x in range(area.position.x, area.end.x):
			assert(a.get_pixel(x, y).a == 1.0 and b.get_pixel(x, y).a == 1.0)
			if a.get_pixel(x, y) != b.get_pixel(x, y): changed += 1
	return changed

func check_material_profiles() -> void:
	var kinds := ["grass", "dry_grass", "desert", "sand", "tundra", "snow", "ice", "jungle", "swamp", "gravel", "rock", "lava"]
	var slots := ["Grass", "DryGrass", "Sand", "Sand", "Dirt", "Snow", "Snow", "Grass", "Mud", "Gravel", "Rock", "Lava"]
	var viewport := SubViewport.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	for y in 32:
		for x in 32:
			cells.call("SetTerrainKind", Vector2i(x, y), kinds[(y / 8) * 4 + x / 8] if y < 24 else "deep_water")
	var view: Node2D = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../Cells"))
	view.set("BoundsSize", Vector2i(32, 32))
	view.set("TileSize", 16)
	view.set("GroundTextureTiles", 12.0)
	view.set("ShallowTiles", 0.0)
	view.set("ShadeStrength", 0.0)
	view.set("FoamStrength", 0.0)
	view.set("WaveIntensity", 0.0)
	viewport.add_child(view)
	view.call("Rebuild")
	var material: ShaderMaterial = view.get_node("SplatSurface").material
	material.set_shader_parameter("wave_speed", 0.0)
	var pixels := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	for y in 64:
		for x in 64:
			pixels.set_pixel(x, y, Color(0.25, 0.3, 0.35) if (x / 8 + y / 8) % 2 == 0 else Color(0.85, 0.8, 0.75))
	pixels.generate_mipmaps()
	var texture := ImageTexture.create_from_image(pixels)
	for slot in ["grass", "dry_grass", "sand", "dirt", "snow", "mud", "gravel", "rock", "lava", "shallow", "deep"]:
		material.set_shader_parameter("tex_" + slot, texture)
	var data: Dictionary = {}
	for slot in ["id_map", "shade_map", "coast_map"]:
		data[slot] = material.get_shader_parameter(slot)
	var snapshot := var_to_bytes(cells.call("GetCells"))
	var profile = load(BASE + "terrain/TerrainMaterialTiling.cs").new()
	for zoom in [0.5, 1.0, 2.0]:
		viewport.size = Vector2i(Vector2(512, 512) * zoom)
		view.scale = Vector2.ONE * zoom
		view.set("MaterialTiling", null)
		view.call("Rebuild")
		await process_frame
		await RenderingServer.frame_post_draw
		var baseline := viewport.get_texture().get_image()
		for slot in ["Grass", "DryGrass", "Sand", "Dirt", "Snow", "Mud", "Gravel", "Rock", "Lava"]:
			profile.set(slot, 4.0)
			view.set("MaterialTiling", profile)
			view.call("Rebuild")
			for key in data: assert(material.get_shader_parameter(key) == data[key], "Tiling rebuilt terrain data")
			await process_frame
			await RenderingServer.frame_post_draw
			var changed := viewport.get_texture().get_image()
			for i in kinds.size():
				var area := Rect2i(Vector2((i % 4) * 128 + 32, (i / 4) * 128 + 32) * zoom, Vector2(64, 64) * zoom)
				var count := changed_pixels(baseline, changed, area)
				assert(count > area.get_area() / 4 if slots[i] == slot else count == 0,
					"Material repeat affected wrong terrain: slot=%s kind=%s zoom=%s count=%s" % [slot, kinds[i], zoom, count])
			var water := Rect2i(Vector2(32, 448) * zoom, Vector2(448, 32) * zoom)
			assert(changed_pixels(baseline, changed, water) == 0, "Ground override changed open sea")
			profile.set(slot, 0.0)
			view.call("Rebuild")
			await process_frame
			await RenderingServer.frame_post_draw
			assert(viewport.get_texture().get_image().get_data() == baseline.get_data(), "Clearing a slot did not restore default tiling")
		# Removing an assigned resource must clear previous shader uniforms too.
		profile.set("Rock", 3.0)
		profile.set("SeamBlend", 0.04)
		view.call("Rebuild")
		view.set("MaterialTiling", null)
		view.call("Rebuild")
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().get_data() == baseline.get_data(), "Removed tiling resource remained active")
		profile.set("Rock", 0.0)
		profile.set("SeamBlend", 0.0)
		print("[terrain-material-scale] all texture slots and aliases at zoom ", zoom)
	assert(var_to_bytes(cells.call("GetCells")) == snapshot, "Material tiling changed live grid state")
	# Explicit sand tiling also feeds the shared submerged-sand texture sample.
	view.set("MaterialTiling", profile)
	view.set("ShallowTiles", 8.0)
	var water_images: Array[Image] = []
	for sand_tiles in [0.0, 4.0]:
		profile.set("Sand", sand_tiles)
		view.call("Rebuild")
		await process_frame
		await RenderingServer.frame_post_draw
		water_images.append(viewport.get_texture().get_image())
	var shallow := Rect2i(64, 784, 896, 48)
	assert(changed_pixels(water_images[0], water_images[1], shallow) > shallow.get_area() / 4,
		"Sand profile resized the beach but not the visible seabed")
	viewport.free()
