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
	var p: Node = load(BASE + "GridProductionComponent.cs").new()
	p.ResourceWalletPath = NodePath("../Wallet")
	p.WorkClockPath = NodePath("../Clock")
	p.Recipes = [{"recipe_id": "planks", "duration_turns": 2.0,
		"inputs": [{"resource_id": "wood", "amount": 1}],
		"outputs": [{"resource_id": "plank", "amount": 1}]}]
	host.add_child(p)
	p.StartProduction("planks")
	check(not p.is_processing(), "Production still processes every frame")
	check(clock.ScheduledWorkCount == 1, "Production did not use a single shared-clock deadline")
	for i in 100: clock.AdvanceTurns(0.01)
	check(wallet.GetAmount("plank") == 0 and is_equal_approx(p.RemainingTurns, 1.0), "Progress requires per-tick production callbacks")
	var saved: Dictionary = p.CaptureState()
	p.PauseProduction()
	check(clock.ScheduledWorkCount == 0, "Pause retained a deadline")
	clock.AdvanceTurns(10.0)
	check(wallet.GetAmount("plank") == 0 and is_equal_approx(p.RemainingTurns, 1.0), "Pause consumed world time")
	p.RestoreState(saved)
	check(clock.ScheduledWorkCount == 1, "Restore did not rebuild deadline")
	clock.AdvanceTurns(1.01)
	check(wallet.GetAmount("plank") == 1 and is_equal_approx(p.RemainingTurns, 1.99), "Restored cycle lost residual elapsed time")
	host.remove_child(clock)
	check(clock.ScheduledWorkCount == 0, "Clock detach retained jobs")
	host.add_child(clock)
	clock.set_process(false)
	check(clock.ScheduledWorkCount == 1, "Clock reattachment stranded production")
	clock.AdvanceTurns(0.5)
	var remaining: float = p.RemainingTurns
	host.remove_child(p)
	check(clock.ScheduledWorkCount == 0, "Producer detach retained deadline")
	clock.AdvanceTurns(5.0)
	host.add_child(p)
	check(clock.ScheduledWorkCount == 1 and is_equal_approx(p.RemainingTurns, remaining), "Producer reattachment lost progress")
	clock.AdvanceTurns(remaining + 0.01)
	check(wallet.GetAmount("plank") == 2, "Reattached producer did not complete")
	p.CancelProduction(false)
	p.StartProduction("planks")
	clock.AdvanceTurns(1000.0)
	check(wallet.GetAmount("plank") == 258 and is_equal_approx(p.PendingWorkTurns, 488.0), "Scheduled catch-up discarded bounded backlog")
	clock.AdvanceTurns(0.5)
	check(wallet.GetAmount("plank") == 502 and is_equal_approx(p.RemainingTurns, 1.5), "Scheduled backlog produced incorrect results")
	p.CancelProduction(false)
	check(clock.ScheduledWorkCount == 0, "Cancelled producer retained deadline")
	var before: int = wallet.GetAmount("plank")
	for i in 1000:
		var factory: Node = load(BASE + "GridProductionComponent.cs").new()
		factory.ResourceWalletPath = NodePath("../Wallet")
		factory.WorkClockPath = NodePath("../Clock")
		factory.Recipes = p.Recipes
		host.add_child(factory)
		check(factory.StartProduction("planks") and not factory.is_processing(), "Factory did not enter scheduled simulation")
	check(clock.ScheduledWorkCount == 1000, "Factory population has missing or duplicate deadlines")
	clock.AdvanceTurns(1.0)
	check(wallet.GetAmount("plank") == before, "Future factories completed before deadline")
	clock.AdvanceTurns(1.0)
	var completed: int = wallet.GetAmount("plank") - before
	check(completed > 0 and completed <= 256, "Factory population ignored dispatch budget")
	for i in 3: clock.AdvanceTurns(0.125)
	check(wallet.GetAmount("plank") == before + 1000, "Delayed factory cycles were lost or duplicated")
	host.free()
	print("[production-scheduling] OK" if failures.is_empty() else "[production-scheduling] FAILED")
	quit(0 if failures.is_empty() else 1)
