using Godot;
using System;

public static class BeepScriptGenerator
{
    public static string CreateTopDownPlayer(string path = "res://scripts/player/top_down_player.gd")
    {
        var code = @"extends CharacterBody2D
class_name TopDownPlayer

signal moved(direction: Vector2)
signal dashed
signal interacted

@export var speed: float = 220.0
@export var acceleration: float = 1800.0
@export var friction: float = 1400.0
@export var can_dash: bool = true
@export var dash_speed: float = 600.0
@export var dash_duration: float = 0.15
@export var dash_cooldown: float = 0.8

var _dash_timer: float = 0.0
var _dash_cooldown_timer: float = 0.0
var _dash_direction: Vector2 = Vector2.ZERO

func _physics_process(delta: float) -> void:
	_dash_cooldown_timer = max(0.0, _dash_cooldown_timer - delta)
	if _dash_timer > 0.0:
		_dash_timer -= delta
		velocity = _dash_direction * dash_speed
		move_and_slide()
		return
	var direction := Vector2(
		Input.get_axis(""move_left"", ""move_right""),
		Input.get_axis(""move_up"", ""move_down"")
	)
	if direction.length() > 0.0:
		velocity = velocity.move_toward(direction.normalized() * speed, acceleration * delta)
		moved.emit(direction)
	else:
		velocity = velocity.move_toward(Vector2.ZERO, friction * delta)
	move_and_slide()
	if can_dash and Input.is_action_just_pressed(""dash"") and _dash_cooldown_timer <= 0.0:
		_start_dash(direction if direction.length() > 0.0 else Vector2.RIGHT)
	if Input.is_action_just_pressed(""interact""):
		interacted.emit()

func _start_dash(dir: Vector2) -> void:
	_dash_direction = dir.normalized()
	_dash_timer = dash_duration
	_dash_cooldown_timer = dash_cooldown
	dashed.emit()
";
        BeepFileUtils.SafeWriteText(path, code, true); return path;
    }

    public static string CreatePlatformerPlayer(string path = "res://scripts/player/platformer_player.gd")
    {
        var code = @"extends CharacterBody2D
class_name PlatformerPlayer

signal moved(direction: float)
signal jumped
signal landed

@export var speed: float = 300.0
@export var jump_velocity: float = -480.0
@export var gravity: float = 1200.0
@export var coyote_time: float = 0.08
@export var jump_buffer_time: float = 0.1
@export var acceleration: float = 1600.0
@export var friction: float = 1200.0

var _coyote_timer: float = 0.0
var _jump_buffer_timer: float = 0.0
var _was_on_floor: bool = false

func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y += gravity * delta
		_coyote_timer -= delta
	else:
		_coyote_timer = coyote_time
	_jump_buffer_timer -= delta
	if Input.is_action_just_pressed(""jump""):
		_jump_buffer_timer = jump_buffer_time
	if _jump_buffer_timer > 0.0 and _coyote_timer > 0.0:
		velocity.y = jump_velocity
		_coyote_timer = 0.0
		_jump_buffer_timer = 0.0
		jumped.emit()
	if Input.is_action_just_released(""jump"") and velocity.y < 0.0:
		velocity.y *= 0.5
	var direction := Input.get_axis(""move_left"", ""move_right"")
	if direction != 0.0:
		velocity.x = move_toward(velocity.x, direction * speed, acceleration * delta)
		moved.emit(direction)
	else:
		velocity.x = move_toward(velocity.x, 0.0, friction * delta)
	_was_on_floor = is_on_floor()
	move_and_slide()
	if is_on_floor() and not _was_on_floor:
		landed.emit()
";
        BeepFileUtils.SafeWriteText(path, code, true); return path;
    }

