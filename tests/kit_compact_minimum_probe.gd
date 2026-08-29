extends SceneTree

const MAX_COMPACT_WIDTH := 360.0

const KIT_ARROW_SELECTOR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitArrowSelector.cs")
const KIT_AVATAR_FRAME := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitAvatarFrame.cs")
const KIT_BOOK_SPREAD := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitBookSpread.cs")
const KIT_BUILD_TILE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitBuildTile.cs")
const KIT_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitButton.cs")
const KIT_CHECK_BOX := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCheckBox.cs")
const KIT_CHECK_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCheckButton.cs")
const KIT_CHIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitChip.cs")
const KIT_COLLAPSIBLE_PANEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCollapsiblePanel.cs")
const KIT_CONTEXT_MENU := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitContextMenu.cs")
const KIT_CURRENCY_BAR := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitCurrencyBar.cs")
const KIT_DIALOG_BOX := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitDialogBox.cs")
const KIT_GEM_SLOT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitGemSlot.cs")
const KIT_HEART_ROW := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitHeartRow.cs")
const KIT_HUD_TEXT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitHudText.cs")
const KIT_ICON_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitIconButton.cs")
const KIT_INPUT_HINT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitInputHint.cs")
const KIT_INVENTORY_SLOT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitInventorySlot.cs")
const KIT_ITEM_CARD := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitItemCard.cs")
const KIT_KNOB := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitKnob.cs")
const KIT_LABEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLabel.cs")
const KIT_LABEL_VALUE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLabelValue.cs")
const KIT_LEVEL_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLevelButton.cs")
const KIT_LEVEL_PATH := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitLevelPath.cs")
const KIT_METER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitMeter.cs")
const KIT_NODE_CARD := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitNodeCard.cs")
const KIT_OPTION_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitOptionButton.cs")
const KIT_ORB_METER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitOrbMeter.cs")
const KIT_ORNAMENT := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitOrnament.cs")
const KIT_PAGER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPager.cs")
const KIT_PANEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPanel.cs")
const KIT_PANEL_CONTAINER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPanelContainer.cs")
const KIT_PANEL_HANGER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPanelHanger.cs")
const KIT_PUSH_BUTTON := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs")
const KIT_RADAR_CHART := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRadarChart.cs")
const KIT_RADIAL_METER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRadialMeter.cs")
const KIT_REMOVABLE_CHIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRemovableChip.cs")
const KIT_ROW := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitRow.cs")
const KIT_SEGMENTED_ICON_GROUP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSegmentedIconGroup.cs")
const KIT_SLIDER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlider.cs")
const KIT_SLOT_GRID := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSlotGrid.cs")
const KIT_SPEECH_BUBBLE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSpeechBubble.cs")
const KIT_SPIN_WHEEL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSpinWheel.cs")
const KIT_SPINNER := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSpinner.cs")
const KIT_STAR_RATING := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitStarRating.cs")
const KIT_SWITCH_VISUAL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitSwitchVisual.cs")
const KIT_TAB_STRIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTabStrip.cs")
const KIT_TABLE_CELL := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTableCell.cs")
const KIT_TOAST := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitToast.cs")
const KIT_TOGGLE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitToggle.cs")
const KIT_TOOLTIP := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTooltip.cs")
const KIT_TREE := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitTree.cs")
const KIT_WEATHER_FORECAST_CARD := preload("res://addons/beep_game_builder_cs/ecs/ui/kit/KitWeatherForecastCard.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(390, 844)
	root.content_scale_size = root.size

	var host := VBoxContainer.new()
	host.name = "CompactHost"
	host.theme = _probe_theme()
	host.set_meta("_beep_kit_genre", "citybuilder")
	root.add_child(host)

	var controls: Array[Control] = []
	_add(controls, host, KIT_ARROW_SELECTOR, "KitArrowSelector")
	_add(controls, host, KIT_AVATAR_FRAME, "KitAvatarFrame")
	_add(controls, host, KIT_BOOK_SPREAD, "KitBookSpread")
	_add(controls, host, KIT_BUILD_TILE, "KitBuildTile")
	_add(controls, host, KIT_BUTTON, "KitButton")
	_add(controls, host, KIT_CHECK_BOX, "KitCheckBox")
	_add(controls, host, KIT_CHECK_BUTTON, "KitCheckButton")
	_add(controls, host, KIT_CHIP, "KitChip")
	_add(controls, host, KIT_COLLAPSIBLE_PANEL, "KitCollapsiblePanel")
	_add(controls, host, KIT_CONTEXT_MENU, "KitContextMenu")
	_add(controls, host, KIT_CURRENCY_BAR, "KitCurrencyBar")
	_add(controls, host, KIT_DIALOG_BOX, "KitDialogBox")
	_add(controls, host, KIT_GEM_SLOT, "KitGemSlot")
	_add(controls, host, KIT_HEART_ROW, "KitHeartRow")
	_add(controls, host, KIT_HUD_TEXT, "KitHudText")
	_add(controls, host, KIT_ICON_BUTTON, "KitIconButton")
	_add(controls, host, KIT_INPUT_HINT, "KitInputHint")
	_add(controls, host, KIT_INVENTORY_SLOT, "KitInventorySlot")
	var item_row := _add(controls, host, KIT_ITEM_CARD, "KitItemCardRow")
	item_row.set("Layout", 0)
	var item_tile := _add(controls, host, KIT_ITEM_CARD, "KitItemCardTile")
	item_tile.set("Layout", 1)
	_add(controls, host, KIT_KNOB, "KitKnob")
	_add(controls, host, KIT_LABEL, "KitLabel")
	_add(controls, host, KIT_LABEL_VALUE, "KitLabelValue")
	_add(controls, host, KIT_LEVEL_BUTTON, "KitLevelButton")
	_add(controls, host, KIT_LEVEL_PATH, "KitLevelPath")
	_add(controls, host, KIT_METER, "KitMeter")
	_add(controls, host, KIT_NODE_CARD, "KitNodeCard")
	_add(controls, host, KIT_OPTION_BUTTON, "KitOptionButton")
	_add(controls, host, KIT_ORB_METER, "KitOrbMeter")
	_add(controls, host, KIT_ORNAMENT, "KitOrnament")
	_add(controls, host, KIT_PAGER, "KitPager")
	_add(controls, host, KIT_PANEL, "KitPanel")
	_add(controls, host, KIT_PANEL_CONTAINER, "KitPanelContainer")
	_add(controls, host, KIT_PANEL_HANGER, "KitPanelHanger")
	_add(controls, host, KIT_PUSH_BUTTON, "KitPushButton")
	_add(controls, host, KIT_RADAR_CHART, "KitRadarChart")
	_add(controls, host, KIT_RADIAL_METER, "KitRadialMeter")
	_add(controls, host, KIT_REMOVABLE_CHIP, "KitRemovableChip")
	_add(controls, host, KIT_ROW, "KitRow")
	_add(controls, host, KIT_SEGMENTED_ICON_GROUP, "KitSegmentedIconGroup")
	_add(controls, host, KIT_SLIDER, "KitSlider")
	_add(controls, host, KIT_SLOT_GRID, "KitSlotGrid")
	_add(controls, host, KIT_SPEECH_BUBBLE, "KitSpeechBubble")
	_add(controls, host, KIT_SPIN_WHEEL, "KitSpinWheel")
	_add(controls, host, KIT_SPINNER, "KitSpinner")
	_add(controls, host, KIT_STAR_RATING, "KitStarRating")
	_add(controls, host, KIT_SWITCH_VISUAL, "KitSwitchVisual")
	_add(controls, host, KIT_TAB_STRIP, "KitTabStrip")
	_add(controls, host, KIT_TABLE_CELL, "KitTableCell")
	_add(controls, host, KIT_TOAST, "KitToast")
	_add(controls, host, KIT_TOGGLE, "KitToggle")
	_add(controls, host, KIT_TOOLTIP, "KitTooltip")
	_add(controls, host, KIT_TREE, "KitTree")
	_add(controls, host, KIT_WEATHER_FORECAST_CARD, "KitWeatherForecastCard")

	await process_frame
	await process_frame
	await process_frame

	var wide := PackedStringArray()
	var broken := PackedStringArray()
	for control in controls:
		var min_size := control.get_combined_minimum_size()
		if min_size.x > MAX_COMPACT_WIDTH:
			wide.append(str(control.name) + "=" + str(min_size))
		if min_size.x < 0.0 or min_size.y < 0.0 or is_nan(min_size.x) or is_nan(min_size.y):
			broken.append(str(control.name) + "=" + str(min_size))

	if not broken.is_empty():
		return _fail("Invalid minimum sizes: " + ", ".join(broken))
	if not wide.is_empty():
		return _fail("Default minimum width exceeds " + str(MAX_COMPACT_WIDTH) + " px: " + ", ".join(wide))

	print("[kit-compact-minimum] OK: default kit controls fit a phone-width compact column.")
	quit(0)

func _add(controls: Array[Control], host: Node, script: Script, name: String) -> Control:
	var control := script.new() as Control
	if control == null:
		_fail(name + " did not instantiate as Control.")
		return null
	control.name = name
	host.add_child(control)
	controls.append(control)
	return control

func _probe_theme() -> Theme:
	var theme := Theme.new()
	theme.set_color("neutral", "BeepSemantic", Color(0.09, 0.10, 0.11, 1.0))
	theme.set_color("accent", "BeepSemantic", Color(0.86, 0.54, 0.14, 1.0))
	theme.set_color("success", "BeepSemantic", Color(0.28, 0.62, 0.40, 1.0))
	theme.set_color("info", "BeepSemantic", Color(0.22, 0.53, 0.75, 1.0))
	theme.set_color("warning", "BeepSemantic", Color(0.88, 0.68, 0.23, 1.0))
	theme.set_color("danger", "BeepSemantic", Color(0.78, 0.25, 0.18, 1.0))
	theme.set_color("font_color", "Label", Color(0.94, 0.93, 0.88, 1.0))
	theme.set_font_size("font_size", "Label", 16)
	return theme

func _fail(message: String) -> void:
	push_error("[kit-compact-minimum] " + message)
	quit(1)
