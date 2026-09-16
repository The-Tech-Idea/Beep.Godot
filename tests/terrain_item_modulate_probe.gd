extends SceneTree

# VIEW-14: every terrain shader draws under BOTH tints a canvas item is given.
#
# 1. The item modulate - the node's Modulate, SelfModulate and every parent's.
#    Godot hands it to a canvas_item fragment inside COLOR (texture x vertex colour
#    x modulate). A shader that writes COLOR without the COLOR it received drops
#    it: a renderer tinted or faded through its node kept full colour and opacity.
# 2. The scene's CanvasModulate - the one AmbientController composes day/night,
#    weather and seasons into. Godot multiplies it in AFTER the fragment function,
#    so it never passes through COLOR - but it is skipped entirely under
#    `render_mode unshaded`. An unshaded terrain shader stayed noon-bright at
#    midnight while every sprite standing on it darkened.
#
# Every comparison is made inside ONE frame between two copies of the same surface
# at the same local coordinates - one untinted, one tinted - so the animated water
# cannot move between the reference and the result.

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const SHADERS := "res://addons/beep_game_builder_cs/shaders/"
const TINT := Color(1.0, 0.25, 0.25, 1.0)
const FADE := Color(1.0, 1.0, 1.0, 0.5)
const TOLERANCE := 3.0 / 255.0
# Below one in every channel and not green-dominant, so neither the tile detail's
# brightening nor the natural-terrain green key can clip or cut it out.
const GROUND := Color(0.62, 0.55, 0.48, 1.0)

func _initialize() -> void:
	call_deferred("run")

func viewport_for(size: Vector2i, ambient: Color) -> SubViewport:
	var viewport := SubViewport.new()
	viewport.size = size
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	if ambient != Color.WHITE:
		var canvas_modulate := CanvasModulate.new()
		canvas_modulate.color = ambient
		viewport.add_child(canvas_modulate)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	viewport.add_child(cells)
	for y in range(4):
		for x in range(8):
			cells.call("SetTerrainKind", Vector2i(x, y), "grass")
	return viewport

func parent(viewport: SubViewport, node_name: String, at: Vector2, modulate_colour: Color) -> Node2D:
	var node := Node2D.new()
	node.name = node_name
	node.position = at
	node.modulate = modulate_colour
	viewport.add_child(node)
	return node

func painted(under: Node2D) -> void:
	var view: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	view.set("RefreshOnReady", false)
	view.set("CellDataPath", NodePath("../../Cells"))
	view.set("BoundsSize", Vector2i(8, 4))
	under.add_child(view)
	view.call("Rebuild")

func water(under: Node2D) -> void:
	var material := ShaderMaterial.new()
	material.shader = load(SHADERS + "iso_water.gdshader")
	var coast := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	coast.fill(Color(1, 1, 0, 1))
	material.set_shader_parameter("coast_map", ImageTexture.create_from_image(coast))
	material.set_shader_parameter("flat_projection", 1.0)
	material.set_shader_parameter("map_size", Vector2(1, 1))
	material.set_shader_parameter("cell_size", Vector2(64, 64))
	material.set_shader_parameter("max_opacity", 1.0)
	material.set_shader_parameter("shore_opacity", 1.0)
	material.set_shader_parameter("lake_opacity", 1.0)
	material.set_shader_parameter("tile_batch", false)
	sprite(under, Color.WHITE, material)

# A 64x64 sprite of one colour drawn with the given material.
func sprite(under: Node2D, colour: Color, material: Material) -> void:
	var image := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	image.fill(colour)
	var node := Sprite2D.new()
	node.centered = false
	node.texture = ImageTexture.create_from_image(image)
	node.material = material
	under.add_child(node)

func production(file_name: String) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = load(SHADERS + file_name)
	return material

# The tinted pixel must be the reference pixel scaled channel by channel by the tint.
# Returns the failure, empty when it holds: the caller asserts, because a failed assert
# only leaves the function it is in and run() would otherwise go on to print OK.
func tint_failure(what: String, reference: Color, tinted: Color) -> String:
	print("[terrain-item-modulate] %s reference=%s tinted=%s" % [what, str(reference), str(tinted)])
	if reference.g <= 0.08 or reference.b <= 0.08:
		return "%s reference too dark to measure a tint: %s" % [what, str(reference)]
	if absf(tinted.r - reference.r * TINT.r) > TOLERANCE \
			or absf(tinted.g - reference.g * TINT.g) > TOLERANCE \
			or absf(tinted.b - reference.b * TINT.b) > TOLERANCE:
		return "%s is not tinted: reference=%s tinted=%s" % [what, str(reference), str(tinted)]
	return ""

