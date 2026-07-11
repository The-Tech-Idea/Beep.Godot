@tool
extends BeepPreset

## Sports — Bold stadium / athletic UI.
## Ports ThemePresetSports.cs. Dark navy surface, energetic red accent, gold border
## highlights, energetic scale animations. Sports broadcast overlay feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.10, 0.10, 0.18, 1.0)
	surface_hover = Color(0.16, 0.16, 0.27, 1.0)
	surface_pressed = Color(0.06, 0.06, 0.12, 1.0)
	surface_disabled = Color(0.08, 0.08, 0.13, 1.0)

	# ── Text ──
	text_primary = Color(0.95, 0.95, 0.97, 1.0)
	text_hover = Color(1.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.45, 0.45, 0.52, 1.0)
	text_on_dark = Color(1.0, 0.85, 0.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.23, 0.23, 1.0)      # red
	accent_secondary = Color(1.0, 0.85, 0.0, 1.0)     # gold

	# ── Border ──
	border_normal = Color(0.25, 0.25, 0.38, 1.0)
	border_hover = Color(1.0, 0.85, 0.0, 0.50)        # gold 50%
	border_focus = Color(1.0, 0.85, 0.0, 0.80)        # gold 80%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.40)

	# ── Background ──
	bg_panel = Color(0.08, 0.08, 0.14, 0.95)
	bg_canvas = Color(0.04, 0.04, 0.09, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.15, 0.70, 0.30, 1.0)
	semantic_danger = Color(1.0, 0.23, 0.23, 1.0)
	semantic_warning = Color(1.0, 0.85, 0.0, 1.0)
	semantic_info = Color(0.25, 0.55, 0.85, 1.0)

	# ── Geometry ──
	corner_radius = 8
	border_width = 2
	pad_h = 18.0
	pad_v = 9.0
	shadow_size_normal = 6
	shadow_size_hover = 10
	shadow_size_pressed = 2
	shadow_size_focus = 6
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.55)

	# ── Animation ──
	anim_hover_scale = 1.05
	anim_hover_scale_dur = 0.12
	anim_press_scale = 0.95
	anim_press_scale_dur = 0.06
	anim_shadow_lift = true
	anim_focus_glow = true
