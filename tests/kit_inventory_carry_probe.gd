extends SceneTree

# InventoryComponent has had MoveItem since it shipped, and exactly one route to it: a mouse drag.
# OnSlotGuiInput branched on InputEventMouseButton alone, so rearranging an inventory required a
# pointing device — no keyboard path, and nothing a controller could reach.
#
# This probe holds the lift-and-place path open: ui_accept lifts a slot, ui_accept on another
# places it through the same MoveItem, ui_cancel puts it back, and an empty slot cannot be lifted.

const INVENTORY := preload("res://addons/beep_game_builder_cs/ecs/InventoryComponent.cs")

var _checks := 0
var _carry_events: Array[int] = []

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var host := Control.new()
	host.name = "InventoryHost"
	root.add_child(host)

	var inv = INVENTORY.new()
	inv.name = "Inventory"
	inv.set("MaxSlots", 6)
	host.add_child(inv)
	inv.connect("CarryChanged", Callable(self, "_on_carry_changed"))
	await process_frame

	var sword := GameItem.new()
	sword.set("Id", "sword")
	var shield := GameItem.new()
	shield.set("Id", "shield")

	inv.AddItem(sword, 1)
	inv.AddItem(shield, 1)
	await process_frame

	if inv.IsSlotEmpty(0) or inv.IsSlotEmpty(1):
		return _fail("the two seeded items did not land in slots 0 and 1")
	_checks += 1

	# Nothing is held to begin with.
	if inv.CarrySlot != -1:
		return _fail("a fresh inventory reports something already in hand")
	_checks += 1

	# An empty slot holds nothing, so there is nothing to lift from it.
	inv.CarryOrPlace(4)
	if inv.CarrySlot != -1:
		return _fail("lifting an empty slot picked up a hole")
	_checks += 1

	# Lift, then place onto an empty slot: the item moves through MoveItem.
	inv.CarryOrPlace(0)
	if inv.CarrySlot != 0:
		return _fail("ui_accept on an occupied slot did not lift it")
	_checks += 1

	inv.CarryOrPlace(3)
	if inv.CarrySlot != -1:
		return _fail("placing did not clear what was in hand")
	_checks += 1
	if not inv.IsSlotEmpty(0):
		return _fail("the source slot still holds the item after a place")
	_checks += 1
	if inv.IsSlotEmpty(3):
		return _fail("the destination slot did not receive the item")
	_checks += 1

	# Cancelling puts it back where it came from and moves nothing.
	inv.CarryOrPlace(1)
	if inv.CarrySlot != 1:
		return _fail("second lift did not take hold")
	_checks += 1
	inv.CancelCarry()
	if inv.CarrySlot != -1:
		return _fail("cancel left something in hand")
	_checks += 1
	if inv.IsSlotEmpty(1):
		return _fail("cancel moved the item instead of putting it back")
	_checks += 1

	# Every lift and place announced itself, so a view can mark what is held.
	if _carry_events != [0, -1, 1, -1]:
		return _fail("CarryChanged sequence was %s, expected [0, -1, 1, -1]" % str(_carry_events))
	_checks += 1

	print("[kit-inventory-carry] OK: %d checks passed; inventory reorders without a mouse." % _checks)
	quit(0)

func _on_carry_changed(slot: int) -> void:
	_carry_events.append(slot)

func _fail(message: String) -> void:
	push_error("[kit-inventory-carry] " + message)
	quit(1)
