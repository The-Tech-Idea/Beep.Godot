@tool
class_name BeepThemeApplier
extends Node

## BeepThemeApplier — styles an entire Control subtree with a Beep preset.
## Port of the C# ThemePresetComponent, with the silent-no-op bug FIXED:
## it works whether placed as a CHILD of a Control (original design) or as a
## PARENT of Control(s), and push_warning()s if no Control target is found.
##
## Place it anywhere; it resolves the right target(s) automatically.

signal theme_applied

@export_enum("Modern", "SciFi", "Cartoon", "Classic", "Desert", "OilGas", "Sea", "Sports", "Soccer", "Fantasy", "Horror", "Nature", "Space", "Military", "Steampunk", "Retro80s", "Pixel8Bit", "Winter", "Cyberpunk", "Japan", "Toxic", "Candy") var preset: String = "Modern" : set = set_preset
@export var enable_animations: bool = true
@export var enable_ripple: bool = true
@export var active: bool = true : set = set_active

# Extracted geometry (from the preset's normal button) used to derive the rest.
var _g_corner: int = 6
var _g_border: int = 1
var _g_border_col: Color = Color.BLACK
var _g_shadow: int = 4
var _g_shadow_col: Color = Color.BLACK
var _g_pad_h: float = 16.0
var _g_pad_v: float = 8.0

var _last_theme: Theme = null
var _injected: Dictionary = {}          # Button -> true (animations already wired)

# Cached animation config (from the current preset) for the button callbacks.
var _a_hover: float = 1.04
var _a_hover_dur: float = 0.15
var _a_press: float = 0.96
var _a_press_dur: float = 0.08
var _a_lift: bool = true


func _ready() -> void:
	_apply_if_possible()


func _exit_tree() -> void:
	_injected.clear()


# ════════════════════════════════════════════════════════════════
# Property setters
# ════════════════════════════════════════════════════════════════

func set_preset(v: String) -> void:
	preset = v
	_reapply()


func set_active(v: bool) -> void:
	active = v
	_reapply()


func _reapply() -> void:
	if is_inside_tree():
		_apply_if_possible()


# ════════════════════════════════════════════════════════════════
# Target resolution — the FIX. Supports parent OR child placement.
# ════════════════════════════════════════════════════════════════

func _resolve_targets() -> Array[Control]:
	var targets: Array[Control] = []
	var parent: Node = get_parent()

	# 1) Child placement (original design): parent is the Control to theme.
	if parent is Control:
		targets.append(parent)
		return targets

	# 2) Parent placement: theme every Control child (and its subtree).
	for child in get_children():
		if child is Control:
			targets.append(child)
	if not targets.is_empty():
		return targets

	# 3) Fallback: nearest Control ancestor.
	var n: Node = parent
	while n != null:
		if n is Control:
			targets.append(n)
			return targets
		n = n.get_parent()

	# 4) Nothing found.
	return targets


# ════════════════════════════════════════════════════════════════
# Apply
# ════════════════════════════════════════════════════════════════

func _apply_if_possible() -> void:
	if not active:
		return
	var targets := _resolve_targets()
	if targets.is_empty():
		push_warning("[Beep UI] BeepThemeApplier at \"%s\" found no Control target. Place it as a child of a Control, or as a parent of Control(s)." % str(get_path()))
		return

	var p: BeepPreset = BeepPreset.get_preset(preset)
	if p == null:
		return

	# Prune freed buttons from the injected map so it can't grow unbounded in
	# scenes that dynamically add/remove buttons.
	for btn in _injected.keys():
		if not is_instance_valid(btn):
			_injected.erase(btn)

	_last_theme = _build_theme(p)
	for ctrl in targets:
		if is_instance_valid(ctrl) and ctrl is Control:
			ctrl.theme = _last_theme
			_apply_button_overrides(ctrl, p)
			if enable_animations and not Engine.is_editor_hint():
				_inject_button_animations(ctrl, p)

	theme_applied.emit()


# ════════════════════════════════════════════════════════════════
# Theme assembly (port of ThemePresetComponent.ApplyToSubtree)
# ════════════════════════════════════════════════════════════════

