extends SceneTree

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var probe = load("res://tests/TerrainGenerationJobSmoke.cs").new()
	root.add_child(probe)
	probe.call("Start")
	var frames := 0
	var result := 0
	while result == 0:
		await process_frame
		frames += 1
		result = probe.call("Poll")
	probe.free()
	print("[terrain-generation-job] main thread processed %d frames during worker generation" % frames)
	quit(0 if result == 1 else 1)
