extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var smoke: Node = load("res://tests/TerrainShorelineContourSmoke.cs").new()
	root.add_child(smoke)
	assert(smoke.call("RunGenerated"))
	smoke.free()
	print("[terrain-coastal-grass] OK")
	quit()
