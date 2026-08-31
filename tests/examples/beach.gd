extends SceneTree

# BeachWidth must actually WIDEN the beach.
#
# The interesting question about a setting is never "is it configurable" but
# "what reads it to make a decision". A beach that ignores its width setting
# looks perfectly plausible on screen - sand along every coast, nothing obviously
# wrong - and the control silently does nothing. That is the failure this guards,
# so it asks the map to prove the setting moved the result.
#
# Depth is sand tiles divided by the length of coast they sit on, which is
# scale-free: share of an island that is sand cannot answer this on its own,
# because a one-tile rim is a third of a 120-tile island and two thirds of a
# 25-tile one without the beach being any wider in either. Measured at width
# 1.0, mean depth came out 0.9 to 1.4 tiles across map types - so depth tracks
# the setting, and this checks it keeps doing so.

func depth(gen, size: Vector2i) -> float:
	var seen := {}
	var sand := 0
	var coast := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			if seen.has(c): continue
			seen[c] = true
			var k: String = gen.TerrainKindAt(c)
			if k == "deep_water" or k == "shallow_water" or k == "": continue
			if k == "sand":
				sand += 1
			for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
				var p: Vector2i = c + d
				if p.x < 0 or p.y < 0 or p.x >= size.x or p.y >= size.y:
					coast += 1
					break
				var pk: String = gen.TerrainKindAt(p)
				if pk == "deep_water" or pk == "shallow_water" or pk == "":
					coast += 1
					break
	return 0.0 if coast == 0 else float(sand) / float(coast)

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)

	gen.BoundsSize = Vector2i(128, 80)
	gen.Landform = 2
	gen.ArchipelagoIslandCount = 7
	gen.LandmassScale = 0.30

	var widths := [0.0, 1.0, 3.0]
	var depths: Array[float] = []
	for w in widths:
		gen.BeachWidth = float(w)
		gen.GenerateTerrain()
		var d: float = depth(gen, Vector2i(128, 80))
		depths.append(d)
		print("BeachWidth %.1f -> beach depth %.2f tiles" % [w, d])

	var failed := 0

	# The test is the DELTA, not an absolute floor, and the reason matters.
	# Width 0 does not give a sandless map: a LAKE keeps its own rim, which is a
	# separate rule with its own width, deliberately so - a sea beach is
	# surf-built and wide, a lake shore is a thin rim. Measured 0.21 tiles of
	# depth at width 0, all of it lake rim. That is correct behaviour, and an
	# earlier version of this guard called it a failure because the measurement
	# counted lake sand as coastal sand.
	#
	# Lake rims are the same in all three runs, so whatever changes between them
	# is the setting doing its job.
	if depths[1] <= depths[0] * 2.0:
		print("  width 1 (%.2f) barely moved from width 0 (%.2f)  FAIL"
			% [depths[1], depths[0]])
		failed += 1

	# Loose on the exact number: the beach follows a distance field through a
	# coastline that is not straight, and on a small island a three-tile beach
	# simply runs out of land to occupy, so depth never scales with the setting
	# one-for-one. The ORDER is what has to hold.
	if depths[2] <= depths[1] * 1.25:
		print("  width 3 (%.2f) was not clearly deeper than width 1 (%.2f)  FAIL"
			% [depths[2], depths[1]])
		failed += 1

	print("RESULT: ", "all checks passed" if failed == 0 else "%d FAILED" % failed)
	quit(1 if failed > 0 else 0)
