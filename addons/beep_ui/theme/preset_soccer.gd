@tool
extends BeepPreset

## Soccer — Pitch green football UI.
## Ports ThemePresetSoccer.cs. Grass green surface, white border (pitch lines),
## black accent, clean shadows. Football match broadcast feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.11, 0.37, 0.13, 1.0)
	surface_hover = Color(0.15, 0.47, 0.17, 1.0)
	surface_pressed = Color(0.07, 0.27, 0.09, 1.0)
	surface_disabled = Color(0.10, 0.25, 0.11, 1.0)

	# ── Text ──
	text_primary = Color(1.0, 1.0, 1.0, 1.0)
	text_hover = Color(1.0, 1.0, 1.0, 1.0)
	text_disabled = Color(0.55, 0.60, 0.55, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 1.0, 1.0, 1.0)
	accent_secondary = Color(0.85, 0.85, 0.85, 1.0)

	# ── Border ──
	border_normal = Color(1.0, 1.0, 1.0, 1.0)         # white pitch lines
	border_hover = Color(1.0, 1.0, 1.0, 1.0)
	border_focus = Color(0.0, 0.0, 0.0, 0.80)         # black focus
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.30)

	# ── Background ──
	bg_panel = Color(0.08, 0.30, 0.10, 0.95)
	bg_canvas = Color(0.05, 0.22, 0.07, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.15, 0.65, 0.25, 1.0)
	semantic_danger = Color(0.90, 0.20, 0.15, 1.0)
	semantic_warning = Color(1.0, 0.82, 0.0, 1.0)
	semantic_info = Color(0.25, 0.55, 0.85, 1.0)

	# ── Geometry ──
	corner_radius = 12
	border_width = 2
	pad_h = 18.0
	pad_v = 9.0
	shadow_size_normal = 4
	shadow_size_hover = 8
	shadow_size_pressed = 1
	shadow_size_focus = 4
	shadow_tint_hover = Color(0.0, 0.0, 0.0, 0.40)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.14
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.07
	anim_shadow_lift = true
	anim_focus_glow = true
