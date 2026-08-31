extends SceneTree

# ONE layer stack, obeyed by every renderer in the scene.
#
# Each renderer used to carry its own z index export, and the numbers only
# happened to agree. The top-down feature renderer's RenderZIndex was the proof:
# declared, set to -84 in three scenes, and never assigned to anything, so the
# node kept Node2D's default of 0. That sat above the painted view's surface at
# -95 and looked correct, and put every tree UNDER the tile view's ground the
# moment its layers moved onto the shared stack. Nothing failed; the trees were
# simply drawn first and covered.
#
# A screenshot cannot tell "no trees were generated" from "the trees are behind
# the map", which is why this reads the z indices back instead of looking.
#
# The order this requires, bottom to top, is TerrainLayers':
#   floor < seabed < sea < ground < hills < mountains < props < markers

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

# The z indices a renderer occupies, lowest and highest.
#
# EVERY drawing node counts, empty layers included. Skipping the empty ones
# looked more careful and made this guard nearly worthless: the lab shows one
# view at a time, so the other two have no cells, and their whole terrain stack
# dropped out of the comparison. It reported the tile view topping out at z0 -
# its water sprite, the one node that is not a tile layer - and happily
# confirmed that trees at z11 drew above it. A layer's z is set when the layer
# is built, so it is just as true of an empty one, and comparing all of them is
# what makes the answer mean anything.
func z_span(node: Node) -> Array:
	var lo := 1 << 30
	var hi := -(1 << 30)
	var stack: Array[Node] = [node]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		if n is CanvasItem and not (n is Control):
			lo = min(lo, n.z_index)
			hi = max(hi, n.z_index)
		for c in n.get_children():
			stack.append(c)
	return [lo, hi]

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_generator_lab.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(60): await process_frame

	var preview = root_node.find_child("Preview", true, false)
	check(preview != null, "the lab has a Preview holding every renderer")
	if preview == null:
		quit(1)
		return

	# BUILD ALL THREE VIEWS before measuring anything.
	#
	# The lab only builds the view it is showing, so straight after load two of
	# the three renderers have no layers at all - and a renderer with no layers
	# has no z indices to disagree with. Measured then, this guard compared the
	# trees against a bare Node2D sitting at zero and called it a pass.
	var picker = root_node.find_child("View", true, false)
	check(picker != null, "the lab exposes its view picker")

	# Which renderers each view is allowed to draw. The isometric view has its
	# own feature renderer, so the flat one must be OFF there - it stamps trees
	# on the square grid, and over a diamond map they stand on open water.
	var expected := {
		0: {"Splat": true,  "TileRenderer": false, "Features": true,
			"Iso": false, "IsoFeatures": false},
		1: {"Splat": false, "TileRenderer": true,  "Features": true,
			"Iso": false, "IsoFeatures": false},
		2: {"Splat": false, "TileRenderer": false, "Features": false,
			"Iso": true,  "IsoFeatures": true},
	}
	var view_names := ["Painted", "Game tiles", "Isometric"]

	if picker != null:
		for index in range(3):
			picker.selected = index
			root_node.Generate()
			for i in range(15): await process_frame

			for node_name in expected[index]:
				var n = preview.find_child(node_name, true, false)
				if n == null:
					continue
				var want: bool = expected[index][node_name]
				# is_visible_in_tree, not `visible`: a renderer whose parent is
				# hidden is off however its own flag reads.
				check(n.is_visible_in_tree() == want,
					"%s: %s is %s" % [view_names[index], node_name,
						"drawn" if want else "off"])

	# Every renderer in the one scene, so their z indices are comparable.
	var splat = preview.find_child("Splat", true, false)
	var tiles = preview.find_child("TileRenderer", true, false)
	var features = preview.find_child("Features", true, false)
	var iso = preview.find_child("Iso", true, false)
	var iso_features = preview.find_child("IsoFeatures", true, false)

	for pair in [["Splat", splat], ["TileRenderer", tiles],
			["Features", features], ["Iso", iso], ["IsoFeatures", iso_features]]:
		check(pair[1] != null, "the scene has a %s renderer" % pair[0])
	if splat == null or tiles == null or features == null or iso == null:
		quit(1)
		return

	# --- the fault this guard exists for -----------------------------------
	#
	# Trees must draw OVER the ground of every view, not under it.
	var tree_z: int = z_span(features)[0]
	var tile_span := z_span(tiles)
	var iso_span := z_span(iso)
	var splat_span := z_span(splat)

	check(tree_z > tile_span[1],
		"trees draw over the tile view (props z%d > its top terrain z%d)"
			% [tree_z, tile_span[1]])
	check(tree_z > iso_span[1],
		"trees draw over the isometric view (props z%d > its top terrain z%d)"
			% [tree_z, iso_span[1]])
	check(tree_z > splat_span[1],
		"trees draw over the painted view (props z%d > its surface z%d)"
			% [tree_z, splat_span[1]])

	# --- both views bottom out at the SAME floor ----------------------------
	#
	# Not one below the other: only one view is ever visible, and each puts the
	# bottom of its world at the shared floor - the painted view's single
	# composited surface, the tile view's filled base. Agreeing on the number is
	# the property worth holding; an order between two things that never draw
	# together is not.
	check(splat_span[0] == tile_span[0],
		"the painted surface and the tile base share the stack's floor (z%d)"
			% splat_span[0])
	check(splat_span[0] < iso_span[0],
		"the floor is below the isometric seabed (z%d < z%d)"
			% [splat_span[0], iso_span[0]])

	# --- no renderer may invent its own z any more --------------------------
	#
	# The exports are gone; this is what stops one growing back.
	for pair in [["Splat", splat], ["TileRenderer", tiles], ["Features", features],
			["Iso", iso], ["IsoFeatures", iso_features]]:
		if pair[1] == null:
			continue
		check(not ("RenderZIndex" in pair[1]),
			"%s takes its z from TerrainLayers, not its own export" % pair[0])

	# --- markers clear the props -------------------------------------------
	var markers = preview.find_child("MapOverlay", true, false)
	if markers != null:
		var marker_z: int = z_span(markers)[0]
		if marker_z < (1 << 30):
			check(marker_z > tree_z,
				"resource markers draw over the trees (z%d > z%d)" % [marker_z, tree_z])

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