func _build_theme(p: BeepPreset) -> Theme:
	_extract_geometry(p.get_button_normal())
	var t := Theme.new()

	var button_types := ["Button", "CheckButton", "CheckBox", "OptionButton", "MenuButton", "ColorPickerButton"]
	for type in button_types:
		t.set_stylebox("normal", type, _dup(p.get_button_normal()))
		t.set_stylebox("hover", type, _dup(p.get_button_hover()))
		t.set_stylebox("pressed", type, _dup(p.get_button_pressed()))
		t.set_stylebox("disabled", type, _dup(p.get_button_disabled()))
		t.set_stylebox("focus", type, _dup(p.get_button_focus()))

	for type in ["Button", "CheckButton", "CheckBox", "OptionButton", "MenuButton",
			"ColorPickerButton", "Label", "RichTextLabel", "LineEdit", "TextEdit",
			"SpinBox", "Tree", "ItemList", "PopupMenu", "TabBar"]:
		t.set_color("font_color", type, p.text_primary)
		t.set_font_size("font_size", type, 14)

	var panel_sb: StyleBoxFlat = _build_panel(p)
	t.set_stylebox("panel", "Panel", panel_sb)
	t.set_stylebox("panel", "PanelContainer", panel_sb)
	t.set_stylebox("panel", "TabContainer", panel_sb)

	t.set_stylebox("tab_unselected", "TabBar", _build_surface(p, p.surface_primary))
	t.set_stylebox("tab_selected", "TabBar", _build_surface(p, p.surface_hover))
	t.set_stylebox("tab_disabled", "TabBar", _build_surface(p, p.surface_disabled))

	var input_sb: StyleBoxFlat = _build_input(p)
	t.set_stylebox("normal", "LineEdit", input_sb)
	t.set_stylebox("focus", "LineEdit", _build_input_focus(p))
	t.set_stylebox("read_only", "LineEdit", _build_input_read_only(p))
	t.set_stylebox("normal", "TextEdit", input_sb)
	t.set_stylebox("focus", "TextEdit", _build_input_focus(p))
	t.set_stylebox("normal", "SpinBox", input_sb)

	t.set_stylebox("background", "ProgressBar", _build_progress_bg(p))
	t.set_stylebox("fill", "ProgressBar", _build_progress_fill(p))
	t.set_color("font_color", "ProgressBar", p.text_on_dark)

	var grabber: StyleBoxFlat = _build_slider_grabber(p)
	t.set_stylebox("grabber_area", "HSlider", grabber)
	t.set_stylebox("grabber_area_highlight", "HSlider", _build_slider_grabber_hover(p))
	t.set_stylebox("slider", "HSlider", _build_slider_track(p))
	t.set_stylebox("grabber_area", "VSlider", grabber)
	t.set_stylebox("grabber_area_highlight", "VSlider", _build_slider_grabber_hover(p))
	t.set_stylebox("slider", "VSlider", _build_slider_track(p))

	t.set_stylebox("grabber", "HScrollBar", _build_scroll_grabber(p))
	t.set_stylebox("grabber_highlight", "HScrollBar", _build_scroll_grabber_hover(p))
	t.set_stylebox("grabber", "VScrollBar", _build_scroll_grabber(p))
	t.set_stylebox("grabber_highlight", "VScrollBar", _build_scroll_grabber_hover(p))
	t.set_stylebox("scroll", "HScrollBar", _build_scroll_track(p))
	t.set_stylebox("scroll", "VScrollBar", _build_scroll_track(p))

	t.set_stylebox("panel", "Tree", panel_sb)
	t.set_stylebox("selected", "Tree", _build_selected(p))
	t.set_stylebox("selected_focus", "Tree", _build_selected_focus(p))
	t.set_stylebox("cursor", "Tree", _build_selected(p))
	t.set_stylebox("selected", "ItemList", _build_selected(p))
	t.set_stylebox("selected_focus", "ItemList", _build_selected_focus(p))
	t.set_stylebox("panel", "ItemList", panel_sb)

	t.set_stylebox("panel", "PopupMenu", panel_sb)
	t.set_stylebox("hover", "PopupMenu", _build_selected(p))
	t.set_color("font_hover_color", "PopupMenu", p.text_hover)

	t.set_constant("separation", "HSeparator", 4)
	t.set_constant("separation", "VSeparator", 4)
	var sep_sb: StyleBoxFlat = _build_separator(p)
	t.set_stylebox("separator", "HSeparator", sep_sb)
	t.set_stylebox("separator", "VSeparator", sep_sb)

	return t


func _apply_button_overrides(node: Node, p: BeepPreset) -> void:
	if node is Button:
		var btn: Button = node
		btn.add_theme_stylebox_override("normal", p.get_button_normal().duplicate())
		btn.add_theme_stylebox_override("hover", p.get_button_hover().duplicate())
		btn.add_theme_stylebox_override("pressed", p.get_button_pressed().duplicate())
		btn.add_theme_stylebox_override("disabled", p.get_button_disabled().duplicate())
		btn.add_theme_stylebox_override("focus", p.get_button_focus().duplicate())
		btn.add_theme_color_override("font_color", p.text_primary)
	for child in node.get_children():
		if child is Node:
			_apply_button_overrides(child, p)


