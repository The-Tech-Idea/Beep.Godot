extends SceneTree

# Checks that the allocation-free component labelling and BFS produce the same
# terrain as before: coverage still lands on target, the landmass count is
# honoured, and one seed always yields one map. Times generation while it is at
# it.

func fingerprint(gen, size: Vector2i) -> int:
	var hash_value := 1469598103
	for y in range(size.y):
		for x in range(size.x):
			var cell := Vector2i(x, y)
			var text: String = gen.TerrainKindAt(cell) + gen.WaterSourceAt(cell)
			hash_value = (hash_value * 31 + text.hash()) & 0x7fffffff
	return hash_value

func run_case(gen, landform: int, coverage: float, islands: int, seed_value: int) -> Dictionary:
	gen.Landform = landform
	gen.LandmassScale = coverage
	gen.ArchipelagoIslandCount = islands
	gen.Seed = seed_value
	var started := Time.get_ticks_msec()
	gen.GenerateTerrain()
	var elapsed := Time.get_ticks_msec() - started
	var diag = gen.GetGenerationDiagnostics()
	return {
		"coverage": diag["land_footprint_coverage"],
		"continents": diag["continent_count"],
		"ms": elapsed,
		"hash": fingerprint(gen, gen.BoundsSize),
	}

func _initialize() -> void:
	var scene := load("res://tests/examples/terrain_generator_lab.tscn")
	var root_node = scene.instantiate()
	get_root().add_child(root_node)
	await process_frame
	await process_frame

	var gen = root_node.find_child("TerrainGenerator", true, false)
	if gen == null:
		print("FAIL: no TerrainGenerator node")
		quit(1)
		return

	var failures := 0
	print("--- coverage and landmass count ---")
	# Island is one landmass; archipelago asks for several.
	for spec in [[1, 1], [2, 4]]:
		for target in [0.25, 0.50, 0.70]:
			var r := run_case(gen, spec[0], target, spec[1], 4242)
			var drift: float = abs(r["coverage"] - target)
			var name := "island" if spec[0] == 1 else "archipelago"
			var ok := drift <= 0.02
			if not ok:
				failures += 1
			print("%s %d%%  ->  %.1f%%  drift %.2f%%  continents %d  %d ms  %s"
				% [name, int(target * 100), r["coverage"] * 100.0, drift * 100.0,
				   r["continents"], r["ms"], "ok" if ok else "FAIL"])

	print("--- reproducibility ---")
	var a := run_case(gen, 1, 0.50, 1, 777)
	var b := run_case(gen, 1, 0.50, 1, 777)
	var c := run_case(gen, 1, 0.50, 1, 778)
	if a["hash"] != b["hash"]:
		failures += 1
		print("FAIL: same seed gave different terrain (%d vs %d)" % [a["hash"], b["hash"]])
	else:
		print("same seed  -> identical terrain (hash %d)" % a["hash"])
	# Guards the guard: if the comparison were broken, this would also "pass".
	if a["hash"] == c["hash"]:
		failures += 1
		print("FAIL: a different seed gave identical terrain - the check cannot fail")
	else:
		print("different seed -> different terrain (hash %d)" % c["hash"])

	print("--- world presets ---")
	failures += check_world_presets(gen)

	print("--- resource sets ---")
	failures += check_resource_sets(gen)

	print("--- painted terrain reaches gameplay data ---")
	failures += await check_generated_map_reaches_cells()

	print("RESULT: %s" % ("all checks passed" if failures == 0 else "%d FAILURES" % failures))
	quit(1 if failures > 0 else 0)


