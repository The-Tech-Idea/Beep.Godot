@tool
extends BeepPreset

## Retro 80s — Synthwave / outrun UI.
## Ports ThemePresetRetro80s.cs. Dark synth purple surface, hot pink accent, cyan
## neon glow border, 4px corners. 80s arcade / synthwave album cover feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.10, 0.0, 0.20, 1.0)
	surface_hover = Color(0.18, 0.02, 0.30, 1.0)
	surface_pressed = Color(0.06, 0.0, 0.14, 1.0)
	surface_disabled = Color(0.06, 0.02, 0.12, 1.0)

	# ── Text ──
	text_primary = Color(0.0, 1.0, 1.0, 1.0)          # cyan
	text_hover = Color(1.0, 0.0, 1.0, 1.0)            # pink
	text_disabled = Color(0.30, 0.20, 0.40, 1.0)
	text_on_dark = Color(0.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.0, 1.0, 1.0)        # hot pink
	accent_secondary = Color(0.0, 1.0, 1.0, 1.0)      # cyan

	# ── Border ──
	border_normal = Color(0.0, 1.0, 1.0, 0.40)        # cyan 40%
	border_hover = Color(1.0, 0.0, 1.0, 0.70)         # pink 70%
	border_focus = Color(0.0, 1.0, 1.0, 0.90)         # cyan 90%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(1.0, 0.0, 1.0, 0.40)         # pink neon glow

	# ── Background ──
	bg_panel = Color(0.07, 0.0, 0.14, 0.95)
	bg_canvas = Color(0.04, 0.0, 0.09, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.0, 1.0, 0.50, 1.0)
	semantic_danger = Color(1.0, 0.0, 0.30, 1.0)
	semantic_warning = Color(1.0, 1.0, 0.0, 1.0)
	semantic_info = Color(0.0, 1.0, 1.0, 1.0)

	# ── Geometry ──
	corner_radius = 4
	border_width = 2
	pad_h = 18.0
	pad_v = 9.0
	shadow_size_normal = 10
	shadow_size_hover = 18
	shadow_size_pressed = 4
	shadow_size_focus = 22
	shadow_tint_hover = Color(1.0, 0.0, 1.0, 0.60)
	shadow_tint_focus = Color(0.0, 1.0, 1.0, 0.70)

	# ── Animation ──
	anim_hover_scale = 1.05
	anim_hover_scale_dur = 0.12
	anim_press_scale = 0.95
	anim_press_scale_dur = 0.06
	anim_shadow_lift = false
	anim_focus_glow = true
