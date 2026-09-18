extends "res://tests/terrain_lab_build.gd"

# How each rendering type SAMPLES its own art, pinned per view, because the answer is not one
# answer. Godot's four useful canvas filters differ in two independent ways, and only one of them
# is a matter of taste:
#
#   * mipmaps or not. Every view here minifies - a tile, a tree or an icon is drawn smaller than
#     its art as soon as the camera pulls back - and without a mip chain that is aliasing, which
#     reads as a shimmering grid on a moving map. So every art-bearing node in this engine is on a
#     WITH_MIPMAPS filter. Plain Nearest and plain Linear cannot sample a mip level at all.
#   * what happens when the camera MAGNIFIES. Linear blends the four nearest texels, which is right
#     for art drawn with soft anti-aliased edges (resource icons) and for ground that is a
#     continuous material anyway (tiles, blocks). Nearest keeps the artist's own texels, which is
#     right for the prop sheets: TerrainPropSizing.DrawnPixels already refuses to draw a stamp
#     larger than its art, so the only magnification left is the player's zoom, and interpolating a
#     hard cartoon edge there produces a smear rather than detail.
#
# The two families are pinned separately below. A node is only pinned once it has actually DRAWN:
# a renderer that never rebuilt would otherwise sit at Godot's inherited default and pass.
#
# Two views deliberately state a filter that samples no art at all: the painted surface and every
# shader sea. Their materials declare a filter on each sampler2D they use (terrain_splat.gdshader
# and water_common.gdshaderinc: ids nearest so they never interpolate, materials and foam linear
# over their own mip chains), so the canvas filter covers only the blank tile the surface is built
# from. That is pinned too, so nobody "fixes" the ground's sharpness in the wrong place.

const NEAREST_MIPS := CanvasItem.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS
const LINEAR_MIPS := CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
const LINEAR := CanvasItem.TEXTURE_FILTER_LINEAR
const PACK := "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/godot/grass_dirt_library_pack.tres"
const NAMES := {
	CanvasItem.TEXTURE_FILTER_PARENT_NODE: "inherited",
	CanvasItem.TEXTURE_FILTER_NEAREST: "nearest",
	CanvasItem.TEXTURE_FILTER_LINEAR: "linear",
	CanvasItem.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS: "nearest+mips",
	CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS: "linear+mips",
	CanvasItem.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS_ANISOTROPIC: "nearest+mips+aniso",
	CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC: "linear+mips+aniso",
}

var failures: Array[String] = []
var pinned := 0
var preview: Node

func find_view(view_name: String) -> Node:
	# Recursive, like terrain_view_parity_probe: the lab is free to group its renderers, and a
	# renderer that has moved must fail the pin it feeds rather than abort the probe here.
	var node: Node = preview.find_child(view_name, true, false)
	if node == null:
		failures.append("the lab has no %s renderer" % view_name)
	return node

func _initialize() -> void:
	call_deferred("run")

func named(filter: int) -> String:
	return NAMES.get(filter, "filter %d" % filter)

# Pins one drawer, and only one that has something on screen: `drew` is the count of whatever that
# view stamps, paints or fills, so a renderer that never ran fails here instead of passing on an
# inherited default.
func pin(node: CanvasItem, drew: int, expected: int, why: String) -> void:
	if node == null:
		failures.append("%s: the view has no such node" % why)
		print("  FAIL  %s: missing" % why)
		return
	if drew <= 0:
		failures.append("%s: drew nothing, so its filter proves nothing" % why)
		print("  FAIL  %s: drew nothing" % why)
		return
	var filter: int = node.texture_filter
	if filter == expected:
		pinned += 1
		print("  ok    %s: %s (%d drawn)" % [why, named(filter), drew])
	else:
		failures.append("%s: %s, wanted %s" % [why, named(filter), named(expected)])
		print("  FAIL  %s: %s, wanted %s (%d drawn)" % [why, named(filter), named(expected), drew])

