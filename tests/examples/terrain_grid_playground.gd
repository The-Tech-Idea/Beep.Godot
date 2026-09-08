extends Node2D

@onready var grid = $World/Grid
@onready var cells = $World/Cells
@onready var navigation = $World/Navigation
@onready var follower = $World/Truck/PathFollower
@onready var deposit = $World/Deposits/Stone
@onready var worker = $World/Truck/GridWorker
@onready var jobs = $World/Jobs
@onready var placement = $World/Placement
@onready var build_sites = $World/BuildSites
@onready var message: Label = $HUD/ActionStatus
var mode := "move"
var gather_pending := false
var spawn_cell := Vector2i.ZERO
var deposit_cell := Vector2i.ZERO
var initialized := false
var restoring := false
@export var checkpoint_path := "user://terrain_grid_playground.save"

func _ready() -> void:
	$WorldBuilder.connect("WorldBuilt", _world_built)
	follower.connect("DestinationReached", _arrived)
	follower.connect("MoveFailed", func(_x, _y, reason):
		gather_pending = false
		message.text = str(reason))
	$HUD/Toolbar/Move.pressed.connect(func(): mode = "move")
	$HUD/Toolbar/Gather.pressed.connect(func(): mode = "gather")
	$HUD/Toolbar/Flatten.pressed.connect(func(): mode = "flatten")
	$HUD/Toolbar/Flood.pressed.connect(func(): mode = "flood")
	$HUD/Toolbar/ClearJob.pressed.connect(func(): mode = "clear_job")
	$HUD/BuildingToolbar/Place.pressed.connect(func(): mode = "place")
	$HUD/BuildingToolbar/Demolish.pressed.connect(func(): mode = "demolish")
	$HUD/Toolbar/CancelJobs.pressed.connect(cancel_jobs)
	worker.connect("WorkerStartedJob", func(_worker_id, job_id):
		message.text = "Building shelter" if jobs.call("GetJobKind", job_id) == "build" else "Clearing land")
	jobs.connect("JobCompleted", func(job_id, _worker_id):
		if jobs.call("GetJobKind", job_id) == "clear_land": $World/JobEffects.call("ApplyJobEffect", job_id))
	build_sites.connect("BuildSiteCompleted", func(_build, _job, _placed, _x, _y): message.text = "Shelter ready")
	build_sites.connect("BuildSiteRejected", func(_build, _x, _y, reason): message.text = str(reason))
	worker.connect("WorkerFailedJob", func(_worker_id, job_id, reason):
		if job_id != "":
			jobs.call("CancelJob", job_id, reason)
			message.text = str(reason))
	$World/JobEffects.connect("JobEffectApplied", func(_id, _kind, _x, _y, _effect): message.text = "Land cleared")
	$World/JobEffects.connect("JobEffectRejected", func(_id, _kind, _x, _y, reason): message.text = str(reason))
	$HUD/Toolbar/Save.pressed.connect(save_checkpoint)
	$HUD/Toolbar/Load.pressed.connect(load_checkpoint)
	$HUD/Toolbar/Load.disabled = not FileAccess.file_exists(checkpoint_path)
	$HUD/Toolbar/Regenerate.pressed.connect(func():
		$WorldBuilder.set("Seed", $WorldBuilder.get("Seed") + 1)
		$WorldBuilder.call("NewWorld"))

func _world_built(size: Vector2i) -> void:
	if restoring: return
	initialized = false
	cancel_jobs()
	clear_buildings()
	follower.call("CancelMove")
	gather_pending = false
	# Choose an existing walkable adjacent pair; do not carve a second map for the demo.
	for y in range(3, size.y - 3):
		for x in range(3, size.x - 3):
			var at := Vector2i(x, y)
			var next := at + Vector2i.RIGHT
			if _open_neighbourhood(at) and navigation.call("CanTraverse", at, next):
				spawn_cell = at
				deposit_cell = next
				$World/Truck.global_position = grid.call("CellToWorld", at)
				$World/Truck.z_index = $World/Resources.z_index
				cells.call("ClearLand", at)
				cells.call("ClearLand", next)
				deposit.call("RestoreState", {"cell": next, "resource_id": "stone", "amount": 3, "depleted": false})
				$World/Resources.call("Rebuild")
				$Camera/CameraController.call("SetZoomLevel", 1.2, true)
				$Camera/CameraController.call("FocusWorld", $World/Truck.global_position, true)
				message.text = "Stone: %d" % $World/Wallet.call("GetAmount", "stone")
				initialized = true
				return
	message.text = "No reachable starting pair in this seed"

