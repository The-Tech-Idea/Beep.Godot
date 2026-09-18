extends "res://tests/terrain_lab_build.gd"

var failures: Array[String] = []

func _initialize() -> void: run.call_deferred()

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func capture(filename: String) -> void:
	if DisplayServer.get_name() == "headless": return
	RenderingServer.force_draw(false)
	var picture := root.get_texture().get_image()
	check(picture != null and not picture.is_empty(), "Empty lab screenshot")
	if picture != null:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/output/terrain_async"))
		picture.save_png("res://tests/output/terrain_async/" + filename + ".png")

func run() -> void:
	root.size = Vector2i(1280, 800)
	var scene: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = scene.get_node("World")
	world.MapSize = 0
	# Generating WITH an art style set, because a null MapArt takes a different path through the
	# painted renderer. Which style does not matter here - this probe is the generation lifecycle,
	# not the look - so it takes the lab's first rather than naming one. This used to read
	# `scene.CartoonProfile`, from when the lab held a property per style instead of one list.
	var styles = scene.get("StyleProfiles")
	assert(styles != null and not styles.is_empty(), "the lab scene lists no art styles")
	world.MapArt = styles[0]
	root.add_child(scene)
	var controls: Node = scene.get_node("HUD/Settings/Scroll/Controls")
	await process_frame
	await process_frame
	check(world.IsGenerating, "Lab did not start background generation")
	check(controls.get_node("Actions/Generate").disabled, "Generate still enabled while busy")
	check(controls.get_node("Generation/Cancel").visible, "Cancel is missing")
	check(controls.get_node("Generation/Progress").visible, "Progress is missing")
	capture("loading")
	controls.get_node("Generation/Cancel").pressed.emit()
	# The build is already running here, so its GenerationFinished is still to come.
	var cancelled := await await_lab_build(world)
	check(cancelled.finished and not cancelled.success, "Cancelling did not end the build as a failure (%s)" % cancelled.message)
	check(world.BuiltSize == Vector2i.ZERO, "Cancelled initial build was published")
	check(not controls.get_node("Actions/Generate").disabled, "Controls did not recover after cancellation")
	controls.get_node("Actions/Generate").pressed.emit()
	var build := await await_lab_build(world)
	check(build.success and world.BuiltSize.x > 0, "Lab did not publish generated world (%s)" % build.message)
	check(not controls.get_node("Generation/Cancel").visible, "Cancel left visible after success")
	check(not controls.get_node("Actions/Generate").disabled, "Controls left disabled after success")
	for i in 3: await process_frame
	capture("complete")
	scene.free()
	print("[terrain-lab-generation] OK" if failures.is_empty() else "[terrain-lab-generation] FAILED")
	quit(0 if failures.is_empty() else 1)