    public static string CreateRobotNpc(string path = "res://scripts/npc/robot_npc.gd")
    {
        var code = @"extends CharacterBody2D
class_name RobotNPC

enum State { IDLE, PATROL, CHASE, DISABLED }

@export var move_speed: float = 80.0
@export var chase_speed: float = 140.0
@export var detection_range: float = 160.0
@export var patrol_points: Array[Vector2] = []
@export var idle_time: float = 2.0

var _state: State = State.IDLE
var _patrol_index: int = 0
var _idle_timer: float = 0.0
var _start_position: Vector2

func _ready() -> void:
	_start_position = global_position
	if patrol_points.is_empty():
		patrol_points = [_start_position + Vector2(100, 0), _start_position - Vector2(100, 0)]

func _physics_process(delta: float) -> void:
	match _state:
		State.IDLE:
			_idle_timer -= delta; velocity = Vector2.ZERO
			if _idle_timer <= 0.0: _set_state(State.PATROL)
		State.PATROL: _patrol_move(delta)
		State.CHASE: _chase_move(delta)
		State.DISABLED: velocity = Vector2.ZERO
	move_and_slide()

func _set_state(s: State) -> void: _state = s; if s == State.IDLE: _idle_timer = idle_time

func _patrol_move(_delta: float) -> void:
	if patrol_points.is_empty(): _set_state(State.IDLE); return
	var target := patrol_points[_patrol_index]
	velocity = (target - global_position).normalized() * move_speed
	if global_position.distance_to(target) < 8.0:
		_patrol_index = (_patrol_index + 1) % patrol_points.size()
		_set_state(State.IDLE)

func _chase_move(_delta: float) -> void: velocity = Vector2.ZERO

func on_target_detected(target: Node2D) -> void:
	if _state == State.DISABLED: return
	_set_state(State.CHASE)
	velocity = (target.global_position - global_position).normalized() * chase_speed

func disable() -> void: _set_state(State.DISABLED)
";
        BeepFileUtils.SafeWriteText(path, code, true); return path;
    }

    public static string CreateCameraFollow(string path = "res://scripts/player/camera_follow.gd")
    {
        var code = @"extends Camera2D
class_name CameraFollow

@export var target: NodePath
@export var follow_speed: float = 8.0
@export var limit_left: float = -10000.0
@export var limit_right: float = 10000.0
@export var limit_top: float = -10000.0
@export var limit_bottom: float = 10000.0

var _target_node: Node2D

func _ready() -> void:
	if not target.is_empty(): _target_node = get_node(target) as Node2D
	limit_left = limit_left; limit_right = limit_right; limit_top = limit_top; limit_bottom = limit_bottom

func _process(delta: float) -> void:
	if _target_node: global_position = global_position.lerp(_target_node.global_position, follow_speed * delta)

func shake(strength: float = 8.0, duration: float = 0.25) -> void:
	var original := offset
	var t := create_tween()
	for _i in range(6):
		t.tween_property(self, ""offset"", Vector2(randf_range(-strength, strength), randf_range(-strength, strength)), duration / 6.0)
	t.tween_property(self, ""offset"", original, 0.05)
";
        BeepFileUtils.SafeWriteText(path, code, true); return path;
    }