func used_cells(node: Node) -> int:
	return node.get_used_cells().size() if node is TileMapLayer else 0

# A C# member read as an untyped Variant: a renamed or missing one must fail the pin it feeds,
# not abort the probe on a bad cast.
func count_of(node: Node, member: String) -> int:
	if node == null:
		return -1
	var value = node.get(member)
	return int(value) if typeof(value) == TYPE_INT else -1

func stamps(node: Node) -> int:
	if node == null:
		return -1
	var bounds = node.call("GetStampBounds")
	return bounds.size() if bounds != null else -1

# Every TileMapLayer a view has actually painted terrain into. The sea layer is excluded by name:
# its art is sampled by the water shader's own uniforms, not through the canvas filter.
func painted_layers(from_view: Node) -> Array:
	var layers: Array = []
	if from_view == null:
		return layers
	for child in from_view.get_children():
		if child is TileMapLayer and child.name != "TileWater" and used_cells(child) > 0:
			layers.append(child)
	return layers

# A pack this map can actually be drawn with. The shipped candidate pack
# (grass_dirt_library_pack.tres) binds grass and dirt only, and TerrainLibraryPainter refuses a
# cell whose kind the pack does not bind - "no binding for 'deep_water' at (0, 0)" on any generated
# world. What is under test here is which FILTER a pack gets, so the fixture binds every standard
# kind to the pack's first terrain and reuses its authored TileSet.
func pack_fixture() -> Resource:
	var shipped: Resource = load(PACK)
	var fixture = TerrainLibraryPack.new()
	fixture.set("PackId", "texture-filter-probe")
	fixture.set("Version", shipped.get("Version"))
	fixture.set("Projection", shipped.get("Projection"))
	fixture.set("Tiles", shipped.get("Tiles"))
	fixture.set("TerrainSet", shipped.get("TerrainSet"))
	# Without this a pack must bind exactly two terrain ids and carry a complete 47-configuration.
	fixture.set("RequireCompleteBinaryConnections", false)
	var bindings := {}
	for kind in ["grass", "dry_grass", "desert", "sand", "tundra", "snow", "ice", "jungle", "swamp",
			"gravel", "rock", "lava", "mud", "dirt", "water", "shallow_water", "deep_water"]:
		bindings[kind] = 0
	fixture.set("TerrainBindings", bindings)
	return fixture

