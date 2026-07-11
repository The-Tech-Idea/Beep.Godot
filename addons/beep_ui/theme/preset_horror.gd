@tool
extends BeepPreset

## Horror — Dark grunge / spooky UI.
## Ports ThemePresetHorror.cs. Near-black surface, blood red accent, rough 2px
## corners, jagged large shadow. Survival horror inventory feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.10, 0.04, 0.04, 1.0)
	surface_hover = Color(0.16, 0.06, 0.06, 1.0)
	surface_pressed = Color(0.06, 0.02, 0.02, 1.0)
	surface_disabled = Color(0.08, 0.05, 0.05, 1.0)

	# ── Text ──
	text_primary = Color(0.85, 0.80, 0.78, 1.0)
	text_hover = Color(1.0, 0.90, 0.88, 1.0)
	text_disabled = Color(0.40, 0.35, 0.33, 1.0)
	text_on_dark = Color(0.85, 0.15, 0.15, 1.0)

	# ── Accent ──
	accent_primary = Color(0.55, 0.0, 0.0, 1.0)       # blood red
	accent_secondary = Color(0.35, 0.0, 0.0, 1.0)

	# ── Border ──
	border_normal = Color(0.23, 0.0, 0.0, 1.0)
	border_hover = Color(0.55, 0.0, 0.0, 0.60)        # blood 60%
	border_focus = Color(0.55, 0.0, 0.0, 0.90)        # blood 90%
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.75)

	# ── Background ──
	bg_panel = Color(0.07, 0.03, 0.03, 0.95)
	bg_canvas = Color(0.04, 0.01, 0.01, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.20, 0.45, 0.20, 1.0)
	semantic_danger = Color(0.55, 0.0, 0.0, 1.0)
	semantic_warning = Color(0.65, 0.40, 0.05, 1.0)
	semantic_info = Color(0.35, 0.30, 0.40, 1.0)

	# ── Geometry ──
	corner_radius = 2
	border_width = 2
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 8
	shadow_size_hover = 14
	shadow_size_pressed = 0
	shadow_size_focus = 12
	shadow_tint_hover = Color(0.55, 0.0, 0.0, 0.40)
	shadow_tint_focus = Color(0.55, 0.0, 0.0, 0.50)

	# ── Animation ──
	anim_hover_scale = 1.02
	anim_hover_scale_dur = 0.10
	anim_press_scale = 0.98
	anim_press_scale_dur = 0.05
	anim_shadow_lift = false
	anim_focus_glow = true
