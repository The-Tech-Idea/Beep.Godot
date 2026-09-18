extends SceneTree

# INLAND WATER MUST READ AS WATER.
#
# The painted view scores depth and how much bed shows from distance to the waterline, in tiles.
# That is right for a sea and wrong for anything narrow: measured on this very map, the coast field
# inside an enclosed body never exceeds +0.085 tiles, so on the sea's ramps every one of its pixels
# is scored as touching the shore, takes the palest water and keeps a third of the sand bed showing
# through. It came out the colour of the grass around it - the owner reported it twice as "lakes
# look bad" - and the fix is the LAKE_DEPTH floor and LAKE_BED ceiling in water_common.gdshaderinc.
#
# NAMED "lake" after what the owner was pointing at, but on this map the largest enclosed body is
# half lake and half RIVER, and the other six are pure river (FIX-19). That does not weaken the
# check - the rule being guarded is about water with no fetch, which is what both are - but do not
# read a pass here as saying anything about lakes specifically.
#
# The shader's own contour debug paints flat blue wherever it considers the fragment water, over the
# same camera, so it is an exact mask of what the shader thinks it is drawing. This compares the two
# frames: every pixel the shader calls water must actually LOOK like water. Measured on this map,
# seed 31415: 100.0% of them are blue-dominant with the fix and 0.0% without it, so the threshold
# below has the whole gap to sit in and fails the moment the floor is removed.
const SHALLOW := 11
const DEEP := 12
# vec3(0.10, 0.48, 0.64) in terrain_splat.gdshader's contour_debug branch.
const DEBUG_WATER := Color(0.10, 0.48, 0.64)
const MASK_TOLERANCE := 0.03
const MINIMUM_WATER_PIXELS := 5000
const MINIMUM_BLUE_FRACTION := 0.95

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func settle(frames: int) -> void:
	for i in frames:
		await process_frame
	await RenderingServer.frame_post_draw

func run() -> void:
	if DisplayServer.get_name() == "headless":
		# Not skipped quietly: this probe is pixels, and one that reports OK without reading any
		# would be a guard that cannot fail.
		print("[terrain-lake-colour] FAILED - needs a display; run it without --headless")
		quit(1)
		return

	root.size = Vector2i(1280, 720)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_splat_demo.tscn").instantiate()
	var world: Node = scene.get_node("WorldBuilder")
	world.set("Seed", 31415)
	scene.get_node("HUD").visible = false
	var built := [false]
	world.connect("WorldBuilt", func(_size): built[0] = true)
	root.add_child(scene)
	var frames := 0
	while not built[0] and frames < 3000:
		await process_frame
		frames += 1
	check(built[0], "the demo world never finished building")
	if not built[0]:
		return finish(scene)
	await settle(12)

	var splat: Node = scene.find_children("*", "TerrainPaintedRendererComponent", true, false)[0]
	var material: ShaderMaterial = splat.get_node("SplatSurface").material
	# Untyped on purpose: the search answers null on a map with no enclosed lake at all.
	var centre = largest_enclosed_lake(material.get_shader_parameter("id_map").get_image())
	if centre == null:
		check(false, "this map has no enclosed lake to measure")
		return finish(scene)

	# Framed on the lake alone, so the sea cannot supply the water pixels this measures.
	var camera: Camera2D = root.get_camera_2d()
	for child in camera.get_children():
		child.process_mode = Node.PROCESS_MODE_DISABLED
	camera.zoom = Vector2.ONE * 2.0
	camera.position = (centre + Vector2(0.5, 0.5)) * 64.0
	await settle(6)
	var painted: Image = root.get_texture().get_image()

	material.set_shader_parameter("contour_debug", true)
	await settle(4)
	var mask: Image = root.get_texture().get_image()
	material.set_shader_parameter("contour_debug", false)

	var water := 0
	var bluer := 0
	for y in painted.get_height():
		for x in painted.get_width():
			var m := mask.get_pixel(x, y)
			if absf(m.r - DEBUG_WATER.r) > MASK_TOLERANCE \
					or absf(m.g - DEBUG_WATER.g) > MASK_TOLERANCE \
					or absf(m.b - DEBUG_WATER.b) > MASK_TOLERANCE:
				continue
			water += 1
			var c := painted.get_pixel(x, y)
			if c.b > c.g:
				bluer += 1
	var fraction := float(bluer) / float(maxi(1, water))
	print("[terrain-lake-colour] lake at %s: %d water pixels, %.1f%% read as water"
		% [str(centre.round()), water, fraction * 100.0])
	check(water >= MINIMUM_WATER_PIXELS,
		"only %d water pixels in frame - too few to conclude anything" % water)
	check(fraction >= MINIMUM_BLUE_FRACTION,
		"only %.1f%% of the lake reads as water; it is being drawn as half ground" % (fraction * 100.0))
	finish(scene)

func finish(scene: Node) -> void:
	scene.free()
	print("[terrain-lake-colour] OK" if failures.is_empty() else "[terrain-lake-colour] FAILED")
	quit(0 if failures.is_empty() else 1)

# The biggest body of water that does not touch the map edge: a lake, never the sea.
func largest_enclosed_lake(ids: Image):
	var size := Vector2i(ids.get_width(), ids.get_height())
	var water := {}
	for y in size.y:
		for x in size.x:
			var id := int(round(ids.get_pixel(x, y).r * 255.0))
			if id == SHALLOW or id == DEEP:
				water[Vector2i(x, y)] = true
	var seen := {}
	var best := -1
	var centre = null
	for start in water.keys():
		if seen.has(start):
			continue
		var body: Array[Vector2i] = []
		var edge := false
		var queue := [start]
		seen[start] = true
		while not queue.is_empty():
			var cell: Vector2i = queue.pop_back()
			body.append(cell)
			if cell.x == 0 or cell.y == 0 or cell.x == size.x - 1 or cell.y == size.y - 1:
				edge = true
			for step in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var next: Vector2i = cell + step
				if water.has(next) and not seen.has(next):
					seen[next] = true
					queue.append(next)
		if edge or body.size() <= best:
			continue
		best = body.size()
		var sum := Vector2.ZERO
		for cell in body:
			sum += Vector2(cell)
		centre = sum / float(body.size())
	return centre
