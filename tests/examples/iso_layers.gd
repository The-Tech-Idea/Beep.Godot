extends SceneTree

# Checks the isometric layer stack is INTERLEAVED, not piled.
#
# The order a scene needs is sea, ground, the props standing on the ground,
# upper ground, the props standing on that - five layers. Getting this wrong
# does not look like a bug from the code: every tile and every sprite still
# draws, the map still fills the screen, and the only symptom is that a tree on
# low ground hangs in front of the cliff that should hide it. That is exactly
# the kind of defect a screenshot argues about and a guard settles.
#
#   godot --headless --path <project> --script res://tests/examples/iso_layers.gd

# The stack ABOVE the waterline, which is the one the eye reads. The seabed
# sits below it and is checked separately, by depth.
# All terrain first, then props by level. Props must NOT interleave: higher
# terrain is not always further from the camera, so a hill behind a tree would
# draw over it and cut the tree off at the trunk.
const EXPECTED := [
	{"kind": "water", "level": 0, "z": 0},    # the sea, one surface
	{"kind": "terrain", "level": 1, "z": 2},  # ground
	{"kind": "terrain", "level": 2, "z": 4},  # hills
	{"kind": "terrain", "level": 3, "z": 6},  # mountain flanks
	{"kind": "terrain", "level": 4, "z": 8},  # summits, deep inside a massif
	{"kind": "props", "level": 1, "z": 11},   # trees standing on the ground
	{"kind": "props", "level": 2, "z": 12},   # trees standing on the hills
	{"kind": "props", "level": 3, "z": 13},   # trees standing on the flanks
	{"kind": "props", "level": 4, "z": 14},   # nothing grows this high, but it owns a slot
]

# Seven slots above the waterline, plus the bed beneath it. Nine layers spanning
# three tile heights was the shape this replaced.
#
# The third TERRAIN level is what makes a mountain read as a mountain. Hills and
# mountains are separate relief bands but were drawn at the same height, so a
# peak was the same two-block stack as a hillside and the classification made no
# visible difference. Each band now has its own step, and a cell draws every
# level from the ground up to its own - which is why level 2 still covers every
# raised cell, mountains included, rather than only the hills.

var failures: Array[String] = []

func check(condition: bool, message: String) -> void:
	if condition:
		print("  ok    ", message)
	else:
		print("  FAIL  ", message)
		failures.append(message)

