extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var clock: Node = load(BASE + "GridWorkClockComponent.cs").new()
	clock.ScheduledWorkBudgetMilliseconds = 0.0 # Count-budget assertions must not depend on machine speed.
	clock.name = "Clock"
	host.add_child(clock)
	clock.set_process(false)
	var wallet: Node = load(BASE + "GridResourceWalletComponent.cs").new()
	wallet.name = "Wallet"
	host.add_child(wallet)
	wallet.SetAmount("wood", 10000)
	var sim: Node = load(BASE + "GridProductionSimulationComponent.cs").new()
	sim.ResourceWalletPath = NodePath("../Wallet")
	sim.WorkClockPath = NodePath("../Clock")
	sim.Recipes = [{"recipe_id": "planks", "duration_turns": 2.0,
		"inputs": [{"resource_id": "wood", "amount": 1}],
		"outputs": [{"resource_id": "plank", "amount": 1}]}]
	host.add_child(sim)
	var nodes_before: int = get_node_count()
	for i in 1000: check(sim.StartProduction("factory_%s" % i, "planks"), "Record creation failed")
	check(get_node_count() == nodes_before and sim.get_child_count() == 0, "Simulation instantiated per-factory nodes")
	check(not sim.is_processing() and sim.ProductionCount == 1000, "Simulation count/process mode incorrect")
	check(wallet.GetAmount("wood") == 9000 and not sim.StartProduction("factory_0", "planks"), "Duplicate ID spent inputs")
	clock.AdvanceTurns(1.0)
	check(wallet.GetAmount("plank") == 0 and sim.GetProduction("factory_999").remaining_turns == 1.0, "Record progress is incorrect")
	clock.AdvanceTurns(1.0)
	check(wallet.GetAmount("plank") == 256, "Record dispatch was not bounded")
	for i in 3: clock.AdvanceTurns(0.125)
	check(wallet.GetAmount("plank") == 1000 and wallet.GetAmount("wood") == 8000, "Record transactions lost or duplicated cycles")
	check(is_equal_approx(sim.GetProduction("factory_999").remaining_turns, 1.625), "Delayed record lost world time")
	sim.PauseProduction("factory_0")
	var state: Dictionary = sim.CaptureState()
	var amounts: Dictionary = wallet.CaptureState()
	var deadlines: int = clock.ScheduledWorkCount
	var invalid := {"broken": {"state": 1, "recipe_id": "missing"}}
	check(not sim.RestoreState(invalid) and sim.CaptureState() == state and clock.ScheduledWorkCount == deadlines, "Invalid restore partially replaced records")
	check(sim.RemoveProduction("factory_1", true) and wallet.GetAmount("wood") == 8001, "Removal did not refund its committed input")
	check(not sim.RemoveProduction("factory_1", true) and wallet.GetAmount("wood") == 8001, "Removal refunded twice")
	wallet.RestoreState(amounts)
	check(sim.RestoreState(state) and sim.ProductionCount == 1000, "Restore did not rebuild records")
	check(sim.GetProduction("factory_0").state == 2 and clock.ScheduledWorkCount == 999, "Restore resumed a paused record")
	clock.AdvanceTurns(2.0)
	for i in 3: clock.AdvanceTurns(0.125)
	check(wallet.GetAmount("plank") == 1999, "Restored records lost or duplicated transactions")
	check(sim.GetProduction("factory_0").remaining_turns == state.factory_0.remaining_turns, "Paused record consumed elapsed time")
	var retained: Dictionary = sim.CaptureState()
	host.remove_child(sim)
	check(clock.ScheduledWorkCount == 0, "Detached service retained callbacks")
	clock.AdvanceTurns(10.0)
	host.add_child(sim)
	check(sim.CaptureState() == retained and clock.ScheduledWorkCount == 999, "Reattached service lost retained records")
	check(sim.RestoreState({}), "Could not clear previous simulation records")
	wallet.SetAmount("wood", 10000)
	wallet.SetAmount("plank", 0)
	sim.Loop = false
	clock.ScheduledWorkBudgetMilliseconds = 2.0
	for i in 1000: check(sim.StartProduction("timed_%s" % i, "planks"), "Timed record creation failed")
	clock.AdvanceTurns(2.0)
	var dispatches := 1
	while clock.ScheduledWorkCount > 0 and dispatches < 1100:
		clock.AdvanceTurns(0.000001)
		dispatches += 1
	check(wallet.GetAmount("plank") == 1000 and wallet.GetAmount("wood") == 9000
		and clock.ScheduledWorkCount == 0, "Timed production dispatch lost transactions or starved records")
	check(get_node_count() == nodes_before, "Timed dispatch instantiated factory scenes")
	print("[production-simulation] timed 1000-record backlog drained in %s dispatches" % dispatches)
	host.free()
	print("[production-simulation] OK: 1000 records, zero per-factory nodes" if failures.is_empty() else "[production-simulation] FAILED")
	quit(0 if failures.is_empty() else 1)
