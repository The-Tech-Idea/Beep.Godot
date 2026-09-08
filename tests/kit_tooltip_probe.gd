extends SceneTree

# The kit shipped a fully drawn KitTooltip that nothing ever showed, and set TooltipText on no
# widget at all, so Godot never asked for a tooltip. KitSegmentedIconGroup went further: it
# exported a SegmentTips array, stored a tip per segment, and read it back nowhere.
#
# This probe holds that closed. It checks the per-item text a widget reports for a position, and
# that the panel Godot is handed back is the kit's own, carrying the genre and theme across the
# popup boundary where neither is inherited.

const KIT_SEGMENTED_ICON_GROUP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs")
const KIT_SLOT_GRID := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs")
const KIT_TAB_STRIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs")

var _checks := 0

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var host := Control.new()
	host.name = "TooltipHost"
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	root.add_child(host)

	await _check_segment_tips(host)
	await _check_slot_requirement(host)
	await _check_tab_label(host)
	await _check_tooltip_panel(host)

	print("[kit-tooltip] OK: %d checks passed; per-item tips reach Godot's tooltip system." % _checks)
	quit(0)

func _check_segment_tips(host: Control) -> void:
	var group: Control = KIT_SEGMENTED_ICON_GROUP.new()
	group.name = "Segments"
	host.add_child(group)
	group.set("SegmentGlyphs", PackedStringArray(["A", "B", "C"]))
	group.set("SegmentTips", PackedStringArray(["Build roads", "Zone housing", "Demolish"]))
	group.size = Vector2(300, 40)
	await process_frame

	# Three even segments across 300px: 0-100, 100-200, 200-300.
	_expect(group.get_tooltip(Vector2(50, 20)), "Build roads", "segment 0 tip")
	_expect(group.get_tooltip(Vector2(150, 20)), "Zone housing", "segment 1 tip")
	_expect(group.get_tooltip(Vector2(250, 20)), "Demolish", "segment 2 tip")
	group.queue_free()

func _check_slot_requirement(host: Control) -> void:
	var grid: Control = KIT_SLOT_GRID.new()
	grid.name = "Slots"
	host.add_child(grid)
	grid.set("Columns", 2)
	grid.set("Rows", 1)
	# SlotKind: 0 Filled, 1 Blank, 2 Invite, 3 Locked.
	grid.set("SlotKinds", PackedInt32Array([3, 0]))
	grid.set("SlotRequirements", PackedStringArray(["Needs a workshop", ""]))
	grid.set("SlotCounts", PackedInt32Array([0, 7]))
	grid.size = Vector2(200, 100)
	await process_frame

	_expect(grid.get_tooltip(Vector2(50, 50)), "Needs a workshop", "locked slot states its requirement")
	_expect(grid.get_tooltip(Vector2(150, 50)), "7", "filled slot reports its stack count")
	grid.queue_free()

func _check_tab_label(host: Control) -> void:
	var tabs: Control = KIT_TAB_STRIP.new()
	tabs.name = "Tabs"
	host.add_child(tabs)
	tabs.size = Vector2(240, 32)
	tabs.set("TabLabels", PackedStringArray(["Overview", "Production"]))
	await process_frame
	await process_frame

	if tabs.get_tab_count() != 2:
		return _fail("tab strip laid out %d native tabs, expected 2" % tabs.get_tab_count())
	_checks += 1

	# TabBar resolves tooltips in C++, so the widget publishes the label as a native per-tab
	# tooltip rather than overriding _get_tooltip, which the engine would never consult.
	_expect(tabs.get_tab_tooltip(0), "Overview", "tab 0 publishes its full label")
	_expect(tabs.get_tab_tooltip(1), "Production", "tab 1 publishes its full label")
	tabs.queue_free()

func _check_tooltip_panel(host: Control) -> void:
	var group: Control = KIT_SEGMENTED_ICON_GROUP.new()
	group.name = "PanelSource"
	host.add_child(group)
	await process_frame

	var panel: Variant = group.call("_make_custom_tooltip","Zone housing")
	if panel == null or not (panel is Control):
		return _fail("_MakeCustomTooltip returned no Control for non-empty text")
	_checks += 1

	if not str(panel.get_script().resource_path).ends_with("KitTooltip.cs"):
		return _fail("tooltip panel is %s, not the kit's own KitTooltip" % panel.get_script().resource_path)
	_checks += 1

	_expect(panel.get("Text"), "Zone housing", "tooltip panel carries the text")

	# Godot parents the panel into a popup outside this tree, so neither the genre meta nor the
	# theme would be inherited. Both must have been copied onto it.
	_expect(panel.get_meta("_beep_kit_genre"), "citybuilder", "tooltip panel carries the genre")
	if panel.theme == null:
		return _fail("tooltip panel carries no theme, so it would draw with default colours")
	_checks += 1

	var empty: Variant = group.call("_make_custom_tooltip","   ")
	if empty != null:
		return _fail("blank text must yield no panel, not an empty one")
	_checks += 1

	panel.queue_free()
	group.queue_free()

func _expect(actual: Variant, expected: Variant, what: String) -> void:
	if actual != expected:
		return _fail("%s: expected '%s', got '%s'" % [what, expected, actual])
	_checks += 1

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("info", "BeepSemantic", Color(0.22, 0.53, 0.75, 1.0))
	theme.set_color("focus", "BeepSemantic", Color(0.54, 0.85, 0.66, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-tooltip] " + message)
	quit(1)
