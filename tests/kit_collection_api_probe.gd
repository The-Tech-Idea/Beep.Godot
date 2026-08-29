extends SceneTree

const KIT_ARROW_SELECTOR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs")
const KIT_CURRENCY_BAR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs")
const KIT_SEGMENTED_ICON_GROUP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs")
const KIT_SLOT_GRID := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs")
const KIT_SPIN_WHEEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs")
const KIT_TAB_STRIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs")
const KIT_RADAR_CHART := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs")
const KIT_TREE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var host := Control.new()
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	root.add_child(host)

	var selector = KIT_ARROW_SELECTOR.new()
	host.add_child(selector)
	selector.set("OptionLabels", PackedStringArray(["Low", "Medium", "High"]))
	selector.set("Current", 2)
	if selector.get("Current") != 2:
		return _fail("KitArrowSelector did not keep requested current option.")
	if not selector.RemoveOption(2):
		return _fail("KitArrowSelector.RemoveOption rejected a valid index.")
	if selector.get("Current") != 1:
		return _fail("KitArrowSelector did not clamp Current after removing the selected option.")
	if selector.RemoveOption(10):
		return _fail("KitArrowSelector.RemoveOption accepted an invalid index.")
	selector.ClearOptions()
	if (selector.get("OptionLabels") as PackedStringArray).size() != 0 or selector.get("Current") != 0:
		return _fail("KitArrowSelector.ClearOptions did not empty labels and reset Current.")

	var currency = KIT_CURRENCY_BAR.new()
	host.add_child(currency)
	currency.set("EntryValues", PackedStringArray(["120", "34", "9"]))
	currency.set("EntryGlyphs", PackedStringArray(["$", "*", "+"]))
	if not currency.RemoveEntry(1):
		return _fail("KitCurrencyBar.RemoveEntry rejected a valid index.")
	if (currency.get("EntryValues") as PackedStringArray) != PackedStringArray(["120", "9"]):
		return _fail("KitCurrencyBar.RemoveEntry did not remove the expected entry.")
	if currency.RemoveEntry(7):
		return _fail("KitCurrencyBar.RemoveEntry accepted an invalid index.")
	currency.ClearEntries()
	if (currency.get("EntryValues") as PackedStringArray).size() != 0:
		return _fail("KitCurrencyBar.ClearEntries did not clear values.")

	var segments = KIT_SEGMENTED_ICON_GROUP.new()
	host.add_child(segments)
	segments.set("SegmentGlyphs", PackedStringArray(["A", "B", "C"]))
	segments.set("Current", 2)
	if not segments.RemoveSegment(2):
		return _fail("KitSegmentedIconGroup.RemoveSegment rejected a valid index.")
	if segments.get("Current") != 1:
		return _fail("KitSegmentedIconGroup did not clamp Current after removing the selected segment.")
	if segments.RemoveSegment(-1):
		return _fail("KitSegmentedIconGroup.RemoveSegment accepted an invalid index.")
	segments.ClearSegments()
	if (segments.get("SegmentGlyphs") as PackedStringArray).size() != 0 or segments.get("Current") != 0:
		return _fail("KitSegmentedIconGroup.ClearSegments did not reset the group.")

	var tabs = KIT_TAB_STRIP.new()
	host.add_child(tabs)
	tabs.set("TabLabels", PackedStringArray(["Build", "Map", "Crew"]))
	tabs.set("current_tab", 2)
	if not tabs.RemoveKitTab(1):
		return _fail("KitTabStrip.RemoveKitTab rejected a valid index.")
	if (tabs.get("TabLabels") as PackedStringArray) != PackedStringArray(["Build", "Crew"]):
		return _fail("KitTabStrip.RemoveKitTab did not remove the expected tab.")
	if tabs.RemoveKitTab(9):
		return _fail("KitTabStrip.RemoveKitTab accepted an invalid index.")
	tabs.ClearKitTabs()
	if (tabs.get("TabLabels") as PackedStringArray).size() != 0:
		return _fail("KitTabStrip.ClearKitTabs did not clear labels.")

	var slots = KIT_SLOT_GRID.new()
	host.add_child(slots)
	slots.set("SlotKinds", PackedInt32Array([0, 1, 2]))
	slots.set("SlotCounts", PackedInt32Array([5, 6, 7]))
	if not slots.RemoveSlot(1):
		return _fail("KitSlotGrid.RemoveSlot rejected a valid index.")
	if (slots.get("SlotCounts") as PackedInt32Array) != PackedInt32Array([5, 7]):
		return _fail("KitSlotGrid.RemoveSlot did not remove the expected slot data.")
	if slots.RemoveSlot(5):
		return _fail("KitSlotGrid.RemoveSlot accepted an invalid index.")
	slots.ClearSlots()
	if (slots.get("SlotKinds") as PackedInt32Array).size() != 0:
		return _fail("KitSlotGrid.ClearSlots did not clear slot data.")

	var wheel = KIT_SPIN_WHEEL.new()
	host.add_child(wheel)
	wheel.set("WedgeLabels", PackedStringArray(["50", "100", "x2"]))
	if not wheel.RemoveWedge(1):
		return _fail("KitSpinWheel.RemoveWedge rejected a valid index.")
	if (wheel.get("WedgeLabels") as PackedStringArray) != PackedStringArray(["50", "x2"]):
		return _fail("KitSpinWheel.RemoveWedge did not remove the expected label.")
	if wheel.RemoveWedge(-1):
		return _fail("KitSpinWheel.RemoveWedge accepted an invalid index.")
	wheel.ClearWedges()
	if (wheel.get("WedgeLabels") as PackedStringArray).size() != 0:
		return _fail("KitSpinWheel.ClearWedges did not clear labels.")

	var radar = KIT_RADAR_CHART.new()
	host.add_child(radar)
	radar.set("AxisLabels", PackedStringArray(["Speed", "Grip", "Brakes"]))
	radar.set("AxisValues", PackedFloat32Array([0.7, 1.4, -0.5]))
	var values := radar.get("AxisValues") as PackedFloat32Array
	if values.size() != 3 or not is_equal_approx(values[1], 1.0) or not is_equal_approx(values[2], 0.0):
		return _fail("KitRadarChart.SetData did not clamp values into 0..1.")
	if not radar.RemoveAxis(1):
		return _fail("KitRadarChart.RemoveAxis rejected a valid index.")
	if (radar.get("AxisLabels") as PackedStringArray) != PackedStringArray(["Speed", "Brakes"]):
		return _fail("KitRadarChart.RemoveAxis did not remove the expected axis label.")
	if radar.RemoveAxis(8):
		return _fail("KitRadarChart.RemoveAxis accepted an invalid index.")
	radar.ClearAxes()
	if (radar.get("AxisLabels") as PackedStringArray).size() != 0 or (radar.get("AxisValues") as PackedFloat32Array).size() != 0:
		return _fail("KitRadarChart.ClearAxes did not clear labels and values.")

	var tree = KIT_TREE.new()
	host.add_child(tree)
	tree.set("NodeColumns", PackedInt32Array([0, 1, 2]))
	tree.set("NodeTiers", PackedInt32Array([0, 1, 2]))
	tree.set("NodeParentIndices", PackedStringArray(["", "0", "1"]))
	tree.set("Selected", 2)
	if not tree.RemoveNode(0, true):
		return _fail("KitTree.RemoveNode rejected a valid index.")
	if (tree.get("NodeColumns") as PackedInt32Array) != PackedInt32Array([1, 2]):
		return _fail("KitTree.RemoveNode did not remove the expected node.")
	if (tree.get("NodeParentIndices") as PackedStringArray) != PackedStringArray(["", "0"]):
		return _fail("KitTree.RemoveNode did not remap parent references.")
	if tree.get("Selected") != 1:
		return _fail("KitTree.RemoveNode did not keep selection near the removed node.")
	if tree.RemoveNode(8, true):
		return _fail("KitTree.RemoveNode accepted an invalid index.")
	tree.ClearNodes()
	if (tree.get("NodeColumns") as PackedInt32Array).size() != 0 or tree.get("Selected") != -1:
		return _fail("KitTree.ClearNodes did not clear nodes and reset selection.")

	print("[kit-collection-api] OK: collection-backed kit widgets mutate through refresh-safe APIs.")
	quit(0)

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-collection-api] " + message)
	quit(1)
