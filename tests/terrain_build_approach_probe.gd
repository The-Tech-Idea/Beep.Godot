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
	create_timer(30).timeout.connect(func(): push_error("Build approach probe timed out"); quit(1))
	var host := Node2D.new()
	root.add_child(host)
	var cells := make("GridCellData", host, "Cells")
	var nav := make("GridNavigation", host, "Navigation", {"CellDataPath": NodePath("../Cells"), "BoundsSize": Vector2i(4, 4)})
	var jobs := make("GridJobQueue", host, "Jobs")
	var definition: Resource = load(BASE + "GridBuildDefinition.cs").new()
	definition.set("BuildId", "shelter")
	definition.set("BuildTurns", 1)
	make("GridBuildCatalog", host, "Catalog", {"Builds": [definition]})
	var sites := make("GridBuildSite", host, "Sites", {"BuildCatalogPath": NodePath("../Catalog"),
		"JobQueuePath": NodePath("../Jobs"), "NavigationPath": NodePath("../Navigation")})
	var created: Array[String] = []
	sites.connect("BuildSiteCreated", func(_build, job, _placed, _x, _y): created.append(job))
	var placed := Node2D.new()
	host.add_child(placed)
	var anchor := Vector2i(1, 3)
	assert(sites.call("RegisterPlacedBuild", "shelter", placed, anchor))
	assert(jobs.call("GetJobApproachCell", created.back()) == Vector2i(1, 2), "Map-edge site chose out-of-bounds front")
	assert(jobs.call("CancelJob", created.back(), "test"))
	await process_frame
	var second := Node2D.new()
	host.add_child(second)
	anchor = Vector2i(1, 1)
	for cell in [Vector2i(1, 2), Vector2i(1, 0), Vector2i(0, 1)]: cells.call("SetTerrainKind", cell, "water")
	assert(sites.call("RegisterPlacedBuild", "shelter", second, anchor))
	assert(jobs.call("GetJobApproachCell", created.back()) == Vector2i(2, 1), "Coastal site ignored dry side")
	assert(jobs.call("CancelJob", created.back(), "test"))
	await process_frame
	var sealed := Node2D.new()
	host.add_child(sealed)
	cells.call("SetTerrainKind", Vector2i(2, 1), "water")
	assert(not sites.call("RegisterPlacedBuild", "shelter", sealed, anchor))
	assert(not sealed.has_meta("grid_build_site_state"), "Rejected site was marked under construction")
	assert(jobs.get("QueuedCount") == 0)
	cells.call("SetTerrainKind", Vector2i(2, 1), "grass")
	sites.set("NavigationPath", NodePath("../Missing"))
	assert(not sites.call("RegisterPlacedBuild", "shelter", sealed, anchor), "Broken explicit navigation used old map")
	sites.set("NavigationPath", NodePath("../Navigation"))
	assert(sites.call("RegisterPlacedBuild", "shelter", sealed, anchor))
	assert(nav.call("IsBlocked", jobs.call("GetJobApproachCell", created.back())) == false)
	assert(jobs.call("CancelJob", created.back(), "test"))
	await process_frame
	definition.set("RequiredMaterials", [{"resource_id": "wood", "amount": 2}])
	var bare := Node2D.new()
	bare.modulate = Color(0.4, 0.5, 0.6, 1)
	host.add_child(bare)
	var bare_object := make("GridObject", bare, "GridObject")
	sites.set("HidePlacedUntilBuilt", true)
	assert(not sites.call("RegisterPlacedBuild", "shelter", bare, anchor))
	assert(bare.visible and bare.modulate == Color(0.4, 0.5, 0.6, 1))
	assert(not bare.has_meta("grid_build_site_state") and bare_object.get("Complete"), "Rejected storage-less site changed building state")
	sites.set("HidePlacedUntilBuilt", false)
	var pending := Node2D.new()
	host.add_child(pending)
	var storage := make("GridStorage", pending, "Storage")
	assert(sites.call("RegisterPlacedBuild", "shelter", pending, anchor))
	cells.call("SetTerrainKind", Vector2i(2, 1), "water")
	assert(storage.call("Load", "wood", 2) == 2)
	assert(sites.get("PendingMaterialsCount") == 1 and jobs.get("QueuedCount") == 0)
	assert(storage.call("Stored", "wood") == 2, "Inaccessible site consumed delivered materials")
	assert(storage.Reserved("wood") == 2 and storage.Available("wood") == 0, "Pending site did not reserve delivered materials")
	assert(storage.Unload("wood", 2) == 0, "Another consumer stole the pending site's materials")
	cells.call("SetTerrainKind", Vector2i(2, 1), "grass")
	for frame in range(4):
		await process_frame
		if sites.PendingMaterialsCount == 0: break
	assert(sites.get("PendingMaterialsCount") == 0 and jobs.get("QueuedCount") == 1)
	assert(storage.call("Stored", "wood") == 0)
	assert(storage.Reserved("wood") == 0, "Starting construction retained a material claim")
	var build_job: String = created.back()
	var completed: Array[String] = []
	var rejected: Array[String] = []
	sites.connect("BuildSiteCompleted", func(_build, job, _placed, _x, _y): completed.append(job))
	sites.connect("BuildSiteRejected", func(_build, _x, _y, reason): rejected.append(reason))
	assert(jobs.call("ClaimJob", build_job, "crew"))
	cells.call("SetTerrainKind", Vector2i(2, 1), "water")
	assert(jobs.call("CompleteJob", build_job, "crew"))
	assert(completed.is_empty() and rejected == ["build_approach_blocked"], "Flooded work cell completed construction")
	assert(sites.get("ActiveBuildSiteCount") == 0)
	await process_frame
	assert(not is_instance_valid(pending), "Rejected completion left unfinished site")
	# Cancelling a blocked site releases the claim without consuming its supplies.
	cells.SetTerrainKind(Vector2i(2, 1), "grass")
	var cancelled_site := Node2D.new()
	host.add_child(cancelled_site)
	var cancelled_stock := make("GridStorage", cancelled_site, "Storage")
	assert(sites.RegisterPlacedBuild("shelter", cancelled_site, anchor))
	cells.SetTerrainKind(Vector2i(2, 1), "water")
	cancelled_stock.Load("wood", 2)
	assert(cancelled_stock.Reserved("wood") == 2)
	sites.RemovePlacedOnJobCancelled = false
	assert(sites.CancelPendingBuild(cancelled_site))
	assert(cancelled_stock.Reserved("wood") == 0 and cancelled_stock.Stored("wood") == 2, "Cancellation lost delivered stock or leaked its claim")
	definition.RequiredMaterials = [{"resource_id": "wood", "amount": 0}]
	cells.SetTerrainKind(Vector2i(2, 1), "grass")
	var zero_site := Node2D.new()
	host.add_child(zero_site)
	make("GridStorage", zero_site, "Storage")
	assert(sites.RegisterPlacedBuild("shelter", zero_site, anchor))
	assert(sites.PendingMaterialsCount == 0 and jobs.QueuedCount == 1, "Zero material amount stalled construction without a claim")
	jobs.CancelJob(created.back(), "test")
	definition.RequiredMaterials = [{"resource_id": "wood", "amount": 2}]
	# Bounded, round-robin retries must reach every site without a manual refresh.
	sites.PendingChecksPerFrame = 1
	nav.BoundsSize = Vector2i(24, 4)
	var waiting: Array[Node2D] = []
	for index in range(5):
		var site := Node2D.new()
		host.add_child(site)
		var stock := make("GridStorage", site, "Storage")
		var site_cell := Vector2i(5 + index * 3, 1)
		assert(sites.RegisterPlacedBuild("shelter", site, site_cell))
		for offset in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			cells.SetTerrainKind(site_cell + offset, "water")
		stock.Load("wood", 2)
		waiting.append(site)
	for frame in range(3):
		await process_frame
		assert(sites.PendingChecksLastFrame <= 1 and sites.PendingMaterialsCount == 5)
	# A removed site must not remain in the retry queue.
	waiting.pop_back().free()
	for index in range(5): cells.SetTerrainKind(Vector2i(5 + index * 3, 2), "grass")
	for frame in range(12):
		await process_frame
		assert(sites.PendingChecksLastFrame <= 1)
		if sites.PendingMaterialsCount == 0: break
	assert(sites.PendingMaterialsCount == 0 and jobs.QueuedCount == 4, "Automatic retry: pending=%s queued=%s" % [sites.PendingMaterialsCount, jobs.QueuedCount])
	for job in created.slice(created.size() - 4): jobs.CancelJob(job, "test")
	# The coordinator may leave while buildings and their storage remain alive.
	cells.SetTerrainKind(Vector2i(2, 1), "grass")
	var detached_site := Node2D.new()
	host.add_child(detached_site)
	var detached_stock := make("GridStorage", detached_site, "Storage")
	assert(sites.RegisterPlacedBuild("shelter", detached_site, anchor))
	cells.SetTerrainKind(Vector2i(2, 1), "water")
	detached_stock.Load("wood", 2)
	assert(detached_stock.Reserved("wood") == 2)
	host.remove_child(sites)
	assert(detached_stock.Reserved("wood") == 0 and detached_stock.Stored("wood") == 2, "Coordinator exit leaked material reservation")
	sites.free()
	host.free()
	print("[terrain-build-approach] OK")
	quit()
