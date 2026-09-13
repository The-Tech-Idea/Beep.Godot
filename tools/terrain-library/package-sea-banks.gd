extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_banks_v1/"
const LAKE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"

func _initialize() -> void:
	var errors = []
	for projection in ["square","isometric"]:
		var scene = load(LAKE+projection+"/lake_review.tscn").instantiate()
		scene.name = "SeaCoastCandidate"
		var layer: TileMapLayer = scene.get_node("Water")
		var tiles: TileSet = layer.tile_set.duplicate(true)
		tiles.set_terrain_name(0,0,"sea")
		var texture = ImageTexture.create_from_image(Image.load_from_file(BASE+projection+"/grass_sea_16.png"))
		var texture_file = BASE+projection+"/grass_sea_16.res"
		if ResourceSaver.save(texture,texture_file)!=OK: errors.append("Sea texture save failed")
		var atlas: TileSetAtlasSource = tiles.get_source(0)
		atlas.texture = load(texture_file)
		if ResourceSaver.save(tiles,BASE+projection+"/grass_sea.tres")!=OK: errors.append("Sea TileSet save failed")
		layer.tile_set = load(BASE+projection+"/grass_sea.tres")
		layer.clear()
		var water: Array[Vector2i] = []
		# Extend the ocean beyond the review camera instead of drawing a closed pond.
		for y in range(-8,21):
			for x in range(-2,25):
				var cell = Vector2i(x,y)
				layer.set_cell(cell,0,Vector2i(7,5),0)
				var land = x<4 or (x<7 and y>=3 and y<=5) or (x>=9 and x<=10 and y>=7 and y<=8)
				if not land: water.append(cell)
		layer.set_cells_terrain_connect(water,0,0,false)
		for cell in layer.get_used_cells():
			if layer.get_cell_tile_data(cell)==null: errors.append("Invalid native coastline tile")
		scene.set_meta("production_ready",false)
		scene.set_meta("pending","Visual surf approval, estuary connectors, depth boundaries, sand and rock coasts")
		var packed = PackedScene.new()
		var result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+projection+"/sea_review.tscn")
		if result!=OK: errors.append("Sea scene save failed")
		scene.free()
	print("SEA BANK PACKAGE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
