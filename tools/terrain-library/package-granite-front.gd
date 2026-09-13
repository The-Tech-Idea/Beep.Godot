extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/"
const WATER = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/"
const RIVER = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/square/river_sections.tres"
const GRASS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/runtime/surfaces_v1/grass_256.res"
const CONTACT = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_contact_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func own(parent: Node, child: Node, scene: Node) -> void:
	parent.add_child(child)
	child.owner = scene

func run() -> void:
	var texture = ImageTexture.create_from_image(Image.load_from_file(BASE+"runtime/front_wall_320.png"))
	check(ResourceSaver.save(texture,BASE+"runtime/front_wall_320.res")==OK,"Save cliff texture")
	var tiles = TileSet.new()
	tiles.tile_size = Vector2i(64,64)
	var atlas = TileSetAtlasSource.new()
	atlas.texture = load(BASE+"runtime/front_wall_320.res")
	atlas.texture_region_size = Vector2i(64,80)
	tiles.add_source(atlas,0)
	for index in range(5):
		atlas.create_tile(Vector2i(index,0))
	check(ResourceSaver.save(tiles,BASE+"runtime/front_wall.tres")==OK,"Save cliff regions")
	var scene = Node2D.new()
	scene.name = "RiverWaterfallLowerRiverCandidate"
	scene.set_meta("production_ready",false)
	scene.set_meta("visual_rise_pixels",64)
	scene.set_meta("missing_connections","Side faces, outer repeat seam, lake/sea outlet")
	for data in [["UpperGround",Vector2(0,-64),Vector2(320,128)],["LowerGround",Vector2(0,128),Vector2(320,128)]]:
		var ground = Sprite2D.new()
		ground.name = data[0]
		ground.texture = load(GRASS)
		ground.centered = false
		ground.position = data[1]
		ground.region_enabled = true
		ground.region_rect = Rect2(data[1],data[2])
		ground.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
		ground.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		own(scene,ground,scene)
	var cliff = Sprite2D.new()
	cliff.name = "StaticCliff"
	cliff.texture = load(BASE+"runtime/front_wall_320.res")
	cliff.centered = false
	cliff.position = Vector2(0,48)
	cliff.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	var mask = ImageTexture.create_from_image(Image.load_from_file(BASE+"runtime/grass_surface_mask.png"))
	check(ResourceSaver.save(mask,BASE+"runtime/grass_surface_mask.res")==OK,"Save separate grass mask")
	var cliff_material = ShaderMaterial.new()
	cliff_material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_masked_art_surface.gdshader")
	cliff_material.set_shader_parameter("surface_enabled",true)
	cliff_material.set_shader_parameter("surface_texture",load(GRASS))
	cliff_material.set_shader_parameter("surface_mask",load(BASE+"runtime/grass_surface_mask.res"))
	cliff.material = cliff_material
	own(scene,cliff,scene)
	var contact_texture = ImageTexture.create_from_image(Image.load_from_file(CONTACT+"runtime/contact_320.png"))
	check(ResourceSaver.save(contact_texture,CONTACT+"runtime/contact_320.res")==OK,"Save contact texture")
	var contact = Sprite2D.new()
	contact.name = "CliffBottomContact"
	contact.texture = load(CONTACT+"runtime/contact_320.res")
	contact.centered = false
	contact.position = Vector2(0,108)
	contact.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	contact.set_meta("nominal_contact_y",20)
	contact.set_meta("visual_rise_contribution",0)
	own(scene,contact,scene)
	var fall = AnimatedSprite2D.new()
	fall.name = "WaterfallWaterOnly"
	fall.sprite_frames = load(WATER+"water_rise_64.tres")
	fall.animation = "narrow"
	fall.autoplay = "narrow"
	fall.centered = false
	fall.position = Vector2(128,0)
	own(scene,fall,scene)
	var river_tiles: TileSet = load(RIVER)
	var river_atlas: TileSetAtlasSource = river_tiles.get_source(0)
	var frames = SpriteFrames.new()
	frames.remove_animation("default")
	frames.add_animation("flow")
	frames.set_animation_speed("flow",16.0/1.2)
	for frame in range(16):
		var region = AtlasTexture.new()
		region.atlas = river_atlas.texture
		region.region = Rect2(0,frame*256,64,64)
		region.filter_clip = true
		frames.add_frame("flow",region)
	for data in [["Upstream",-64],["Downstream",192]]:
		var river = AnimatedSprite2D.new()
		river.name = data[0]
		river.sprite_frames = frames
		river.animation = "flow"
		river.autoplay = "flow"
		river.centered = false
		river.position = Vector2(128,data[1])
		var material = ShaderMaterial.new()
		material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_mask_surface_cartoon.gdshader")
		material.set_shader_parameter("secondary_enabled",true)
		material.set_shader_parameter("secondary_texture",load(GRASS))
		material.set_shader_parameter("surface_u",Vector2(1.0/256,0))
		material.set_shader_parameter("surface_v",Vector2(0,1.0/256))
		river.material = material
		own(scene,river,scene)
	var plane = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd").new()
	plane.name = "SurfacePlane"
	plane.layer_paths.assign([NodePath("../StaticCliff"),NodePath("../Upstream"),NodePath("../Downstream")])
	own(scene,plane,scene)
	var packed = PackedScene.new()
	check(packed.pack(scene)==OK,"Pack connected route")
	check(ResourceSaver.save(packed,BASE+"river_waterfall_review.tscn")==OK,"Save connected route")
	scene.free()
	print("GRANITE ROUTE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
