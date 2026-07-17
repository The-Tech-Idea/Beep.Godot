@tool
extends VBoxContainer

## Beep UI — Theme Studio dock.
## Tab 1 "Themes": visual gallery of all 22 presets + swatches + one-click apply.
## Tab 2 "Widgets": every widget in BeepWidgetFactory.catalog(), searchable; click
## to drop a real themed widget under the current selection. (Deliberately no count
## here — it said 84 while the catalog held 114.)

const THEME_APPLIER_SCRIPT := preload("res://addons/beep_ui/theme/theme_applier.gd")
# BeepWidgetFactory is referenced via its global class_name declared in
# widgets/widget_factory.gd — do NOT add a local `const WIDGET_FACTORY :=
# preload(...)` here, that would shadow the global identifier and break
# calls like `BeepWidgetFactory.catalog()` (which would then try to call
# `.catalog()` on a raw `GDScript` script reference and fail at runtime).

var _selected_preset: String = "Modern"

var _theme_search: LineEdit
var _theme_grid: GridContainer
var _preview: PanelContainer
var _status: Label

var _widget_search: LineEdit
var _widget_grid: GridContainer

# Preview sample controls
var _pv_button: Button
var _pv_primary: Button
var _pv_danger: Button
var _pv_success: Button
var _pv_input: LineEdit
var _pv_panel_label: Label


func _ready() -> void:
	_build_ui()
	_refresh_theme_grid("")
	_refresh_widget_grid("")


func _build_ui() -> void:
	for c in get_children():
		c.queue_free()

	var tabs := TabContainer.new()
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	tabs.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(tabs)

	_build_themes_tab(tabs)
	_build_widgets_tab(tabs)

	_status = Label.new()
	_status.add_theme_font_size_override("font_size", 11)
	add_child(_status)


# ════════════════════════════════════════════════════════════════
# THEMES TAB
# ════════════════════════════════════════════════════════════════

func _build_themes_tab(tabs: TabContainer) -> void:
	var root := VBoxContainer.new()
	root.name = "Themes"
	tabs.add_child(root)

	_theme_search = LineEdit.new()
	_theme_search.placeholder_text = "Search 22 presets…"
	_theme_search.text_changed.connect(_refresh_theme_grid)
	root.add_child(_theme_search)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.custom_minimum_size = Vector2(0, 200)
	root.add_child(scroll)

	_theme_grid = GridContainer.new()
	_theme_grid.columns = 2
	_theme_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_theme_grid.add_theme_constant_override("h_separation", 6)
	_theme_grid.add_theme_constant_override("v_separation", 6)
	scroll.add_child(_theme_grid)

	_preview = PanelContainer.new()
	_preview.custom_minimum_size = Vector2(0, 150)
	root.add_child(_preview)
	var pv_inner := MarginContainer.new()
	pv_inner.add_theme_constant_override("margin_left", 10)
	pv_inner.add_theme_constant_override("margin_right", 10)
	pv_inner.add_theme_constant_override("margin_top", 8)
	pv_inner.add_theme_constant_override("margin_bottom", 8)
	_preview.add_child(pv_inner)
	var pv_vbox := VBoxContainer.new()
	pv_vbox.add_theme_constant_override("separation", 6)
	pv_inner.add_child(pv_vbox)

	_pv_panel_label = Label.new()
	_pv_panel_label.text = "Preview"
	pv_vbox.add_child(_pv_panel_label)

	_pv_button = Button.new()
	_pv_button.text = "Button"
	pv_vbox.add_child(_pv_button)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	pv_vbox.add_child(row)
	_pv_primary = Button.new()
	_pv_primary.text = "Primary"
	row.add_child(_pv_primary)
	_pv_danger = Button.new()
	_pv_danger.text = "Danger"
	row.add_child(_pv_danger)
	_pv_success = Button.new()
	_pv_success.text = "Success"
	row.add_child(_pv_success)

	_pv_input = LineEdit.new()
	_pv_input.text = "Type here…"
	_pv_input.editable = false
	pv_vbox.add_child(_pv_input)

	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 6)
	root.add_child(actions)

	var btn_sel := Button.new()
	btn_sel.text = "Apply to Selected"
	btn_sel.tooltip_text = "Add a BeepThemeApplier under the selected node."
	btn_sel.pressed.connect(_apply_theme_to_selected)
	actions.add_child(btn_sel)

	var btn_root := Button.new()
	btn_root.text = "Apply to Root"
	btn_root.tooltip_text = "Add a BeepThemeApplier under the scene root."
	btn_root.pressed.connect(_apply_theme_to_root)
	actions.add_child(btn_root)

	var btn_remove := Button.new()
	btn_remove.text = "Remove Theme"
	btn_remove.tooltip_text = "Remove any BeepThemeApplier under the selection / root."
	btn_remove.pressed.connect(_remove_theme)
	actions.add_child(btn_remove)