# ════════════════════════════════════════════════════════════════
# Geometry + derived StyleBox builders (port of ThemePresetComponent helpers)
# ════════════════════════════════════════════════════════════════

func _extract_geometry(sb: StyleBoxFlat) -> void:
	_g_corner = int(sb.corner_radius_top_left)
	_g_border = int(sb.border_width_left)
	_g_border_col = sb.border_color
	_g_shadow = sb.shadow_size
	_g_shadow_col = sb.shadow_color
	_g_pad_h = sb.content_margin_left
	_g_pad_v = sb.content_margin_top


func _new_box() -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.set_corner_radius_all(_g_corner)
	sb.border_width_left = _g_border
	sb.border_width_right = _g_border
	sb.border_width_top = _g_border
	sb.border_width_bottom = _g_border
	sb.border_color = _g_border_col
	sb.shadow_size = _g_shadow
	sb.shadow_color = _g_shadow_col
	sb.content_margin_left = _g_pad_h
	sb.content_margin_right = _g_pad_h
	sb.content_margin_top = _g_pad_v
	sb.content_margin_bottom = _g_pad_v
	return sb


func _build_surface(p: BeepPreset, surface: Color) -> StyleBoxFlat:
	var sb := _new_box()
	sb.bg_color = surface
	sb.border_color = p.border_normal
	return sb


func _build_panel(p: BeepPreset) -> StyleBoxFlat:
	var sb := _new_box()
	sb.bg_color = p.bg_panel
	sb.border_color = p.border_normal
	sb.shadow_color = p.shadow_color
	sb.shadow_size = max(0, _g_shadow - 2)
	return sb


func _build_input(p: BeepPreset) -> StyleBoxFlat:
	var sb := _new_box()
	sb.bg_color = p.surface_pressed
	sb.border_color = p.border_normal
	sb.shadow_size = 0
	sb.content_margin_left = max(4.0, _g_pad_h - 4.0)
	sb.content_margin_right = max(4.0, _g_pad_h - 4.0)
	sb.content_margin_top = max(2.0, _g_pad_v - 3.0)
	sb.content_margin_bottom = max(2.0, _g_pad_v - 3.0)
	return sb


