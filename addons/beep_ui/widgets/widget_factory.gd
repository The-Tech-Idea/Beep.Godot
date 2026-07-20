@tool
class_name BeepWidgetFactory
extends RefCounted

## BeepWidgetFactory — builds real, themed Beep UI widgets on demand.
## Each built widget is a Control subtree + a BeepThemeApplier child, so it is
## styled automatically by the 22-preset engine. Drop into any scene from the dock.
##
## Archetypes: bar, stat, caption, panel, button_list, grid, toast_host,
## crosshair, overlay, scaffold, system.

const THEME_APPLIER_SCRIPT := preload("res://addons/beep_ui/theme/theme_applier.gd")
const TOAST_SCRIPT := preload("res://addons/beep_ui/widgets/toast_host.gd")

# ── Catalog: every entry is a Dictionary with quoted keys. ──
# Bare-key shorthand (e.g. `{id:"x"}`) is unreliable in Godot 4.7's `const`
# array parser, so we use the explicit quoted-key form throughout. The
# quoted form also matches what `JSON.parse()` produces, which keeps
# round-trips via the dock's "Export to JSON" action lossless.
const CATALOG: Array = [
	# HUD — bars
	{"id":"health_bar", "name":"Health Bar", "category":"HUD", "archetype":"bar", "label":"HP", "value":100},
	{"id":"boss_health_bar", "name":"Boss Health Bar", "category":"HUD", "archetype":"bar", "label":"BOSS", "value":100},
	{"id":"ammo_display", "name":"Ammo Display", "category":"HUD", "archetype":"bar", "label":"AMMO", "value":30},
	{"id":"cooldown_indicator", "name":"Cooldown Indicator", "category":"HUD", "archetype":"bar", "label":"CD", "value":0},
	{"id":"segmented_progress", "name":"Segmented Progress", "category":"HUD", "archetype":"bar", "label":"LAP", "value":0},
	{"id":"match_timer", "name":"Match Timer Bar", "category":"HUD", "archetype":"bar", "label":"TIME", "value":50},
	# HUD — stats
	{"id":"score_display", "name":"Score Display", "category":"HUD", "archetype":"stat", "label":"SCORE", "suffix":""},
	{"id":"timer", "name":"Timer", "category":"HUD", "archetype":"stat", "label":"TIME", "suffix":"s"},
	{"id":"speedometer", "name":"Speedometer", "category":"HUD", "archetype":"stat", "label":"SPD", "suffix":"km/h"},
	{"id":"altitude_meter", "name":"Altitude Meter", "category":"HUD", "archetype":"stat", "label":"ALT", "suffix":"m"},
	{"id":"accuracy_display", "name":"Accuracy Display", "category":"HUD", "archetype":"stat", "label":"ACC", "suffix":"%"},
	{"id":"combo_counter", "name":"Combo Counter", "category":"HUD", "archetype":"stat", "label":"COMBO", "suffix":"x"},
	{"id":"wave_counter", "name":"Wave Counter", "category":"HUD", "archetype":"stat", "label":"WAVE", "suffix":""},
	{"id":"counter", "name":"Counter", "category":"HUD", "archetype":"stat", "label":"COUNT", "suffix":""},
	# HUD — captions
	{"id":"interaction_prompt", "name":"Interaction Prompt", "category":"HUD", "archetype":"caption", "label":"Press E to interact"},
	{"id":"subtitles", "name":"Subtitles", "category":"HUD", "archetype":"caption", "label":"Subtitle line goes here"},
	{"id":"zone_warning", "name":"Zone Warning", "category":"HUD", "archetype":"caption", "label":"DANGER ZONE"},
	{"id":"spectator_label", "name":"Spectator Label", "category":"HUD", "archetype":"caption", "label":"SPECTATING"},
	{"id":"floating_damage", "name":"Floating Damage", "category":"HUD", "archetype":"caption", "label":"-42"},
	{"id":"hit_indicator", "name":"Hit Indicator", "category":"HUD", "archetype":"caption", "label":"HIT"},
	{"id":"respawn_overlay", "name":"Respawn Overlay", "category":"HUD", "archetype":"caption", "label":"RESPAWNING…"},
	{"id":"pickup_log", "name":"Pickup Log", "category":"HUD", "archetype":"caption", "label":"+1 Item"},
	{"id":"kill_feed", "name":"Kill Feed", "category":"HUD", "archetype":"caption", "label":"PlayerA ▸ PlayerB"},
	# HUD — panels
	{"id":"quest_log", "name":"Quest Log", "category":"HUD", "archetype":"panel", "label":"Quests"},
	{"id":"leaderboard", "name":"Leaderboard", "category":"HUD", "archetype":"panel", "label":"Leaderboard"},
	{"id":"teammate_panel", "name":"Teammate Panel", "category":"HUD", "archetype":"panel", "label":"Team"},
	{"id":"debug_overlay", "name":"Debug Overlay", "category":"HUD", "archetype":"panel", "label":"Debug"},
	{"id":"console_log", "name":"Console Log", "category":"HUD", "archetype":"panel", "label":"Console"},
	{"id":"chat_box", "name":"Chat Box", "category":"HUD", "archetype":"panel", "label":"Chat"},
	{"id":"notifications", "name":"Notifications", "category":"HUD", "archetype":"toast_host", "label":"Notifications"},
	{"id":"loot_popup", "name":"Loot Popup", "category":"HUD", "archetype":"toast_host", "label":"Loot"},
	{"id":"objective_markers", "name":"Objective Markers", "category":"HUD", "archetype":"panel", "label":"Objectives"},
	{"id":"status_effect_icons", "name":"Status Effect Icons", "category":"HUD", "archetype":"grid", "label":"Buffs"},
	{"id":"input_hints", "name":"Input Hints", "category":"HUD", "archetype":"button_list", "label":"Controls"},
	{"id":"damage_preview", "name":"Damage Preview", "category":"HUD", "archetype":"stat", "label":"DMG", "suffix":""},
	# HUD — interactive
	{"id":"weapon_wheel", "name":"Weapon Wheel", "category":"HUD", "archetype":"button_list", "label":"Weapons"},
	{"id":"skill_tree", "name":"Skill Tree", "category":"HUD", "archetype":"button_list", "label":"Skills"},
	{"id":"crafting_menu", "name":"Crafting Menu", "category":"HUD", "archetype":"panel", "label":"Crafting"},
	{"id":"equipment_shop", "name":"Equipment Shop", "category":"HUD", "archetype":"panel", "label":"Shop"},
	{"id":"quest_map", "name":"Quest Map", "category":"HUD", "archetype":"panel", "label":"Map"},
	{"id":"tutorial_end_screen", "name":"Tutorial / End Screen", "category":"HUD", "archetype":"panel", "label":"Results"},
	{"id":"dialog_minigame", "name":"Dialog / Minigame", "category":"HUD", "archetype":"panel", "label":"Dialog"},
	{"id":"reticle_ping", "name":"Reticle + Ping", "category":"HUD", "archetype":"crosshair", "label":"+"},
	# HUD — complex scaffolds
	{"id":"crosshair", "name":"Crosshair", "category":"HUD", "archetype":"crosshair", "label":"+"},
	{"id":"minimap", "name":"Minimap", "category":"HUD", "archetype":"scaffold", "label":"Minimap"},
	{"id":"compass", "name":"Compass", "category":"HUD", "archetype":"scaffold", "label":"Compass"},
	{"id":"edge_indicator", "name":"Edge Indicator", "category":"HUD", "archetype":"scaffold", "label":"Off-screen"},
	{"id":"vignette", "name":"Vignette", "category":"HUD", "archetype":"overlay", "label":"Vignette"},
	{"id":"virtual_joystick", "name":"Virtual Joystick", "category":"HUD", "archetype":"scaffold", "label":"Move"},
	{"id":"boss_wave_ui", "name":"Boss Wave UI", "category":"HUD", "archetype":"panel", "label":"BOSS INCOMING"},

	# Canvas & FX
	{"id":"canvas_anchor", "name":"Canvas Anchor", "category":"Canvas", "archetype":"scaffold", "label":"Anchor"},
	{"id":"safe_area", "name":"Safe Area", "category":"Canvas", "archetype":"scaffold", "label":"Safe Area"},
	{"id":"tooltip", "name":"Tooltip", "category":"Canvas", "archetype":"caption", "label":"Tooltip"},
	{"id":"drag_drop", "name":"Drag Drop Target", "category":"Canvas", "archetype":"panel", "label":"Drag here"},
	{"id":"tab_panel", "name":"Tab Panel", "category":"Canvas", "archetype":"panel", "label":"Tabs"},
	{"id":"context_menu", "name":"Context Menu", "category":"Canvas", "archetype":"button_list", "label":"Menu"},
	{"id":"accordion", "name":"Accordion", "category":"Canvas", "archetype":"button_list", "label":"Sections"},
	{"id":"radial_menu", "name":"Radial Menu", "category":"Canvas", "archetype":"button_list", "label":"Radial"},
	{"id":"carousel", "name":"Carousel", "category":"Canvas", "archetype":"panel", "label":"Carousel"},
	{"id":"wizard", "name":"Wizard", "category":"Canvas", "archetype":"panel", "label":"Step 1"},
	{"id":"theme_manager", "name":"Theme Manager", "category":"Canvas", "archetype":"scaffold", "label":"Theme"},
	{"id":"inventory_grid", "name":"Inventory Grid", "category":"Canvas", "archetype":"grid", "label":"Inventory"},
	{"id":"sprite_anim", "name":"Sprite Anim", "category":"Canvas", "archetype":"scaffold", "label":"Sprite"},
	{"id":"button_group", "name":"Button Group", "category":"Canvas", "archetype":"button_list", "label":"Buttons"},
	{"id":"parallax_background", "name":"Parallax Background", "category":"Canvas", "archetype":"scaffold", "label":"Parallax"},
	{"id":"marquee", "name":"Marquee", "category":"Canvas", "archetype":"caption", "label":"Scrolling marquee text…"},
	{"id":"shimmer", "name":"Shimmer", "category":"Canvas", "archetype":"panel", "label":"Loading…"},
	{"id":"gradient_background", "name":"Gradient Background", "category":"Canvas", "archetype":"overlay", "label":"Gradient"},
	{"id":"aspect_ratio", "name":"Aspect Ratio Container", "category":"Canvas", "archetype":"scaffold", "label":"Aspect"},
	{"id":"grid_view", "name":"Grid View", "category":"Canvas", "archetype":"grid", "label":"Items"},
	{"id":"typewriter_label", "name":"Typewriter Label", "category":"Canvas", "archetype":"caption", "label":"Typewriter text…"},
	{"id":"animated_number", "name":"Animated Number", "category":"Canvas", "archetype":"stat", "label":"COUNT", "suffix":""},
	{"id":"flip_card", "name":"Flip Card", "category":"Canvas", "archetype":"panel", "label":"Flip"},
	{"id":"pulse_ring", "name":"Pulse Ring", "category":"Canvas", "archetype":"crosshair", "label":"○"},
	{"id":"glitch_effect", "name":"Glitch Effect", "category":"FX", "archetype":"overlay", "label":"Glitch"},
	{"id":"scanlines", "name":"Scanlines", "category":"FX", "archetype":"overlay", "label":"Scanlines"},
	{"id":"blur_panel", "name":"Blur Panel", "category":"FX", "archetype":"panel", "label":"Blur"},
	{"id":"ripple_effect", "name":"Ripple Effect", "category":"FX", "archetype":"overlay", "label":"Ripple"},
	{"id":"chromatic_aberration", "name":"Chromatic Aberration", "category":"FX", "archetype":"overlay", "label":"Aberration"},
	{"id":"film_grain", "name":"Film Grain", "category":"FX", "archetype":"overlay", "label":"Grain"},
	{"id":"color_grade", "name":"Color Grade", "category":"FX", "archetype":"overlay", "label":"Grade"},
	{"id":"dissolve_effect", "name":"Dissolve Effect", "category":"FX", "archetype":"overlay", "label":"Dissolve"},
	{"id":"outline_shadow", "name":"Outline / Shadow Text", "category":"FX", "archetype":"caption", "label":"OUTLINE"},
	{"id":"motion_blur", "name":"Motion Blur", "category":"FX", "archetype":"overlay", "label":"Motion"},
	{"id":"pixelate", "name":"Pixelate", "category":"FX", "archetype":"overlay", "label":"Pixelate"},
	{"id":"water_fx", "name":"Water FX", "category":"FX", "archetype":"overlay", "label":"Water"},
	{"id":"freeze_frame", "name":"Freeze Frame", "category":"FX", "archetype":"overlay", "label":"Freeze"},
	{"id":"screen_wipes", "name":"Screen Wipes", "category":"FX", "archetype":"overlay", "label":"Wipe"},
	{"id":"neon_glow", "name":"Neon Glow", "category":"FX", "archetype":"caption", "label":"NEON"},
	{"id":"liquid_fill", "name":"Liquid Fill", "category":"FX", "archetype":"bar", "label":"LIQUID", "value":60},
	{"id":"split_screen", "name":"Split Screen", "category":"FX", "archetype":"scaffold", "label":"Split"},
	{"id":"magnifier", "name":"Magnifier", "category":"FX", "archetype":"scaffold", "label":"Magnify"},
	{"id":"retro_fx", "name":"Retro FX (VHS/Dither)", "category":"FX", "archetype":"overlay", "label":"Retro"},
	{"id":"tech_tree", "name":"Tech Tree", "category":"Canvas", "archetype":"grid", "label":"Tech"},
	{"id":"text_fx", "name":"Text FX (Terminal)", "category":"Canvas", "archetype":"caption", "label":"TERMINAL>"},
	{"id":"screen_shake_node", "name":"Screen Shake", "category":"FX", "archetype":"scaffold", "label":"Shake"},
	{"id":"scene_transition", "name":"Scene Transition", "category":"Canvas", "archetype":"overlay", "label":"Transition"},
	{"id":"particle_ui", "name":"Particle UI", "category":"FX", "archetype":"scaffold", "label":"Particles"},
	{"id":"screen_fx", "name":"Screen FX", "category":"FX", "archetype":"overlay", "label":"FX"},

	# Core systems (non-visual — architecture, not widgets)
	{"id":"data_binder", "name":"Data Binder", "category":"Core", "archetype":"system", "label":"data binding"},
	{"id":"data_grid", "name":"Data Grid", "category":"Core", "archetype":"system", "label":"data grid"},
	{"id":"form_builder", "name":"Form Builder", "category":"Core", "archetype":"system", "label":"forms"},
	{"id":"tree_view", "name":"Tree View", "category":"Core", "archetype":"system", "label":"tree"},
	{"id":"dropdown", "name":"Dropdown", "category":"Core", "archetype":"system", "label":"dropdown"},
	{"id":"keybind_manager", "name":"Keybind Manager", "category":"Core", "archetype":"system", "label":"keybinds"},
	{"id":"state_machine", "name":"State Machine + EventBus", "category":"Core", "archetype":"system", "label":"FSM"},
	{"id":"pool_save_manager", "name":"Pool + Save Manager", "category":"Core", "archetype":"system", "label":"pool/save"},
	{"id":"audio_manager", "name":"Audio Manager", "category":"Core", "archetype":"system", "label":"audio"},
	{"id":"localization", "name":"Localization", "category":"Core", "archetype":"system", "label":"i18n"},
	{"id":"coroutine", "name":"Coroutine", "category":"Core", "archetype":"system", "label":"coroutines"},
	{"id":"config_manager", "name":"Config Manager", "category":"Core", "archetype":"system", "label":"config"},
	{"id":"weighted_table", "name":"Weighted Table", "category":"Core", "archetype":"system", "label":"loot tables"},
	{"id":"command_history", "name":"Command History", "category":"Core", "archetype":"system", "label":"undo/redo"},
	{"id":"service_locator", "name":"Service Locator", "category":"Core", "archetype":"system", "label":"services"},
]


