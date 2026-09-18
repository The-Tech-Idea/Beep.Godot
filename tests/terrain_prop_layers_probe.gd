extends "res://tests/terrain_lab_build.gd"

# WHAT STANDS ON THE GROUND DRAWS OVER WHAT LIES ON IT.
#
# The stack has three prop layers and they have to stay in this order, bottom to top:
#
#   clutter   small rocks lying on the ground        TerrainLayers.ZForClutter()   9
#   props     trees and bushes standing on it        ZForProps(Ground)            11
#   peaks     mountains, which occlude a tree         ZForProps(Mountains)        13
#   markers   icons that are not part of the world    ZForMarkers()               15
#
# Until 2026-09-18 there was no clutter layer at all, and the relief renderer took the MOUNTAINS
# prop slot for everything it drew - so a pebble on flat grass carried a peak's z and drew over the
# canopy of every tree near it. The owner reported it as "rocks and small grass should be a below
# layer than tree".
#
# Every layer is checked for having actually DRAWN before its z is trusted. A renderer that never
# rebuilt reports Godot's inherited default and would pass an ordering test on a blank map, which is
# the shape of guard that passes forever and guards nothing.

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	# Young and wet: hills for the small rocks, woods for the trees to stand in.
	world.set("MapSize", 3)
	world.set("WorldAge", 0)
	world.set("Rainfall", 2)
	root.add_child(scene)
	var build := await await_lab_build(world)
	check(build.success, "the lab's build did not succeed: %s" % build.message)
	if not build.success:
		return finish(scene)
	for i in 4:
		await process_frame

	var features: Node2D = scene.get_node("Preview/Features")
	var relief: Node2D = scene.get_node("Preview/RockObjects")
	var clutter: Node2D = relief.get_node_or_null("Clutter")
	check(clutter != null, "the relief renderer built no Clutter node, so small rocks have no layer")
	if clutter == null:
		return finish(scene)

	# Drawn, not merely present.
	var trees: int = features.get("StampCount")
	var rocks: int = relief.get("StampCount")
	check(trees > 0, "no tree stamps drew, so the prop layer proves nothing")
	check(rocks > 0, "no rock stamps drew, so the clutter layer proves nothing")

	var clutter_z: int = clutter.z_index
	var props_z: int = features.z_index
	var peaks_z: int = relief.z_index
	print("[terrain-prop-layers] clutter %d < props %d < peaks %d (%d trees, %d rocks)"
		% [clutter_z, props_z, peaks_z, trees, rocks])

	check(clutter_z < props_z,
		"small rocks (%d) must draw BELOW trees (%d)" % [clutter_z, props_z])
	check(props_z < peaks_z,
		"trees (%d) must draw below mountain peaks (%d)" % [props_z, peaks_z])
	# Above the terrain it lies on, or the ground covers the rock.
	check(clutter_z > 2 * 4,
		"clutter (%d) must draw above the topmost terrain level" % clutter_z)
	check(not clutter.z_as_relative and not features.z_as_relative and not relief.z_as_relative,
		"a relative z makes these orderings depend on the scene's nesting")
	finish(scene)

func finish(scene: Node) -> void:
	scene.free()
	print("[terrain-prop-layers] OK" if failures.is_empty() else "[terrain-prop-layers] FAILED")
	quit(0 if failures.is_empty() else 1)
