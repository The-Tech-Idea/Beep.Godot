@tool
extends BeepPreset

## Modern — Clean minimal UI.
## Ports ThemePresetModern.cs. Soft shadow, 12px rounded corners,
## neutral blue-grey surface, subtle transitions.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.18, 0.18, 0.23, 1.0)
	surface_hover = Color(0.24, 0.24, 0.30, 1.0)
	surface_pressed = Color(0.13, 0.13, 0.18, 1.0)
	surface_disabled = Color(0.10, 0.10, 0.15, 1.0)

	# ── Text ──
	text_primary = Color(0.91, 0.91, 0.93, 1.0)
	text_hover = Color(1.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.40, 0.40, 0.45, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.29, 0.56, 0.85, 1.0)
	accent_secondary = Color(0.42, 0.64, 0.89, 1.0)

	# ── Border ──
	border_normal = Color(1.0, 1.0, 1.0, 0.0)          # transparent
	border_hover = Color(0.29, 0.56, 0.85, 0.30)      # accent 30%
	border_focus = Color(0.29, 0.56, 0.85, 0.70)      # accent 70%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.22)

	# ── Background ──
	bg_panel = Color(0.12, 0.12, 0.16, 0.95)
	bg_canvas = Color(0.08, 0.08, 0.11, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.18, 0.72, 0.35, 1.0)
	semantic_danger = Color(0.85, 0.22, 0.22, 1.0)
	semantic_warning = Color(0.90, 0.68, 0.14, 1.0)
	semantic_info = Color(0.29, 0.56, 0.85, 1.0)

	# ── Geometry ──
	corner_radius = 12
	border_width = 0
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 8
	shadow_size_hover = 12
	shadow_size_pressed = 2
	shadow_size_focus = 8
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.30)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.15
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.08
	anim_shadow_lift = true
	anim_focus_glow = true
