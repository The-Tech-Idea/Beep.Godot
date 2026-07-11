@tool
class_name BeepPreset
extends RefCounted

## BeepPreset — base class for all 22 Beep UI theme presets.
## Ports the C# IThemePreset / ColorSchema / AnimationConfig contract to GDScript.
## Each preset_*.gd extends this and sets its colors + animation + geometry in _init().

# ── ColorSchema ──
var surface_primary: Color
var surface_hover: Color
var surface_pressed: Color
var surface_disabled: Color
var text_primary: Color
var text_hover: Color
var text_disabled: Color
var text_on_dark: Color
var accent_primary: Color
var accent_secondary: Color
var border_normal: Color
var border_hover: Color
var border_focus: Color
var border_bevel_light: Color
var border_bevel_dark: Color
var shadow_color: Color
var bg_panel: Color
var bg_canvas: Color
var semantic_success: Color
var semantic_danger: Color
var semantic_warning: Color
var semantic_info: Color

# ── AnimationConfig ──
var anim_hover_scale: float = 1.04
var anim_hover_scale_dur: float = 0.15
var anim_press_scale: float = 0.96
var anim_press_scale_dur: float = 0.08
var anim_shadow_lift: bool = true
var anim_focus_glow: bool = true

# ── Geometry (read from the preset's normal button; reused to derive the rest) ──
var corner_radius: int = 6
var border_width: int = 1
var pad_h: float = 16.0
var pad_v: float = 8.0
var shadow_size_normal: int = 4
var shadow_size_hover: int = 8
var shadow_size_pressed: int = 2
var shadow_size_focus: int = 12

# ── Per-state shadow tint overrides (default to schema shadow_color) ──
var shadow_tint_hover: Color = Color(0, 0, 0, -1)   # -1 alpha = use shadow_color
var shadow_tint_focus: Color = Color(0, 0, 0, -1)


# ════════════════════════════════════════════════════════════════
# Button state StyleBoxes
# ════════════════════════════════════════════════════════════════

func get_button_normal() -> StyleBoxFlat:
	return _flat(surface_primary, border_normal, corner_radius, border_width, shadow_size_normal, shadow_color, pad_h, pad_v)

func get_button_hover() -> StyleBoxFlat:
	var sc: Color = shadow_color if shadow_tint_hover.a < 0 else shadow_tint_hover
	return _flat(surface_hover, border_hover, corner_radius, border_width, shadow_size_hover, sc, pad_h, pad_v)

func get_button_pressed() -> StyleBoxFlat:
	return _flat(surface_pressed, accent_secondary, corner_radius, border_width, shadow_size_pressed, shadow_color, pad_h, pad_v + 1.0)

func get_button_disabled() -> StyleBoxFlat:
	var bd := Color(border_normal.r, border_normal.g, border_normal.b, 0.6)
	return _flat(surface_disabled, bd, corner_radius, max(1, border_width - 1), 0, shadow_color, pad_h, pad_v)

func get_button_focus() -> StyleBoxFlat:
	var sc: Color = Color(border_focus.r, border_focus.g, border_focus.b, 0.7) if shadow_tint_focus.a < 0 else shadow_tint_focus
	return _flat(surface_primary, border_focus, corner_radius, border_width + 1, shadow_size_focus, sc, pad_h, pad_v)

func get_primary_button_normal() -> StyleBoxFlat:
	return _flat(accent_primary, Color(0, 0, 0, 0), corner_radius, 0, shadow_size_hover, shadow_color, pad_h + 4.0, pad_v + 2.0)

func get_danger_button_normal() -> StyleBoxFlat:
	return _flat(semantic_danger, Color(0, 0, 0, 0), corner_radius, 0, shadow_size_normal, shadow_color, pad_h, pad_v)

func get_success_button_normal() -> StyleBoxFlat:
	return _flat(semantic_success, Color(0, 0, 0, 0), corner_radius, 0, shadow_size_normal, shadow_color, pad_h, pad_v)