func _open_neighbourhood(cell: Vector2i) -> bool:
	for y in range(-1, 2):
		for x in range(-1, 2):
			var at := cell + Vector2i(x, y)
			if navigation.call("IsBlocked", at) or cells.call("GetMetadata", at, "terrain_relief") != 0:
				return false
	return true

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		act_at(grid.call("WorldToCell", get_global_mouse_position()))
		get_viewport().set_input_as_handled()

func act_at(cell: Vector2i) -> bool:
	if not initialized: return false
	if not Rect2i(Vector2i.ZERO, $WorldBuilder.get("BuiltSize")).has_point(cell): return false
	match mode:
		"place", "demolish":
			if follower.get("IsMoving") or worker.get("CurrentJobId") != "" or jobs.get("QueuedCount") > 0:
				message.text = "Wait for truck to finish"
				return false
			if mode == "demolish":
				for building in $World/Buildings.get_children():
					if building.get_node("GridObject").get("Cell") == cell:
						building.free()
						message.text = "Shelter removed"
						return true
				return false
			if cell == grid.call("WorldToCell", $World/Truck.global_position) or (cell == deposit.call("CurrentCell") and not deposit.get("IsDepleted")):
				message.text = "Cell is in use"
				return false
			if not place_shelter(cell):
				message.text = "Shelter cannot be placed here"
				return false
			cells.call("ClearLand", cell)
			message.text = "Shelter construction queued"
		"clear_job":
			if not $World/Tools.call("ApplyToCell", cell, 5):
				message.text = "Land cannot be queued"
				return false
			message.text = "Clear job queued"
		"flatten":
			cells.call("SetMetadata", cell, "terrain_relief", 0)
			cells.call("SetMetadata", cell, "terrain_elevation", 0.0)
			message.text = "Ground levelled"
		"flood":
			if placement.call("IsOccupied", cell):
				message.text = "Remove shelter before flooding"
				return false
			cells.call("SetTerrainKind", cell, "water")
			message.text = "Water added"
		_:
			if worker.get("CurrentJobId") != "" or jobs.get("QueuedCount") > 0:
				message.text = "Truck assigned to jobs"
				return false
			gather_pending = mode == "gather" and cell == deposit.call("CurrentCell") and not deposit.get("IsDepleted")
			if mode == "gather" and not gather_pending:
				message.text = "No deposit at this cell"
				return false
			if not follower.call("MoveToCell", cell):
				gather_pending = false
				message.text = "No traversable route"
				return false
	return true

func _arrived(_x: int, _y: int) -> void:
	if gather_pending:
		gather_pending = false
		deposit.call("GatherAllForJob", "")
		message.text = "Stone: %d" % $World/Wallet.call("GetAmount", "stone")
	else:
		message.text = "Destination reached"

func capture_checkpoint() -> Dictionary:
	if not initialized or follower.get("IsMoving") or worker.get("CurrentJobId") != "" or jobs.get("QueuedCount") > 0 or build_sites.get("ActiveBuildSiteCount") > 0: return {}
	var buildings: Array[Vector2i] = []
	for building in $World/Buildings.get_children(): buildings.append(building.get_node("GridObject").get("Cell"))
	return {
		"version": 2,
		"buildings": buildings,
		"world": $WorldBuilder.call("CaptureState"),
		"grid": $World/Snapshot.call("CaptureState"),
		"wallet": $World/Wallet.call("CaptureState"),
		"deposit": deposit.call("CaptureState"),
		"truck_cell": grid.call("WorldToCell", $World/Truck.global_position),
		"spawn_cell": spawn_cell, "deposit_cell": deposit_cell
	}.duplicate(true)