static func catalog() -> Array:
	return CATALOG


static func build_by_id(p_id: String) -> Control:
	for e in CATALOG:
		if e["id"] == p_id:
			return build(e)
	push_warning("[Beep UI] Unknown widget id: %s" % p_id)
	return null


static func build(entry: Dictionary) -> Control:
	var root: Control = null
	match String(entry["archetype"]):
		"bar": root = _bar(entry)
		"stat": root = _stat(entry)
		"caption": root = _caption(entry)
		"panel": root = _panel(entry)
		"button_list": root = _button_list(entry)
		"grid": root = _grid(entry)
		"toast_host": root = _toast_host(entry)
		"crosshair": root = _crosshair(entry)
		"overlay": root = _overlay(entry)
		"scaffold": root = _scaffold(entry)
		"system": root = _system(entry)
		_: root = _scaffold(entry)
	root.name = String(entry["id"]).capitalize()
	_attach_theme(root)
	return root


# Attach a BeepThemeApplier as a child so the widget subtree is auto-styled.
static func _attach_theme(root: Control) -> void:
	var applier: BeepThemeApplier = THEME_APPLIER_SCRIPT.new()
	applier.name = "Theme"
	root.add_child(applier)


# ════════════════════════════════════════════════════════════════
# Archetype builders
# ════════════════════════════════════════════════════════════════

