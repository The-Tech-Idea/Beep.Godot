@tool
extends BeepPreset

## Sea — Ocean / underwater / pirate UI.
## Ports ThemePresetSea.cs. Deep teal surface, seafoam accent, soft blue glow
## shadow, rounded flowing corners.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.10, 0.23, 0.29, 1.0)
	surface_hover = Color(0.14, 0.30, 0.38, 1.0)
	surface_pressed = Color(0.06, 0.16, 0.22, 1.0)
	surface_disabled = Color(0.05, 0.12, 0.16, 1.0)

	# ── Text ──
	text_primary = Color(0.31, 0.80, 0.77, 1.0)       # seafoam
	text_hover = Color(0.47, 0.90, 0.88, 1.0)
	text_disabled = Color(0.20, 0.40, 0.42, 1.0)
	text_on_dark = Color(0.90, 0.97, 0.96, 1.0)

	# ── Accent ──
	accent_primary = Color(0.31, 0.80, 0.77, 1.0)
	accent_secondary = Color(0.18, 0.60, 0.60, 1.0)

	# ── Border ──
	border_normal = Color(0.16, 0.42, 0.48, 1.0)
	border_hover = Color(0.31, 0.80, 0.77, 0.50)      # seafoam 50%
	border_focus = Color(0.31, 0.80, 0.77, 0.80)      # seafoam 80%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.10, 0.30, 0.35, 0.30)      # blue glow

	# ── Background ──
	bg_panel = Color(0.07, 0.18, 0.24, 0.95)
	bg_canvas = Color(0.04, 0.12, 0.18, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.18, 0.60, 0.45, 1.0)
	semantic_danger = Color(0.85, 0.25, 0.25, 1.0)
	semantic_warning = Color(0.95, 0.75, 0.20, 1.0)
	semantic_info = Color(0.31, 0.80, 0.77, 1.0)

	# ── Geometry ──
	corner_radius = 10
	border_width = 1
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 10
	shadow_size_hover = 16
	shadow_size_pressed = 3
	shadow_size_focus = 14
	shadow_tint_hover = Color(0.10, 0.50, 0.55, 0.40)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.20
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.10
	anim_shadow_lift = true
	anim_focus_glow = true
