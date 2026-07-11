@tool
extends BeepPreset

## Classic — Retro 3D beveled UI.
## Ports ThemePresetClassic.cs. 4px square-ish corners, 2px bevel border,
## chunky shadow, warm grey + gold palette, inset press look.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.78, 0.75, 0.72, 1.0)
	surface_hover = Color(0.85, 0.82, 0.78, 1.0)
	surface_pressed = Color(0.66, 0.63, 0.60, 1.0)
	surface_disabled = Color(0.61, 0.59, 0.56, 1.0)

	# ── Text ──
	text_primary = Color(0.12, 0.10, 0.08, 1.0)
	text_hover = Color(0.0, 0.0, 0.0, 1.0)
	text_disabled = Color(0.55, 0.53, 0.50, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.78, 0.66, 0.31, 1.0)     # gold
	accent_secondary = Color(0.63, 0.53, 0.22, 1.0)

	# ── Border ──
	border_normal = Color(0.55, 0.53, 0.50, 1.0)
	border_hover = Color(0.78, 0.66, 0.31, 1.0)       # gold hover
	border_focus = Color(0.78, 0.66, 0.31, 1.0)       # gold focus
	border_bevel_light = Color(0.91, 0.88, 0.85, 1.0)
	border_bevel_dark = Color(0.53, 0.50, 0.47, 1.0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.50)

	# ── Background ──
	bg_panel = Color(0.72, 0.69, 0.66, 1.0)
	bg_canvas = Color(0.55, 0.50, 0.44, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.25, 0.55, 0.35, 1.0)
	semantic_danger = Color(0.65, 0.25, 0.20, 1.0)
	semantic_warning = Color(0.75, 0.60, 0.20, 1.0)
	semantic_info = Color(0.30, 0.50, 0.65, 1.0)

	# ── Geometry ──
	corner_radius = 4
	border_width = 2
	pad_h = 14.0
	pad_v = 6.0
	shadow_size_normal = 4
	shadow_size_hover = 6
	shadow_size_pressed = 0
	shadow_size_focus = 4
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.60)

	# ── Animation ──
	anim_hover_scale = 1.02
	anim_hover_scale_dur = 0.12
	anim_press_scale = 0.98
	anim_press_scale_dur = 0.06
	anim_shadow_lift = true
	anim_focus_glow = false
