extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func cost(id: String, amount: int) -> Dictionary:
	return {"resource_id": id, "amount": amount}
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node.new()
	root.add_child(host)
	var storage = load("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs").new()
	storage.ParticipatesInSave = false
	host.add_child(storage)
	var a := Node.new()
	var b := Node.new()
	host.add_child(a)
	host.add_child(b)
	storage.Load("wood", 10)
	storage.Load("stone", 5)
	check(storage.TryReserveMaterials(a, [cost("WOOD", 3), cost(" wood ", 3), cost("stone", 2)]), "Initial material claim failed")
	check(storage.Stored("wood") == 10 and storage.Reserved("wood") == 6 and storage.Available("wood") == 4, "Reservation moved or duplicated stock")
	check(not storage.TryReserveMaterials(b, [cost("wood", 5)]), "Overlapping material promise succeeded")
	check(not storage.CanProvide([cost("wood", 5)]) and not storage.TryConsume([cost("wood", 5)]), "Consumer spent reserved stock")
	check(storage.Unload("wood", 10) == 4 and storage.Stored("wood") == 6, "Hauling spent reserved stock")
	check(not storage.TryReserveMaterials(a, [cost("wood", 7)]), "Unsatisfied replacement succeeded")
	check(not storage.TryReserveMaterials(a, [cost("wood", -1)]) and not storage.TryReserveMaterials(a, [{}]), "Malformed replacement succeeded")
	check(storage.Reserved("wood") == 6 and storage.Reserved("stone") == 2, "Failed replacement lost old claim")
	check(storage.TryReserveMaterials(a, [cost("wood", 4)]), "Reducing claim failed")
	check(storage.Reserved("stone") == 0 and storage.Available("wood") == 2, "Replacement retained stale resources")
	check(storage.TryReserveMaterials(b, [cost("wood", 2), cost("stone", 2)]), "Second owner could not reserve remaining stock")
	var repeated := [true]
	var on_changed := func(_id, _stored, _load): repeated[0] = storage.TryConsumeReserved(a)
	storage.StorageChanged.connect(on_changed)
	check(storage.TryConsumeReserved(a) and not repeated[0], "Reserved consumption was not exactly once before callbacks")
	storage.StorageChanged.disconnect(on_changed)
	check(storage.Stored("wood") == 2 and storage.Reserved("wood") == 2, "Consumption spent another owner's stock")
	b.free()
	check(storage.Reserved("wood") == 0 and storage.Reserved("stone") == 0, "Owner exit leaked claims")
	check(storage.TryReserveMaterials(a, [cost("wood", 2)]), "Could not reclaim released stock")
	var saved: Dictionary = storage.CaptureState()
	storage.RestoreState(saved)
	check(storage.Stored("wood") == 2 and storage.Reserved("wood") == 0 and not storage.TryConsumeReserved(a), "Restore retained transient claims or lost stock")
	check(storage.TryReserveMaterials(a, [cost("wood", 2)]), "Could not reserve after restore")
	host.remove_child(storage)
	check(storage.Reserved("wood") == 0, "Storage exit leaked claims")
	storage.free()
	host.free()
	print("[storage-material-reservations] OK" if failures.is_empty() else "[storage-material-reservations] FAILED")
	quit(0 if failures.is_empty() else 1)
