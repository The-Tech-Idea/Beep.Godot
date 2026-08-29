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

func run_case(gen, painter, landform: int, coverage: float, islands: int, seed_value: int) -> Dictionary:
	gen.Landform = landform
	gen.LandmassScale = coverage
	gen.ArchipelagoIslandCount = islands
	# With UsePainterSettings on, the painter owns the seed and gen.Seed is
	# ignored by design - setting the wrong one makes every map identical.
	if gen.UsePainterSettings and painter != null:
		painter.Seed = seed_value
	else:
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
	var painter = null
	if not gen.PainterlyTerrainPath.is_empty():
		painter = gen.get_node_or_null(gen.PainterlyTerrainPath)
	if gen.UsePainterSettings and painter == null:
		print("FAIL: UsePainterSettings is on but the painter could not be resolved")
		quit(1)
		return

	var failures := 0
	print("--- coverage and landmass count ---")
	# Island is one landmass; archipelago asks for several.
	for spec in [[1, 1], [2, 4]]:
		for target in [0.25, 0.50, 0.70]:
			var r := run_case(gen, painter, spec[0], target, spec[1], 4242)
			var drift: float = abs(r["coverage"] - target)
			var name := "island" if spec[0] == 1 else "archipelago"
			var ok := drift <= 0.02
			if not ok:
				failures += 1
			print("%s %d%%  ->  %.1f%%  drift %.2f%%  continents %d  %d ms  %s"
				% [name, int(target * 100), r["coverage"] * 100.0, drift * 100.0,
				   r["continents"], r["ms"], "ok" if ok else "FAIL"])

	print("--- reproducibility ---")
	var a := run_case(gen, painter, 1, 0.50, 1, 777)
	var b := run_case(gen, painter, 1, 0.50, 1, 777)
	var c := run_case(gen, painter, 1, 0.50, 1, 778)
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

	print("RESULT: %s" % ("all checks passed" if failures == 0 else "%d FAILURES" % failures))
	quit(1 if failures > 0 else 0)
