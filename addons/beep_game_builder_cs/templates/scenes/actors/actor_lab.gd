extends Node2D

const COMMAND = preload("res://addons/beep_game_builder_cs/ecs/actors/ActorCommand.cs")
@export var background_generation := false
@export_range(1, 12, 1) var spawn_search_radius := 12
var home := Vector2i.ZERO
var available_cells: Array[Vector2i] = []
var saved_json := ""
var ready_to_play := false

func _ready() -> void:
	$HUD/Toolbar.resized.connect(layout_status)
	layout_status.call_deferred()
	$HUD/Toolbar/Row/Spawn.pressed.connect(spawn_worker)
	$HUD/Toolbar/Row/Select.pressed.connect(select_all)
	$HUD/Toolbar/Row/Work.pressed.connect(assign_jobs)
	$HUD/Toolbar/Row/Escort.pressed.connect(escort_selection)
	$HUD/Toolbar/Row/Stop.pressed.connect(func(): $Player.IssueOrder(2, Vector2i.ZERO, "", false, ""))
	$HUD/Toolbar/Row/Save.pressed.connect(save_snapshot)
	$HUD/Toolbar/Row/Load.pressed.connect(load_snapshot)
	$HUD/Toolbar/Row/Fit.pressed.connect(fit_view)
	$Player.SelectionChanged.connect(update_status)
	$Registry.CommandRejected.connect(func(_id, reason): $HUD/Status.text = "Order rejected: " + reason)
	$Jobs.QueueChanged.connect(func(_a, _b, _c): update_status())
	$World.WorldBuilt.connect(world_built)
	# Let the authored loading state render before starting generation.
	await get_tree().process_frame
	await get_tree().process_frame
	if background_generation:
		$World.BeginNewWorld()
	else:
		$World.NewWorld()

func layout_status() -> void:
	$HUD/Status.position.y = $HUD/Toolbar.position.y + $HUD/Toolbar.size.y + 8.0

func escort_selection() -> void:
	if not ready_to_play: return
	var ids: Array = $Player.GetSelectedActors()
	if ids.size() < 2: return
	ids.sort()
	var leader: String = ids.pop_front()
	var preceding := leader
	var assigned := 0
	for id in ids:
		var command: Resource = COMMAND.new()
		command.IssuerId = $Player.PlayerId
		command.Recipients = [id]
		command.Action = 5
		command.TargetActorId = preceding
		command.FollowDistance = maxf($Grid.TileSize.x, $Grid.TileSize.y) * 1.5
		if $Registry.Submit(command) == 1:
			assigned += 1
			preceding = id
	if assigned > 0:
		$Player.SelectActor(leader, false)
	update_status()

func world_built(_size: Vector2i) -> void:
	var starts: Array = $Generator.GetStartPositions()
	if starts.is_empty():
		$HUD/Status.text = "No usable start position"
		return
	home = starts[0]
	available_cells.clear()
	for y in range(-spawn_search_radius, spawn_search_radius + 1):
		for x in range(-spawn_search_radius, spawn_search_radius + 1):
			var cell := home + Vector2i(x, y)
			if $Navigation.IsInBounds(cell) and not $Navigation.IsBlocked(cell):
				if not $Navigation.FindCellPath(home, cell).is_empty(): available_cells.append(cell)
	available_cells.sort_custom(func(a, b): return a.distance_squared_to(home) < b.distance_squared_to(home))
	ready_to_play = true
	for i in 6: spawn_worker()
	select_all()
	$Camera2D/Controller.FocusWorld($Grid.CellToWorld(home), true)
	$Camera2D/Controller.SetZoomLevel(0.8, true)
	update_status()

func spawn_worker() -> void:
	if not ready_to_play or $Registry.ActorCount >= available_cells.size(): return
	var occupied := {}
	for id in $Player.GetOwnedActors():
		occupied[$Grid.WorldToCell($Registry.FindActor(id).get_parent().global_position)] = true
	for cell in available_cells:
		if occupied.has(cell): continue
		var actor = $Registry.SpawnActor("worker", "settlement", $Grid.CellToWorld(cell), "")
		if actor != null: actor.CommandFinished.connect(func(_action, _success, _reason): update_status())
		break
	update_status()

func select_all() -> void:
	$Player.ClearSelection()
	for id in $Player.GetOwnedActors(): $Player.SelectActor(id, true)

func assign_jobs() -> void:
	var ids: Array = $Player.GetSelectedActors()
	for i in ids.size():
		if available_cells.is_empty(): break
		var cell: Vector2i = available_cells[mini(available_cells.size() - 1, 35 + i * 3)]
		var job: String = $Jobs.AddJob(cell, "prepare", 3.0, 0)
		var command: Resource = COMMAND.new()
		command.IssuerId = "settlement"
		command.Recipients = [ids[i]]
		command.Action = 3
		command.JobId = job
		if $Registry.Submit(command) == 0: $Jobs.CancelJob(job, "order_rejected")

func save_snapshot() -> void:
	var jobs: Array = $Jobs.GetJobs()
	for job in jobs:
		for key in ["cell", "approach_cell", "reserved_cell"]:
			if job.has(key):
				var cell: Vector2i = job[key]
				job[key] = {"x": cell.x, "y": cell.y}
	saved_json = JSON.stringify({"jobs": jobs, "actors": $Registry.CaptureState()})
	$HUD/Status.text = "Snapshot saved: %d workers" % $Registry.ActorCount

func load_snapshot() -> void:
	if saved_json.is_empty(): return
	var state: Variant = JSON.parse_string(saved_json)
	if state is Dictionary:
		$Jobs.LoadJobs(state["jobs"], true)
		$Registry.RestoreState(state["actors"])
		update_status()

func fit_view() -> void:
	if not ready_to_play: return
	var extent: Vector2 = Vector2($World.BuiltSize) * $Grid.TileSize
	$Camera2D/Controller.FocusWorld(extent * 0.5, true)
	var viewport := get_viewport_rect().size - Vector2(40, 150)
	var fit := minf(viewport.x / extent.x, viewport.y / extent.y)
	$Camera2D/Controller.SetZoomLevel(fit, true)

func update_status() -> void:
	if not ready_to_play: return
	var escorting := 0
	for id in $Player.GetOwnedActors():
		var actor = $Registry.FindActor(id)
		if actor != null and not actor.FollowTargetActorId.is_empty(): escorting += 1
	$HUD/Toolbar/Row/Escort.disabled = $Player.GetSelectedActors().size() < 2
	$HUD/Status.text = "%d workers | %d selected | %d working | %d escorting | %d jobs completed" % [$Registry.ActorCount, $Player.GetSelectedActors().size(), $Jobs.ClaimedCount, escorting, $Jobs.CompletedCount]