func _build_input_focus(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_input(p)
	sb.border_width_left = max(2, _g_border)
	sb.border_width_right = max(2, _g_border)
	sb.border_width_top = max(2, _g_border)
	sb.border_width_bottom = max(2, _g_border)
	sb.border_color = p.border_focus
	return sb


func _build_input_read_only(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_input(p)
	sb.bg_color = Color(p.surface_disabled.r, p.surface_disabled.g, p.surface_disabled.b, 0.6)
	sb.border_color = Color(p.border_normal.r, p.border_normal.g, p.border_normal.b, 0.4)
	return sb


func _build_progress_bg(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = p.surface_disabled
	sb.corner_radius_top_left = max(2, _g_corner - 4)
	sb.corner_radius_top_right = max(2, _g_corner - 4)
	sb.corner_radius_bottom_right = max(2, _g_corner - 4)
	sb.corner_radius_bottom_left = max(2, _g_corner - 4)
	sb.content_margin_left = 2; sb.content_margin_right = 2
	sb.content_margin_top = 2; sb.content_margin_bottom = 2
	return sb


func _build_progress_fill(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_progress_bg(p)
	sb.bg_color = p.accent_primary
	return sb


func _build_slider_grabber(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = p.accent_primary
	var r: int = _g_corner
	sb.set_corner_radius_all(r)
	sb.shadow_size = 3
	sb.shadow_color = p.shadow_color
	return sb


func _build_slider_grabber_hover(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_slider_grabber(p)
	sb.bg_color = p.accent_secondary
	sb.shadow_size = 5
	return sb


func _build_slider_track(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = p.surface_disabled
	sb.corner_radius_top_left = max(2, _g_corner / 2)
	sb.corner_radius_top_right = max(2, _g_corner / 2)
	sb.corner_radius_bottom_right = max(2, _g_corner / 2)
	sb.corner_radius_bottom_left = max(2, _g_corner / 2)
	return sb


func _build_scroll_grabber(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(p.text_disabled.r, p.text_disabled.g, p.text_disabled.b, 0.5)
	var r: int = max(3, _g_corner / 3)
	sb.set_corner_radius_all(r)
	return sb


func _build_scroll_grabber_hover(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_scroll_grabber(p)
	sb.bg_color = Color(p.text_disabled.r, p.text_disabled.g, p.text_disabled.b, 0.8)
	return sb


func _build_scroll_track(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(p.bg_canvas.r, p.bg_canvas.g, p.bg_canvas.b, 0.7)
	return sb


func _build_selected(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(p.accent_primary.r, p.accent_primary.g, p.accent_primary.b, 0.25)
	var r: int = max(2, _g_corner / 2)
	sb.set_corner_radius_all(r)
	sb.content_margin_left = 4; sb.content_margin_right = 4
	return sb


func _build_selected_focus(p: BeepPreset) -> StyleBoxFlat:
	var sb := _build_selected(p)
	sb.bg_color = Color(p.accent_primary.r, p.accent_primary.g, p.accent_primary.b, 0.40)
	sb.border_width_left = 1; sb.border_width_right = 1
	sb.border_width_top = 1; sb.border_width_bottom = 1
	sb.border_color = p.border_focus
	return sb


func _build_separator(p: BeepPreset) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = p.border_normal
	return sb


# ════════════════════════════════════════════════════════════════
# Button hover/press animations (runtime only — port of SetupButtonAnimations)
# ════════════════════════════════════════════════════════════════

func _inject_button_animations(root: Node, p: BeepPreset) -> void:
	_a_hover = p.anim_hover_scale
	_a_hover_dur = p.anim_hover_scale_dur
	_a_press = p.anim_press_scale
	_a_press_dur = p.anim_press_scale_dur
	_a_lift = p.anim_shadow_lift
	for btn in _find_buttons(root):
		if _injected.has(btn):
			continue
		_injected[btn] = true
		# Godot 4.7: the offset_transform_* properties are a self-contained visual
		# transform layer that containers (VBox/HBox) will NOT overwrite. Animating
		# these instead of `position`/`scale` prevents the layout drift that the old
		# position:y mutation caused on container-hosted buttons.
		btn.offset_transform_enabled = true
		btn.mouse_entered.connect(_on_btn_entered.bind(btn))
		btn.mouse_exited.connect(_on_btn_exited.bind(btn))
		btn.button_down.connect(_on_btn_down.bind(btn))
		btn.button_up.connect(_on_btn_up.bind(btn))


func _find_buttons(node: Node) -> Array[Button]:
	var list: Array[Button] = []
	_collect_buttons(node, list)
	return list


func _collect_buttons(node: Node, list: Array[Button]) -> void:
	if node is Button:
		list.append(node)
	for child in node.get_children():
		if child is Node:
			_collect_buttons(child, list)


func _on_btn_entered(btn: Button) -> void:
	if not active or not is_instance_valid(btn) or not btn.is_visible_in_tree():
		return
	var t := btn.create_tween().set_parallel(true)
	t.tween_property(btn, "offset_transform_scale", Vector2(_a_hover, _a_hover), _a_hover_dur).set_ease(Tween.EASE_OUT)
	# Lift is animated to an ABSOLUTE offset (-2px), so repeated hover cycles never
	# accumulate drift — exit always returns it to 0.
	if _a_lift:
		t.tween_property(btn, "offset_transform_position:y", -2.0, _a_hover_dur).set_ease(Tween.EASE_OUT)


func _on_btn_exited(btn: Button) -> void:
	if not active or not is_instance_valid(btn):
		return
	var t := btn.create_tween().set_parallel(true)
	t.tween_property(btn, "offset_transform_scale", Vector2.ONE, _a_hover_dur).set_ease(Tween.EASE_OUT)
	if _a_lift:
		t.tween_property(btn, "offset_transform_position:y", 0.0, _a_hover_dur).set_ease(Tween.EASE_OUT)


func _on_btn_down(btn: Button) -> void:
	if not active or not is_instance_valid(btn):
		return
	var t := btn.create_tween()
	t.tween_property(btn, "offset_transform_scale", Vector2(_a_press, _a_press), _a_press_dur).set_ease(Tween.EASE_IN)


func _on_btn_up(btn: Button) -> void:
	if not active or not is_instance_valid(btn):
		return
	var t := btn.create_tween()
	t.tween_property(btn, "offset_transform_scale", Vector2.ONE, _a_press_dur * 1.5).set_ease(Tween.EASE_OUT)


func _dup(sb: StyleBoxFlat) -> StyleBoxFlat:
	return sb.duplicate()
