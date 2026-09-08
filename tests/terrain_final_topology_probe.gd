extends SceneTree

func _initialize() -> void:
	var probe = load("res://tests/TerrainFinalTopologySmoke.cs").new()
	var passed: bool = probe.call("Run")
	probe.free()
	if passed: print("[terrain-final-topology] OK")
	quit(0 if passed else 1)