func _refresh_theme_grid(filter: String) -> void:
	if _theme_grid == null:
		return
	# remove_child first: queue_free() is deferred to end-of-frame, so the old cards were
	# still children when the new ones were added below. This refresh runs on every
	# keystroke, so the grid re-flowed holding both sets.
	for c in _theme_grid.get_children():
		_theme_grid.remove_child(c)
		c.queue_free()
	var f := filter.to_lower()
	for pname in BeepPreset.preset_names():
		if not f.is_empty() and pname.to_lower().find(f) == -1:
			continue
		_theme_grid.add_child(_make_theme_card(pname))


func _make_theme_card(preset_name: String) -> Control:
	var p: BeepPreset = BeepPreset.get_preset(preset_name)
	# get_preset() returns null on an unknown name or a failed load, and this runs in a loop
	# from _refresh_theme_grid — so without this guard one bad preset_*.gd took the whole
	# dock down on the swatch read below. _update_preview() already guards the same call.
	if p == null:
		return Label.new()
	var card := VBoxContainer.new()
	card.add_theme_constant_override("separation", 2)

	var btn := Button.new()
	btn.text = preset_name
	btn.toggle_mode = true
	btn.button_pressed = (preset_name == _selected_preset)
	btn.pressed.connect(_select_preset.bind(preset_name))
	card.add_child(btn)

	var sw := HBoxContainer.new()
	sw.add_theme_constant_override("separation", 2)
	for col in [p.surface_primary, p.accent_primary, p.semantic_success, p.semantic_warning, p.semantic_danger]:
		var cr := ColorRect.new()
		cr.color = col
		cr.custom_minimum_size = Vector2(26, 14)
		sw.add_child(cr)
	card.add_child(sw)
	return card


func _select_preset(preset_name: String) -> void:
	_selected_preset = preset_name
	for card in _theme_grid.get_children():
		if card is VBoxContainer and card.get_child_count() > 0:
			var b: Button = card.get_child(0)
			if b is Button:
				b.button_pressed = (b.text == preset_name)
	_update_preview()


func _update_preview() -> void:
	var p: BeepPreset = BeepPreset.get_preset(_selected_preset)
	if p == null:
		return
	_preview.add_theme_stylebox_override("panel", p.get_panel_background())
	_pv_panel_label.add_theme_color_override("font_color", p.text_primary)
	_pv_button.add_theme_stylebox_override("normal", p.get_button_normal())
	_pv_button.add_theme_stylebox_override("hover", p.get_button_hover())
	_pv_button.add_theme_stylebox_override("pressed", p.get_button_pressed())
	_pv_button.add_theme_stylebox_override("focus", p.get_button_focus())
	_pv_button.add_theme_color_override("font_color", p.text_primary)
	_pv_primary.add_theme_stylebox_override("normal", p.get_primary_button_normal())
	_pv_primary.add_theme_stylebox_override("hover", p.get_primary_button_normal())
	_pv_primary.add_theme_color_override("font_color", p.text_on_dark)
	_pv_danger.add_theme_stylebox_override("normal", p.get_danger_button_normal())
	_pv_danger.add_theme_stylebox_override("hover", p.get_danger_button_normal())
	_pv_danger.add_theme_color_override("font_color", p.text_on_dark)
	_pv_success.add_theme_stylebox_override("normal", p.get_success_button_normal())
	_pv_success.add_theme_stylebox_override("hover", p.get_success_button_normal())
	_pv_success.add_theme_color_override("font_color", p.text_on_dark)
	_pv_input.add_theme_stylebox_override("normal", p.get_line_edit_normal())
	_pv_input.add_theme_color_override("font_color", p.text_primary)


