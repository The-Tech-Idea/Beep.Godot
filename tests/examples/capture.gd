extends SceneTree

# Renders terrain scenes to PNGs so changes can actually be looked at. The MCP
# capture bridge cannot carry a frame of this size, so a headless render to disk
# is the way to see what a change did.
#
#   CAPTURE_DIR=<dir> godot --path <project> --script res://tests/examples/capture.gd
#
# CAPTURE_SCENES optionally narrows it to a comma-separated list of the keys
# below; the default renders all of them.

const SCENES := {
	"lab": "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn",
	"splat": "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_splat_demo.tscn",
	"tilemap": "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_tilemap_demo.tscn",
	"iso": "res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_iso_demo.tscn",
}

func _initialize() -> void:
	var out_dir: String = OS.get_environment("CAPTURE_DIR")
	if out_dir == "":
		print("FAIL: CAPTURE_DIR is not set")
		quit(1)
		return

	var wanted: Array = SCENES.keys()
	var requested: String = OS.get_environment("CAPTURE_SCENES")
	if requested != "":
		wanted = []
		for key in requested.split(","):
			var trimmed := key.strip_edges()
			if SCENES.has(trimmed):
				wanted.append(trimmed)
			else:
				print("FAIL: unknown scene '%s'" % trimmed)

	var failures := 0
	for name in wanted:
		var started := Time.get_ticks_msec()
		var root_node = load(SCENES[name]).instantiate()
		get_root().add_child(root_node)
		# Enough frames for the deferred generate and render to have landed.
		for i in range(45):
			await process_frame
		print("%s: ready in %d ms" % [name, Time.get_ticks_msec() - started])
		failures += await shoot(out_dir, name)

		# CAPTURE_ZOOM takes a second shot pushed in to the given zoom level, to
		# show the art at the distance it is actually played at rather than only
		# with the whole map in view.
		var zoom: String = OS.get_environment("CAPTURE_ZOOM")
		if zoom != "":
			var camera = find_camera_controller(root_node)
			if camera == null:
				print("FAIL: %s has no GridCameraControllerComponent to zoom" % name)
				failures += 1
			else:
				camera.SetZoomLevel(float(zoom), true)
				var focus: String = OS.get_environment("CAPTURE_FOCUS")
				if focus != "":
					var parts := focus.split(",")
					camera.FocusWorld(Vector2(float(parts[0]), float(parts[1])), true)
				for i in range(10):
					await process_frame
				failures += await shoot(out_dir, "%s_zoom" % name)

				# CAPTURE_ANIM takes a second shot later in the animation cycle,
				# so that "it animates" can be measured by diffing the two rather
				# than asserted from the fact that a time uniform exists.
				var anim: String = OS.get_environment("CAPTURE_ANIM")
				if anim != "":
					for i in range(int(anim)):
						await process_frame
					failures += await shoot(out_dir, "%s_zoom_t2" % name)

		root_node.queue_free()
		await process_frame

	quit(1 if failures > 0 else 0)

## Saves the current frame; returns 1 on failure so the caller can count them.
func shoot(out_dir: String, name: String) -> int:
	var image := get_root().get_texture().get_image()
	var path := "%s/%s.png" % [out_dir, name]
	if image.save_png(path) != OK:
		print("FAIL: could not save %s" % path)
		return 1
	print("saved %s  %dx%d" % [path, image.get_width(), image.get_height()])
	return 0

func find_camera_controller(node: Node):
	if node.get_class() == "Node" or node is Node:
		if node.has_method("SetZoomLevel") and node.has_method("FocusWorld"):
			return node
	for child in node.get_children():
		var found = find_camera_controller(child)
		if found != null:
			return found
	return null
