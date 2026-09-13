extends SceneTree

# FIX-12: GridPorts.Transfer is the one safe hand-off - "cargo is never duplicated and never lost".
# A giver-only (IUnloadPort-shaped) source handing to a capacity-limited sink used to be over-drawn:
# the whole amount left the giver, the sink took only what it had room for, and the remainder was
# dropped because an unload-only giver has no Load to take it back. The transport chain is the real
# consumer, so this drives the fix through it rather than calling the internal helper directly.

const CHAIN := preload("res://addons/beep_game_builder_cs/ecs/grid/GridTransportChainComponent.cs")

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)


class SourcePort extends Node:
	# An IUnloadPort-shaped source: Unload/Stored/StoredIds, deliberately NO Load.
	var held := 0

	func Unload(_resource_id: String, amount: int) -> int:
		var given: int = int(min(amount, held))
		held -= given
		return given

	func Stored(_resource_id: String) -> int:
		return held

	func StoredIds() -> Array:
		return ["iron"] if held > 0 else []


class SinkPort extends Node:
	var Capacity := 4
	var CurrentLoad := 0

	func CanAccept(_resource_id: String) -> bool:
		return CurrentLoad < Capacity

	func Load(_resource_id: String, amount: int) -> int:
		var taken: int = int(min(amount, int(max(0, Capacity - CurrentLoad))))
		CurrentLoad += taken
		return taken

	func Stored(_resource_id: String) -> int:
		return CurrentLoad

	func StoredIds() -> Array:
		return ["iron"] if CurrentLoad > 0 else []


class FlexibleSource extends Node:
	# A giver that CAN re-accept, so a declined remainder has somewhere to go.
	var held := 0
	var Capacity := 20

	func Unload(_resource_id: String, amount: int) -> int:
		var given: int = int(min(amount, held))
		held -= given
		return given

	func Load(_resource_id: String, amount: int) -> int:
		var taken: int = int(min(amount, int(max(0, Capacity - held))))
		held += taken
		return taken

	func Stored(_resource_id: String) -> int:
		return held

	func StoredIds() -> Array:
		return ["iron"] if held > 0 else []


class StingySink extends Node:
	# Reports free space it will not actually use: a per-id cap inside Load.
	var Capacity := 10
	var CurrentLoad := 0

	func CanAccept(_resource_id: String) -> bool:
		return true

	func Load(_resource_id: String, amount: int) -> int:
		var taken: int = int(min(amount, 2))
		CurrentLoad += taken
		return taken

	func Stored(_resource_id: String) -> int:
		return CurrentLoad

	func StoredIds() -> Array:
		return ["iron"] if CurrentLoad > 0 else []


func _initialize() -> void: run.call_deferred()

func run() -> void:
	# --- (1) unload-only giver into a capacity-limited sink: nothing may be lost ---
	var host := Node2D.new()
	host.name = "Host"
	root.add_child(host)
	var source := SourcePort.new()
	source.name = "Source"
	source.held = 10
	host.add_child(source)
	var sink := SinkPort.new()
	sink.name = "Sink"
	host.add_child(sink)
	var chain: Node = CHAIN.new()
	chain.name = "Chain"
	chain.set("Chain", [NodePath("../Source"), NodePath("../Sink")])
	chain.set("ResourceIds", ["iron"])
	chain.set("FlowRatePerTurn", 10.0)
	host.add_child(chain)
	chain.set_process(false)
	chain.call("AdvanceWork", 1.0)
	check(source.held + sink.CurrentLoad == 10,
		"unload-only giver lost cargo: giver %d + sink %d != 10" % [source.held, sink.CurrentLoad])
	check(sink.CurrentLoad == 4, "sink should hold its whole free space (4), holds %d" % sink.CurrentLoad)
	check(source.held == 6, "giver should have drawn only the sink's room (6 left), holds %d" % source.held)
	host.free()

	# --- (2) a sink that under-takes its own reported room: the remainder returns to a giver that can re-accept ---
	var host2 := Node2D.new()
	host2.name = "Host2"
	root.add_child(host2)
	var source2 := FlexibleSource.new()
	source2.name = "Source"
	source2.held = 10
	host2.add_child(source2)
	var sink2 := StingySink.new()
	sink2.name = "Sink"
	host2.add_child(sink2)
	var chain2: Node = CHAIN.new()
	chain2.name = "Chain"
	chain2.set("Chain", [NodePath("../Source"), NodePath("../Sink")])
	chain2.set("ResourceIds", ["iron"])
	chain2.set("FlowRatePerTurn", 10.0)
	host2.add_child(chain2)
	chain2.set_process(false)
	chain2.call("AdvanceWork", 1.0)
	check(source2.held + sink2.CurrentLoad == 10,
		"declined remainder was not returned: giver %d + sink %d != 10" % [source2.held, sink2.CurrentLoad])
	check(sink2.CurrentLoad == 2, "stingy sink should hold 2, holds %d" % sink2.CurrentLoad)
	check(source2.held == 8, "giver should hold the returned 8, holds %d" % source2.held)
	host2.free()

	print("[grid-ports-transfer] OK" if failures.is_empty() else "[grid-ports-transfer] FAILED")
	quit(0 if failures.is_empty() else 1)
