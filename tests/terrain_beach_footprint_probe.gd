extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func mask(generator: Node, size: Vector2i) -> PackedByteArray:
	var result := PackedByteArray()
	for y in size.y:
		for x in size.x:
			var kind: String = generator.call("TerrainKindAt", Vector2i(x, y))
			result.append(0 if kind in ["shallow_water", "deep_water", "water"] else 1)
	return result

func run() -> void:
	var generator = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.set("TopologySamplesPerCell", 4)
	generator.set("RiverDensity", 0.0)
	generator.set("LakeCoverage", 0.0)
	generator.set("HillsFraction", 0.0)
	generator.set("MountainsFraction", 0.0)
	generator.set("FeatureDensity", 0.0)
	generator.set("ResourceDensity", 0.0)
	generator.set("StartPositionCount", 0)
	generator.set("LandmassScale", 0.5)
	generator.set("ArchipelagoIslandCount", 4)
	root.add_child(generator)
	var failures := 0
	for size in [Vector2i(32, 32), Vector2i(64, 40)]:
		generator.set("BoundsSize", size)
		for landform in [0, 1, 2]:
			generator.set("Landform", landform)
			for seed in [31415, 8675309]:
				generator.set("Seed", seed)
				var baseline := PackedByteArray()
				var previous_sand := 0
				for beach in [0.0, 1.0, 3.0]:
					generator.set("BeachWidth", beach)
					var actual := mask(generator, size)
					var diagnostics: Dictionary = generator.call("GetGenerationDiagnostics")
					if baseline.is_empty():
						baseline = actual
					elif actual != baseline:
						failures += 1
						printerr("[terrain-beach-footprint] FAIL: BeachWidth moved land/water: size=%s landform=%d seed=%d width=%s" % [size, landform, seed, beach])
					if absf(diagnostics.land_footprint_coverage - 0.5) > 0.006:
						failures += 1
						printerr("[terrain-beach-footprint] FAIL: achievable land target missed: ", diagnostics.land_footprint_coverage)
					var sand := 0
					for y in size.y:
						for x in size.x:
							if generator.call("TerrainKindAt", Vector2i(x, y)) == "sand":
								sand += 1
					if sand < previous_sand or (beach == 3.0 and sand == 0):
						failures += 1
						printerr("[terrain-beach-footprint] FAIL: beach width did not paint the existing land")
					previous_sand = sand
	generator.free()
	if failures == 0:
		print("[terrain-beach-footprint] OK")
	quit(0 if failures == 0 else 1)
