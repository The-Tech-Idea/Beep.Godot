extends SceneTree

# VIEW-13: per projection, the lab draws what that projection should - not merely
# "the right nodes are visible". The lab is the one scene wiring every view, so it is
# where parity is measured: for each of the four projections, built for real on a
# Tiny map with the map diagnostics on,
#   (a) exactly the renderers that projection uses are visible in the tree;
#   (b) they drew: trees stamped, one resource icon per cell carrying a surface or
#       liquid resource, one start ring per generated start position;
#   (c) the projection's sea is drawn, and it is the SAME sea in all four - one world's
#       TerrainWaterLook, with a coast map bound (VIEW-04 gave IsometricAutotile its sea);
#   (d) the gameplay grid is bound to the surface on screen;
#   (e) native collision finished building against it.
# Resource icons have ONE drawer, TerrainResourceRendererComponent. The map overlay
# draws start rings and the survey, not a second set of resource discs.

const LAB := "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn"
const VIEW_PATH := "HUD/Settings/Scroll/Controls/ViewRow/View"
const SIZE_PATH := "HUD/Settings/Scroll/Controls/MapSizeRow/MapSize"
const DIAGNOSTICS_PATH := "HUD/Settings/Scroll/Controls/Diagnostics"
const GENERATION_FRAME_LIMIT := 6000
const TINY := 0

const VIEW_NAMES := ["Painted", "Game tiles", "Isometric", "Isometric tiles"]
const RENDERERS := ["Splat", "TileRenderer", "Iso", "IsoFeatures", "IsoAutotile",
	"Features", "RockObjects", "Resources", "MapOverlay"]
# Renderers each projection draws; every other name in RENDERERS must be off.
const DRAWN := {
	0: ["Splat", "Features", "RockObjects", "Resources", "MapOverlay"],
	1: ["TileRenderer", "Features", "RockObjects", "Resources", "MapOverlay"],
	2: ["Iso", "IsoFeatures"],
	3: ["IsoAutotile", "Features", "RockObjects", "Resources", "MapOverlay"],
}
# The sea node for each projection, relative to Preview. Isometric tiles gained one in VIEW-04.
const SEA := {0: "Splat/SplatSurface", 1: "TileRenderer/TileWater", 2: "Iso/IsoWater", 3: "IsoAutotile/TileWater"}
# One world, one sea: every view's water material carries the same shared look (VIEW-04).
const WATER_UNIFORMS := ["foam_strength", "deep_tiles", "shallow_tiles", "wave_intensity",
	"ground_texture_tiles", "water_texture_tiles", "foam_tiles_along", "swell_directionality"]

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func _initialize() -> void:
	call_deferred("run")

func await_idle(world: Node) -> bool:
	var frames := 0
	while world.IsGenerating and frames < GENERATION_FRAME_LIMIT:
		await process_frame
		frames += 1
	return not world.IsGenerating

