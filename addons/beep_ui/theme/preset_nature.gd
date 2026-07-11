@tool
extends BeepPreset

## Nature — Forest / organic earth UI.
## Ports ThemePresetNature.cs. Forest green surface, wood brown border, leaf green
## accent, large organic 14px corners, soft green shadow.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.23, 0.37, 0.17, 1.0)
	surface_hover = Color(0.30, 0.47, 0.22, 1.0)
	surface_pressed = Color(0.16, 0.27, 0.11, 1.0)
	surface_disabled = Color(0.15, 0.22, 0.12, 1.0)

	# ── Text ──
	text_primary = Color(0.90, 0.95, 0.85, 1.0)
	text_hover = Color(1.0, 1.0, 0.95, 1.0)
	text_disabled = Color(0.45, 0.50, 0.42, 1.0)
	text_on_dark = Color(0.78, 0.92, 0.60, 1.0)

	# ── Accent ──
	accent_primary = Color(0.48, 0.70, 0.26, 1.0)     # leaf green
	accent_secondary = Color(0.55, 0.78, 0.35, 1.0)

	# ── Border ──
	border_normal = Color(0.35, 0.48, 0.29, 1.0)
	border_hover = Color(0.48, 0.70, 0.26, 0.50)      # green 50%
	border_focus = Color(0.48, 0.70, 0.26, 0.80)      # green 80%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.10, 0.20, 0.08, 0.35)      # green shadow

	# ── Background ──
	bg_panel = Color(0.18, 0.28, 0.13, 0.95)
	bg_canvas = Color(0.12, 0.20, 0.08, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.48, 0.70, 0.26, 1.0)
	semantic_danger = Color(0.80, 0.25, 0.20, 1.0)
	semantic_warning = Color(0.85, 0.65, 0.15, 1.0)
	semantic_info = Color(0.28, 0.55, 0.72, 1.0)

	# ── Geometry ──
	corner_radius = 14
	border_width = 1
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 8
	shadow_size_hover = 14
	shadow_size_pressed = 2
	shadow_size_focus = 10
	shadow_tint_hover = Color(0.15, 0.30, 0.10, 0.45)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.18
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.08
	anim_shadow_lift = true
	anim_focus_glow = true
