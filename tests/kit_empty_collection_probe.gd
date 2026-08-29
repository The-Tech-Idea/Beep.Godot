extends SceneTree

const KIT_ARROW_SELECTOR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs")
const KIT_CURRENCY_BAR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs")
const KIT_LEVEL_PATH := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs")
const KIT_RADAR_CHART := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs")
const KIT_SEGMENTED_ICON_GROUP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs")
const KIT_SLOT_GRID := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs")
const KIT_SPIN_WHEEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs")
const KIT_TAB_STRIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs")
const KIT_TREE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var host := Control.new()
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	root.add_child(host)

	var selector = KIT_ARROW_SELECTOR.new()
	var currency = KIT_CURRENCY_BAR.new()
	var level_path = KIT_LEVEL_PATH.new()
	var radar = KIT_RADAR_CHART.new()
	var segments = KIT_SEGMENTED_ICON_GROUP.new()
	var slots = KIT_SLOT_GRID.new()
	var wheel = KIT_SPIN_WHEEL.new()
	var tabs = KIT_TAB_STRIP.new()
	var tree = KIT_TREE.new()
	for control in [selector, currency, level_path, radar, segments, slots, wheel, tabs, tree]:
		host.add_child(control)

	await process_frame
	await process_frame

	_expect_empty_strings(selector, "OptionLabels", "KitArrowSelector")
	_expect_empty_strings(currency, "EntryValues", "KitCurrencyBar")
	_expect_empty_strings(level_path, "LevelLabels", "KitLevelPath")
	_expect_empty_strings(radar, "AxisLabels", "KitRadarChart")
	_expect_empty_strings(segments, "SegmentGlyphs", "KitSegmentedIconGroup")
	_expect_empty_ints(slots, "SlotKinds", "KitSlotGrid")
	_expect_empty_strings(wheel, "WedgeLabels", "KitSpinWheel")
	_expect_empty_strings(tabs, "TabLabels", "KitTabStrip")
	if tabs.get_tab_count() != 0:
		return _fail("KitTabStrip seeded native tabs during startup.")
	_expect_empty_ints(tree, "NodeColumns", "KitTree")

	for entry in [
		["KitArrowSelector", selector],
		["KitCurrencyBar", currency],
		["KitLevelPath", level_path],
		["KitRadarChart", radar],
		["KitSegmentedIconGroup", segments],
		["KitSlotGrid", slots],
		["KitSpinWheel", wheel],
		["KitTabStrip", tabs],
		["KitTree", tree],
	]:
		var minimum: Vector2 = entry[1].get_combined_minimum_size()
		if minimum.x <= 4.0 or minimum.y <= 4.0:
			return _fail(str(entry[0]) + " lost its usable natural minimum when empty: " + str(minimum))

	print("[kit-empty-collection] OK: empty collection widgets keep authored data empty and retain usable natural sizes.")
	quit(0)

func _expect_empty_strings(control: Object, property: String, label: String) -> void:
	var value := control.get(property) as PackedStringArray
	if value.size() != 0:
		_fail(label + " seeded " + property + " during startup: " + str(value))

func _expect_empty_ints(control: Object, property: String, label: String) -> void:
	var value := control.get(property) as PackedInt32Array
	if value.size() != 0:
		_fail(label + " seeded " + property + " during startup: " + str(value))

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-empty-collection] " + message)
	quit(1)