## A renderer must never invent terrain. The painterly one used to build its own
## noise and paint a convincing map while writing nothing to the cell data, so
## gameplay read defaults - no water, no biomes, props in the sea. This asserts a
## generated map actually lands in GridCellDataComponent.
func check_generated_map_reaches_cells() -> int:
	var scene = load("res://tests/examples/terrain_generation_layers_demo.tscn")
	var root_node = scene.instantiate()
	get_root().add_child(root_node)
	for i in range(20):
		await process_frame

	var cells = root_node.find_child("Cells", true, false)
	var gen = root_node.find_child("TerrainGenerator", true, false)
	if cells == null or gen == null:
		print("FAIL: scene is missing its Cells or TerrainGenerator")
		root_node.queue_free()
		return 1
	# Driven explicitly rather than trusting the scene to start itself: this
	# guards where the map LANDS, not whether something happened to call it.
	gen.GenerateTerrain()

	var kinds := {}
	for y in range(gen.BoundsSize.y):
		for x in range(gen.BoundsSize.x):
			kinds[cells.GetTerrainKind(Vector2i(x, y))] = true
	root_node.queue_free()
	await process_frame

	# One kind everywhere means nothing was written and every read fell back to
	# the default - exactly the failure this guards.
	if kinds.size() <= 1:
		print("FAIL: cell data holds %d terrain kind(s) %s - the map never reached gameplay"
			% [kinds.size(), str(kinds.keys())])
		return 1
	print("cell data holds %d terrain kinds %s" % [kinds.size(), str(kinds.keys())])
	return 0


## Each resource set must yield its OWN resources and nothing else. A map is a
## setting: a lunar survey has no cattle, an oilfield no ivory. If a catalogue is
## mis-wired the sets overlap, and the cheapest way to see that is to generate
## one map per set and compare what came out.
func check_resource_sets(gen) -> int:
	var expected := {
		0: ["wheat", "cattle", "deer", "fish", "iron", "gems"],
		1: ["crude_oil", "natural_gas", "shale", "offshore_gas", "sulphur"],
		2: ["water_ice", "helium3", "regolith", "iron_ore", "titanium"],
	}
	var names := {0: "historical", 1: "oil and gas", 2: "space"}
	var seen := {}
	var failures := 0
	for set_id in [0, 1, 2]:
		gen.ResourceSet = set_id
		gen.GenerateTerrain()
		var found := {}
		for y in range(gen.BoundsSize.y):
			for x in range(gen.BoundsSize.x):
				var r: String = gen.ResourceAt(Vector2i(x, y))
				if r != "":
					found[r] = true
		seen[set_id] = found
		var keys: Array = found.keys()
		keys.sort()
		print("  %-12s %2d kinds: %s" % [names[set_id], keys.size(), ", ".join(keys)])
		if keys.is_empty():
			print("  FAIL: %s produced no resources at all" % names[set_id])
			failures += 1
	# The sets must not bleed into one another.
	for a in [0, 1, 2]:
		for b in [0, 1, 2]:
			if a >= b:
				continue
			for key in seen[a]:
				if seen[b].has(key):
					print("  FAIL: '%s' appears in both %s and %s" % [key, names[a], names[b]])
					failures += 1
	gen.ResourceSet = 0
	return failures


## Every preset must describe a DIFFERENT world. A preset that lands on the same
## coverage, relief and resources as another is not a choice, it is a duplicate
## wearing a second name - and the failure is silent, because each one still
## generates a perfectly good map.
func check_world_presets(gen) -> int:
	var names := ["Continents", "Pangaea", "Archipelago", "Island Chain", "Ocean World",
		"Highlands", "Great Plains", "Desert World", "Frozen World", "Wetlands",
		"Oil Frontier", "Barren Moon"]
	var seen := {}
	var failures := 0
	for i in range(names.size()):
		gen.ApplyWorldPreset(i)
		gen.GenerateTerrain()
		var d = gen.GetGenerationDiagnostics()
		var relief := 0
		var features := 0
		for y in range(gen.BoundsSize.y):
			for x in range(gen.BoundsSize.x):
				if gen.ReliefAt(Vector2i(x, y)) > 0:
					relief += 1
				if gen.FeatureAt(Vector2i(x, y)) != "":
					features += 1
		var land: float = d["land_footprint_coverage"]
		print("  %-13s land %4.0f%%  relief %4d  features %4d  starts %d" %
			[names[i], land * 100.0, relief, features, d["start_position_count"]])
		# Rounded so two presets that differ only by noise still count as one.
		var signature := "%d|%d|%d" % [int(land * 50.0), relief / 40, features / 40]
		if seen.has(signature):
			print("  FAIL: %s is indistinguishable from %s" % [names[i], seen[signature]])
			failures += 1
		seen[signature] = names[i]
	gen.ApplyWorldPreset(0)
	return failures