func settle() -> void:
	for i in range(4):
		await process_frame
	await RenderingServer.frame_post_draw

func run() -> void:
	assert(DisplayServer.get_name() != "headless")

	# --- 1. the item modulate --------------------------------------------------
	var items := viewport_for(Vector2i(1024, 520), Color.WHITE)
	painted(parent(items, "Plain", Vector2(0, 0), Color.WHITE))
	painted(parent(items, "Tinted", Vector2(0, 264), TINT))
	painted(parent(items, "Faded", Vector2(528, 0), FADE))
	water(parent(items, "PlainWater", Vector2(600, 300), Color.WHITE))
	var tinted_water := parent(items, "TintedWater", Vector2(700, 300), TINT)
	water(tinted_water)
	water(parent(items, "FadedWater", Vector2(800, 300), FADE))
	# Positive control: an item with no shader under a tinted parent proves the
	# capture measures the node modulate at all.
	sprite(parent(items, "Control", Vector2(700, 400), TINT), Color.WHITE, null)

	# --- 2. the canvas modulate ------------------------------------------------
	# The same content twice, in two viewports; only the second has a CanvasModulate.
	var canvases: Array[SubViewport] = [viewport_for(Vector2i(600, 360), Color.WHITE),
		viewport_for(Vector2i(600, 360), TINT)]
	for canvas in canvases:
		painted(parent(canvas, "Painted", Vector2(0, 0), Color.WHITE))
		water(parent(canvas, "Water", Vector2(0, 280), Color.WHITE))
		sprite(parent(canvas, "TileDetail", Vector2(100, 280), Color.WHITE), GROUND, production("terrain_tile_detail.gdshader"))
		sprite(parent(canvas, "NaturalTerrain", Vector2(200, 280), Color.WHITE), GROUND, production("natural_terrain_green.gdshader"))
		sprite(parent(canvas, "Control", Vector2(300, 280), Color.WHITE), GROUND, null)

	await settle()
	var image := items.get_texture().get_image()
	var plain_canvas := canvases[0].get_texture().get_image()
	var ambient_canvas := canvases[1].get_texture().get_image()

	var control := tint_failure("item control", Color.WHITE, image.get_pixel(716, 416))
	assert(control.is_empty(), "the probe cannot measure a node modulate: " + control)
	var ground := tint_failure("painted ground under a node tint", image.get_pixel(128, 128), image.get_pixel(128, 392))
	assert(ground.is_empty(), ground)
	var painted_fade := image.get_pixel(528 + 128, 128).a
	assert(absf(painted_fade - FADE.a) <= 0.03, "Painted ground dropped the node opacity: %f" % painted_fade)
	var sea := tint_failure("sea surface under a node tint", image.get_pixel(632, 332), image.get_pixel(732, 332))
	assert(sea.is_empty(), sea)
	var water_fade := image.get_pixel(832, 332).a
	assert(absf(water_fade - FADE.a) <= 0.03, "Sea surface dropped the node opacity: %f" % water_fade)

	var canvas_control := tint_failure("canvas control", plain_canvas.get_pixel(332, 312), ambient_canvas.get_pixel(332, 312))
	assert(canvas_control.is_empty(), "the probe cannot measure a CanvasModulate: " + canvas_control)
	for probe in [["painted ground under the CanvasModulate", Vector2i(128, 128)],
			["sea surface under the CanvasModulate", Vector2i(32, 312)],
			["tile ground detail under the CanvasModulate", Vector2i(132, 312)],
			["natural terrain under the CanvasModulate", Vector2i(232, 312)]]:
		var at: Vector2i = probe[1]
		var failure := tint_failure(probe[0], plain_canvas.get_pixelv(at), ambient_canvas.get_pixelv(at))
		assert(failure.is_empty(), failure)

	items.free()
	for canvas in canvases:
		canvas.free()
	print("[terrain-item-modulate] OK")
	quit()