func restore_checkpoint(state: Dictionary) -> bool:
	if state.get("version") != 2 or not state.get("buildings") is Array: return false
	for cell in state.buildings:
		if not cell is Vector2i: return false
	for key in ["world", "grid", "wallet", "deposit"]:
		if not state.get(key) is Dictionary: return false
	for key in ["truck_cell", "spawn_cell", "deposit_cell"]:
		if not state.get(key) is Vector2i: return false
	follower.call("CancelMove")
	gather_pending = false
	cancel_jobs()
	clear_buildings()
	restoring = true
	build_sites.call("DisconnectSystems")
	$World/Snapshot.call("RestoreState", state.grid)
	$World/Wallet.call("RestoreState", state.wallet)
	deposit.call("RestoreState", state.deposit)
	$WorldBuilder.call("RestoreState", state.world)
	for cell in state.buildings:
		if not place_shelter(cell):
			build_sites.call("ConnectSystems")
			restoring = false
			initialized = false
			return false
	build_sites.call("ConnectSystems")
	restoring = false
	spawn_cell = state.spawn_cell
	deposit_cell = state.deposit_cell
	$World/Truck.global_position = grid.call("CellToWorld", state.truck_cell)
	$World/Truck.velocity = Vector2.ZERO
	initialized = true
	$Camera/CameraController.call("FocusWorld", $World/Truck.global_position, true)
	message.text = "Loaded | Stone: %d" % $World/Wallet.call("GetAmount", "stone")
	return true

func place_shelter(cell: Vector2i) -> bool:
	placement.call("BeginPlacement", "shelter")
	placement.call("MovePreviewToCell", cell)
	var building = placement.call("ConfirmPlacement")
	if building == null:
		placement.call("CancelPlacement")
		return false
	building.z_index = $World/Resources.z_index
	if not restoring and not building.has_meta("grid_build_site_job_id"):
		building.free()
		return false
	return true

func clear_buildings() -> void:
	placement.call("CancelPlacement")
	for building in $World/Buildings.get_children(): building.free()

func save_checkpoint() -> bool:
	var state := capture_checkpoint()
	if state.is_empty():
		message.text = "Save unavailable while truck is busy"
		return false
	var file := FileAccess.open(checkpoint_path, FileAccess.WRITE)
	if file == null:
		message.text = "Save failed"
		return false
	file.store_var(state, false)
	var saved := file.get_error() == OK
	file.close()
	$HUD/Toolbar/Load.disabled = not FileAccess.file_exists(checkpoint_path)
	message.text = "Saved" if saved else "Save failed"
	return saved

func load_checkpoint() -> bool:
	var file := FileAccess.open(checkpoint_path, FileAccess.READ)
	if file == null:
		message.text = "No saved world"
		return false
	var state: Variant = file.get_var(false)
	file.close()
	if not state is Dictionary or not restore_checkpoint(state):
		message.text = "Invalid saved world"
		return false
	return true

func cancel_jobs() -> void:
	worker.call("CancelCurrentJob", "jobs_cancelled")
	for job in jobs.call("GetJobs"): jobs.call("CancelJob", job.id, "jobs_cancelled")
	jobs.call("ClearJobs")
	gather_pending = false
	$HUD/JobProgress.visible = false

func _process(_delta: float) -> void:
	if not initialized or restoring: return
	if worker.get("CurrentJobId") == "" and not follower.get("IsMoving") and jobs.get("QueuedCount") > 0:
		worker.call("ClaimNextJob")
	var job_id: String = worker.get("CurrentJobId")
	$HUD/JobProgress.visible = job_id != ""
	if job_id != "": $HUD/JobProgress.value = jobs.call("GetJobProgress01", job_id)
