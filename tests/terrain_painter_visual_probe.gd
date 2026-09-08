extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	assert(DisplayServer.get_name() != "headless", "Painter inspection requires rendering")
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	scene.get_node("World").set("MapSize", 0)
	scene.get_node("World").set("Seed", 31415)
	root.add_child(scene)
	await process_frame
	await process_frame
	scene.set_process(false)
	scene.get_node("HUD").hide()
	scene.get_node("Preview/MapOverlay").hide()
	var preview: Node2D = scene.get_node("Preview")
	var size: Vector2i = scene.get_node("World").get("BuiltSize")
	assert(size == Vector2i(32, 32))
	var generator: Node = scene.get_node("Preview/TerrainGenerator")
	print("[terrain-painter-visual] generation: ", generator.call("GetGenerationDiagnostics"))
	print("[terrain-painter-visual] longest straight coast: ", longest_coast(generator, size))
	DirAccess.make_dir_recursive_absolute("res://tests/output/painter_visual")
	for mode in ["overview", "close"]:
		var scale_value := 0.36 if mode == "overview" else 1.0
		preview.scale = Vector2.ONE * scale_value
		var focus := Vector2(16, 16) if mode == "overview" else Vector2(16, 8)
		preview.position = Vector2(640, 400) - focus * 64.0 * scale_value
		await process_frame
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		assert(image.save_png("res://tests/output/painter_visual/" + mode + ".png") == OK)
	if "--source-comparison" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var live_path: NodePath = painted.get("CellDataPath")
		assert(not live_path.is_empty(), "Comparison requires the lab's live grid binding")
		var images: Array[Image] = []
		for source in ["live", "generated"]:
			painted.set("CellDataPath", live_path if source == "live" else NodePath())
			painted.call("Rebuild")
			var material: ShaderMaterial = painted.get_node("SplatSurface").material
			# Compare geometry, not animated waves or different beach compositing.
			material.set_shader_parameter("wave_speed", 0.0)
			material.set_shader_parameter("beach_tiles", 0.0)
			images.append(material.get_shader_parameter("coast_map").get_image())
			for zoom in [0.36, 1.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/source-%s-zoom-%s.png" % [source, zoom]) == OK)
		assert(images[0].get_size() == images[1].get_size())
		var changed := 0
		for y in images[0].get_height():
			for x in images[0].get_width():
				if (images[0].get_pixel(x, y).r > 0.5) != (images[1].get_pixel(x, y).r > 0.5): changed += 1
		print("[terrain-painter-visual] source coast classification differences: %d / %d" % [changed, images[0].get_width() * images[0].get_height()])
		var centre_mismatches := 0
		var feature_mismatches := 0
		var cells: Node = scene.get_node("Preview/Cells")
		for y in size.y:
			for x in size.x:
				var cell := Vector2i(x, y)
				var at := Vector2(cell) + Vector2.ONE * 0.5
				var fine_water: bool = generator.call("IsWaterAtPosition", at)
				var cell_water: bool = generator.call("WaterSourceAt", cell) != ""
				if fine_water != cell_water: centre_mismatches += 1
				var feature = cells.call("GetMetadata", cell, "terrain_feature")
				if fine_water and feature is String and not feature.is_empty(): feature_mismatches += 1
		print("[terrain-painter-visual] fine/cell centre mismatches: %d; feature centres on fine water: %d" % [centre_mismatches, feature_mismatches])
		painted.set("CellDataPath", live_path)
		painted.call("Rebuild")
	if "--coast-filter" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		var helper = load("res://tests/TerrainCoastFilterSmoke.cs").new()
		var cells: Node = scene.get_node("Preview/Cells")
		var raw_coast: Texture2D = helper.call("BuildLive", cells, painted.get("BoundsOrigin"), size, painted.get("CoastDetail"))
		var baked_coast: Texture2D = material.get_shader_parameter("coast_map")
		var data: Dictionary = {}
		for slot in ["id_map", "shade_map"]:
			data[slot] = material.get_shader_parameter(slot).get_image().get_data()
		data["coast_map"] = raw_coast.get_image().get_data()
		material.set_shader_parameter("wave_speed", 0.0)
		RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(), true)
		for variant in ["linear", "baked", "linear", "baked"]:
			material.set_shader_parameter("coast_map", baked_coast if variant == "baked" else raw_coast)
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				var times: Array[float] = []
				for frame in 64:
					preview.queue_redraw()
					await process_frame
					await RenderingServer.frame_post_draw
					if frame >= 32: times.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
				times.sort()
				print("[terrain-painter-visual] coast-filter=", variant, " zoom=", zoom, " GPU median ms=", times[times.size() / 2], " range=", times[0], "..", times[-1])
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/coast-%s-zoom-%s.png" % [variant, zoom]) == OK)
			for slot in ["id_map", "shade_map"]:
				assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot], "Coast filtering rewrote world data")
			assert(raw_coast.get_image().get_data() == data["coast_map"])
		material.set_shader_parameter("coast_map", baked_coast)
		helper.free()
	if "--bedrock" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		material.set_shader_parameter("wave_speed", 0.0)
		var original = painted.get("MaterialTiling")
		var original_path: String = painted.get("RockTexturePath")
		var profile = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainMaterialTiling.cs").new()
		profile.set("Rock", 6.0)
		painted.set("MaterialTiling", profile)
		var data: Dictionary = {}
		for slot in ["id_map", "shade_map", "coast_map"]:
			data[slot] = material.get_shader_parameter(slot).get_image().get_data()
		for art in ["rock", "bedrock_ground"]:
			painted.set("RockTexturePath", "res://addons/beep_game_builder_cs/textures/terrain/" + art + ".png")
			painted.call("Rebuild")
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/%s-zoom-%s.png" % [art, zoom]) == OK)
			for slot in data: assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot], "Bedrock art rewrote terrain")
		painted.set("RockTexturePath", original_path)
		painted.set("MaterialTiling", original)
		painted.call("Rebuild")
	if "--rock-scale" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		material.set_shader_parameter("wave_speed", 0.0)
		var profile = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainMaterialTiling.cs").new()
		var original = painted.get("MaterialTiling")
		var data: Dictionary = {}
		for slot in ["id_map", "shade_map", "coast_map"]:
			data[slot] = material.get_shader_parameter(slot).get_image().get_data()
		painted.set("MaterialTiling", profile)
		for tiles in [12.0, 6.0, 3.0]:
			profile.set("Rock", tiles)
			painted.call("Rebuild")
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/rock-%s-zoom-%s.png" % [tiles, zoom]) == OK)
			for slot in data: assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot], "Rock scale rewrote terrain")
		painted.set("MaterialTiling", original)
		painted.call("Rebuild")
	if "--coast-detail" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		for detail in [4, 8]:
			painted.set("CoastDetail", detail)
			painted.call("Rebuild")
			var material: ShaderMaterial = painted.get_node("SplatSurface").material
			material.set_shader_parameter("wave_speed", 0.0)
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/coast-detail-%s-zoom-%s.png" % [detail, zoom]) == OK)
	if "--material-tuning" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		var ids: PackedByteArray = material.get_shader_parameter("id_map").get_image().get_data()
		var coast: PackedByteArray = material.get_shader_parameter("coast_map").get_image().get_data()
		for texture_tiles in [6.0, 12.0]:
			material.set_shader_parameter("ground_texture_tiles", texture_tiles)
			for neutral in [false, true]:
				material.set_shader_parameter("tint_dry_grass", Vector3.ONE if neutral else Vector3(0.80, 0.84, 0.56))
				for zoom in [0.36, 1.0]:
					preview.scale = Vector2.ONE * zoom
					var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
					preview.position = Vector2(640, 400) - focus * 64.0 * zoom
					await process_frame
					await RenderingServer.frame_post_draw
					var name := "tuning-%s-neutral-%s-zoom-%s.png" % [texture_tiles, neutral, zoom]
					assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/" + name) == OK)
				assert(material.get_shader_parameter("id_map").get_image().get_data() == ids)
				assert(material.get_shader_parameter("coast_map").get_image().get_data() == coast)
	if "--edge-detail" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		material.set_shader_parameter("wave_speed", 0.0)
		for amount in [0.0, 0.75]:
			painted.set("MaterialEdgeDetail", amount)
			painted.call("Rebuild")
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/edge-%s-zoom-%s.png" % [amount, zoom]) == OK)
	if "--meadow-art" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		var meadow: Texture2D = load("res://addons/beep_game_builder_cs/textures/terrain/meadow_ground.png")
		var dry_meadow: Texture2D = load("res://addons/beep_game_builder_cs/textures/terrain/dry_meadow_ground.png")
		var original_grass: Texture2D = load("res://addons/beep_game_builder_cs/textures/terrain/grass.png")
		var original_dry: Texture2D = load("res://addons/beep_game_builder_cs/textures/terrain/dry_grass.png")
		material.set_shader_parameter("wave_speed", 0.0)
		var data: Dictionary = {}
		for slot in ["id_map", "shade_map", "coast_map"]:
			data[slot] = material.get_shader_parameter(slot).get_image().get_data()
		for variant in ["original", "meadow"]:
			material.set_shader_parameter("tex_grass", original_grass if variant == "original" else meadow)
			material.set_shader_parameter("tex_dry_grass", original_dry if variant == "original" else dry_meadow)
			for zoom in [0.36, 1.0, 2.0]:
				preview.scale = Vector2.ONE * zoom
				var focus := Vector2(16, 16) if zoom < 1.0 else Vector2(16, 8)
				preview.position = Vector2(640, 400) - focus * 64.0 * zoom
				await process_frame
				await RenderingServer.frame_post_draw
				assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/art-%s-zoom-%s.png" % [variant, zoom]) == OK)
			for slot in data:
				assert(material.get_shader_parameter(slot).get_image().get_data() == data[slot])
	if "--diagnose" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var material_counts: Dictionary = {}
		var ids: Image = painted.get_node("SplatSurface").material.get_shader_parameter("id_map").get_image()
		for y in range(ids.get_height()):
			for x in range(ids.get_width()):
				var id := roundi(ids.get_pixel(x, y).r * 255.0)
				material_counts[id] = material_counts.get(id, 0) + 1
		print("[terrain-painter-visual] material ID counts: ", material_counts)
		painted.set("ShadeStrength", 0.0)
		painted.call("Rebuild")
		await process_frame
		await RenderingServer.frame_post_draw
		assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/close-no-shade.png") == OK)
		var white := Image.create(4, 4, false, Image.FORMAT_RGBA8)
		white.fill(Color.WHITE)
		var plain := ImageTexture.create_from_image(white)
		var material: ShaderMaterial = painted.get_node("SplatSurface").material
		for slot in ["tex_grass", "tex_dry_grass", "tex_dirt", "tex_snow", "tex_mud", "tex_gravel", "tex_rock", "tex_sand"]:
			material.set_shader_parameter(slot, plain)
		await process_frame
		await RenderingServer.frame_post_draw
		assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/close-flat-material.png") == OK)
	if "--materials" in OS.get_cmdline_user_args():
		var painted: Node = scene.get_node("Preview/Splat")
		var cells: Node = scene.get_node("Preview/Cells")
		var plots: Array[Vector2i] = []
		for y in range(size.y):
			for x in range(size.x):
				if cells.call("GetTerrainKind", Vector2i(x, y)) == "dry_grass":
					plots.append(Vector2i(x, y))
		assert(not plots.is_empty())
		scene.get_node("Preview/Features").hide()
		painted.set("ShadeStrength", 0.35)
		for kind in ["grass", "desert", "mud", "snow"]:
			for cell in plots: cells.call("SetTerrainKind", cell, kind)
			painted.call("Rebuild")
			await process_frame
			await RenderingServer.frame_post_draw
			assert(root.get_texture().get_image().save_png("res://tests/output/painter_visual/" + kind + "-material.png") == OK)
	if "--topology" in OS.get_cmdline_user_args():
		var river: float = generator.get("RiverDensity")
		var lake: float = generator.get("LakeCoverage")
		for variant in [Vector2(river, lake), Vector2(0, lake), Vector2(river, 0), Vector2.ZERO]:
			generator.set("RiverDensity", variant.x)
			generator.set("LakeCoverage", variant.y)
			print("[terrain-painter-visual] rivers=%s lakes=%s regions=%s diagnostics=%s" % [variant.x, variant.y, land_regions(generator, size), generator.call("GetGenerationDiagnostics")])
	scene.free()
	print("[terrain-painter-visual] OK")
	quit()