# ════════════════════════════════════════════════════════════════
# Container / input surfaces
# ════════════════════════════════════════════════════════════════

func get_panel_background() -> StyleBoxFlat:
	return _flat(bg_panel, border_normal, corner_radius, border_width, max(0, shadow_size_normal - 2), shadow_color, 14.0, 14.0)

func get_line_edit_normal() -> StyleBoxFlat:
	return _flat(surface_pressed, border_normal, corner_radius, border_width, 0, shadow_color, max(4.0, pad_h - 4.0), max(2.0, pad_v - 3.0))

func get_canvas_background() -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg_canvas
	return sb


# ════════════════════════════════════════════════════════════════
# Builder helper
# ════════════════════════════════════════════════════════════════

func _flat(p_bg: Color, p_border: Color, p_corner: int, p_border_w: int, p_shadow: int, p_shadow_col: Color, p_pad_h: float, p_pad_v: float) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = p_bg
	sb.set_corner_radius_all(p_corner)
	sb.border_width_left = p_border_w
	sb.border_width_right = p_border_w
	sb.border_width_top = p_border_w
	sb.border_width_bottom = p_border_w
	sb.border_color = p_border
	sb.shadow_size = p_shadow
	sb.shadow_color = p_shadow_col
	sb.content_margin_left = p_pad_h
	sb.content_margin_right = p_pad_h
	sb.content_margin_top = p_pad_v
	sb.content_margin_bottom = p_pad_v
	return sb


# ════════════════════════════════════════════════════════════════
# Registry — preset name → script path (lazy load to avoid parse cycles)
# ════════════════════════════════════════════════════════════════

const _PRESET_SCRIPTS := {
	"Modern": "res://addons/beep_ui/theme/preset_modern.gd",
	"SciFi": "res://addons/beep_ui/theme/preset_scifi.gd",
	"Cartoon": "res://addons/beep_ui/theme/preset_cartoon.gd",
	"Classic": "res://addons/beep_ui/theme/preset_classic.gd",
	"Desert": "res://addons/beep_ui/theme/preset_desert.gd",
	"OilGas": "res://addons/beep_ui/theme/preset_oilgas.gd",
	"Sea": "res://addons/beep_ui/theme/preset_sea.gd",
	"Sports": "res://addons/beep_ui/theme/preset_sports.gd",
	"Soccer": "res://addons/beep_ui/theme/preset_soccer.gd",
	"Fantasy": "res://addons/beep_ui/theme/preset_fantasy.gd",
	"Horror": "res://addons/beep_ui/theme/preset_horror.gd",
	"Nature": "res://addons/beep_ui/theme/preset_nature.gd",
	"Space": "res://addons/beep_ui/theme/preset_space.gd",
	"Military": "res://addons/beep_ui/theme/preset_military.gd",
	"Steampunk": "res://addons/beep_ui/theme/preset_steampunk.gd",
	"Retro80s": "res://addons/beep_ui/theme/preset_retro80s.gd",
	"Pixel8Bit": "res://addons/beep_ui/theme/preset_pixel8bit.gd",
	"Winter": "res://addons/beep_ui/theme/preset_winter.gd",
	"Cyberpunk": "res://addons/beep_ui/theme/preset_cyberpunk.gd",
	"Japan": "res://addons/beep_ui/theme/preset_japan.gd",
	"Toxic": "res://addons/beep_ui/theme/preset_toxic.gd",
	"Candy": "res://addons/beep_ui/theme/preset_candy.gd",
}

static func preset_names() -> PackedStringArray:
	return PackedStringArray(_PRESET_SCRIPTS.keys())

static func get_preset(p_name: String) -> BeepPreset:
	if not _PRESET_SCRIPTS.has(p_name):
		push_warning("[Beep UI] Unknown preset: %s" % p_name)
		return null
	var sc: GDScript = load(_PRESET_SCRIPTS[p_name])
	if sc == null:
		push_warning("[Beep UI] Could not load preset script: %s" % p_name)
		return null
	return sc.new()
