@tool
extends BeepPreset

## Desert — Warm sun-bleached Western / post-apocalyptic UI.
## Ports ThemePresetDesert.cs. Sand beige surface, terracotta accent,
## rough brown border, harsh sharp shadow.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.83, 0.65, 0.41, 1.0)
	surface_hover = Color(0.90, 0.73, 0.50, 1.0)
	surface_pressed = Color(0.72, 0.55, 0.32, 1.0)
	surface_disabled = Color(0.60, 0.48, 0.32, 1.0)

	# ── Text ──
	text_primary = Color(0.22, 0.14, 0.06, 1.0)
	text_hover = Color(0.12, 0.06, 0.0, 1.0)
	text_disabled = Color(0.50, 0.38, 0.25, 1.0)
	text_on_dark = Color(0.96, 0.90, 0.80, 1.0)

	# ── Accent ──
	accent_primary = Color(0.78, 0.36, 0.23, 1.0)     # terracotta
	accent_secondary = Color(0.90, 0.50, 0.35, 1.0)

	# ── Border ──
	border_normal = Color(0.55, 0.42, 0.08, 1.0)
	border_hover = Color(0.40, 0.28, 0.04, 1.0)
	border_focus = Color(0.78, 0.36, 0.23, 0.80)
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.45)

	# ── Background ──
	bg_panel = Color(0.88, 0.75, 0.58, 0.95)
	bg_canvas = Color(0.80, 0.65, 0.45, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.35, 0.55, 0.25, 1.0)
	semantic_danger = Color(0.75, 0.25, 0.18, 1.0)
	semantic_warning = Color(0.85, 0.60, 0.15, 1.0)
	semantic_info = Color(0.30, 0.50, 0.65, 1.0)

	# ── Geometry ──
	corner_radius = 6
	border_width = 1
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 2
	shadow_size_hover = 4
	shadow_size_pressed = 0
	shadow_size_focus = 2
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.55)

	# ── Animation ──
	anim_hover_scale = 1.03
	anim_hover_scale_dur = 0.15
	anim_press_scale = 0.97
	anim_press_scale_dur = 0.08
	anim_shadow_lift = true
	anim_focus_glow = true
