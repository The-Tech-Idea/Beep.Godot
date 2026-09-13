extends SceneTree

const SOURCE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_river_depth_v1/"
const PORT_NAMES = ["north","east","south","west"]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func route_cell(port: int,cross: int,along: int) -> Vector2i:
	if port==1: return Vector2i(9-along,cross)
	if port==2: return Vector2i(cross,9-along)
	if port==3: return Vector2i(along,cross)
	return Vector2i(cross,along)

func run() -> void:
	var scenes: Array = []
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		DirAccess.make_dir_recursive_absolute(folder)
		var tiles = load(SOURCE+projection+"/shared_sea.tres").duplicate(true) as TileSet
		tiles.set_meta("sea_depth_river_contacts_v1",true)
		check(ResourceSaver.save(tiles,folder+"sea_river_depth.tres")==OK,"Save opt-in sea contact tiles")
		for port in range(4):
			var scene = load(SOURCE+projection+"/mouth_widths_"+PORT_NAMES[port]+".tscn").instantiate()
			var water: TileMapLayer = scene.get_node("Water")
			water.tile_set = load(folder+"sea_river_depth.tres")
			var depth = TileMapLayer.new()
			depth.name = "Depth"
			depth.tile_set = load(SOURCE+projection+"/sea_depth.tres")
			depth.material = water.material.duplicate()
			depth.material.resource_local_to_scene = true
			depth.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			depth.collision_enabled = false
			depth.navigation_enabled = false
			depth.z_index = 1
			scene.add_child(depth)
			depth.owner = scene
			var cells: Array[Vector2i] = []
			for cross in range(-2,21):
				for along in range(4,10):
					if along<7 or cross<8: cells.append(route_cell(port,cross,along))
			depth.set_cells_terrain_connect(cells,0,0,false)
			var binding = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSeaRiverDepthBinding.gd").new()
			binding.name = "CoastalDepthBinding"
			binding.water_path = NodePath("../Water")
			binding.depth_path = NodePath("../Depth")
			scene.add_child(binding)
			binding.owner = scene
			var plane = scene.get_node("SurfacePlane")
			plane.layer_paths.append(NodePath("../Depth"))
			root.add_child(scene)
			check(plane.refresh().is_empty(),"Sea contact surface failed")
			check(binding.refresh().is_empty(),"Sea contact rejected: "+binding.last_error)
			water.material.set_shader_parameter("depth_field_enabled",false)
			water.material.set_shader_parameter("depth_field_texture",null)
			scene.name = "SeaRiverDepthWidths"
			scene.set_meta("pending","Visual approval, other banks, production integration and large-map field cost")
			var packed = PackedScene.new()
			var relative = projection+"/mouth_depth_"+PORT_NAMES[port]+".tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+relative)==OK,"Save sea depth contact scene")
			scenes.append(relative)
			scene.free()
	var manifest = {"schemaVersion":1,"status":"technical_candidate","style":"cartoon","projections":["square","isometric"],"ports":PORT_NAMES,"widthExamples":[1,2,3,5],"widthConstruction":"low_bank_repeatable_middle_high_bank","scenes":scenes,"frames":16,"periodSeconds":1.2,"sourceManifest":SOURCE+"manifest.json","sourceManifestSha256":FileAccess.get_sha256(SOURCE+"manifest.json"),"approvalEvidence":null,"pending":["visual_approval","sand_rock_banks","large_map_cost","production_engine_integration"]}
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	print("SEA RIVER DEPTH PACKAGE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
