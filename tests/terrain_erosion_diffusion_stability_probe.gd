extends SceneTree

func _initialize() -> void:
	var probe = load("res://tests/TerrainErosionDiffusionStabilitySmoke.cs").new()
	var passed: bool = probe.call("Run")
	probe.free()
	if passed: print("[terrain-erosion-diffusion] OK")
	quit(0 if passed else 1)
