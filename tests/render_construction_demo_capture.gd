extends SceneTree

# Runs construction_demo.tscn as a real, live scene (--display-driver windows,
# not --headless - headless has no rendering backend, see render_scene_capture.gd)
# and saves three screenshots at different real elapsed times: just placed,
# mid-build, and after completion - so the whole construction-in-progress
# effect family (progress bar, worker particle activity, staged sprite swap,
# reveal shader) can actually be seen working, not just asserted in a probe.

const SCENE_PATH := "res://addons/beep_game_builder_cs/templates/scenes/construction_demo.tscn"
const OUTPUT_DIR := "res://tmp/construction_demo"

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(1280, 720)
	root.content_scale_size = Vector2i(1280, 720)

	var packed = load(SCENE_PATH)
	if packed == null or not (packed is PackedScene):
		push_error("[construction-demo-capture] Could not load " + SCENE_PATH)
		quit(1)
		return

	var scene: Node = packed.instantiate()
	root.add_child(scene)
	paused = false

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT_DIR))

	# Eight frames across a site's life: staked plot (pending), materials
	# piling up, the job claimed and the slab pouring, frame and scaffold,
	# walls, roof, the hard swap with the scaffold coming down, and done.
	# Deliveries take ~1.4-2.8 s, travel ~1 s, the build 6 s, teardown 1 s.
	var frames := [
		[0.6, "01_staked.png"],
		[1.0, "02_deliveries.png"],
		[1.8, "03_slab.png"],
		[1.6, "04_frame.png"],
		[1.6, "05_walls.png"],
		[1.4, "06_roof.png"],
		[1.6, "07_swap.png"],
		[1.9, "08_done.png"],
	]
	for frame in frames:
		await _wait_seconds(float(frame[0]))
		_report(scene, String(frame[1]))
		await _capture(String(frame[1]))

	print("[construction-demo-capture] OK: %d screenshots saved to %s" % [frames.size(), OUTPUT_DIR])
	quit(0)

# What every worker and job is doing at each frame - so a site that does
# not advance in a screenshot can be traced to the job or the path, not
# guessed at from pixels.
func _report(scene: Node, label: String) -> void:
	var grid := scene.get_node_or_null("Grid")
	var jobs := scene.get_node_or_null("Jobs")
	if jobs == null:
		return
	var lines: Array[String] = []
	for entry in jobs.call("GetJobs"):
		lines.append("job %s cell=%s approach=%s state=%s by=%s p=%.2f" % [
			entry.get("id", ""), entry.get("cell", ""), entry.get("approach_cell", ""),
			entry.get("state", ""), entry.get("claimed_by", ""),
			float(jobs.call("GetJobProgress01", str(entry.get("id", ""))))])
	for name in ["WorkerA", "WorkerB", "WorkerC", "WorkerD"]:
		var unit := scene.get_node_or_null(name)
		if unit == null:
			continue
		var worker := unit.get_node_or_null("GridWorker")
		var follower := unit.get_node_or_null("PathFollower")
		var cell = grid.call("WorldToCell", unit.global_position) if grid != null else "?"
		lines.append("%s cell=%s state=%s job=%s moving=%s" % [
			name, cell, worker.get("State") if worker != null else "?",
			worker.get("CurrentJobId") if worker != null else "?",
			follower.get("IsMoving") if follower != null else "?"])
	print("[construction-demo-capture] --- %s ---\n  %s" % [label, "\n  ".join(lines)])

func _wait_seconds(seconds: float) -> void:
	var target := Time.get_ticks_msec() + int(seconds * 1000)
	while Time.get_ticks_msec() < target:
		await process_frame

func _capture(filename: String) -> void:
	for i in range(3):
		RenderingServer.force_draw(false)

	var image := root.get_texture().get_image()
	if image == null or image.is_empty():
		push_error("[construction-demo-capture] Viewport image is empty for " + filename)
		return

	var output_path := OUTPUT_DIR + "/" + filename
	var err := image.save_png(output_path)
	if err != OK:
		push_error("[construction-demo-capture] Could not save " + output_path + ": " + str(err))
	else:
		print("[construction-demo-capture] saved " + output_path)
