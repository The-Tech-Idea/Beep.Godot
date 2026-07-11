@tool
extends BeepPreset

## Oil & Gas — Heavy industrial UI.
## Ports ThemePresetOilGas.cs. Dark steel surface, hazard orange accent stripe on
## left border, harsh shadow, sharp corners. Heavy machinery control panels.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.16, 0.16, 0.18, 1.0)
	surface_hover = Color(0.22, 0.22, 0.24, 1.0)
	surface_pressed = Color(0.11, 0.11, 0.13, 1.0)
	surface_disabled = Color(0.10, 0.10, 0.11, 1.0)

	# ── Text ──
	text_primary = Color(0.90, 0.90, 0.91, 1.0)
	text_hover = Color(1.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.45, 0.45, 0.47, 1.0)
	text_on_dark = Color(1.0, 0.42, 0.10, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.42, 0.10, 1.0)      # hazard orange
	accent_secondary = Color(1.0, 0.55, 0.25, 1.0)

	# ── Border ──
	border_normal = Color(0.30, 0.30, 0.32, 1.0)
	border_hover = Color(1.0, 0.42, 0.10, 0.50)
	border_focus = Color(1.0, 0.42, 0.10, 0.85)
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.55)

	# ── Background ──
	bg_panel = Color(0.12, 0.12, 0.14, 0.95)
	bg_canvas = Color(0.08, 0.08, 0.09, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.20, 0.65, 0.25, 1.0)
	semantic_danger = Color(0.90, 0.15, 0.10, 1.0)
	semantic_warning = Color(1.0, 0.70, 0.05, 1.0)
	semantic_info = Color(1.0, 0.42, 0.10, 1.0)

	# ── Geometry ──
	corner_radius = 3
	border_width = 4
	pad_h = 18.0
	pad_v = 8.0
	shadow_size_normal = 6
	shadow_size_hover = 8
	shadow_size_pressed = 0
	shadow_size_focus = 6
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.65)

	# ── Animation ──
	anim_hover_scale = 1.02
	anim_hover_scale_dur = 0.10
	anim_press_scale = 0.98
	anim_press_scale_dur = 0.05
	anim_shadow_lift = false
	anim_focus_glow = true