func _apply_theme_to_selected() -> void:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		_set_status("No open scene.")
		return
	var sel := EditorInterface.get_selection().get_selected_nodes()
	var parent: Node = sel.back() if not sel.is_empty() else root
	_apply_theme(parent)


func _apply_theme_to_root() -> void:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		_set_status("No open scene.")
		return
	_apply_theme(root)


func _apply_theme(parent: Node) -> void:
	for c in parent.get_children():
		if c is BeepThemeApplier:
			c.preset = _selected_preset
			c.active = true
			_set_status("Updated existing applier → %s" % _selected_preset)
			EditorInterface.edit_node(c)
			return
	var applier: BeepThemeApplier = THEME_APPLIER_SCRIPT.new()
	applier.name = "BeepThemeApplier"
	applier.preset = _selected_preset
	parent.add_child(applier)
	applier.owner = EditorInterface.get_edited_scene_root()
	EditorInterface.edit_node(applier)
	_set_status("Applied %s under '%s'." % [_selected_preset, parent.name])


func _remove_theme() -> void:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		_set_status("No open scene.")
		return
	var sel := EditorInterface.get_selection().get_selected_nodes()
	var scope: Node = sel.back() if not sel.is_empty() else root
	var removed := 0
	for c in scope.get_children():
		if c is BeepThemeApplier:
			c.queue_free()
			removed += 1
	if removed > 0:
		_set_status("Removed %d applier(s) from '%s'." % [removed, scope.name])
	else:
		_set_status("No applier found under '%s'." % scope.name)


# ════════════════════════════════════════════════════════════════
# WIDGETS TAB
# ════════════════════════════════════════════════════════════════

func _build_widgets_tab(tabs: TabContainer) -> void:
	var root := VBoxContainer.new()
	root.name = "Widgets"
	tabs.add_child(root)

	var hint := Label.new()
	hint.text = "Click a widget to drop it (themed) under the selection."
	hint.add_theme_font_size_override("font_size", 11)
	root.add_child(hint)

	_widget_search = LineEdit.new()
	_widget_search.placeholder_text = "Search widgets…"
	_widget_search.text_changed.connect(_refresh_widget_grid)
	root.add_child(_widget_search)

	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(scroll)

	_widget_grid = GridContainer.new()
	_widget_grid.columns = 2
	_widget_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_widget_grid.add_theme_constant_override("h_separation", 4)
	_widget_grid.add_theme_constant_override("v_separation", 4)
	scroll.add_child(_widget_grid)


func _refresh_widget_grid(filter: String) -> void:
	if _widget_grid == null:
		return
	# Same deferred-free hazard as _refresh_theme_grid: remove immediately, then free.
	for c in _widget_grid.get_children():
		_widget_grid.remove_child(c)
		c.queue_free()
	var f := filter.to_lower()
	var last_cat := ""
	for e in BeepWidgetFactory.catalog():
		var cat: String = e["category"]
		var nm: String = e["name"]
		if not f.is_empty() and nm.to_lower().find(f) == -1 and cat.to_lower().find(f) == -1:
			continue
		if cat != last_cat and f.is_empty():
			var hdr := Label.new()
			hdr.text = "— %s —" % cat
			hdr.add_theme_font_size_override("font_size", 11)
			_widget_grid.add_child(hdr)
			last_cat = cat
		var btn := Button.new()
		btn.text = nm
		btn.tooltip_text = "Archetype: %s" % e["archetype"]
		btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		btn.pressed.connect(_add_widget.bind(e["id"]))
		_widget_grid.add_child(btn)


func _add_widget(widget_id: String) -> void:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		_set_status("No open scene.")
		return
	var sel := EditorInterface.get_selection().get_selected_nodes()
	var parent: Node = sel.back() if not sel.is_empty() else root
	var w: Control = BeepWidgetFactory.build_by_id(widget_id)
	if w == null:
		_set_status("Unknown widget: %s" % widget_id)
		return
	parent.add_child(w)
	_set_owner_recursive(w, root)
	EditorInterface.edit_node(w)
	_set_status("Added '%s' under '%s'." % [w.name, parent.name])


func _set_owner_recursive(node: Node, owner: Node) -> void:
	node.owner = owner
	for c in node.get_children():
		_set_owner_recursive(c, owner)


func _set_status(msg: String) -> void:
	if _status:
		_status.text = msg
	print("[Beep UI] ", msg)