func _initialize() -> void:
	var scene := load("res://tests/examples/terrain_iso_demo.tscn")
	var root_node = scene.instantiate()
	get_root().add_child(root_node)
	await process_frame
	await process_frame

	var iso = root_node.find_child("Iso", true, false)
	var features = root_node.find_child("IsoFeatures", true, false)
	if iso == null or features == null:
		print("FAIL: terrain_iso_demo.tscn is missing Iso or IsoFeatures")
		quit(1)
		return

	var stack: Array = []
	stack.append_array(iso.GetLayerDiagnostics())
	stack.append_array(features.GetLayerDiagnostics())
	stack.sort_custom(func(a, b): return int(a["z"]) < int(b["z"]))

	print("isometric layer stack")
	for entry in stack:
		print("  z%-3d %-8s level %d  %d cells" % [
			int(entry["z"]), entry["kind"], int(entry["level"]), int(entry["cells"])])

	# Sorting by z first means this reads the DRAWN order, not the order the two
	# renderers happened to report in.
	var above: Array = []
	var beds: Array = []
	for entry in stack:
		if entry["kind"] == "seabed":
			beds.append(entry)
		else:
			above.append(entry)

	# How much ground each level actually holds. A level that exists but is
	# nearly empty is a level that does nothing visible, and the counts are the
	# only way to tell that from one doing its job.
	var counted: Array = []
	for entry in above:
		if entry["kind"] == "terrain":
			counted.append("L%d:%d" % [int(entry["level"]), int(entry["cells"])])
	print("  terrain cells per level: %s" % " ".join(counted))

	check(above.size() == EXPECTED.size(),
		"%d layers above the waterline, expected %d" % [above.size(), EXPECTED.size()])

	if above.size() == EXPECTED.size():
		for i in range(EXPECTED.size()):
			var got: Dictionary = above[i]
			var want: Dictionary = EXPECTED[i]
			check(
				got["kind"] == want["kind"] and int(got["level"]) == int(want["level"])
					and int(got["z"]) == int(want["z"]),
				"slot %d is %s level %d at z%d" % [
					i, want["kind"], int(want["level"]), int(want["z"])])

	# Every seabed step is below the surface it is meant to be seen through,
	# and each step further out is drawn behind the one before it.
	# ONE seabed layer. It was one per depth band, stacked at descending
	# offsets - which said in geometry what the water shader already says in
	# colour, and made the stack three tile heights deep.
	check(beds.size() == 1, "the seabed is one layer (%d)" % beds.size())
	for bed in beds:
		check(int(bed["z"]) < 0, "the seabed draws below the water")

	# The whole stack has to stay shallow. A map three storeys tall reads as
	# crammed however few kinds of terrain are on it.
	# Derived from the level count rather than a hardcoded 2. The formula said
	# "two levels above the ground" and went stale the moment a third was added:
	# it still reported 1.16 tile heights when the real stack was already 1.66.
	# A cap that measures the wrong thing is worse than no cap, because it passes.
	var terrain_levels := 0
	for entry in above:
		if entry["kind"] == "terrain":
			terrain_levels += 1
	var steps: float = float(terrain_levels)
	var span: float = absf(float(iso.SeabedStep) + float(iso.LevelHeight) * steps)
	var heights: float = span / maxf(1.0, float(iso.CellSize.y))
	# The bar is what the eye can still read as one map, not an arbitrary round
	# number. Nine layers spanning three tile heights was the shape this
	# replaced, and that was unreadable; a tapering mountain reaching about two
	# and a quarter is not. What matters is that this MEASURES the real stack, so
	# the next level added has to be argued for rather than slipping in unseen.
	check(heights <= 2.5, "the stack is %.2f tile heights (max 2.5)" % heights)

	# Every prop layer draws after EVERY terrain layer, and prop layers keep
	# their own level order. This is the invariant the ordering exists for: a
	# tree must not be cut off by ground that happens to be higher.
	var highest_terrain := -99999
	for entry in stack:
		if entry["kind"] == "terrain" or entry["kind"] == "water":
			highest_terrain = max(highest_terrain, int(entry["z"]))
	var last_prop_z := -99999
	var last_prop_level := -99999
	for entry in stack:
		if entry["kind"] != "props":
			continue
		check(int(entry["z"]) > highest_terrain,
			"level %d props draw after all terrain (z%d > z%d)"
				% [int(entry["level"]), int(entry["z"]), highest_terrain])
		check(int(entry["z"]) > last_prop_z and int(entry["level"]) > last_prop_level,
			"level %d props draw above the level below's props" % int(entry["level"]))
		last_prop_z = int(entry["z"])
		last_prop_level = int(entry["level"])

	# Relative z would re-pile the stack the moment either parent moved off
	# zero, and the two halves live under different parents.
	for entry in stack:
		check(not bool(entry["relative_z"]),
			"%s level %d uses absolute z" % [entry["kind"], int(entry["level"])])

	# A correctly ordered stack of empty layers proves nothing. The sea level is
	# the exception: it is a position anchor now, and the surface draws the sea.
	for entry in stack:
		if entry["kind"] == "terrain" and int(entry["level"]) == 0:
			continue
		if entry["kind"] == "terrain" or (entry["kind"] == "props" and int(entry["level"]) == 1):
			check(int(entry["cells"]) > 0,
				"%s level %d actually drew something" % [entry["kind"], int(entry["level"])])

	# The layers must STACK, not partition. Each cell living in exactly one
	# layer is the defect this file exists to catch: it still draws a complete
	# map, so nothing looks broken, but there is no sea under the land and a
	# cliff has no ground beneath it to stand on.
	var gen = root_node.find_child("TerrainGenerator", true, false)
	var size: Vector2i = gen.BoundsSize
	var land := 0
	var raised := 0
	var rivers := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var kind: String = gen.TerrainKindAt(c)
			if kind == "deep_water" or kind == "shallow_water":
				# A river sits AT ground level, so it belongs on the ground layer
				# even though it is water. The sea and lakes stay holes in that
				# layer - the bed shows through them - but a river is one tile
				# wide and a one-tile hole is hidden entirely behind the block
				# sprite of the tile in front of it, which made the drainage
				# network invisible.
				if gen.WaterSourceAt(c) == "river":
					rivers += 1
				continue
			land += 1
			if int(gen.ReliefAt(c)) > 0:
				raised += 1

	var counts := {}
	for entry in stack:
		if entry["kind"] == "terrain":
			counts[int(entry["level"])] = int(entry["cells"])

	var bed_cells := 0
	for entry in stack:
		if entry["kind"] == "seabed":
			bed_cells += int(entry["cells"])

	# Lakes moved to their own layer so they can carry their own opacity; the
	# two surfaces together must still cover the map, or the split lost cells.
	# No empty layer holding a slot open. The sea is the surface; a tile layer
	# for it drew nothing and cost one of the five.
	var empty := 0
	for entry in stack:
		if entry["kind"] == "terrain" and int(entry["cells"]) == 0:
			empty += 1
	check(empty == 0, "no terrain layer is empty (%d are)" % empty)
	# A bed under every water cell WITHIN the see-through band, and none beyond
	# it. Beyond, the water is opaque and a bed is invisible - except at its own
	# edge, which stops at the map border and drew a straight cut across the
	# shallows. Depth here is the same breadth-first sweep out from the coast
	# that the renderer uses.
	var depth := {}
	var queue: Array[Vector2i] = []
	for y in range(size.y):
		for x in range(size.x):
			var k: String = gen.TerrainKindAt(Vector2i(x, y))
			if k != "deep_water" and k != "shallow_water":
				depth[Vector2i(x, y)] = 0
				queue.append(Vector2i(x, y))
	var head := 0
	while head < queue.size():
		var c: Vector2i = queue[head]
		head += 1
		for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
			var n: Vector2i = c + d
			if n.x < 0 or n.y < 0 or n.x >= size.x or n.y >= size.y: continue
			if depth.has(n): continue
			var nk: String = gen.TerrainKindAt(n)
			if nk != "deep_water" and nk != "shallow_water": continue
			depth[n] = int(depth[c]) + 1
			queue.append(n)

	var bedded := 0
	for c in depth:
		var d: int = int(depth[c])
		if d >= 1 and d <= int(iso.SeabedDepth):
			bedded += 1

	check(bed_cells == bedded,
		"a seabed under every see-through water cell, and none beyond (%d of %d)"
			% [bed_cells, bedded])
	# Exact, not "at least": a stray tile on the ground layer is as much a
	# defect as a missing one, and counting rivers in is what keeps this an
	# equality rather than a licence to draw anything there.
	check(counts.get(1, 0) == land + rivers,
		"ground covers every land cell and every river, and nothing else (%d of %d land + %d rivers)"
			% [counts.get(1, 0), land, rivers])
	check(counts.get(2, 0) == raised,
		"second level covers exactly the raised cells (%d of %d)" % [counts.get(2, 0), raised])
	check(raised > 0 and counts.get(1, 0) > counts.get(2, 0),
		"raised cells have ground beneath them, not instead of them")

	# The ground layer must carry each cell's OWN terrain, not more sea. This is
	# what separates a real stack from the old partition-plus-filler, which wrote
	# a block into every layer below a cell and landed on the same totals.
	var ground_layer = iso.get_node("IsoLevel1")
	var cols: int = iso.SheetColumns
	var shallow := Vector2i(int(iso.ShallowWaterFrame) % cols, int(iso.ShallowWaterFrame) / cols)
	var sampled := 0
	var ground_is_terrain := 0
	for y in range(size.y):
		for x in range(size.x):
			var c := Vector2i(x, y)
			var kind: String = gen.TerrainKindAt(c)
			if kind == "deep_water" or kind == "shallow_water":
				continue
			sampled += 1
			var g: Vector2i = ground_layer.get_cell_atlas_coords(c)
			if g != Vector2i(-1, -1) and g != shallow:
				ground_is_terrain += 1
	check(sampled > 0 and ground_is_terrain == sampled,
		"every land cell's ground tile is its own terrain (%d of %d)" % [ground_is_terrain, sampled])

	# A water surface that silently fell back to flat colour is the failure the
	# shader exists to prevent, and it looks like "the effect just isn't very
	# strong" rather than like a bug.
	for entry in stack:
		if not entry.has("surface"):
			continue
		var surf: Dictionary = entry["surface"]
		if entry["kind"] != "water":
			continue
		check(bool(surf["shaded"]), "the sea has its shader, not flat colour")
		check(abs(float(surf["opacity"]) - float(iso.MaxOpacity)) < 0.001,
			"sea opacity reached the shader (%.2f)" % float(surf["opacity"]))
		check(abs(float(surf["lake_opacity"]) - float(iso.LakeOpacity)) < 0.001,
			"lake opacity reached the shader (%.2f)" % float(surf["lake_opacity"]))
	check(float(iso.LakeOpacity) < 1.0, "lakes are see-through (%.2f)" % float(iso.LakeOpacity))
	check(float(iso.LakeOpacity) < float(iso.MaxOpacity),
		"a lake is clearer than the open sea (%.2f vs %.2f)" % [float(iso.LakeOpacity), float(iso.MaxOpacity)])

	# Two terrains on one frame renders them identically - the map still draws,
	# and a whole biome silently stops being distinguishable.
	var frame_names := ["GrassFrame", "DryGrassFrame", "DesertFrame", "SandFrame",
		"TundraFrame", "SnowFrame", "IceFrame", "JungleFrame", "SwampFrame",
		"GravelFrame", "RockFrame", "ShallowWaterFrame", "DeepWaterFrame"]
	var seen := {}
	var clashes: Array[String] = []
	var total: int = int(iso.SheetColumns) * int(iso.SheetRows)
	for fname in frame_names:
		var f: int = int(iso.get(fname))
		if f < 0 or f >= total:
			clashes.append("%s=%d outside 0..%d" % [fname, f, total - 1])
		elif seen.has(f):
			clashes.append("%s and %s both use frame %d" % [seen[f], fname, f])
		else:
			seen[f] = fname
	check(clashes.is_empty(),
		"every terrain has its own frame in the atlas" if clashes.is_empty() else str(clashes))

	if failures.is_empty():
		print("\nPASS: sea -> ground -> hills -> peaks -> props by level")
		quit(0)
	else:
		print("\nFAIL: %d checks" % failures.size())
		quit(1)
