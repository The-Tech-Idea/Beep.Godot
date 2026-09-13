extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_connectors_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	check(manifest.profiles.size()==24,"Expected 24 explicit ports")
	for entry in manifest.profiles:
		var connector = load(BASE+str(entry.resource))
		check(connector.validate().is_empty(),"Invalid saved connector "+str(entry.id))
	var output = load(BASE+"north_south_outlet.tres")
	var input = load(BASE+"north_south_inlet.tres")
	check(output.connection_issues(input).is_empty(),"Compatible straight ports rejected")
	for change in [{"opening_width_pixels":84.24},{"elevation_pixels":32},{"direction":1},{"flow_role":2},{"motion_role":1},{"phase_group":"different"},{"atlas_coords":Vector2i(100,100)},{"period_seconds":0.0}]:
		var altered = input.duplicate()
		for key in change:
			altered.set(key,change[key])
		check(not output.connection_issues(altered).is_empty(),"Incompatible connector accepted: "+str(change))
	check(not output.connection_issues(null).is_empty(),"Missing connector accepted")
	var source = JSON.parse_string(FileAccess.get_file_as_string("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_staging/manifest.json"))
	var placements: Array = []
	for cell in source.map:
		placements.append({"cell":Vector2i(cell.x,cell.y),"ports":[load(BASE+str(cell.profile)+"_inlet.tres"),load(BASE+str(cell.profile)+"_outlet.tres")]})
	var validator = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainFlowConnections.gd")
	var endpoints = {Vector2i(2,0):[0],Vector2i(7,8):[2]}
	check(validator.validate_route(placements,endpoints).is_empty(),"Existing bent river route rejected")
	check(not validator.validate_route(placements).is_empty(),"Unspecified endpoints guessed")
	placements.remove_at(4)
	check(not validator.validate_route(placements,endpoints).is_empty(),"Missing river segment accepted")
	print("CONNECTOR PROBE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