static func _bar(entry: Dictionary) -> Control:
	var box := HBoxContainer.new()
	box.custom_minimum_size = Vector2(220, 36)
	box.add_theme_constant_override("separation", 8)
	var lbl := Label.new()
	lbl.text = String(entry.get("label", "BAR"))
	lbl.custom_minimum_size = Vector2(60, 0)
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	box.add_child(lbl)
	var pb := ProgressBar.new()
	pb.value = int(entry.get("value", 0))
	pb.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pb.show_percentage = false
	box.add_child(pb)
	return box


# Compact shells (a static value / a plain label) stand in for animated widgets. They render nothing
# moving on their own — say so via tooltip so the seam isn't silent (see _scaffold's visible hint).
const _SHELL_HINT := "Static shell — the value/animation is yours to drive (a script, or a BeepUIEffect: Typewriter/Glitch/Shimmer)."


static func _stat(entry: Dictionary) -> Control:
	var box := HBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	box.tooltip_text = _SHELL_HINT
	var cap := Label.new()
	cap.text = String(entry.get("label", "STAT"))
	cap.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	box.add_child(cap)
	var val := Label.new()
	val.text = "0" + String(entry.get("suffix", ""))
	val.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	box.add_child(val)
	return box


static func _caption(entry: Dictionary) -> Control:
	var lbl := Label.new()
	lbl.text = String(entry.get("label", ""))
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.custom_minimum_size = Vector2(180, 28)
	lbl.tooltip_text = _SHELL_HINT
	return lbl