func run() -> void:
	var lab: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = lab.get_node("World")
	world.set("MapSize", 0)
	root.add_child(lab)
	var build := await await_lab_build(world)
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)
	preview = lab.get_node("Preview")
	# The resource icons live under Preview/Diagnostics and only draw with the lab's diagnostics on,
	# which is how terrain_view_parity_probe reaches them too.
	var diagnostics: BaseButton = lab.get_node("HUD/Settings/Scroll/Controls/Diagnostics")
	diagnostics.button_pressed = true
	await process_frame

	for projection in [0, 1, 2, 3]:
		world.set("Projection", projection)
		world.call("Redraw")
		for i in range(3):
			await process_frame
		print("projection %d" % projection)

		if projection == 0:
			# The painted ground: a shader surface. The filter below covers the blank tile it is
			# built from; the ground itself is sampled by terrain_splat.gdshader's own uniforms.
			var splat: Node = find_view("Splat")
			var surface: Node = splat.get_node_or_null("SplatSurface") if splat != null else null
			pin(surface, used_cells(surface), LINEAR, "painted surface (shader samples the ground)")
			if surface != null:
				var shaded: bool = surface.material is ShaderMaterial
				if not shaded:
					failures.append("painted surface: no ShaderMaterial, so nothing samples the ground")
				print("  %s  painted surface carries its shader" % ("ok   " if shaded else "FAIL "))

			# Sprite stamps: nearest above the chain.
			var features: Node = find_view("Features")
			pin(features, count_of(features, "StampCount"), NEAREST_MIPS, "flat props (trees, bushes)")
			var rocks: Node = find_view("RockObjects")
			pin(rocks, stamps(rocks), NEAREST_MIPS, "relief stamps (hills, peaks)")

			# Icons: soft anti-aliased art, drawn at half a tile - linear above the chain.
			var resources: Node = find_view("Resources")
			pin(resources, count_of(resources, "IconCount"), LINEAR_MIPS, "resource icons")

			# The overlay draws rings and survey patches with the canvas primitives and binds no
			# texture at all, so it has no filter to state (TerrainMapOverlayComponent).
			var overlay: Node = find_view("MapOverlay")
			if overlay != null:
				print("  note  map overlay draws no art: %s" % named(overlay.texture_filter))
		elif projection == 1:
			var tiles: Node = find_view("TileRenderer")
			var layers := painted_layers(tiles)
			if layers.is_empty():
				failures.append("tile view: no biome layer painted a cell")
				print("  FAIL  tile view painted nothing")
			for layer in layers:
				pin(layer, used_cells(layer), LINEAR_MIPS, "tile view layer %s" % layer.name)
		elif projection == 2:
			var iso: Node = find_view("Iso")
			var layers := painted_layers(iso)
			if layers.is_empty():
				failures.append("isometric view: no block layer painted a cell")
				print("  FAIL  isometric view painted nothing")
			for layer in layers:
				pin(layer, used_cells(layer), LINEAR_MIPS, "isometric blocks %s" % layer.name)
			var iso_features: Node = find_view("IsoFeatures")
			pin(iso_features, stamps(iso_features), NEAREST_MIPS, "isometric props (parent)")
			# The per-level children are what actually draw the stamps.
			var levels := 0
			if iso_features != null:
				for child in iso_features.get_children():
					if child is CanvasItem:
						levels += 1
						pin(child, stamps(iso_features), NEAREST_MIPS, "isometric props level %s" % child.name)
			if levels == 0:
				failures.append("isometric props: no level node to draw them")
		else:
			var autotile: Node = find_view("IsoAutotile")
			var layers := painted_layers(autotile)
			if layers.is_empty():
				failures.append("isometric tile view: no layer painted a cell")
				print("  FAIL  isometric tile view painted nothing")
			for layer in layers:
				pin(layer, used_cells(layer), LINEAR_MIPS, "isometric tiles %s" % layer.name)

	# A LIBRARY PACK brings its own art and its own answer. Both views that can draw a pack ask it
	# the same question now; the flat one used to draw a pack at whatever the project defaulted to.
	print("library pack")
	world.set("Projection", 1)
	world.call("Redraw")
	await process_frame
	var tile_view: Node = find_view("TileRenderer")
	var pack: Resource = pack_fixture()
	var problem: String = str(pack.call("Validate", pack.get("Projection")))
	if tile_view == null or problem != "":
		if problem != "":
			failures.append("the pack fixture does not validate, so no view can draw it: %s" % problem)
			print("  FAIL  pack fixture does not validate: %s" % problem)
	else:
		for pixel_art in [false, true]:
			pack.set("PixelArt", pixel_art)
			tile_view.set("LibraryPack", pack)
			tile_view.call("Rebuild")
			await process_frame
			var painted: Dictionary = tile_view.call("GetPaintDiagnostics")
			if not painted.get("valid", false):
				failures.append("the pack did not draw: %s" % str(painted.get("reason", "")))
				print("  FAIL  pack did not draw: %s" % str(painted.get("reason", "")))
				continue
			var display: Node = tile_view.get_node_or_null("LibraryTerrain")
			pin(display, used_cells(display), NEAREST_MIPS if pixel_art else LINEAR_MIPS,
				"library pack drawn flat, PixelArt %s" % pixel_art)
	if tile_view != null:
		tile_view.set("LibraryPack", null)
		tile_view.call("Rebuild")

	lab.free()
	print("[terrain-texture-filter] %d pinned" % pinned)
	if failures.is_empty():
		print("[terrain-texture-filter] OK")
		quit(0)
	else:
		for failure in failures:
			print("FAIL: %s" % failure)
		quit(1)