func longest_coast(generator: Node, size: Vector2i) -> int:
	var land: Array[bool] = []
	for y in size.y:
		for x in size.x:
			land.append(generator.call("WaterSourceAt", Vector2i(x, y)) == "")
	var longest := 0
	for direction: Vector2i in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
		var horizontal := direction.x == 0
		for line in (size.y if horizontal else size.x):
			var run := 0
			for step in (size.x if horizontal else size.y):
				var cell := Vector2i(step, line) if horizontal else Vector2i(line, step)
				var other := cell + direction
				var edge := land[cell.y * size.x + cell.x] and Rect2i(Vector2i.ZERO, size).has_point(other) and not land[other.y * size.x + other.x]
				run = run + 1 if edge else 0
				longest = maxi(longest, run)
	return longest

func land_regions(generator: Node, size: Vector2i) -> Array:
	var remaining: Dictionary = {}
	for y in size.y:
		for x in size.x:
			var cell := Vector2i(x, y)
			if generator.call("WaterSourceAt", cell) == "": remaining[cell] = true
	var regions: Array = []
	while not remaining.is_empty():
		var queue: Array[Vector2i] = [remaining.keys()[0]]
		remaining.erase(queue[0])
		var read := 0
		var labels: Dictionary = {}
		while read < queue.size():
			var cell := queue[read]
			read += 1
			var label: int = generator.call("ContinentAt", cell)
			labels[label] = labels.get(label, 0) + 1
			for direction in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
				var other: Vector2i = cell + direction
				if remaining.erase(other): queue.append(other)
		regions.append({"cells": queue.size(), "labels": labels, "small_region_cells": queue if queue.size() < 10 else []})
	return regions
