@tool
extends BeepPreset

## Toxic — Nuclear / biohazard / slime UI.
## Ports ThemePresetToxic.cs. Sludge dark green surface, toxic neon green accent,
## oozing border, green glow shadow, blobby 8px corners. Radioactive feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.10, 0.16, 0.04, 1.0)
	surface_hover = Color(0.15, 0.24, 0.06, 1.0)
	surface_pressed = Color(0.06, 0.10, 0.02, 1.0)
	surface_disabled = Color(0.06, 0.10, 0.04, 1.0)

	# ── Text ──
	text_primary = Color(0.50, 1.0, 0.0, 1.0)         # toxic green
	text_hover = Color(0.70, 1.0, 0.30, 1.0)
	text_disabled = Color(0.20, 0.35, 0.08, 1.0)
	text_on_dark = Color(0.50, 1.0, 0.0, 1.0)

	# ── Accent ──
	accent_primary = Color(0.50, 1.0, 0.0, 1.0)
	accent_secondary = Color(0.35, 0.70, 0.0, 1.0)

	# ── Border ──
	border_normal = Color(0.29, 0.54, 0.0, 1.0)
	border_hover = Color(0.50, 1.0, 0.0, 0.60)        # toxic 60%
	border_focus = Color(0.50, 1.0, 0.0, 0.90)        # toxic 90%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.50, 1.0, 0.0, 0.35)        # green glow

	# ── Background ──
	bg_panel = Color(0.07, 0.12, 0.03, 0.95)
	bg_canvas = Color(0.04, 0.08, 0.02, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.35, 0.70, 0.0, 1.0)
	semantic_danger = Color(0.80, 0.15, 0.10, 1.0)
	semantic_warning = Color(1.0, 0.85, 0.0, 1.0)
	semantic_info = Color(0.50, 1.0, 0.0, 1.0)

	# ── Geometry ──
	corner_radius = 8
	border_width = 2
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 8
	shadow_size_hover = 16
	shadow_size_pressed = 4
	shadow_size_focus = 20
	shadow_tint_hover = Color(0.50, 1.0, 0.0, 0.55)
	shadow_tint_focus = Color(0.50, 1.0, 0.0, 0.65)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.14
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.07
	anim_shadow_lift = false
	anim_focus_glow = true