static func _panel(entry: Dictionary) -> Control:
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(260, 160)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	panel.add_child(margin)
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 6)
	margin.add_child(vbox)
	var title := Label.new()
	title.text = String(entry.get("label", "Panel"))
	vbox.add_child(title)
	var hint := Label.new()
	hint.text = "(content — add children here)"
	hint.add_theme_font_size_override("font_size", 11)
	vbox.add_child(hint)
	return panel


static func _button_list(entry: Dictionary) -> Control:
	var vbox := VBoxContainer.new()
	vbox.custom_minimum_size = Vector2(200, 120)
	vbox.add_theme_constant_override("separation", 4)
	var title := Label.new()
	title.text = String(entry.get("label", "List"))
	vbox.add_child(title)
	for i in 3:
		var b := Button.new()
		b.text = "Option %d" % (i + 1)
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		vbox.add_child(b)
	return vbox


static func _grid(entry: Dictionary) -> Control:
	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 4)
	grid.add_theme_constant_override("v_separation", 4)
	var title := Label.new()
	title.text = String(entry.get("label", "Grid"))
	# Wrap title + grid in a VBox
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 6)
	vbox.add_child(title)
	vbox.add_child(grid)
	for i in 8:
		var slot := PanelContainer.new()
		slot.custom_minimum_size = Vector2(44, 44)
		grid.add_child(slot)
	return vbox


