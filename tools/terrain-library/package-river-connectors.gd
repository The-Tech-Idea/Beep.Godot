extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_staging/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_connectors_v1/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	var tiles = load(BASE+"godot/river_current.tres")
	var script = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainConnectorProfile.gd")
	var directions = {"north":0,"east":1,"south":2,"west":3}
	var entries: Array = []
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	for entry in manifest.profiles:
		var endpoints = str(entry.id).split("_")
		for endpoint in range(2):
			var connector = script.new()
			var role = "inlet" if endpoint==0 else "outlet"
			connector.stable_id = "cartoon.square.river."+str(entry.id)+"."+role+".v1"
			connector.direction = directions[endpoints[endpoint]]
			connector.flow_role = 1 if endpoint==0 else 2
			connector.motion_role = 2
			connector.bank_profile = "river_current_v1"
			# Wet interior begins beyond the authored shoreline blend at distance 0.7.
			connector.opening_width_pixels = 2.0*(64.0*0.34-0.7)
			connector.frame_count = 16
			connector.period_seconds = 1.2
			connector.phase_group = "river_current_v1"
			connector.tiles = tiles
			connector.atlas_coords = Vector2i(entry.atlas[0],entry.atlas[1])
			var issues = connector.validate()
			if not issues.is_empty():
				push_error(str(issues))
				quit(1)
				return
			var file_name = str(entry.id)+"_"+role+".tres"
			if ResourceSaver.save(connector,OUTPUT+file_name)!=OK:
				quit(1)
				return
			entries.append({"id":connector.stable_id,"resource":file_name,"sourceProfile":entry.id,"status":"candidate"})
	var file = FileAccess.open(OUTPUT+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","source":BASE+"manifest.json","profiles":entries,"openingDefinition":"Analytic wet interior at signedDistance > 0.7; raster edge review still required.","pending":["Isometric equivalents","Wide channels","Junctions with explicit inflow/outflow","Authored river/lake/waterfall adapters","Raster opening verification","Visual approval"]},"  "))
	print("RIVER CONNECTORS PASSED: ",entries.size()," source-backed candidate ports")
	quit()
