extends SceneTree

const ORIGINAL = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/river_waterfall_lake_review.tscn"
const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/square/"
const DEPTH = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/square/"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func bind_native_clock(scene: Node2D,name: String) -> void:
	var old = scene.get_node(name) as AnimatedSprite2D
	var first = old.sprite_frames.get_frame_texture(old.animation,0) as AtlasTexture
	var second = old.sprite_frames.get_frame_texture(old.animation,1) as AtlasTexture
	check(first!=null and second!=null and not old.centered,"Route needs top-left atlas sprites")
	var stride = second.region.position-first.region.position
	for i in range(16):
		var frame = old.sprite_frames.get_frame_texture(old.animation,i) as AtlasTexture
		check(frame.atlas==first.atlas and frame.region.position==first.region.position+stride*i and frame.region.size==first.region.size,"Water animation atlas stride differs")
	var sprite = Sprite2D.new()
	sprite.name = old.name
	sprite.position = old.position
	sprite.z_index = old.z_index
	sprite.z_as_relative = old.z_as_relative
	sprite.centered = false
	sprite.texture = first
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var material = ShaderMaterial.new()
	material.resource_local_to_scene = true
	material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_native_clock_sprite.gdshader")
	material.set_shader_parameter("animation_atlas",first.atlas)
	material.set_shader_parameter("animation_origin",first.region.position)
	material.set_shader_parameter("animation_stride",stride)
	material.set_shader_parameter("animation_size",first.region.size)
	material.set_shader_parameter("animation_atlas_size",first.atlas.get_size())
	if old.material is ShaderMaterial:
		material.set_shader_parameter("secondary_enabled",true)
		material.set_shader_parameter("secondary_texture",old.material.get_shader_parameter("secondary_texture"))
	sprite.material = material
	var index = old.get_index()
	scene.remove_child(old)
	scene.add_child(sprite)
	scene.move_child(sprite,index)
	sprite.owner = scene
	old.free()

func run() -> void:
	DirAccess.make_dir_recursive_absolute(BASE+"square")
	var scene = load(ORIGINAL).instantiate()
	for name in ["Upstream","WaterfallWaterOnly","Downstream"]: bind_native_clock(scene,name)
	var reference = load(DEPTH+"lake_depth_review.tscn").instantiate()
	var lake: TileMapLayer = scene.get_node("LowerLake")
	lake.tile_set = load(LAKE+"lake_river_depth.tres")
	lake.material = reference.get_node("Water").material.duplicate()
	lake.material.resource_local_to_scene = true
	var clock_error = load("res://tools/terrain-library/connected-water-clock.gd").enable(lake)
	check(clock_error.is_empty(),clock_error)
	if not clock_error.is_empty(): quit(1); return
	var depth = TileMapLayer.new()
	depth.name = "LakeDepth"
	depth.tile_set = load(DEPTH+"lake_depth.tres")
	depth.material = reference.get_node("Depth").material.duplicate()
	depth.material.resource_local_to_scene = true
	depth.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	depth.navigation_enabled = false
	depth.collision_enabled = false
	scene.add_child(depth)
	depth.owner = scene
	var shallow: Array[Vector2i] = [Vector2i(2,5)]
	for y in range(5,9): shallow.append(Vector2i(1,y))
	depth.set_cells_terrain_connect(shallow,0,0,false)
	var binding = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainLakeDepthBinding.gd").new()
	binding.name = "LakeDepthBinding"
	binding.water_path = NodePath("../LowerLake")
	binding.depth_path = NodePath("../LakeDepth")
	scene.add_child(binding)
	binding.owner = scene
	scene.get_node("SurfacePlane").layer_paths.append(NodePath("../LakeDepth"))
	root.add_child(scene)
	check(scene.get_node("SurfacePlane").refresh().is_empty(),"Route surface binding failed")
	check(binding.refresh().is_empty(),"Route lake depth rejected: "+binding.last_error)
	lake.material.set_shader_parameter("depth_field_enabled",false)
	lake.material.set_shader_parameter("depth_field_texture",null)
	lake.material.set_shader_parameter("shore_field_texture",null)
	scene.name = "DepthAwareWaterfallLakeRoute"
	scene.set_meta("pending","Isometric depth route, wider routes, other banks, visual approval and production integration")
	var packed = PackedScene.new()
	check(packed.pack(scene)==OK and ResourceSaver.save(packed,BASE+"square/route.tscn")==OK,"Save depth-aware waterfall route")
	var manifest = {"schemaVersion":1,"status":"technical_candidate","style":"cartoon","projection":"square","scene":"square/route.tscn","visualRisePixels":64,"sourceScene":ORIGINAL,"sourceSceneSha256":FileAccess.get_sha256(ORIGINAL),"retainedArtwork":["UpperGround","LowerGround","StaticCliff","CliffBottomContact","WaterfallWaterOnly","Upstream","Downstream"],"spriteRendering":"original_atlas_frames_with_native_renderer_TIME_clock","route":["upper_river","waterfall","lower_river_sprite","lower_river_native_tile","lake_inlet","depth_painted_lake"],"approvalEvidence":null,"pending":["isometric_depth_route","wide_routes","sand_rock_banks","visual_approval","production_integration"]}
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	reference.free()
	scene.free()
	print("DEPTH WATERFALL ROUTE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