func run() -> void:
	var lab: Node = load(LAB).instantiate()
	root.add_child(lab)
	await process_frame
	await process_frame
	var world: Node = lab.get_node("World")
	check(await await_idle(world), "the lab's first build finishes")
	var preview: Node = lab.get_node("Preview")
	var generator: Node = preview.get_node("TerrainGenerator")
	var grid: Node = preview.get_node("Grid")
	var picker: OptionButton = lab.get_node(VIEW_PATH)
	var size_picker: OptionButton = lab.get_node(SIZE_PATH)
	var diagnostics: BaseButton = lab.get_node(DIAGNOSTICS_PATH)
	size_picker.selected = TINY
	diagnostics.button_pressed = true

	# uniform -> [first view that drew it, value]; every later view must match.
	var water := {}
	for index in range(4):
		var view: String = VIEW_NAMES[index]
		picker.selected = index
		var outcome := []
		var on_finished := func(success: bool, message: String) -> void: outcome.append([success, message])
		world.GenerationFinished.connect(on_finished)
		lab.Generate()
		check(world.IsGenerating, "%s: Generate() started a build" % view)
		var settled: bool = await await_idle(world)
		world.GenerationFinished.disconnect(on_finished)
		check(settled and outcome.size() == 1 and outcome[0][0], "%s: the build finished (%s)" % [view, str(outcome)])
		for i in range(3):
			await process_frame

		# (a) exactly this projection's renderers
		var nodes := {}
		for renderer_name in RENDERERS:
			var node: Node = preview.find_child(renderer_name, true, false)
			check(node != null, "%s: the lab has a %s renderer" % [view, renderer_name])
			nodes[renderer_name] = node
			if node == null:
				continue
			var want: bool = renderer_name in DRAWN[index]
			check(node.is_visible_in_tree() == want, "%s: %s is %s" % [view, renderer_name, "drawn" if want else "off"])

		# (b) the grid companions drew what the generator made. C# members are read as
		# untyped Variants: a missing member must fail this check, not abort the probe.
		if "Features" in DRAWN[index] and nodes["Features"] != null:
			var stamps = nodes["Features"].get("StampCount")
			check(typeof(stamps) == TYPE_INT and stamps > 0, "%s: trees are stamped (%s)" % [view, str(stamps)])
		if "Resources" in DRAWN[index] and nodes["Resources"] != null:
			var expected := 0
			var built: Vector2i = world.BuiltSize
			for y in range(built.y):
				for x in range(built.x):
					var cell := Vector2i(x, y)
					if generator.ResourceAt(cell) != "" or generator.LiquidResourceAt(cell) != "":
						expected += 1
			var icons = nodes["Resources"].get("IconCount")
			check(expected > 0 and typeof(icons) == TYPE_INT and icons == expected,
				"%s: one resource icon per resource cell (%s icons, %d cells)" % [view, str(icons), expected])
		if "MapOverlay" in DRAWN[index] and nodes["MapOverlay"] != null:
			var starts: int = generator.GetStartPositions().size()
			var rings = nodes["MapOverlay"].get("StartMarkerCount")
			check(starts > 0 and typeof(rings) == TYPE_INT and rings == starts,
				"%s: one start ring per start position (%s rings, %d starts)" % [view, str(rings), starts])

		# (c) the sea, and that it is the SAME sea in every view
		if SEA.has(index):
			var sea: Node = preview.get_node_or_null(SEA[index])
			check(sea != null and sea.is_visible_in_tree(), "%s: the sea %s is drawn" % [view, SEA[index]])
			if index == 0 and sea != null:
				check(sea.material is ShaderMaterial, "%s: the painted surface is shaded" % view)
			if sea != null and sea.material is ShaderMaterial:
				# Without the coast field the sea has no shallows: it draws at open-sea
				# opacity right up to the beach, and nothing reports it.
				check(sea.material.get_shader_parameter("coast_map") != null,
					"%s: the sea has no coast map, so it draws without shallows" % view)
				for uniform in WATER_UNIFORMS:
					var value = sea.material.get_shader_parameter(uniform)
					check(value != null, "%s: the sea never received %s" % [view, uniform])
					if value != null:
						if not water.has(uniform):
							water[uniform] = [view, value]
						else:
							check(is_equal_approx(float(water[uniform][1]), float(value)),
								"%s: %s is %s, but the %s view drew %s - one world, one sea"
									% [view, uniform, str(value), water[uniform][0], str(water[uniform][1])])

		# (d) the grid is bound to the surface on screen
		if index == 2:
			check(grid.get_node_or_null(grid.ElevatedTerrainPath) == nodes["Iso"],
				"%s: the grid is bound to the block surface" % view)
		else:
			var active: Node = nodes[["Splat", "TileRenderer", "", "IsoAutotile"][index]]
			check(active != null and grid.get_node_or_null(grid.TileMapLayerPath) == active.call("GetTerrainLayer"),
				"%s: the grid is bound to the view's layer" % view)

		# (e) collision
		var collision: Node = preview.find_child("Collision", true, false)
		check(collision != null, "%s: the lab has terrain collision" % view)
		if collision != null:
			var ready = collision.get("IsReady")
			var shapes = collision.get("ShapeCount")
			check(ready == true and typeof(shapes) == TYPE_INT and shapes > 0,
				"%s: collision is ready (%s shapes)" % [view, str(shapes)])

	lab.free()
	print("\n[terrain-view-parity] %s" % ("OK" if failures.is_empty() else "%d FAILED" % failures.size()))
	quit(1 if failures.size() > 0 else 0)
