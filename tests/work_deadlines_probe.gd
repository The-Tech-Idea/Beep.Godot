extends SceneTree

class Receiver extends Node:
	var calls: Array[float] = []
	var clock: Node
	var reenter := false
	var delay_usec := 0
	func due(_request_id: int, turns: float) -> void:
		calls.append(turns)
		if delay_usec > 0: OS.delay_usec(delay_usec)
		if reenter:
			reenter = false
			clock.ScheduleWork(self, 0.5, "due")
			clock.AdvanceTurns(1.0)

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func _initialize() -> void: run.call_deferred()

func run() -> void:
	var clock: Node = load("res://addons/beep_game_builder_cs/ecs/grid/GridWorkClockComponent.cs").new()
	check(clock.ScheduledWorkBudgetMilliseconds > 0, "Runtime dispatch time budget is disabled by default")
	clock.ScheduledWorkBudgetMilliseconds = 0.0
	root.add_child(clock)
	clock.set_process(false)
	var owner := Receiver.new()
	root.add_child(owner)
	check(clock.ScheduleWork(owner, 1.0, "missing") == 0, "Invalid callback accepted")
	check(clock.ScheduleWork(owner, 0.0, "due") == 0, "Zero deadline accepted")
	for i in 1000: check(clock.ScheduleWork(owner, 1.0, "due") > 0, "Scheduled work rejected")
	for i in 4: clock.AdvanceTurns(0.125)
	check(owner.calls.is_empty() and clock.ScheduledWorkCount == 1000, "Future work received callbacks")
	clock.AdvanceTurns(0.5)
	check(owner.calls.size() == 256 and clock.ScheduledWorkCount == 744, "Deadline dispatch exceeded budget or lost jobs")
	for i in 3: clock.AdvanceTurns(0.125)
	check(owner.calls.size() == 1000 and clock.ScheduledWorkCount == 0, "Due backlog did not drain")
	check(owner.calls[0] == 1.0 and owner.calls[-1] == 1.375, "Delayed callback lost actual elapsed time")
	var cancelled: int = clock.ScheduleWork(owner, 1.0, "due")
	check(clock.CancelScheduledWork(cancelled) and not clock.CancelScheduledWork(cancelled), "Cancellation was not idempotent")
	clock.AdvanceTurns(2.0)
	check(owner.calls.size() == 1000, "Cancelled job ran")
	for i in 1000:
		var id: int = clock.ScheduleWork(owner, 1000.0, "due")
		clock.CancelScheduledWork(id)
	clock.ScheduleWork(owner, 1.0, "due")
	root.remove_child(owner)
	check(clock.ScheduledWorkCount == 0, "Detached owner retained scheduled work")
	root.add_child(owner)
	clock.AdvanceTurns(2.0)
	check(owner.calls.size() == 1000, "Reattached owner resurrected cancelled work")
	clock.ScheduleWork(owner, 1.0, "due")
	root.remove_child(clock)
	root.add_child(clock)
	clock.set_process(false)
	check(clock.ScheduledWorkCount == 0, "Clock reattachment retained old callbacks")
	clock.ScheduleWork(owner, 1.0, "due")
	clock.AdvanceTurns(1.0)
	check(owner.calls.size() == 1001, "Reattached clock stopped scheduling")
	owner.clock = clock
	owner.reenter = true
	clock.ScheduleWork(owner, 1.0, "due")
	clock.AdvanceTurns(1.0)
	check(owner.calls.size() == 1002 and clock.ScheduledWorkCount == 1, "Reentrant time recursively dispatched new work")
	clock.AdvanceTurns(0.5)
	check(owner.calls.size() == 1003 and clock.ScheduledWorkCount == 0, "Reentrant queued work did not resume")
	owner.calls.clear()
	owner.delay_usec = 10000
	clock.ScheduledWorkBudgetMilliseconds = 0.5
	for i in 3: clock.ScheduleWork(owner, 1.0, "due")
	clock.AdvanceTurns(1.0)
	check(owner.calls.size() == 1 and clock.ScheduledWorkCount == 2, "Time budget admitted another expensive callback")
	check(clock.LastWorkDispatchCount == 1 and clock.LastWorkDispatchMilliseconds >= 10.0,
		"Dispatch diagnostics concealed a callback exceeding the cooperative budget")
	clock.AdvanceTurns(0.25)
	clock.AdvanceTurns(0.25)
	check(owner.calls == [1.0, 1.25, 1.5] and clock.ScheduledWorkCount == 0,
		"Time-budgeted backlog lost elapsed time or starved queued callbacks")
	owner.delay_usec = 0
	clock.AdvanceTurns(0.25)
	check(clock.LastWorkDispatchCount == 0, "Empty dispatch retained stale callback count")
	clock.ScheduleWork(owner, 1.0, "due")
	owner.free()
	check(clock.ScheduledWorkCount == 0, "Destroyed owner retained work")
	clock.free()
	print("[work-deadlines] OK" if failures.is_empty() else "[work-deadlines] FAILED")
	quit(0 if failures.is_empty() else 1)
