extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func verify_encoding(generator: Node, image: Image) -> void:
	var size := image.get_size()
	var wet := PackedByteArray()
	var ocean := PackedByteArray()
	for y in range(32):
		for x in range(48):
			ocean.append(1 if generator.call("WaterSourceAt", Vector2i(x, y)) == "ocean" else 0)
	for y in size.y:
		for x in size.x:
			var water: bool = generator.call("IsWaterAtPosition", Vector2(x + 0.5, y + 0.5) / 4.0)
			wet.append(1 if water else 0)
			var colour := image.get_pixel(x, y)
			assert((colour.r > 0.5) == water, "Encoded coast sign disagrees with generated water")
			assert(colour.b >= 0.0 and colour.b <= 1.0 and colour.a == 1.0, "Invalid ocean distance or generated-geometry flag")
			var near_ocean := false
			for dy in range(-1, 2):
				for dx in range(-1, 2):
					var cx := x / 4 + dx
					var cy := y / 4 + dy
					if cx >= 0 and cy >= 0 and cx < 48 and cy < 32:
						near_ocean = near_ocean or ocean[cy * 48 + cx] == 1
			assert((colour.g > 0.5) == near_ocean, "Ocean channel disagrees with generated water-source metadata")
	# Independent nearest-opposite-sample Euclidean oracle.
	# This validates encoding without freezing a historical island-generation bug.
	for y in [0, 35, 64, 103, 127]:
		for x in [0, 33, 96, 160, 191]:
			var water := wet[y * size.x + x]
			var nearest := 1000000
			for sy in size.y:
				for sx in size.x:
					if wet[sy * size.x + sx] == water:
						continue
					var dx := absi(x - sx)
					var dy := absi(y - sy)
					nearest = mini(nearest, dx * dx + dy * dy)
			var signed_distance := (sqrt(nearest) - 0.5) / 4.0 * (1.0 if water == 1 else -1.0)
			var expected := clampf(signed_distance / 5.0 * 0.5 + 0.5, 0, 1)
			assert(absf(image.get_pixel(x, y).r - expected) < 0.00001, "Coast distance disagrees with independent reference")

func run() -> void:
	var helper = load("res://tests/TerrainCoastFilterSmoke.cs").new()
	var host := Node2D.new()
	root.add_child(host)
	var generator: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.set("BoundsSize", Vector2i(48, 32))
	generator.set("TopologySamplesPerCell", 4)
	host.add_child(generator)
	var view: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("TerrainGeneratorPath", NodePath("../Generator"))
	view.set("BoundsSize", Vector2i(48, 32))
	view.set("CoastDetail", 4)
	host.add_child(view)
	var hashes: Array[String] = []
	for seed in [12345, 98765]:
		generator.set("Seed", seed)
		view.call("Rebuild")
		var baseline: PackedByteArray = view.get_node("SplatSurface").material.get_shader_parameter("coast_map").get_image().get_data()
		var times: Array[float] = []
		for iteration in range(5):
			var start := Time.get_ticks_usec()
			view.call("Rebuild")
			times.append((Time.get_ticks_usec() - start) / 1000.0)
			assert(view.get_node("SplatSurface").material.get_shader_parameter("coast_map").get_image().get_data() == baseline, "Identical rebuild changed coast bytes")
		times.sort()
		var material: ShaderMaterial = view.get_node("SplatSurface").material
		var image: Image = material.get_shader_parameter("coast_map").get_image()
		var hash := HashingContext.new()
		hash.start(HashingContext.HASH_SHA256)
		hash.update(image.get_data())
		var checksum := hash.finish().hex_encode()
		var raw: ImageTexture = helper.call("BuildRaw", generator, Vector2i(48, 32), 4)
		verify_encoding(generator, raw.get_image())
		var reconstructed: ImageTexture = helper.call("Bake", raw, Vector2i(48, 32))
		assert(image.get_data() == reconstructed.get_image().get_data(), "Renderer did not bind reconstructed raw coast")
		hashes.append(checksum)
		print("[terrain-generated-coast] seed=%d median_ms=%.3f coast=%s" % [seed, times[2], checksum])
	assert(hashes[0] != hashes[1], "Seed change reused obsolete generated coast")
	host.free()
	helper.free()
	print("[terrain-generated-coast] OK")
	quit()
