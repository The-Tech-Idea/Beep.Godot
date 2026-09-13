extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_iso_corner_v1/"

func _initialize() -> void:
	var error = OK
	for name in ["corner_64","plateau_mask"]:
		var texture = ImageTexture.create_from_image(Image.load_from_file(BASE+"runtime/"+name+".png"))
		var saved = ResourceSaver.save(texture,BASE+"runtime/"+name+".res")
		if saved!=OK: error = saved
	var tiles = TileSet.new()
	tiles.tile_size = Vector2i(64,32)
	tiles.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tiles.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	var atlas = TileSetAtlasSource.new()
	atlas.texture = load(BASE+"runtime/corner_64.res")
	atlas.texture_region_size = Vector2i(64,96)
	tiles.add_source(atlas,0)
	atlas.create_tile(Vector2i.ZERO)
	atlas.get_tile_data(Vector2i.ZERO,0).texture_origin = Vector2i(0,32)
	var saved = ResourceSaver.save(tiles,BASE+"runtime/corner.tres")
	if saved!=OK: error = saved
	var scene = Node2D.new()
	scene.name = "IsometricGraniteCornerCandidate"
	scene.set_meta("production_ready",false)
	scene.set_meta("rise_pixels",64)
	var layer = TileMapLayer.new()
	layer.name = "Corner"
	layer.tile_set = load(BASE+"runtime/corner.tres")
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	layer.set_cell(Vector2i.ZERO,0,Vector2i.ZERO,0)
	var material = ShaderMaterial.new()
	material.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_masked_art_surface.gdshader")
	material.set_shader_parameter("surface_enabled",true)
	material.set_shader_parameter("surface_texture",load("res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/runtime/surfaces_v1/grass_256.res"))
	material.set_shader_parameter("surface_mask",load(BASE+"runtime/plateau_mask.res"))
	layer.material = material
	scene.add_child(layer)
	layer.owner = scene
	var plane = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd").new()
	plane.name = "SurfacePlane"
	plane.projection = 1
	plane.cell_size = Vector2(64,32)
	plane.position.y = -64
	plane.layer_paths.assign([NodePath("../Corner")])
	scene.add_child(plane)
	plane.owner = scene
	var packed = PackedScene.new()
	saved = packed.pack(scene)
	if saved==OK: saved = ResourceSaver.save(packed,BASE+"corner_review.tscn")
	if saved!=OK: error = saved
	scene.free()
	print("ISOMETRIC CORNER ","PASSED" if error==OK else "FAILED")
	quit(0 if error==OK else 1)
