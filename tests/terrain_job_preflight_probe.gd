extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"

func make(kind: String, parent: Node, label: String, values: Dictionary = {}) -> Node:
	var node: Node = load(BASE + kind + "Component.cs").new()
	node.name = label
	for key in values: node.set(key, values[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells := make("GridCellData", host, "Cells")
	var replacement := make("GridCellData", host, "Replacement")
	var wallet := make("GridResourceWallet", host, "Wallet", {"ParticipatesInSave": false})
	var jobs := make("GridJobQueue", host, "Jobs")
	var tools := make("GridToolAction", host, "Tools", {"CellDataPath": NodePath("../Cells"), "UseNavigationBounds": false})
	var resource := make("GridResourceNode", host, "Deposit", {"UseExplicitCell": true,
		"Cell": Vector2i.ZERO, "ResourceId": "wood", "Amount": 3, "ResourceWalletPath": NodePath("../Wallet")})
	var effects := make("GridJobEffect", host, "Effects", {"JobQueuePath": NodePath("../Jobs"),
		"CellDataPath": NodePath("../Cells"), "ToolActionPath": NodePath("../Tools"), "ResourceNodesRootPath": NodePath("..")})
	var rejected: Array[String] = []
	effects.connect("JobEffectRejected", func(_id, _kind, _x, _y, reason): rejected.append(reason))
	var job: String = jobs.call("AddJob", Vector2i.ZERO, "clear_land", 1.0, 0)
	assert(jobs.call("ClaimJob", job, "crew"))
	cells.call("SetTerrainKind", Vector2i.ZERO, "water")
	assert(jobs.call("CompleteJob", job, "crew"))
	assert(rejected == ["clear_rejected"], "Flooded completion did not reject clearing")
	assert(resource.get("Amount") == 3 and wallet.call("GetAmount", "wood") == 0, "Rejected clear consumed deposit before validation")
	assert(cells.call("GetFlags", Vector2i.ZERO) & 2 == 0)
	cells.call("SetTerrainKind", Vector2i.ZERO, "grass")
	effects.set("ToolActionPath", NodePath("../MissingTools"))
	for kind in ["clear_land", "till", "water", "harvest"]:
		assert(not effects.call("ApplyJobEffect", "missing", kind, Vector2i.ZERO), "Broken explicit tool silently fell back to direct mutation")
	assert(resource.get("Amount") == 3 and cells.call("GetFlags", Vector2i.ZERO) == 0)
	effects.set("ToolActionPath", NodePath("../Tools"))
	replacement.call("SetTerrainKind", Vector2i.ZERO, "water")
	tools.set("CellDataPath", NodePath("../Replacement"))
	assert(not effects.call("ApplyJobEffect", "replacement", "clear_land", Vector2i.ZERO), "Tool retained previous live source")
	assert(resource.get("Amount") == 3)
	tools.set("CellDataPath", NodePath("../Cells"))
	assert(effects.call("ApplyJobEffect", "valid", "clear_land", Vector2i.ZERO))
	assert(resource.get("IsDepleted") and wallet.call("GetAmount", "wood") == 3)
	assert(cells.call("GetFlags", Vector2i.ZERO) & 2 != 0, "Valid clear did not apply land effect")
	# Replacing a queue at the same path must retire the old world's subscription.
	jobs.name = "OldJobs"
	var current_jobs := make("GridJobQueue", host, "Jobs")
	effects.call("ConnectQueue")
	var old_cell := Vector2i(1, 0)
	var current_cell := Vector2i(2, 0)
	var old_job: String = jobs.call("AddJob", old_cell, "clear_land", 1.0, 0)
	assert(jobs.call("ClaimJob", old_job, "old_crew"))
	assert(jobs.call("CompleteJob", old_job, "old_crew"))
	assert(cells.call("GetFlags", old_cell) & 2 == 0, "Detached queue still mutated terrain")
	var current_job: String = current_jobs.call("AddJob", current_cell, "clear_land", 1.0, 0)
	assert(current_jobs.call("ClaimJob", current_job, "crew"))
	assert(current_jobs.call("CompleteJob", current_job, "crew"))
	assert(cells.call("GetFlags", current_cell) & 2 != 0, "Replacement queue did not apply terrain effect")
	effects.set("JobQueuePath", NodePath("../MissingJobs"))
	effects.call("ConnectQueue")
	var detached_cell := Vector2i(3, 0)
	var detached_job: String = current_jobs.call("AddJob", detached_cell, "clear_land", 1.0, 0)
	assert(current_jobs.call("ClaimJob", detached_job, "crew"))
	assert(current_jobs.call("CompleteJob", detached_job, "crew"))
	assert(cells.call("GetFlags", detached_cell) & 2 == 0, "Missing queue path retained old subscription")
	host.free()
	print("[terrain-job-preflight] OK")
	quit()
