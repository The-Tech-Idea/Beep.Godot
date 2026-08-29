extends SceneTree

const WEATHER_FORECAST_SCRIPT := preload("res://addons/beep_game_builder_cs/core/WeatherForecast.cs")
const WEATHER_SPRITE_LAYER_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/atmosphere/WeatherSpriteLayer.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	_check_forecast_values()
	await _check_sprite_filtering()
	print("[weather-behavior] OK: forecast severity and weather sprite filtering are coherent.")
	quit(0)

func _check_forecast_values() -> void:
	var forecast := WEATHER_FORECAST_SCRIPT.new()
	forecast.set("RandomSeed", 9001)
	forecast.set("PerlinNoiseScale", 0.16)
	forecast.set("BaseTemperature", 24.0)
	forecast.set("TemperatureVariance", 22.0)

	var known := {
		"Clear": true,
		"Cloudy": true,
		"Rain": true,
		"Snow": true,
		"Storm": true,
		"Fog": true,
		"Sandstorm": true,
		"Hail": true,
		"LeafFall": true,
		"Heatwave": true,
	}

	for start_day in range(0, 120, 7):
		forecast.GenerateForecast(start_day)
		for day in forecast.get("DaysForward"):
			var kind := str(day.get("WeatherType"))
			var intensity := float(day.get("Intensity"))
			var wind := float(day.get("WindSpeed"))
			if not known.has(kind):
				_fail("Unknown weather type in forecast: " + kind)
			if intensity < 0.0 or intensity > 1.0:
				_fail("Forecast intensity outside 0..1 for " + kind + ": " + str(intensity))
			if wind < 0.0 or wind > 12.0:
				_fail("Forecast wind outside expected 0..12 range for " + kind + ": " + str(wind))
			if kind == "Clear" and intensity > 0.15:
				_fail("Clear forecast should be low severity, got " + str(intensity))
			if kind == "Storm" and intensity < 0.70:
				_fail("Storm forecast should be high severity, got " + str(intensity))

func _check_sprite_filtering() -> void:
	var layer := WEATHER_SPRITE_LAYER_SCRIPT.new() as Node2D
	layer.name = "WeatherSpriteLayer"
	root.add_child(layer)
	await process_frame

	if layer.texture_filter != CanvasItem.TEXTURE_FILTER_LINEAR:
		_fail("WeatherSpriteLayer should default to linear filtering for non-pixel art.")

	layer.set("UsePixelArtSampling", true)
	await process_frame
	if layer.texture_filter != CanvasItem.TEXTURE_FILTER_NEAREST:
		_fail("WeatherSpriteLayer did not switch to nearest filtering when pixel-art sampling was enabled.")

	root.remove_child(layer)
	layer.free()

func _fail(message: String) -> void:
	push_error("[weather-behavior] " + message)
	quit(1)
