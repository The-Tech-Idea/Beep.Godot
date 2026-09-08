extends SceneTree

const HAULER = preload("res://addons/beep_game_builder_cs/ecs/grid/GridHaulerComponent.cs")
const WALLET = preload("res://addons/beep_game_builder_cs/ecs/grid/GridResourceWalletComponent.cs")
const FLOW = preload("res://addons/beep_game_builder_cs/ecs/GameFlowComponent.cs")
const TRACKER = preload("res://addons/beep_game_builder_cs/ecs/grid/GridObjectiveTrackerComponent.cs")
const BINDER = preload("res://addons/beep_game_builder_cs/ecs/grid/GridObjectiveEventBinderComponent.cs")
var failures: Array[String] = []
var wins := 0
var losses := 0

func _initialize() -> void:
	call_deferred("run")

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)

func run() -> void:
	var world := Node.new()
	root.add_child(world)
	var wallet = WALLET.new()
	wallet.name = "Wallet"
	world.add_child(wallet)
	var hauler = HAULER.new()
	hauler.name = "Hauler"
	hauler.set("RegisterOnReady", false)
	hauler.set("ResourceWalletPath", NodePath("../Wallet"))
	world.add_child(hauler)
	hauler.set_process(false)
	hauler.call("RestoreState", {"cargo_id": "wood", "cargo_amount": 7})
	check(int(hauler.get("CarryingAmount")) == 7, "Restore delivered cargo before wallet restore")
	check(int(wallet.call("GetAmount", "wood")) == 0, "Restore mutated wallet")
	wallet.call("SetAmount", "wood", 11)
	hauler.call("Tick", 3.0)
	check(int(wallet.call("GetAmount", "wood")) + int(hauler.get("CarryingAmount")) == 18, "Restore lost resources")
	wallet.call("SetAmount", "wood", 11)
	hauler.call("RestoreState", {"cargo_id": "wood", "cargo_amount": 7})
	hauler.call("Tick", 3.0)
	check(int(wallet.call("GetAmount", "wood")) + int(hauler.get("CarryingAmount")) == 18, "Reverse restore order lost resources")
	var orphan = HAULER.new()
	orphan.set("RegisterOnReady", false)
	orphan.call("Load", "wood", 5)
	orphan.call("TryDeliverCargo")
	check(int(orphan.get("CarryingAmount")) == 5, "Missing destination discarded cargo")
	orphan.free()
	var flow = FLOW.new()
	flow.set("AutoNavigateOnEnd", false)
	flow.set("EnablePauseMenu", false)
	world.add_child(flow)
	flow.connect("LevelComplete", func(): wins += 1)
	flow.connect("GameOver", func(): losses += 1)
	flow.call("TriggerLevelComplete")
	flow.call("TriggerLevelComplete")
	flow.call("TriggerGameOver")
	check(wins == 1 and losses == 0, "Terminal events were repeated or contradictory")
	flow.call("Reset", 3)
	flow.call("TriggerGameOver")
	flow.call("TriggerGameOver")
	flow.call("TriggerLevelComplete")
	check(wins == 1 and losses == 1, "Reset did not rearm terminal gate")
	var tracker = TRACKER.new()
	tracker.name = "Objectives"
	tracker.set("Objectives", [{"objective_id":"gather_wood", "target_count":3}])
	world.add_child(tracker)
	var binder = BINDER.new()
	binder.set("ObjectiveTrackerPath", NodePath("../Objectives"))
	world.add_child(binder)
	binder.call("ApplyObjectiveEvent", "gather_wood", 0, "test")
	binder.call("ApplyObjectiveEvent", "gather_wood", -1, "test")
	check(int(tracker.call("GetProgress", "gather_wood")) == 0, "Nonpositive event advanced objective")
	tracker.call("SetProgress", "gather_wood", 2)
	check(not tracker.call("AddProgress", "gather_wood", -1), "Negative cumulative progress was accepted")
	check(int(tracker.call("GetProgress", "gather_wood")) == 2, "Negative increment reduced progress")
	tracker.call("AddProgress", "gather_wood", 2147483647)
	check(tracker.call("IsComplete", "gather_wood"), "Large progress increment overflowed")
	tracker.call("SetProgress", "gather_wood", 0)
	check(int(tracker.call("GetProgress", "gather_wood")) == 3, "Completed objective regressed without reset")
	tracker.call("ResetObjective", "gather_wood")
	check(not tracker.call("IsComplete", "gather_wood") and int(tracker.call("GetProgress", "gather_wood")) == 0, "Objective reset did not clear completion")
	tracker.call("RestoreState", {"objectives": [{"objective_id":"gather_wood", "progress":-5, "completed":true}]})
	check(int(tracker.call("GetProgress", "gather_wood")) == 3, "Restore left completed progress below target")
	world.free()
	for failure in failures:
		print("[rts-lifecycle] FAIL: " + failure)
	if failures.is_empty():
		print("[rts-lifecycle] OK")
	quit(0 if failures.is_empty() else 1)
