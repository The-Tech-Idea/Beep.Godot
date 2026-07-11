@tool
extends BeepPreset

## Cyberpunk — rain-slick neon dystopian UI.
## Ports ThemePresetCyberpunk.cs. Dark void surface, neon pink accent,
## cyan glow border, angular 2px corners, neon spread shadow.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.05, 0.05, 0.10, 1.0)
	surface_hover = Color(0.10, 0.05, 0.15, 1.0)
	surface_pressed = Color(0.03, 0.03, 0.07, 1.0)
	surface_disabled = Color(0.04, 0.04, 0.08, 1.0)

	# ── Text ──
	text_primary = Color(0.0, 1.0, 1.0, 1.0)        # cyan
	text_hover = Color(1.0, 0.0, 1.0, 1.0)          # pink
	text_disabled = Color(0.25, 0.30, 0.40, 1.0)
	text_on_dark = Color(1.0, 0.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.0, 1.0, 1.0)      # neon pink
	accent_secondary = Color(0.0, 1.0, 1.0, 1.0)    # cyan

	# ── Border ──
	border_normal = Color(0.0, 1.0, 1.0, 0.35)
	border_hover = Color(1.0, 0.0, 1.0, 0.65)
	border_focus = Color(1.0, 0.0, 1.0, 0.9)
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(1.0, 0.0, 1.0, 0.40)       # pink neon spread

	# ── Background ──
	bg_panel = Color(0.03, 0.03, 0.08, 0.95)
	bg_canvas = Color(0.02, 0.02, 0.05, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.0, 1.0, 0.50, 1.0)
	semantic_danger = Color(1.0, 0.0, 0.30, 1.0)
	semantic_warning = Color(1.0, 1.0, 0.0, 1.0)
	semantic_info = Color(0.0, 1.0, 1.0, 1.0)

	# ── Geometry ──
	corner_radius = 2
	border_width = 2
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 12
	shadow_size_hover = 20
	shadow_size_pressed = 6
	shadow_size_focus = 24
	shadow_tint_hover = Color(1.0, 0.0, 1.0, 0.60)
	shadow_tint_focus = Color(1.0, 0.0, 1.0, 0.75)

	# ── Animation ──
	anim_hover_scale = 1.04
	anim_hover_scale_dur = 0.10
	anim_press_scale = 0.96
	anim_press_scale_dur = 0.05
	anim_shadow_lift = false
	anim_focus_glow = true
