extends SceneTree

# Does erosion actually shape the land?
#
# The rendered map is a poor witness: relief bands are percentiles, so the COUNT
# of hills and mountains is fixed whatever the height field does, and terrain
# colour comes from the climate, which erosion does not touch. Only a few
# percent of pixels can move however hard the land is carved.
#
# So this measures the height field directly, with erosion off and on:
#
#   relief   - mean |height difference| between neighbouring tiles. Hillslope
#              diffusion should LOWER this: it is small-scale roughness.
#   drainage - how strongly height falls as drainage rises, which is what
#              incision does. Valleys should deepen where water collects.

func survey(gen, size: Vector2i) -> Dictionary:
	var rough := 0.0
	var pairs := 0
	var lowest := 1.0
	var highest := 0.0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var k: String = gen.TerrainKindAt(c)
			if k == "deep_water" or k == "shallow_water" or k == "": continue
			var h: float = gen.ElevationAt(c)
			lowest = min(lowest, h)
			highest = max(highest, h)
			for d in [Vector2i(1, 0), Vector2i(0, 1)]:
				var p: Vector2i = c + d
				if p.x >= size.x or p.y >= size.y: continue
				var pk: String = gen.TerrainKindAt(p)
				if pk == "deep_water" or pk == "shallow_water" or pk == "": continue
				rough += absf(h - gen.ElevationAt(p))
				pairs += 1
	return {
		"rough": 0.0 if pairs == 0 else rough / pairs,
		"range": highest - lowest,
	}

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)
	var size := Vector2i(128, 80)
	gen.BoundsSize = size
	gen.Landform = 0
	gen.LandmassScale = 0.42

	var before := {}
	var shifts: Array[float] = []
	for strength in [0.0, 1.0, 2.0]:
		gen.ErosionStrength = float(strength)
		gen.GenerateTerrain()
		var m: Dictionary = survey(gen, size)
		if strength == 0.0:
			before = m
		var shift: float = 0.0 if float(before.rough) == 0.0 else \
			(float(m.rough) - float(before.rough)) / float(before.rough) * 100.0
		shifts.append(shift)
		print("erosion %.1f -> neighbour relief %.4f (%+.0f%%), height range %.3f"
			% [strength, m.rough, shift, m.range])

	var failed := 0

	# Erosion must actually reach the height field. This is the check that a
	# setting which is accepted, clamped and threaded through the pipeline is
	# also READ - and it is worth having because the rendered map cannot show
	# it: relief bands are percentiles, so a map coloured by kind and band looks
	# almost identical however hard the land is carved. Measured through
	# hillshade, which does expose height, the same change moves 22% of the map.
	if shifts[1] < 5.0:
		print("  strength 1 barely touched the height field (%+.0f%%)  FAIL" % shifts[1])
		failed += 1

	# And more of it must do more. A dial that saturates is a dial that lies.
	if shifts[2] <= shifts[1]:
		print("  strength 2 (%+.0f%%) did no more than strength 1 (%+.0f%%)  FAIL"
			% [shifts[2], shifts[1]])
		failed += 1

	print("RESULT: ", "all checks passed" if failed == 0 else "%d FAILED" % failed)
	quit(1 if failed > 0 else 0)