    public static string CreateSceneManager(string path = "res://scripts/managers/scene_manager.gd")
    {
        BeepFileUtils.SafeWriteText(path, @"extends Node
class_name SceneManager

func change_scene(scene_path: String) -> void: get_tree().change_scene_to_file(scene_path)
func reload_current_scene() -> void: get_tree().reload_current_scene()
func quit_game() -> void: get_tree().quit()
", true); return path;
    }

    public static string CreateSaveManager(string path = "res://scripts/managers/save_manager.gd")
    {
        BeepFileUtils.SafeWriteText(path, @"extends Node
class_name SaveManager

func save_json(file_name: String, data: Dictionary) -> void:
	var f := FileAccess.open(""user://"" + file_name, FileAccess.WRITE)
	if f == null: push_error(""SaveManager: could not write""); return
	f.store_string(JSON.stringify(data, ""\t"")); f.close()

func load_json(file_name: String) -> Dictionary:
	var full := ""user://"" + file_name
	if not FileAccess.file_exists(full): return {}
	var f := FileAccess.open(full, FileAccess.READ)
	if f == null: return {}
	var text := f.get_as_text(); f.close()
	var result = JSON.parse_string(text)
	return result if typeof(result) == TYPE_DICTIONARY else {}

func has_save(file_name: String) -> bool:
	return FileAccess.file_exists(""user://"" + file_name)

func delete_save(file_name: String) -> void:
	var full := ""user://"" + file_name
	if FileAccess.file_exists(full): DirAccess.remove_absolute(full)
", true); return path;
    }

    public static string CreateAudioManager(string path = "res://scripts/managers/audio_manager.gd")
    {
        BeepFileUtils.SafeWriteText(path, @"extends Node
class_name AudioManager

var _music_player: AudioStreamPlayer
var _sfx_volume_db: float = 0.0
var _music_volume_db: float = -6.0

func _ready() -> void: _music_player = AudioStreamPlayer.new(); add_child(_music_player)

func play_sfx(stream: AudioStream, volume_db: float = 0.0) -> AudioStreamPlayer:
	var p := AudioStreamPlayer.new(); add_child(p)
	p.stream = stream; p.volume_db = volume_db + _sfx_volume_db
	p.finished.connect(p.queue_free); p.play(); return p

func play_music(stream: AudioStream) -> void:
	if _music_player.stream == stream and _music_player.playing: return
	_music_player.stream = stream; _music_player.volume_db = _music_volume_db; _music_player.play()

func stop_music() -> void: _music_player.stop()
func set_music_volume(v: float) -> void: _music_volume_db = v; _music_player.volume_db = v
func set_sfx_volume(v: float) -> void: _sfx_volume_db = v
", true); return path;
    }

    // ══════════════════════════════════════════════════════════
    // New 2D gameplay templates (copied from template files)
    // ══════════════════════════════════════════════════════════

    private static string CopyTemplate(string templateName, string targetPath)
    {
        var src = $"res://addons/beep_game_builder_cs/templates/scripts/{templateName}";
        var dir = targetPath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
        using var srcFile = FileAccess.Open(src, FileAccess.ModeFlags.Read);
        if (srcFile == null) return $"Error: template not found: {src}";
        var content = srcFile.GetAsText();
        using var dstFile = FileAccess.Open(targetPath, FileAccess.ModeFlags.Write);
        dstFile?.StoreString(content);
        return targetPath;
    }

    public static string CreateEnemyPatrol(string path = "res://scripts/enemies/enemy_patrol.gd")
        => CopyTemplate("enemy_patrol.gd.template", path);

    public static string CreateEnemyChase(string path = "res://scripts/enemies/enemy_chase.gd")
        => CopyTemplate("enemy_patrol.gd.template", path); // patrol script includes chase

    public static string CreateHealthComponent(string path = "res://scripts/components/health_component.gd")
        => CopyTemplate("health_component.gd.template", path);

    public static string CreatePickupItem(string path = "res://scripts/items/pickup_item.gd")
        => CopyTemplate("pickup_item.gd.template", path);

    public static string CreateMovingPlatform(string path = "res://scripts/level/moving_platform.gd")
        => CopyTemplate("moving_platform.gd.template", path);

    public static string CreateCheckpoint(string path = "res://scripts/level/checkpoint.gd")
        => CopyTemplate("checkpoint.gd.template", path);

    public static string CreateGameManager(string path = "res://scripts/managers/game_manager.gd")
        => CopyTemplate("game_manager.gd.template", path);

    public static string CreateDoorSwitch(string path = "res://scripts/level/door_switch.gd")
        => CopyTemplate("door_switch.gd.template", path);

    public static string CreateTurret(string path = "res://scripts/enemies/turret.gd")
        => CopyTemplate("turret.gd.template", path);

    public static string CreateInventory(string path = "res://scripts/systems/inventory.gd")
        => CopyTemplate("inventory.gd.template", path);

    public static string CreateWeatherSystem(string path = "res://scripts/systems/weather_system.gd")
        => CopyTemplate("weather_system.gd.template", path);

    public static string CreateDayNightCycle(string path = "res://scripts/systems/day_night_cycle.gd")
        => CopyTemplate("day_night_cycle.gd.template", path);

    public static string CreateProjectileVariants(string path = "res://scripts/projectiles/projectile_variants.gd")
        => CopyTemplate("projectile_variants.gd.template", path);

    public static string CreateDialogSystem(string path = "res://scripts/systems/dialog_system.gd")
        => CopyTemplate("dialog_system.gd.template", path);

    public static string CreateObjectPool(string path = "res://scripts/systems/object_pool.gd")
        => CopyTemplate("object_pool.gd.template", path);

    public static string CreatePlatformerExtras(string path = "res://scripts/player/platformer_extras.gd")
        => CopyTemplate("platformer_extras.gd.template", path);

    public static string CreateScreenTransition(string path = "res://scripts/systems/screen_transition.gd")
        => CopyTemplate("screen_transition.gd.template", path);
}
