extends "res://tests/terrain_lab_build.gd"

# VIEW-04's evidence, rendered from the LAB - the one scene that wires all four views.
#
# Two things to look at:
#   1. lab_<view>.png, one per projection, all four now drawing the same sea off the same
#      coastline. Isometric tiles had no sea at all before this item.
#   2. lab_tiles_before.png / lab_tiles_after.png - the look change that needs the owner's
#      decision. The tile view used to default to FoamStrength 1.0 / DeepTiles 6.0 /
#      ShallowTiles 6.0 while the painted and block views drew 0.50 / 4.5 / 1.8 off the same
#      map; the shared look ends that, and the tile view is the one that moves.
#
#   godot --path . --script res://tests/terrain_water_look_capture.gd --quit-after 3600 \
#       --rendering-method gl_compatibility --resolution 1280x800
#
# Writes tests/output/water_look/*.png and prints each view's sea uniforms.

const LAB := "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn"
const VIEW_PATH := "HUD/Settings/Scroll/Controls/ViewRow/View"
const SIZE_PATH := "HUD/Settings/Scroll/Controls/MapSizeRow/MapSize"
const OUT := "res://tests/output/water_look"
const LOOK_SCRIPT := "res://addons/beep_game_builder_cs/ecs/terrain/TerrainWaterLook.cs"
const WATER := "res://addons/beep_game_builder_cs/textures/water/"
const SMALL := 1

const VIEW_NAMES := ["painted", "tiles", "isometric", "isometric_tiles"]
# Where each projection's sea lives, relative to Preview. The autotile sea is new (VIEW-04).
const SEA := {0: "Splat/SplatSurface", 1: "TileRenderer/TileWater", 2: "Iso/IsoWater", 3: "IsoAutotile/TileWater"}
const UNIFORMS := ["foam_strength", "deep_tiles", "shallow_tiles", "wave_intensity", "water_texture_tiles"]

# The tile renderer's own defaults before VIEW-04, with the water textures the lab authored
# on the node and no foam sheet - which is what that view actually drew.
const BEFORE := {"WaveIntensity": 1.0, "FoamStrength": 0.4, "DeepTiles": 6.0, "ShallowTiles": 6.0,
	"GroundTextureTiles": 12.0, "WaterTextureTiles": 6.0}

var lab: Node
var world: Node
var preview: Node

func _initialize() -> void:
	call_deferred("run")

func before_look() -> Resource:
	var look: Resource = load(LOOK_SCRIPT).new()
	for property in BEFORE:
		look.set(property, BEFORE[property])
	look.set("ShallowTexturePath", WATER + "seamless_turquoise_shallow_water_texture.png")
	look.set("DeepTexturePath", WATER + "seamless_deep_blue_ocean_texture.png")
	look.set("SeabedSandTexturePath", WATER + "seamless_golden_desert_sand_texture.png")
	return look

func build(view: int) -> void:
	var picker: OptionButton = lab.get_node(VIEW_PATH)
	picker.selected = view
	lab.Generate()
	var result := await await_lab_build(world)
	if not result.success:
		print("[water-look] view %d did not build: %s" % [view, result.message])
	for i in range(30):
		await process_frame
	await RenderingServer.frame_post_draw

func sea_report(view: int) -> String:
	var node: Node = preview.get_node_or_null(SEA[view])
	if node == null:
		return "no sea node at Preview/%s" % SEA[view]
	var material = node.get("material")
	if material is not ShaderMaterial:
		return "Preview/%s carries no ShaderMaterial" % SEA[view]
	var parts := PackedStringArray()
	for uniform in UNIFORMS:
		parts.append("%s=%s" % [uniform, str(material.get_shader_parameter(uniform))])
	return ", ".join(parts)

func shoot(name: String) -> void:
	var image := get_root().get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(OUT)
	image.save_png("%s/%s.png" % [OUT, name])
	print("[water-look] wrote %s.png" % name)

func run() -> void:
	lab = load(LAB).instantiate()
	root.add_child(lab)
	world = lab.get_node("World")
	preview = lab.get_node("Preview")
	var first := await await_lab_build(world)
	if not first.success:
		print("[water-look] the lab's first build did not succeed: %s" % first.message)
	var size_picker: OptionButton = lab.get_node(SIZE_PATH)
	size_picker.selected = SMALL
	# The map, not the panel over it.
	lab.get_node("HUD").visible = false

	for view in range(4):
		await build(view)
		shoot("lab_%s" % VIEW_NAMES[view])
		print("[water-look] %s sea: %s" % [VIEW_NAMES[view], sea_report(view)])

	# The painted view at the art's own scale as well as fitted: a map fitted to the window is
	# minified, and minification is mipmaps, which is blur that says nothing about the material.
	await build(0)
	var preview_2d := preview as Node2D
	var placed := preview_2d.position
	# Centred on a cell in the middle of the map, where the land is.
	var focus := Vector2(24, 20) * 64.0
	preview_2d.scale = Vector2.ONE * 3.0
	preview_2d.position = Vector2(640, 400) - focus * 3.0
	for i in range(20):
		await process_frame
	await RenderingServer.frame_post_draw
	shoot("lab_painted_close")
	preview_2d.scale = Vector2.ONE
	preview_2d.position = placed
	for i in range(10):
		await process_frame

	# Does the painted view draw anything but surf differently from before VIEW-04? Its control is
	# the shipped look with the one value that moved put back: the lab authored a foam sheet and no
	# FoamStrength, so the painted view ran on its own default of 0.50, where the shipped look says
	# 0.40. Everything else it reads was already these numbers.
	# ONE map, redrawn: Generate() would make a different world and every comparison with it is
	# noise. Redraw re-applies the materials over the map that is already built.
	await build(0)
	# A still sea: the water animates, so two captures at different times differ everywhere it is
	# drawn and no comparison between them means anything.
	var painted_material: ShaderMaterial = preview.get_node("Splat/SplatSurface").material
	painted_material.set_shader_parameter("wave_speed", 0.0)
	for i in range(10):
		await process_frame
	await RenderingServer.frame_post_draw
	var now := get_root().get_texture().get_image()
	var control: Resource = load("res://addons/beep_game_builder_cs/textures/terrain/terrain_water_look.tres").duplicate()
	control.set("FoamStrength", 0.5)
	world.set("WaterLook", control)
	world.Redraw()
	for i in range(10):
		await process_frame
	preview.get_node("Splat/SplatSurface").material.set_shader_parameter("wave_speed", 0.0)
	for i in range(10):
		await process_frame
	await RenderingServer.frame_post_draw
	var before := get_root().get_texture().get_image()
	shoot("lab_painted_before_view04")
	var differing := 0
	for y in range(0, now.get_height(), 2):
		for x in range(0, now.get_width(), 2):
			if now.get_pixel(x, y) != before.get_pixel(x, y):
				differing += 1
	print("[water-look] painted now vs its pre-VIEW-04 values: %d of %d sampled pixels differ"
		% [differing, (now.get_height() / 2) * (now.get_width() / 2)])
	world.set("WaterLook", load("res://addons/beep_game_builder_cs/textures/terrain/terrain_water_look.tres"))

	# The look change, on the view that moves.
	world.set("WaterLook", before_look())
	await build(1)
	shoot("lab_tiles_before")
	print("[water-look] tiles BEFORE: %s" % sea_report(1))
	world.set("WaterLook", load("res://addons/beep_game_builder_cs/textures/terrain/terrain_water_look.tres"))
	await build(1)
	shoot("lab_tiles_after")
	print("[water-look] tiles AFTER:  %s" % sea_report(1))

	print("[water-look] OK")
	quit(0)
