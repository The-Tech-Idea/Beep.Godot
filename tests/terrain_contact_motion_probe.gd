extends SceneTree

const CONTACTS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/waterfall_contacts_v1/"
const ROUTES = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func viewport_for(scene: Node2D) -> SubViewport:
	var viewport = SubViewport.new()
	viewport.size = Vector2i(900, 700)
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.add_child(scene)
	scene.position = Vector2(64, 80)
	return viewport

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT + "contact_motion")
	var records: Array = []
	for width in [1, 2, 3, 5]:
		var original = load(ROUTES + "square/width_%d.tscn" % width).instantiate()
		var candidate = load(CONTACTS + "staging/route_width_%d.tscn" % width).instantiate()
		var baseline_view = viewport_for(original)
		var candidate_view = viewport_for(candidate)
		var left = 64 + 128 + 10
		var right = 64 + (width + 1) * 64 + 54
		var changed_water = 0
		var visible_contact = 0
		var motion_frames: Dictionary = {}
		for frame in range(16):
			for scene in [original, candidate]:
				for section in range(width):
					scene.get_node("Fall%d" % section).material.set_shader_parameter("frame_override", frame)
			for tick in range(3): await process_frame
			await RenderingServer.frame_post_draw
			var before = baseline_view.get_texture().get_image()
			var after = candidate_view.get_texture().get_image()
			var water_bytes = PackedByteArray()
			# Include the lip and impact pool, not only the middle of the curtain.
			for y in range(80 + 48, 80 + 160):
				for x in range(left - 24, right + 24):
					var a = before.get_pixel(x, y)
					var b = after.get_pixel(x, y)
					if x >= left and x < right:
						if a != b: changed_water += 1
						if y >= 80 + 70 and y < 80 + 120:
							water_bytes.append_array([roundi(b.r * 255), roundi(b.g * 255), roundi(b.b * 255)])
					elif a != b:
						visible_contact += 1
			motion_frames[hash(water_bytes)] = true
			if width == 3:
				var capture_error = after.save_png(OUTPUT + "contact_motion/frame_%02d.png" % frame)
				if capture_error != OK: errors.append("Could not save motion frame")
		if changed_water != 0: errors.append("Contacts alter water pixels at width %d: %d" % [width, changed_water])
		if visible_contact == 0: errors.append("Contacts are not visible at width %d" % width)
		if motion_frames.size() != 16: errors.append("Missing distinct water frames at width %d" % width)
		records.append({"width":width, "frames":16, "changedWaterPixels":changed_water, "visibleLandContactSamples":visible_contact, "distinctCurtainFrames":motion_frames.size()})
		baseline_view.queue_free()
		candidate_view.queue_free()
		await process_frame
	var file = FileAccess.open(OUTPUT + "contact_motion_report.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"passed":errors.is_empty(), "cases":records, "errors":errors, "scope":"Controlled 16-frame contact overlap; not real-time clock or appearance approval"}, "  "))
	for error in errors: push_error(error)
	print("CONTACT MOTION ", "PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
