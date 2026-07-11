@tool
extends BeepPreset

## Pixel 8-Bit — NES / SNES retro gaming UI.
## Ports ThemePresetPixel8Bit.cs. Sharp zero-radius corners, solid white 4px border,
## chunky primary colors, no shadow (flat pixel look). 8-bit RPG menu feel.

func _init() -> void:
	# ── Surface ──
	surface_primary = Color(0.0, 0.0, 0.0, 1.0)
	surface_hover = Color(0.80, 0.0, 0.0, 1.0)        # red
	surface_pressed = Color(0.50, 0.0, 0.0, 1.0)      # dark red
	surface_disabled = Color(0.20, 0.20, 0.20, 1.0)

	# ── Text ──
	text_primary = Color(1.0, 1.0, 1.0, 1.0)
	text_hover = Color(1.0, 1.0, 0.0, 1.0)            # yellow
	text_disabled = Color(0.40, 0.40, 0.40, 1.0)
	text_on_dark = Color(1.0, 1.0, 1.0, 1.0)

	# ── Accent ──
	accent_primary = Color(1.0, 0.0, 0.0, 1.0)
	accent_secondary = Color(0.0, 0.0, 1.0, 1.0)

	# ── Border ──
	border_normal = Color(1.0, 1.0, 1.0, 1.0)         # white
	border_hover = Color(1.0, 1.0, 0.0, 1.0)          # yellow
	border_focus = Color(1.0, 0.0, 0.0, 1.0)          # red
	border_bevel_light = Color(1, 1, 1, 0)
	border_bevel_dark = Color(1, 1, 1, 0)

	# ── Shadow ──
	shadow_color = Color(0.0, 0.0, 0.0, 0.0)          # no shadow — flat

	# ── Background ──
	bg_panel = Color(0.0, 0.0, 0.47, 1.0)             # NES blue
	bg_canvas = Color(0.0, 0.0, 0.0, 1.0)

	# ── Semantic ──
	semantic_success = Color(0.0, 0.80, 0.0, 1.0)
	semantic_danger = Color(1.0, 0.0, 0.0, 1.0)
	semantic_warning = Color(1.0, 1.0, 0.0, 1.0)
	semantic_info = Color(0.0, 0.0, 1.0, 1.0)

	# ── Geometry ──
	corner_radius = 0
	border_width = 4
	pad_h = 16.0
	pad_v = 8.0
	shadow_size_normal = 0
	shadow_size_hover = 0
	shadow_size_pressed = 0
	shadow_size_focus = 0

	# ── Animation ──  (AnimationConfig.None — flat pixel, no scale)
	anim_hover_scale = 1.0
	anim_hover_scale_dur = 0.0
	anim_press_scale = 1.0
	anim_press_scale_dur = 0.0
	anim_shadow_lift = false
	anim_focus_glow = false
