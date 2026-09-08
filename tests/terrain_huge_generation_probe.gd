extends SceneTree

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var probe: Node = load("res://tests/TerrainHugeGenerationSmoke.cs").new()
	root.add_child(probe)
	probe.CancelDuringErosion = "--cancel-erosion" in OS.get_cmdline_user_args()
	probe.CancelDuringStartPositions = "--cancel-starts" in OS.get_cmdline_user_args()
	probe.Start()
	var result := 0
	while result == 0:
		await create_timer(0.1).timeout
		result = probe.Poll()
	probe.free()
	quit(0 if result == 1 else 1)
