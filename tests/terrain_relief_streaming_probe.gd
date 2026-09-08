extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func make_view(host: Node, size: Vector2i) -> Node2D:
	var view: Node2D = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainReliefRendererComponent.cs").new()
	view.CellDataPath = NodePath("../Cells")
	view.RefreshOnReady = false
	view.BoundsSize = size
	view.MapArt = load("res://addons/beep_game_builder_cs/textures/map_art/cartoon.tres")
	view.HillsSheetPath = "res://addons/beep_game_builder_cs/textures/rocks/rock_debris.png"
	view.MountainsSheetPath = view.HillsSheetPath
	view.ReliefCellsPerFrame = 128
	view.ReliefPreloadChunks = 0
	host.add_child(view)
	view.Rebuild()
	view.set_process(false)
	return view

func settle(view: Node) -> void:
	for i in 300:
		view.UpdateReliefResidency()
		check(view.ReliefCellsProcessedLastFrame <= 128, "Relief exceeded cell budget")
		if view.PendingReliefChunkCount == 0: break
	check(view.PendingReliefChunkCount == 0, "Relief chunks did not settle")

func run() -> void:
	root.size = Vector2i(800, 600)
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	for center in [Vector2i(64, 64), Vector2i(700, 700), Vector2i(1020, 1020)]:
		for y in range(center.y - 4, mini(center.y + 4, 1024)):
			for x in range(center.x - 4, mini(center.x + 4, 1024)):
				cells.SetMetadata(Vector2i(x, y), "terrain_relief", 1 + (x % 2))
	for y in range(60, 64): cells.SetTerrainKind(Vector2i(64, y), "deep_water")
	var camera := Camera2D.new()
	host.add_child(camera)
	camera.position = Vector2(64, 64) * 64
	await process_frame
	camera.force_update_scroll()
	var view := make_view(host, Vector2i(1024, 1024))
	check(view.StampCount == 0 and view.ResidentReliefCellCount == 0, "Relief rebuilt entire map eagerly")
	settle(view)
	check(view.ResidentReliefCellCount > 0 and view.ResidentReliefCellCount < 20000, "Relief residency not bounded")
	var reference := make_view(host, Vector2i(128, 128))
	var expected: Array = Array(reference.GetStampBounds())
	var actual: Array = Array(view.GetStampBounds())
	check(not actual.is_empty() and actual.size() == expected.size(), "Relief count differs from full renderer")
	for rect: Rect2 in actual:
		check(rect in expected, "Streaming changed relief position or size")
		var cell := Vector2i((rect.get_center() / 64).floor())
		check(cells.GetTerrainKind(cell) != "deep_water", "Rock anchored in water")
	reference.free()
	var before: int = view.StampCount
	cells.SetMetadata(Vector2i(60, 60), "terrain_relief", 0)
	settle(view)
	check(view.StampCount < before, "Flattened cell retained rocks")
	camera.position = Vector2(700, 700) * 64
	camera.force_update_scroll()
	settle(view)
	check(view.StampCount > 0 and view.ResidentReliefCellCount < 20000, "Camera jump failed residency")
	for rect: Rect2 in view.GetStampBounds():
		check(rect.get_center().x > 600 * 64, "Distant rocks retained after jump")
	camera.position = Vector2(1023, 1023) * 64
	camera.rotation = 0.5
	camera.force_update_scroll()
	settle(view)
	for rect: Rect2 in view.GetStampBounds():
		check(Rect2(0, 0, 1024 * 64, 1024 * 64).has_point(rect.get_center()), "Rock outside finite bounds")
	view.visible = false
	view.UpdateReliefResidency()
	check(view.StampCount == 0 and view.ResidentReliefChunkCount == 0, "Hidden relief retained chunks")
	view.visible = true
	await process_frame
	view.set_process(false)
	settle(view)
	check(view.StampCount > 0, "Shown relief did not reload")
	var detailed: Array = Array(view.GetStampBounds())
	camera.zoom = Vector2(0.01, 0.01)
	camera.force_update_scroll()
	view.UpdateReliefResidency()
	check(view.IsReliefDetailSuppressed and view.StampCount == 0 and view.ResidentReliefCellCount == 0, "Overview retained relief stamps")
	check(view.ReliefCellsProcessedLastFrame == 0, "Suppressed relief still processed cells")
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	settle(view)
	check(Array(view.GetStampBounds()) == detailed, "Zoom restore changed relief placement")
	host.free()
	print("[terrain-relief-streaming] OK" if failures.is_empty() else "[terrain-relief-streaming] FAILED")
	quit(0 if failures.is_empty() else 1)
