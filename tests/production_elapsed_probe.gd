extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/grid/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func fixture() -> Node:
	var host := Node.new()
	root.add_child(host)
	var wallet: Node = load(BASE + "GridResourceWalletComponent.cs").new()
	wallet.name = "Wallet"
	host.add_child(wallet)
	wallet.SetAmount("wood", 10000)
	var production: Node = load(BASE + "GridProductionComponent.cs").new()
	production.name = "Production"
	production.ResourceWalletPath = NodePath("../Wallet")
	production.Recipes = [{"recipe_id": "planks", "duration_turns": 2.0,
		"inputs": [{"resource_id": "wood", "amount": 1}],
		"outputs": [{"resource_id": "plank", "amount": 1}]}]
	host.add_child(production)
	production.set_process(false)
	check(production.StartProduction("planks"), "Could not start fixture")
	return host

func run() -> void:
	var coarse := fixture()
	var fine := fixture()
	var p: Node = coarse.get_node("Production")
	var q: Node = fine.get_node("Production")
	p.AdvanceWork(12.5)
	for i in 50: q.AdvanceWork(0.25)
	check(p.CaptureState() == q.CaptureState(), "Coarse and fine production time disagree")
	check(coarse.get_node("Wallet").GetAmount("plank") == 6, "Coarse time dropped recipe completions")
	check(coarse.get_node("Wallet").GetAmount("wood") == fine.get_node("Wallet").GetAmount("wood"), "Coarse update changed input costs")
	q.PauseProduction()
	var paused: Dictionary = q.CaptureState()
	q.AdvanceWork(100.0)
	check(q.CaptureState() == paused, "Paused production accumulated elapsed work")
	q.ResumeProduction()
	q.Loop = false
	q.AdvanceWork(100.0)
	check(fine.get_node("Wallet").GetAmount("plank") == 7 and q.PendingWorkTurns == 0, "Non-looping production banked unrelated work")
	var recurse = func(_id): p.AdvanceWork(100.0)
	p.ProductionCompleted.connect(recurse)
	p.AdvanceWork(1.5)
	p.ProductionCompleted.disconnect(recurse)
	check(coarse.get_node("Wallet").GetAmount("plank") == 7, "Callback advanced production reentrantly")
	p.AdvanceWork(1000.0)
	check(coarse.get_node("Wallet").GetAmount("plank") == 263, "Catch-up did not respect completion limit")
	check(is_equal_approx(p.PendingWorkTurns, 488.0), "Bounded catch-up discarded elapsed work")
	var saved: Dictionary = p.CaptureState()
	p.RestoreState(saved)
	p.AdvanceWork(0.5)
	check(coarse.get_node("Wallet").GetAmount("plank") == 507 and is_equal_approx(p.RemainingTurns, 1.5), "Saved backlog did not finish exactly once")
	p.CancelProduction(false)
	check(p.PendingWorkTurns == 0, "Cancellation retained time for a later job")
	p.ConsumeInputsOnStart = false
	coarse.get_node("Wallet").SetAmount("wood", 0)
	p.StartProduction("planks")
	p.AdvanceWork(1000.0)
	check(p.PendingWorkTurns == 0 and p.RemainingTurns == 0, "Input starvation banked future production")
	coarse.free()
	fine.free()
	print("[production-elapsed] OK" if failures.is_empty() else "[production-elapsed] FAILED")
	quit(0 if failures.is_empty() else 1)
