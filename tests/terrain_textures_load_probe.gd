extends SceneTree

func _initialize() -> void:
	var probe = load("res://tests/TerrainTexturesLoadSmoke.cs").new()
	var passed: bool = probe.call("Run")
	probe.free()
	if passed: print("[terrain-textures-load] OK")
	quit(0 if passed else 1)