static func _toast_host(entry: Dictionary) -> Control:
	# Instantiate the toast host from the preloaded script. We avoid the bare
	# global class_name here because class-registry resolution order between
	# @tool scripts can fail at parse time, which breaks the preload chain.
	var host: Control = TOAST_SCRIPT.new()
	host.custom_minimum_size = Vector2(320, 120)
	# Fill the parent so the host's size (which toasts now center on) matches the visible area — a
	# fixed-size host pushed toasts off-position. A Container parent may still override this, but a
	# free/anchored parent gets correct placement.
	host.set_anchors_preset(Control.PRESET_FULL_RECT)
	var lbl := Label.new()
	lbl.text = String(entry.get("label", "Toasts")) + " — host (call show_toast(\"msg\"))"
	lbl.add_theme_font_size_override("font_size", 11)
	host.add_child(lbl)
	return host


static func _crosshair(entry: Dictionary) -> Control:
	var c := CenterContainer.new()
	c.custom_minimum_size = Vector2(48, 48)
	var lbl := Label.new()
	lbl.text = String(entry.get("label", "+"))
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	c.add_child(lbl)
	return c


static func _overlay(entry: Dictionary) -> Control:
	var cr := ColorRect.new()
	cr.color = Color(0, 0, 0, 0.35)
	cr.custom_minimum_size = Vector2(320, 180)
	cr.set_anchors_preset(Control.PRESET_FULL_RECT)
	cr.tooltip_text = _SHELL_HINT
	var lbl := Label.new()
	lbl.text = String(entry.get("label", "Overlay"))
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.set_anchors_preset(Control.PRESET_FULL_RECT)
	cr.add_child(lbl)
	# This overlay is a static tint + name — an evocative catalog id (glitch_effect, chromatic_aberration)
	# is NOT self-animating. A visible note here, since the overlay has the room (unlike _stat/_caption),
	# so a dropped-in FX widget doesn't read as "working but broken".
	var hint := Label.new()
	hint.text = "static shell — add a shader or BeepUIEffect to animate"
	hint.add_theme_font_size_override("font_size", 10)
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	hint.modulate = Color(1, 1, 1, 0.6)
	cr.add_child(hint)
	return cr


static func _scaffold(entry: Dictionary) -> Control:
	# Honest themed starter for genuinely complex widgets (minimap, compass, FX).
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(200, 120)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	panel.add_child(margin)
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 4)
	margin.add_child(vbox)
	var title := Label.new()
	title.text = String(entry.get("label", "Widget"))
	vbox.add_child(title)
	var hint := Label.new()
	hint.text = "Starter scaffold — themed shell, extend with your logic/sprites."
	hint.add_theme_font_size_override("font_size", 11)
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	vbox.add_child(hint)
	return panel


static func _system(entry: Dictionary) -> Control:
	# Non-visual architecture module — surfaced as a clearly-labeled note.
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(260, 70)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	panel.add_child(margin)
	var lbl := Label.new()
	lbl.text = "System module (non-visual): %s.\nNot a UI widget — implement as an autoload/manager." % String(entry.get("label", ""))
	lbl.add_theme_font_size_override("font_size", 11)
	lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	margin.add_child(lbl)
	return panel
