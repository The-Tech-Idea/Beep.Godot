extends SceneTree

# CooldownComponent has always exposed Progress, IsReady and a per-tick signal, and exactly one
# thing consumed it: AttackComponent, to decide whether an attack may fire. Nothing displayed a
# cooldown and no widget could.
#
# This probe holds the new path open end to end: a running cooldown reaches the slot it belongs to,
# the slot stores what REMAINS rather than what has elapsed, and finishing both clears the slot and
# announces itself.

const KIT_SLOT_GRID := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs")
const ABILITY_BAR := preload("res://addons/beep_game_builder_cs/ecs/ui/AbilityBarComponent.cs")
const COOLDOWN := preload("res://addons/beep_game_builder_cs/ecs/CooldownComponent.cs")

var _checks := 0
var _ready_slots: Array[int] = []

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var host := Control.new()
	host.name = "AbilityHost"
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "rpg")
	root.add_child(host)

	var grid: Control = KIT_SLOT_GRID.new()
	grid.name = "AbilityBar"
	host.add_child(grid)
	grid.set("Columns", 2)
	grid.set("Rows", 1)
	grid.size = Vector2(160, 80)

	var first = COOLDOWN.new()
	first.name = "Ability0"
	first.set("CooldownDuration", 2.0)
	host.add_child(first)

	var second = COOLDOWN.new()
	second.name = "Ability1"
	second.set("CooldownDuration", 4.0)
	host.add_child(second)

	var bar = ABILITY_BAR.new()
	bar.name = "Bar"
	bar.set("SlotGridPath", NodePath("../AbilityBar"))
	bar.set("AbilityPaths", [NodePath("../Ability0"), NodePath("../Ability1")])
	bar.set("Hotkeys", PackedStringArray(["Q", "W"]))
	host.add_child(bar)
	bar.connect("AbilityReady", Callable(self, "_on_ability_ready"))

	await process_frame

	# Hotkeys are pushed onto the grid when the bar is bound, not when a cooldown first ticks.
	var hotkeys: PackedStringArray = grid.get("SlotHotkeys")
	if hotkeys.size() < 2 or hotkeys[0] != "Q" or hotkeys[1] != "W":
		return _fail("hotkeys did not reach the slots: %s" % str(hotkeys))
	_checks += 1

	# Nothing has been triggered, so nothing is winding down.
	_expect_cooldown(grid, 0, 0.0, "an untriggered ability shows no sweep")

	# Half of a two second cooldown has run, so three quarters of it remains.
	first.Trigger()
	first.call("_process", 0.5)
	_expect_cooldown(grid, 0, 0.75, "a running cooldown reaches its slot as REMAINING time")

	# The second ability is untouched: one ability's cooldown must not paint another's slot.
	_expect_cooldown(grid, 1, 0.0, "an untriggered neighbour stays clear")

	first.call("_process", 1.0)
	_expect_cooldown(grid, 0, 0.25, "the sweep unwinds as the cooldown runs down")

	# Finishing clears the slot and announces the slot index, so a HUD can react without
	# subscribing to every cooldown itself.
	first.Reset()
	await process_frame
	_expect_cooldown(grid, 0, 0.0, "a finished cooldown leaves no sweep")
	if not _ready_slots.has(0):
		return _fail("AbilityReady did not report slot 0; got %s" % str(_ready_slots))
	_checks += 1
	if _ready_slots.has(1):
		return _fail("AbilityReady reported slot 1, which never ran")
	_checks += 1

	print("[kit-ability-bar] OK: %d checks passed; cooldowns reach the bar that draws them." % _checks)
	quit(0)

func _on_ability_ready(slot: int) -> void:
	_ready_slots.append(slot)

func _expect_cooldown(grid: Control, slot: int, expected: float, what: String) -> void:
	var cooldowns: PackedFloat32Array = grid.get("SlotCooldowns")
	if slot >= cooldowns.size():
		return _fail("%s: slot %d has no cooldown entry (%d present)" % [what, slot, cooldowns.size()])
	if absf(cooldowns[slot] - expected) > 0.01:
		return _fail("%s: expected %.2f, got %.2f" % [what, expected, cooldowns[slot]])
	_checks += 1

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-ability-bar] " + message)
	quit(1)
