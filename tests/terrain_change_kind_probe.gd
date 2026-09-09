extends SceneTree

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var probe: Node = load("res://tests/TerrainChangeKindSmoke.cs").new()
	root.add_child(probe)
	var ok: bool = probe.Run()
	probe.free()
	quit(0 if ok else 1)
