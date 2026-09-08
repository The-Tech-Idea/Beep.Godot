extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var helper = load("res://tests/TerrainCoastFilterSmoke.cs").new()
	for edge in [144, 240]:
		var host := Node2D.new()
		root.add_child(host)
		var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
		cells.name = "Cells"
		host.add_child(cells)
		for y in range(edge):
			for x in range(edge):
				cells.call("SetTerrainKind", Vector2i(x, y), "water" if x < edge / 3 else "grass")
		var view: Node = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainPaintedRendererComponent.cs").new()
		view.set("RefreshOnReady", false)
		view.set("CellDataPath", NodePath("../Cells"))
		view.set("BoundsSize", Vector2i(edge, edge))
		view.set("CoastDetail", 4)
		host.add_child(view)
		view.call("Rebuild")
		var times: Array[float] = []
		for iteration in range(5):
			var start := Time.get_ticks_usec()
			view.call("Rebuild")
			times.append((Time.get_ticks_usec() - start) / 1000.0)
		times.sort()
		var material: ShaderMaterial = view.get_node("SplatSurface").material
		var coast: Image = material.get_shader_parameter("coast_map").get_image()
		var hash := HashingContext.new()
		hash.start(HashingContext.HASH_SHA256)
		var raw: ImageTexture = helper.call("BuildLive", cells, Vector2i.ZERO, Vector2i(edge, edge), 4)
		hash.update(raw.get_image().get_data())
		var reconstructed: ImageTexture = helper.call("Bake", raw, Vector2i(edge, edge))
		assert(coast.get_data() == reconstructed.get_image().get_data(), "Renderer coast differs from cached reconstruction")
		var checksum := hash.finish().hex_encode()
		# Analytic straight coast, independent of the implementation and byte format.
		var raw_image := raw.get_image()
		for x in raw_image.get_width():
			var signed_distance: float = edge / 3.0 - (x + 0.5) / 4.0
			var expected := clampf(signed_distance / 10.0 + 0.5, 0.0, 1.0)
			assert(absf(raw_image.get_pixel(x, edge * 2).r - expected) < 0.00001)
		print("[terrain-repaint-profile] edge=%d median_ms=%.3f coast=%s" % [edge, times[2], checksum])
		for y in range(edge):
			for x in range(edge):
				cells.call("SetMetadata", Vector2i(x, y), "terrain_elevation", ((x * 7 + y * 3) % 37) / 36.0)
		view.call("Rebuild")
		for parameter in ["id_map", "shade_map"]:
			hash.start(HashingContext.HASH_SHA256)
			hash.update(material.get_shader_parameter(parameter).get_image().get_data())
			var texture_hash := hash.finish().hex_encode()
			var expected_maps := {
				144: {"id_map": "a0d879a6956894246e5f8ef119db2aedb2c7d84505f26a506c4586e1ac356ee1", "shade_map": "b2a83747ac7eff162af8308441b6a74a49bf2a5993bea6f65f0de019152925b9"},
				240: {"id_map": "3f5c97c75b8267bdda21c3cc2ba08f14ce53514c996f30926ced806a2c2af0c9", "shade_map": "bfc944d242cd63932eacfc48bee3ba3acd27ef74469818283577212c5f0f9337"}
			}
			if parameter == "shade_map":
				assert(texture_hash == expected_maps[edge][parameter], "Batched image changed slope lighting")
			else:
				var ids: Image = material.get_shader_parameter(parameter).get_image()
				for y in edge:
					for x in edge:
						var pixel := ids.get_pixel(x, y)
						var expected_id := 12 if x < edge / 3 else 0
						assert(roundi(pixel.r * 255) == expected_id and roundi(pixel.b * 255) == expected_id and pixel.a == 0.0,
							"Manual terrain IDs or absent generated-beach width changed")
			print("[terrain-repaint-profile] edge=%d %s=%s" % [edge, parameter, texture_hash])
		host.free()
	helper.free()
	print("[terrain-repaint-profile] OK")
	quit()
