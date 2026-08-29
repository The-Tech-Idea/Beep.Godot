extends SceneTree

const WEATHER_SYSTEM_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/WeatherSystemComponent.cs")
const DYNAMIC_FOG_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/DynamicFogLayer.cs")
const WEATHER_AUDIO_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/WeatherAudioController.cs")
const AMBIENT_AUDIO_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/AmbientAudioComponent.cs")
const SHELTER_ZONE_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/ShelterZoneComponent.cs")
const DAY_NIGHT_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/DayNightCycleComponent.cs")
const SEASONAL_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/SeasonalComponent.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	await _check_weather_system_lazy_setup()
	await _check_fog_lazy_setup()
	await _check_weather_audio_lazy_setup()
	await _check_ambient_audio_lazy_setup()
	await _check_ambient_audio_public_calls_before_setup()
	await _check_shelter_zone_overlap_refcount()
	await _check_invalid_time_tuning_is_bounded()
	print("[weather-lifecycle] OK: inactive atmosphere components defer setup, audio public calls are guarded, shelter zones aggregate overlap, and time tuning is bounded.")
	quit(0)

func _check_weather_system_lazy_setup() -> void:
	var world := Node2D.new()
	world.name = "World"
	var weather := WEATHER_SYSTEM_SCRIPT.new()
	weather.name = "Weather"
	weather.set("IsActive", false)
	world.add_child(weather)
	root.add_child(world)
	await process_frame
	await process_frame

	if world.get_node_or_null("WeatherParticles") != null:
		_fail("Inactive WeatherSystemComponent created WeatherParticles.")
	if world.get_node_or_null("WeatherOverlayLayer") != null:
		_fail("Inactive WeatherSystemComponent created WeatherOverlayLayer.")

	weather.set("IsActive", true)
	await process_frame
	await process_frame

	if world.get_node_or_null("WeatherParticles") == null:
		_fail("Activated WeatherSystemComponent did not create WeatherParticles.")
	if world.get_node_or_null("WeatherOverlayLayer") == null:
		_fail("Activated WeatherSystemComponent did not create WeatherOverlayLayer.")

	root.remove_child(world)
	world.free()

func _check_fog_lazy_setup() -> void:
	var fog := DYNAMIC_FOG_SCRIPT.new()
	fog.name = "Fog"
	fog.set("IsActive", false)
	root.add_child(fog)
	await process_frame
	await process_frame

	if fog.get_node_or_null("FogCanvasLayer") != null:
		_fail("Inactive DynamicFogLayer created FogCanvasLayer.")

	fog.set("IsActive", true)
	await process_frame
	await process_frame

	if fog.get_node_or_null("FogCanvasLayer") == null:
		_fail("Activated DynamicFogLayer did not create FogCanvasLayer.")

	root.remove_child(fog)
	fog.free()

func _check_weather_audio_lazy_setup() -> void:
	var audio := WEATHER_AUDIO_SCRIPT.new()
	audio.name = "WeatherAudio"
	audio.set("IsActive", false)
	root.add_child(audio)
	await process_frame
	await process_frame

	if audio.get_node_or_null("RainPlayer") != null:
		_fail("Inactive WeatherAudioController created audio players.")

	audio.set("IsActive", true)
	await process_frame
	await process_frame

	if audio.get_node_or_null("RainPlayer") == null:
		_fail("Activated WeatherAudioController did not create RainPlayer.")
	if audio.get_node_or_null("ThunderPlayer") == null:
		_fail("Activated WeatherAudioController did not create ThunderPlayer.")

	root.remove_child(audio)
	audio.free()

func _check_ambient_audio_lazy_setup() -> void:
	var area := Area2D.new()
	area.name = "AmbientZone"
	var audio := AMBIENT_AUDIO_SCRIPT.new()
	audio.name = "AmbientAudio"
	audio.set("IsActive", false)
	area.add_child(audio)
	root.add_child(area)
	await process_frame
	await process_frame

	if audio.get_node_or_null("AmbientPlayer") != null:
		_fail("Inactive AmbientAudioComponent created audio players.")

	audio.set("IsActive", true)
	await process_frame
	await process_frame

	if audio.get_node_or_null("AmbientPlayer") == null:
		_fail("Activated AmbientAudioComponent did not create AmbientPlayer.")
	if audio.get_node_or_null("ThunderPlayer") == null:
		_fail("Activated AmbientAudioComponent did not create ThunderPlayer.")

	root.remove_child(area)
	area.free()

