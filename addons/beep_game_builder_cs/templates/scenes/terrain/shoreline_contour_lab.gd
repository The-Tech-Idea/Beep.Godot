extends Node2D

const ShorelineField = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainShorelineField.cs")
const CELL_PIXELS := 32.0

@onready var generator: Node = $Generator
@onready var surface: ColorRect = $Surface
@onready var camera: Camera2D = $Camera2D
@onready var controls: HBoxContainer = $HUD/Toolbar/Margin/Controls
@onready var width_input: SpinBox = controls.get_node("Width")
@onready var seed_input: SpinBox = controls.get_node("Seed")
@onready var form: OptionButton = controls.get_node("Landform")
@onready var generate_button: Button = controls.get_node("Generate")
@onready var status: Label = $HUD/Status

var field: RefCounted
var generating := false
var dragging := false
var generation_count := 0

func _ready() -> void:
	field = ShorelineField.new()
	form.select(1)
	generate_button.pressed.connect(generate)
	controls.get_node("Fit").pressed.connect(fit_map)
	width_input.value_changed.connect(set_beach_width)
	get_viewport().size_changed.connect(fit_map)
	call_deferred("generate")

func generate() -> void:
	if generating: return
	generating = true
	generate_button.disabled = true
	status.text = "Generating coastline..."
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var started := Time.get_ticks_msec()
	generator.set("Seed", int(seed_input.value))
	generator.set("Landform", form.selected)
	var texture: Texture2D = field.call("BuildGenerated", generator, 12)
	(surface.material as ShaderMaterial).set_shader_parameter("distance_map", texture)
	set_beach_width(width_input.value)
	surface.size = Vector2(generator.get("BoundsSize")) * CELL_PIXELS
	surface.show()
	fit_map()
	generation_count += 1
	status.text = "Seed %d  |  %d x %d cells  |  %d samples/cell  |  %d ms" % [
		int(seed_input.value), generator.get("BoundsSize").x, generator.get("BoundsSize").y,
		field.call("GetSampleDetail"), Time.get_ticks_msec() - started]
	generate_button.disabled = false
	generating = false

func set_beach_width(value: float) -> void:
	# Changing the inset never regenerates or changes the land/water mask.
	(surface.material as ShaderMaterial).set_shader_parameter("beach_width", value)

func fit_map() -> void:
	var viewport := get_viewport_rect().size
	var available := Vector2(maxf(1.0, viewport.x - 48.0), maxf(1.0, viewport.y - 140.0))
	var zoom_value := minf(available.x / maxf(1.0, surface.size.x), available.y / maxf(1.0, surface.size.y))
	camera.zoom = Vector2.ONE * zoom_value
	camera.position = surface.size * 0.5 - Vector2(0.0, 22.0 / zoom_value)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_MIDDLE:
			dragging = event.pressed
		elif event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			var before := get_global_mouse_position()
			var factor := 1.15 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.0 / 1.15
			camera.zoom = Vector2.ONE * clampf(camera.zoom.x * factor, 0.15, 6.0)
			camera.position += before - get_global_mouse_position()
	if event is InputEventMouseMotion and dragging:
		camera.position -= event.relative / camera.zoom

func _input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_MIDDLE and not event.pressed:
		dragging = false

func _process(delta: float) -> void:
	if get_viewport().gui_get_focus_owner() is LineEdit: return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_UP)))
	camera.position += direction * 650.0 * delta / camera.zoom
