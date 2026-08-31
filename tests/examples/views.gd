extends SceneTree

# Every view must be drawing the SAME world.
#
# The generator keeps two grids: samples, and the tiles they reduce to. The
# painted view draws from samples; the tile and isometric views draw from tiles.
# That is fine while the two agree - and the constraint stage used to change
# only the tiles, so a lake it drained vanished from the tile view and stayed,
# whole, in the painted one. One map, two answers, and nothing failed to say so.
#
# This asks both grids the same question at the same places and requires the
# same answer.

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20): await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)
	var failed := 0

	# Landform, island count, coverage, label.
	var cases := [
		[0, 3, 0.42, "continents"],
		[2, 7, 0.30, "archipelago"],
		[1, 1, 0.66, "pangaea"],
	]

	var size := Vector2i(96, 60)
	gen.BoundsSize = size
	for case in cases:
		gen.Landform = case[0]
		gen.ArchipelagoIslandCount = case[1]
		gen.LandmassScale = float(case[2])
		gen.GenerateTerrain()

		# A tile is a MAJORITY of its samples, so at a coastline the centre
		# sample can honestly differ from the tile's verdict. Scattered
		# single-tile differences along a shore are the reduction working, not a
		# fault. A whole LAKE present in one grid and absent from the other is
		# the fault - so what matters is the largest CONTIGUOUS disagreement,
		# not the raw count.
		var disagree := {}
		var water_tiles := 0
		for y in range(size.y):
			for x in range(size.x):
				var c := Vector2i(x, y)
				var kind: String = gen.TerrainKindAt(c)
				var tile_water: bool = kind == "deep_water" or kind == "shallow_water"
				if tile_water:
					water_tiles += 1
				var at := Vector2(float(x) + 0.5, float(y) + 0.5)
				if gen.IsWaterAtPosition(at) != tile_water:
					disagree[c] = true

		var seen := {}
		var biggest := 0
		for c in disagree:
			if seen.has(c): continue
			var q: Array[Vector2i] = [c]
			seen[c] = true
			var head := 0
			while head < q.size():
				var a: Vector2i = q[head]; head += 1
				for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
					var pp: Vector2i = a + d
					if seen.has(pp) or not disagree.has(pp): continue
					seen[pp] = true
					q.append(pp)
			biggest = max(biggest, q.size())

		# A patch this size is a feature, not a ragged edge.
		var ok: bool = biggest < 12
		if not ok:
			failed += 1
		print("%-12s %d water tiles | %d disagreeing, biggest patch %d  %s"
			% [case[3], water_tiles, disagree.size(), biggest, "ok" if ok else "FAIL"])

	print("RESULT: ", "all checks passed" if failed == 0 else "%d FAILED" % failed)
	quit(1 if failed > 0 else 0)
