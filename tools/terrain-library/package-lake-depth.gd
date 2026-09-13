extends SceneTree

const SOURCE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"
const MASKS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var provenance: Array = []
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		DirAccess.make_dir_recursive_absolute(folder)
		var scene = load(SOURCE+projection+"/lake_review.tscn").instantiate()
		var water: TileMapLayer = scene.get_node("Water")
		var tiles = water.tile_set.duplicate(true) as TileSet
		tiles.set_meta("terrain_library_role","lake_surface")
		var atlas = tiles.get_source(0) as TileSetAtlasSource
		var texture = ImageTexture.create_from_image(atlas.texture.get_image())
		check(ResourceSaver.save(texture,folder+"lake_ripples_16.res")==OK,"Save preserved lake ripple frames")
		atlas.texture = load(folder+"lake_ripples_16.res")
		check(ResourceSaver.save(tiles,folder+"lake_surface.tres")==OK,"Save lake surface")
		water.tile_set = load(folder+"lake_surface.tres")
		var material = ShaderMaterial.new()
		material.resource_local_to_scene = true
		material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_lake_depth.gdshader")
		material.set_shader_parameter("secondary_texture",water.material.get_shader_parameter("secondary_texture"))
		water.material = material
		var depth_tiles = load(MASKS+projection+"/sea_depth.tres").duplicate(true) as TileSet
		depth_tiles.set_meta("terrain_library_role","lake_depth")
		depth_tiles.set_terrain_name(0,0,"shallow_lake")
		var depth_atlas = depth_tiles.get_source(0) as TileSetAtlasSource
		var depth_texture = ImageTexture.create_from_image(depth_atlas.texture.get_image())
		check(ResourceSaver.save(depth_texture,folder+"depth_masks_16.res")==OK,"Save shared geometry masks locally")
		depth_atlas.texture = load(folder+"depth_masks_16.res")
		check(ResourceSaver.save(depth_tiles,folder+"lake_depth.tres")==OK,"Save native lake depth tiles")
		var depth = TileMapLayer.new()
		depth.name = "Depth"
		depth.tile_set = load(folder+"lake_depth.tres")
		depth.material = material.duplicate()
		depth.material.resource_local_to_scene = true
		depth.material.set_shader_parameter("depth_authoring_layer",true)
		depth.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		depth.collision_enabled = false
		depth.navigation_enabled = false
		depth.z_index = 1
		scene.add_child(depth)
		depth.owner = scene
		var shallow: Array[Vector2i] = []
		for cell in water.get_used_cells():
			var data = water.get_cell_tile_data(cell)
			if data!=null and data.terrain==0 and (cell.x<=3 or cell.y<=3 or cell.x>=8): shallow.append(cell)
		depth.set_cells_terrain_connect(shallow,0,0,false)
		var plane = scene.get_node("SurfacePlane")
		plane.layer_paths.append(NodePath("../Depth"))
		var binding = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainLakeDepthBinding.gd").new()
		binding.name = "CoastalDepthBinding"
		binding.water_path = NodePath("../Water")
		binding.depth_path = NodePath("../Depth")
		scene.add_child(binding)
		binding.owner = scene
		root.add_child(scene)
		check(plane.refresh().is_empty(),"Lake surface plane failed")
		check(binding.refresh().is_empty(),"Lake depth binding failed: "+binding.last_error)
		water.material.set_shader_parameter("depth_field_enabled",false)
		water.material.set_shader_parameter("depth_field_texture",null)
		water.material.set_shader_parameter("shore_field_texture",null)
		scene.name = "LakeDepthCandidate"
		scene.set_meta("pending","Visual approval, river/depth contacts, other banks, large-map cost and production integration")
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,folder+"lake_depth_review.tscn")==OK,"Save lake depth scene")
		provenance.append({"projection":projection,"rippleSource":SOURCE+projection+"/grass_lake_16.res","rippleSha256":FileAccess.get_sha256(SOURCE+projection+"/grass_lake_16.res"),"maskSource":MASKS+projection+"/depth_control_16.res","maskSha256":FileAccess.get_sha256(MASKS+projection+"/depth_control_16.res")})
		scene.free()
	var manifest = {"schemaVersion":1,"status":"technical_candidate","style":"cartoon","projections":["square","isometric"],"frames":16,"periodSeconds":1.2,"depthConfigurations":47,"motion":"preserved_local_lake_ripple_frames_no_sea_shader","placement":"lake_cells_including_grass_banks","sources":provenance,"scenes":["square/lake_depth_review.tscn","isometric/lake_depth_review.tscn"],"approvalEvidence":null,"pending":["visual_approval","river_depth_contacts","sand_rock_banks","large_map_cost","production_integration"]}
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	print("LAKE DEPTH PACKAGE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
