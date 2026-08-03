extends CanvasLayer
class_name RainSplashSystem2D

## Reusable screen-space rain and synchronized splash system for Godot 4.x.
## Add rain_splash_system.tscn to a scene and tune the exported properties.

const RAIN_SHADER: Shader = preload("res://addons/rain_splash_2d/rain_splash.gdshader")

@export_group("Master")
@export_range(0.0, 1.0, 0.01) var intensity: float = 0.85
@export_range(0.0, 1.0, 0.01) var shelter_factor: float = 0.0
@export_range(0.0, 1.0, 0.01) var lightning_flash: float = 0.0
@export var overlay_layer: int = 100

@export_group("Rain")
@export var rain_color: Color = Color(0.68, 0.82, 1.0, 0.72)
@export_range(0.0, 1.0, 0.01) var rain_density: float = 0.72
@export_range(80.0, 1600.0, 1.0) var fall_speed: float = 760.0
## Horizontal displacement per vertical pixel. Negative values lean left.
@export_range(-1.25, 1.25, 0.01) var wind_slant: float = -0.18
@export_range(4.0, 120.0, 1.0) var streak_length: float = 34.0
@export_range(0.35, 6.0, 0.05) var streak_width: float = 1.15

@export_group("Splashes")
@export var splash_color: Color = Color(0.78, 0.9, 1.0, 0.8)
@export_range(0.0, 2.0, 0.01) var splash_strength: float = 1.0
@export_range(2.0, 48.0, 0.5) var splash_radius: float = 13.0
@export_range(0.0, 32.0, 0.5) var splash_height: float = 8.0
@export_range(1.0, 6.0, 0.1) var splash_flatness: float = 2.8
@export_range(0.0, 1.0, 0.01) var impact_y_min: float = 0.12
@export_range(0.0, 1.0, 0.01) var impact_y_max: float = 0.94

@export_group("Optional Splash Mask")
## White pixels allow splashes. Black pixels suppress them.
@export var splash_mask: Texture2D
@export var use_splash_mask: bool = false
@export_range(0.0, 1.0, 0.01) var splash_mask_threshold: float = 0.5
@export_range(0.001, 0.5, 0.001) var splash_mask_softness: float = 0.08

@export_group("Pixel Art")
@export var pixel_art_mode: bool = false
@export_range(1.0, 8.0, 1.0) var pixel_size: float = 1.0

var _overlay: ColorRect
var _material: ShaderMaterial
var _active_tween: Tween


func _ready() -> void:
    layer = overlay_layer
    _create_overlay()
    _sync_viewport_size()
    get_viewport().size_changed.connect(_sync_viewport_size)
    _sync_uniforms()


func _process(_delta: float) -> void:
    # Keeping these uniforms synchronized makes inspector changes visible while running.
    _sync_uniforms()


func _create_overlay() -> void:
    _overlay = ColorRect.new()
    _overlay.name = "RainSplashOverlay"
    _overlay.color = Color.WHITE
    _overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
    _overlay.focus_mode = Control.FOCUS_NONE

    _material = ShaderMaterial.new()
    _material.shader = RAIN_SHADER
    _overlay.material = _material
    add_child(_overlay)
    _overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


func _sync_viewport_size() -> void:
    if not is_instance_valid(_material):
        return

    var visible_size := get_viewport().get_visible_rect().size
    visible_size.x = max(visible_size.x, 1.0)
    visible_size.y = max(visible_size.y, 1.0)
    _material.set_shader_parameter("viewport_size", visible_size)


func _sync_uniforms() -> void:
    if not is_instance_valid(_material):
        return

    _material.set_shader_parameter("intensity", clampf(intensity, 0.0, 1.0))
    _material.set_shader_parameter("shelter_factor", clampf(shelter_factor, 0.0, 1.0))
    _material.set_shader_parameter("lightning_flash", clampf(lightning_flash, 0.0, 1.0))

    _material.set_shader_parameter("rain_color", rain_color)
    _material.set_shader_parameter("rain_density", clampf(rain_density, 0.0, 1.0))
    _material.set_shader_parameter("fall_speed", fall_speed)
    _material.set_shader_parameter("wind_slant", wind_slant)
    _material.set_shader_parameter("streak_length", streak_length)
    _material.set_shader_parameter("streak_width", streak_width)

    _material.set_shader_parameter("splash_color", splash_color)
    _material.set_shader_parameter("splash_strength", splash_strength)
    _material.set_shader_parameter("splash_radius", splash_radius)
    _material.set_shader_parameter("splash_height", splash_height)
    _material.set_shader_parameter("splash_flatness", splash_flatness)
    _material.set_shader_parameter("impact_y_min", impact_y_min)
    _material.set_shader_parameter("impact_y_max", impact_y_max)

    var mask_is_available := use_splash_mask and splash_mask != null
    _material.set_shader_parameter("use_splash_mask", mask_is_available)
    if splash_mask != null:
        _material.set_shader_parameter("splash_mask", splash_mask)
    _material.set_shader_parameter("splash_mask_threshold", splash_mask_threshold)
    _material.set_shader_parameter("splash_mask_softness", splash_mask_softness)

    _material.set_shader_parameter("pixel_art_mode", pixel_art_mode)
    _material.set_shader_parameter("pixel_size", pixel_size)


## Fades the complete rain system in or out.
func set_raining(enabled: bool, fade_seconds: float = 1.0) -> void:
    var target := 1.0 if enabled else 0.0
    set_intensity(target, fade_seconds)


## Smoothly changes rain intensity.
func set_intensity(value: float, fade_seconds: float = 1.0) -> void:
    var target := clampf(value, 0.0, 1.0)

    if is_instance_valid(_active_tween):
        _active_tween.kill()

    if fade_seconds <= 0.0:
        intensity = target
        return

    _active_tween = create_tween()
    _active_tween.set_trans(Tween.TRANS_SINE)
    _active_tween.set_ease(Tween.EASE_IN_OUT)
    _active_tween.tween_property(self, "intensity", target, fade_seconds)


## Use 1.0 indoors and 0.0 outdoors. Intermediate values create a soft transition.
func set_shelter(value: float, fade_seconds: float = 0.35) -> void:
    var target := clampf(value, 0.0, 1.0)

    if fade_seconds <= 0.0:
        shelter_factor = target
        return

    var tween := create_tween()
    tween.set_trans(Tween.TRANS_SINE)
    tween.set_ease(Tween.EASE_IN_OUT)
    tween.tween_property(self, "shelter_factor", target, fade_seconds)


## Creates a short screen flash without modifying the rain lifecycle.
func flash_lightning(strength: float = 1.0, duration: float = 0.32) -> void:
    lightning_flash = clampf(strength, 0.0, 1.0)
    var tween := create_tween()
    tween.set_trans(Tween.TRANS_EXPO)
    tween.set_ease(Tween.EASE_OUT)
    tween.tween_property(self, "lightning_flash", 0.0, maxf(duration, 0.01))


## Convenience API using a direction vector such as Vector2(-0.2, 1.0).
func set_wind_direction(direction: Vector2) -> void:
    if absf(direction.y) < 0.001:
        wind_slant = signf(direction.x) * 1.25
        return

    wind_slant = clampf(direction.x / absf(direction.y), -1.25, 1.25)