func _check_ambient_audio_public_calls_before_setup() -> void:
	var area := Area2D.new()
	area.name = "AmbientGuardZone"
	var audio := AMBIENT_AUDIO_SCRIPT.new()
	audio.name = "AmbientAudioGuard"
	audio.set("IsActive", false)
	audio.set("CombatTrack", AudioStreamGenerator.new())
	audio.set("CrossfadeDuration", -1.0)
	area.add_child(audio)
	root.add_child(area)
	await process_frame

	audio.EnterCombat()
	audio.ExitCombat()

	audio.set("IsActive", true)
	await process_frame
	await process_frame
	audio.EnterCombat()
	audio.ExitCombat()

	if audio.get_node_or_null("CombatPlayer") == null:
		_fail("Activated AmbientAudioComponent did not create players after guarded public calls.")

	audio.set("CombatTrack", null)
	root.remove_child(area)
	area.free()

func _check_shelter_zone_overlap_refcount() -> void:
	var world := Node2D.new()
	world.name = "ShelterWorld"

	var weather := WEATHER_SYSTEM_SCRIPT.new()
	weather.name = "Weather"
	weather.add_to_group("weather_system")
	weather.set("IsActive", false)
	world.add_child(weather)

	var area_a := Area2D.new()
	area_a.name = "ShelterA"
	var zone_a := SHELTER_ZONE_SCRIPT.new()
	zone_a.name = "ShelterZoneA"
	zone_a.set("WatchGroup", "players")
	area_a.add_child(zone_a)
	world.add_child(area_a)

	var area_b := Area2D.new()
	area_b.name = "ShelterB"
	var zone_b := SHELTER_ZONE_SCRIPT.new()
	zone_b.name = "ShelterZoneB"
	zone_b.set("WatchGroup", "players")
	area_b.add_child(zone_b)
	world.add_child(area_b)

	var player := Node2D.new()
	player.name = "Player"
	player.add_to_group("players")
	world.add_child(player)

	root.add_child(world)
	await process_frame

	area_a.emit_signal("body_entered", player)
	await process_frame
	if not weather.get("InsideShelter"):
		_fail("Entering one shelter did not mark weather as sheltered.")

	area_b.emit_signal("body_entered", player)
	await process_frame
	area_a.emit_signal("body_exited", player)
	await process_frame
	if not weather.get("InsideShelter"):
		_fail("Exiting one of two overlapping shelters cleared InsideShelter too early.")

	area_b.emit_signal("body_exited", player)
	await process_frame
	if weather.get("InsideShelter"):
		_fail("Exiting the final shelter did not clear InsideShelter.")

	root.remove_child(world)
	world.free()

func _check_invalid_time_tuning_is_bounded() -> void:
	var world := Node2D.new()
	world.name = "InvalidDayLengthWorld"

	var day := DAY_NIGHT_SCRIPT.new()
	day.name = "DayNight"
	day.set("IsActive", false)
	day.set("DayLengthSeconds", 0.0)
	day.set("TimeOfDay", 23.5)
	world.add_child(day)

	root.add_child(world)
	await process_frame
	day._process(1.0)
	await process_frame

	var hour := float(day.get("TimeOfDay"))
	if not is_finite(hour) or hour < 0.0 or hour >= 24.0:
		_fail("DayNightCycleComponent allowed invalid DayLengthSeconds to produce invalid TimeOfDay: " + str(hour))

	root.remove_child(world)
	world.free()

	var season_world := Node2D.new()
	season_world.name = "InvalidSeasonLengthWorld"

	var stable_day := DAY_NIGHT_SCRIPT.new()
	stable_day.name = "StableDayNight"
	stable_day.set("IsActive", false)
	stable_day.set("DayLengthSeconds", 999999.0)
	stable_day.set("TimeOfDay", 8.0)
	season_world.add_child(stable_day)

	var seasonal := SEASONAL_SCRIPT.new()
	seasonal.name = "Seasonal"
	seasonal.set("IsActive", true)
	seasonal.set("AutoCycle", true)
	seasonal.set("DaysPerSeason", 0.0)
	seasonal.set("TransitionDuration", 0.0)
	season_world.add_child(seasonal)

	root.add_child(season_world)
	await process_frame

	var initial_season := int(seasonal.get("CurrentSeason"))
	seasonal._process(1.0)
	await process_frame
	if int(seasonal.get("CurrentSeason")) != initial_season:
		_fail("SeasonalComponent cycled immediately when DaysPerSeason was zero.")

	root.remove_child(season_world)
	season_world.free()

func _fail(message: String) -> void:
	push_error("[weather-lifecycle] " + message)
	quit(1)
