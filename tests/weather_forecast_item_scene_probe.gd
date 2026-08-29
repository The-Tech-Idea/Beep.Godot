extends SceneTree

const WEATHER_FORECAST_UI_SCRIPT := preload("res://addons/beep_game_builder_cs/ecs/ui/WeatherForecastUI.cs")
const WEATHER_FORECAST_SCRIPT := preload("res://addons/beep_game_builder_cs/core/WeatherForecast.cs")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(520, 260)
	root.content_scale_size = root.size

	var forecast = WEATHER_FORECAST_UI_SCRIPT.new()
	forecast.name = "WeatherForecast"
	forecast.custom_minimum_size = Vector2(360, 190)
	forecast.set("GenerateControlsWhenPathsEmpty", false)
	forecast.set("StartCollapsed", false)
	forecast.set("ItemSize", Vector2(96, 72))
	forecast.set("ForecastData", WEATHER_FORECAST_SCRIPT.new())
	forecast.set("ForecastItemScene", _build_card_scene())

	var shell := VBoxContainer.new()
	shell.name = "WeatherRoot"
	var slide := Control.new()
	slide.name = "Slide"
	slide.clip_contents = true
	var cards := VBoxContainer.new()
	cards.name = "ForecastContainer"
	var toggle := Button.new()
	toggle.name = "WeatherToggle"

	slide.add_child(cards)
	shell.add_child(slide)
	shell.add_child(toggle)
	forecast.add_child(shell)
	root.add_child(forecast)

	await process_frame
	await process_frame
	await process_frame

	if forecast.UsesSceneControls() == false:
		return _fail("WeatherForecastUI did not bind the authored shell controls.")

	if cards.get_child_count() == 0:
		return _fail("WeatherForecastUI did not populate the authored ForecastContainer.")

	var row := cards.get_child(0)
	if row.get_child_count() == 0:
		return _fail("WeatherForecastUI created an empty forecast row.")

	var card := row.get_child(0)
	if card.name != "Day0":
		return _fail("ForecastItemScene instance was not configured with the day node name.")

	var day := _label_text(card, "Day")
	var weather := _label_text(card, "Weather")
	var temperature := _label_text(card, "Temperature")
	var wind := _label_text(card, "Wind")

	if day != "Day 1":
		return _fail("Authored card Day label was not bound: " + day)
	if weather == "":
		return _fail("Authored card Weather label was not bound.")
	if not temperature.ends_with("C"):
		return _fail("Authored card Temperature label was not bound: " + temperature)
	if not wind.begins_with("Wind "):
		return _fail("Authored card Wind label was not bound: " + wind)
	if toggle.text == "":
		return _fail("Weather toggle label was not refreshed.")

	root.remove_child(forecast)
	forecast.free()
	await process_frame

	print("[weather-forecast-item-scene] OK: WeatherForecastUI uses authored item scenes and binds card labels.")
	quit(0)

func _build_card_scene() -> PackedScene:
	var card := PanelContainer.new()
	card.name = "ForecastCardTemplate"
	var column := VBoxContainer.new()
	column.name = "Column"
	card.add_child(column)
	column.owner = card

	for label_name in ["Day", "Weather", "Temperature", "Wind"]:
		var label := Label.new()
		label.name = label_name
		column.add_child(label)
		label.owner = card

	var scene := PackedScene.new()
	var err := scene.pack(card)
	if err != OK:
		push_error("[weather-forecast-item-scene] failed to pack card scene: " + str(err))
	card.free()
	return scene

func _label_text(root_node: Node, label_name: String) -> String:
	var label := root_node.find_child(label_name, true, false) as Label
	if label == null:
		return ""
	return label.text

func _fail(message: String) -> void:
	push_error("[weather-forecast-item-scene] " + message)
	quit(1)
