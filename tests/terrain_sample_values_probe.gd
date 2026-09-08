extends SceneTree

func _initialize() -> void:
	create_timer(30).timeout.connect(func(): push_error("Terrain sample values timed out"); quit(1))
	run.call_deferred()

func run() -> void:
	var probe: Node = load("res://tests/TerrainSampleValuesSmoke.cs").new()
	root.add_child(probe)
	var ok: bool = probe.Run()
	probe.free()
	quit(0 if ok else 1)
