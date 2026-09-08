extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var completions := 0
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()

func make(path: String, name_: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = name_
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var clock := make("grid/GridWorkClockComponent", "Clock", host)
	clock.set_process(false)
	var wallet := make("grid/GridResourceWalletComponent", "Wallet", host)
	wallet.SetAmount("wood", 100)
	var recipes := [{"recipe_id": "planks", "duration_turns": 2.0,
		"inputs": [{"resource_id": "wood", "amount": 1}],
		"outputs": [{"resource_id": "plank", "amount": 1}]}]
	var sim := make("grid/GridProductionSimulationComponent", "ProductionSimulation", host,
		{"WorkClockPath": NodePath("../Clock"), "ResourceWalletPath": NodePath("../Wallet"), "Recipes": recipes})
	var actors := Node2D.new()
	actors.name = "Actors"
	host.add_child(actors)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Id = "producer"
	definition.Scene = load("res://tests/production_residency_unit.tscn")
	definition.SimulationPolicy = 1
	var registry := make("actors/ActorRegistryComponent", "Registry", host,
		{"ActorsRootPath": NodePath("../Actors"), "Definitions": [definition]})
	var player := make("actors/PlayerContextComponent", "Player", host,
		{"RegistryPath": NodePath("../Registry"), "PlayerId": "settlement", "ReadLocalInput": false})
	var actor: Node = registry.SpawnActor("producer", "settlement", Vector2(1000, 1000), "mill_1")
	if actor == null:
		push_error("Could not spawn owned economic actor")
		host.free()
		quit(1)
		return
	var view: Node = actor.get_parent().get_node("Production")
	check(view.StartProduction("planks") and sim.ProductionCount == 1, "View did not create one authoritative record")
	check(clock.ScheduledWorkCount == 1 and not view.is_processing(), "View created a second production clock")
	clock.AdvanceTurns(1.0)
	check(is_equal_approx(view.RemainingTurns, 1.0), "View progress disagrees with record")
	check(registry.TrySleepActor("mill_1"), "World-owned production prevented safe scene retirement")
	await process_frame
	check(not is_instance_valid(actor) and not is_instance_valid(view), "Sleeping actor retained its scene")
	check(player.PossessedActorId.is_empty() and player.GetOwnedActors().size() == 1, "Dormant economy lost player ownership or required an avatar")
	clock.AdvanceTurns(5.0)
	check(wallet.GetAmount("plank") == 3 and wallet.GetAmount("wood") == 96, "Production stopped or duplicated while actor was absent")
	var before_wake: Dictionary = sim.GetProduction("mill_1")
	actor = registry.WakeActor("mill_1")
	view = actor.get_parent().get_node("Production")
	check(view.CaptureState() == before_wake and sim.ProductionCount == 1, "Actor snapshot rewound or duplicated world production")
	view.ProductionCompleted.connect(func(_recipe): completions += 1)
	clock.AdvanceTurns(2.0)
	check(wallet.GetAmount("plank") == 4 and completions == 1, "Recreated view did not observe world completion once")
	view.PauseProduction()
	clock.AdvanceTurns(10.0)
	check(wallet.GetAmount("plank") == 4 and view.State == 2, "View pause did not reach the authoritative record")
	view.ResumeProduction()
	clock.AdvanceTurns(2.0)
	check(wallet.GetAmount("plank") == 5 and completions == 2, "View resume did not reach world simulation")
	view.CancelProduction(true)
	check(wallet.GetAmount("wood") == 95 and clock.ScheduledWorkCount == 0, "View cancellation did not refund exactly once")
	check(view.StartProduction("planks"), "Cancelled world record could not restart")
	var local := make("grid/GridProductionComponent", "Local", actor.get_parent(),
		{"ResourceWalletPath": NodePath("../../../Wallet"), "WorkClockPath": NodePath("../../../Clock"), "Recipes": recipes})
	local.StartProduction("planks")
	check(not local.CanSuspendActor(), "Scene-owned active production can be silently discarded")
	check(not registry.TrySleepActor("mill_1"), "Residency ignored scene-owned production")
	local.CancelProduction(false)
	check(local.CanSuspendActor(), "Idle local production unnecessarily prevents residency")
	var destroyed: Array[String] = []
	registry.ActorDestroyed.connect(func(id): destroyed.append(id))
	var actor_saved: Dictionary = registry.CaptureState()
	var production_before_restore: Dictionary = sim.GetProduction("mill_1")
	var empty_actors: Dictionary = actor_saved.duplicate(true)
	empty_actors.actors.clear()
	registry.RestoreState(empty_actors)
	check(destroyed.is_empty() and sim.ProductionCount == 1, "Snapshot reconciliation was treated as gameplay destruction")
	registry.RestoreState(actor_saved)
	check(sim.GetProduction("mill_1") == production_before_restore, "Actor restoration rewound linked production")
	check(registry.TrySleepActor("mill_1"), "Restored producer could not retire")
	await process_frame
	var economy_saved: Dictionary = sim.CaptureState()
	check(economy_saved.mill_1.actor_id == "mill_1", "Actor association was not saved")
	sim.free()
	sim = make("grid/GridProductionSimulationComponent", "ProductionSimulation", host,
		{"WorkClockPath": NodePath("../Clock"), "ResourceWalletPath": NodePath("../Wallet"), "Recipes": recipes})
	check(sim.RestoreState(economy_saved), "Fresh simulation service could not restore actor links")
	var output_before_delete: int = wallet.GetAmount("plank")
	check(registry.RemoveActor("mill_1") and sim.ProductionCount == 0 and clock.ScheduledWorkCount == 0,
		"Permanent dormant actor deletion left orphan production")
	check(not registry.RemoveActor("mill_1") and destroyed == ["mill_1"], "Destruction was delivered more than once")
	clock.AdvanceTurns(10.0)
	check(wallet.GetAmount("plank") == output_before_delete, "Deleted economic actor continued producing")
	actor = registry.SpawnActor("producer", "settlement", Vector2.ZERO, "mill_2")
	view = actor.get_parent().get_node("Production")
	var wood_before: int = wallet.GetAmount("wood")
	check(view.StartProduction("planks"), "Second producer did not start")
	sim.ProductionCompleted.connect(func(id, _recipe):
		if id == "mill_2": registry.RemoveActor("mill_2"))
	clock.AdvanceTurns(20.0)
	check(sim.ProductionCount == 0 and clock.ScheduledWorkCount == 0 and registry.FindActor("mill_2") == null,
		"Destruction inside completion left an active economic record")
	check(wallet.GetAmount("plank") == output_before_delete + 1 and wallet.GetAmount("wood") == wood_before - 1,
		"Destroyed producer started another cycle or duplicated a transaction")
	host.free()
	print("[production-residency] OK: scene retired, world production continued, wake retained current state" if failures.is_empty() else "[production-residency] FAILED")
	quit(0 if failures.is_empty() else 1)
