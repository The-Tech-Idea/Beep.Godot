extends Control

@onready var world: Node = $Layout/Split/Preview/Viewport/World
@onready var registry: Node = world.get_node("Registry")
@onready var wallet: Node = world.get_node("Wallet")
@onready var simulation: Node = world.get_node("ProductionSimulation")
@onready var clock: Node = world.get_node("Clock")
@onready var roster: ItemList = $Layout/Split/Controls/Roster
@onready var status: Label = $Layout/Status
@onready var stock: Label = $Layout/Stock
@onready var controls: VBoxContainer = $Layout/Split/Controls
var snapshot: Dictionary = {}
var selected := "mill_1"
var refresh_elapsed := 0.0

func _ready() -> void:
	wallet.SetAmount("wood", 120)
	for i in 3:
		var id := "mill_%s" % (i + 1)
		registry.SpawnActor("sawmill", "settlement", Vector2(140 + i * 205, 245), id)
		simulation.StartProduction(id, "planks")
	roster.item_selected.connect(func(index): selected = "mill_%s" % (index + 1); refresh())
	controls.get_node("Pause").pressed.connect(toggle_production)
	controls.get_node("Residency").pressed.connect(toggle_residency)
	controls.get_node("Remove").pressed.connect(remove_selected)
	controls.get_node("Save").pressed.connect(capture_snapshot)
	controls.get_node("Restore").pressed.connect(restore_snapshot)
	controls.get_node("Advance").pressed.connect(func(): clock.AdvanceTurns(10.0); refresh())
	world.get_viewport().size_changed.connect(fit_preview)
	fit_preview()
	refresh()

func fit_preview() -> void:
	var size_: Vector2 = world.get_viewport().get_visible_rect().size
	world.get_node("Camera").zoom = Vector2.ONE * maxf(0.1, minf(size_.x / 740.0, size_.y / 480.0))

func _process(delta: float) -> void:
	refresh_elapsed += delta
	if refresh_elapsed >= 0.2:
		refresh_elapsed = 0.0
		refresh()

func toggle_production() -> void:
	var record: Dictionary = simulation.GetProduction(selected)
	if record.is_empty(): return
	if record.state == 1: simulation.PauseProduction(selected)
	elif record.state == 2: simulation.ResumeProduction(selected)
	else: simulation.StartProduction(selected, "planks")
	refresh()

func toggle_residency() -> void:
	if simulation.GetProduction(selected).is_empty(): return
	if registry.FindActor(selected) != null: registry.TrySleepActor(selected)
	else: registry.WakeActor(selected)
	refresh()

func remove_selected() -> void:
	registry.RemoveActor(selected)
	refresh()

func capture_snapshot() -> void:
	snapshot = {"actors": registry.CaptureState(), "wallet": wallet.CaptureState(), "production": simulation.CaptureState()}
	refresh()

func restore_snapshot() -> void:
	if snapshot.is_empty(): return
	wallet.RestoreState(snapshot.wallet)
	registry.RestoreState(snapshot.actors)
	simulation.RestoreState(snapshot.production)
	refresh()

func refresh() -> void:
	roster.clear()
	for i in 3:
		var id := "mill_%s" % (i + 1)
		var record: Dictionary = simulation.GetProduction(id)
		var state := "Removed"
		if not record.is_empty():
			state = ["Idle", "Producing", "Paused"][int(record.state)]
			state += " / " + ("loaded" if registry.FindActor(id) != null else "unloaded")
		var actor: Node = registry.FindActor(id)
		if actor != null: actor.get_parent().get_node("Name").text = "Sawmill %s" % (i + 1)
		roster.add_item("Sawmill %s: %s" % [i + 1, state])
	roster.select(int(selected.trim_prefix("mill_")) - 1)
	stock.text = "Wood  %s     Planks  %s" % [wallet.GetAmount("wood"), wallet.GetAmount("plank")]
	var record: Dictionary = simulation.GetProduction(selected)
	var missing := record.is_empty()
	controls.get_node("Pause").disabled = missing
	controls.get_node("Residency").disabled = missing
	controls.get_node("Remove").disabled = missing
	controls.get_node("Restore").disabled = snapshot.is_empty()
	controls.get_node("Pause").text = "Pause production" if not missing and record.state == 1 else "Resume production"
	controls.get_node("Residency").text = "Unload scene" if registry.FindActor(selected) != null else "Wake scene"
	status.text = "%s production records | %s visible scenes | %s queued deadlines" % [simulation.ProductionCount, world.get_node("Actors").get_child_count(), clock.ScheduledWorkCount]
	if not missing: status.text += " | Next output: %.1f turns" % record.remaining_turns
