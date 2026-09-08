extends SceneTree

func _initialize() -> void:
	var probe = load("res://tests/TerrainResourceScaleSmoke.cs").new()
	var passed: bool = probe.call("Run")
	probe.free()
	if passed: print("[terrain-resource-scale] OK")
	quit(0 if passed else 1)
