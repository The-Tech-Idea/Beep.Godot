extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/"
const TEMPLATE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_plateaus_v1/plateau_1x1.tscn"

func _initialize() -> void:
	var errors = []
	var scene = load(TEMPLATE).instantiate()
	scene.name = "GraniteLongFaceReview"
	var walls: TileMapLayer = scene.get_node("CliffWalls")
	var top: TileMapLayer = scene.get_node("PlateauSurface")
	walls.clear()
	top.clear()
	for y in range(4):
		for x in range(4): top.set_cell(Vector2i(x,y),0,Vector2i(6,5),0)
	for face in ["light","shade"]:
		var image = Image.load_from_file(BASE+"runtime/"+face+"_span.png")
		if image==null: errors.append("Missing face image"); continue
		var texture = ImageTexture.create_from_image(image)
		if ResourceSaver.save(texture,BASE+"runtime/"+face+"_span.res")!=OK: errors.append("Texture save failed")
		var group = Node2D.new()
		group.name = face.capitalize()+"Face"
		group.position = walls.map_to_local(Vector2i(0,3))+Vector2(-32,-64) if face=="light" else walls.map_to_local(Vector2i(3,0))+Vector2(-96,-64)
		scene.add_child(group)
		group.owner = scene
		for index in range(4):
			var offset = Vector2(index*32,index*16 if face=="light" else (3-index)*16)
			var region = AtlasTexture.new()
			region.atlas = load(BASE+"runtime/"+face+"_span.res")
			region.region = Rect2(offset,Vector2(32,80))
			region.filter_clip = true
			var file = BASE+"runtime/%s_module_%d.tres" % [face,index]
			if ResourceSaver.save(region,file)!=OK: errors.append("Module save failed")
			var sprite = Sprite2D.new()
			sprite.name = "Module%d" % index
			sprite.texture = load(file)
			sprite.centered = false
			sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			sprite.position = offset
			group.add_child(sprite)
			sprite.owner = scene
	scene.set_meta("dimensions_cells",Vector2i(4,4))
	scene.set_meta("missing_artwork","Corner and bottom contact refinement; outer end repeat not validated")
	var packed = PackedScene.new()
	var result = packed.pack(scene)
	if result==OK: result = ResourceSaver.save(packed,BASE+"faces_review.tscn")
	if result!=OK: errors.append("Scene save failed")
	scene.free()
	print("ISOMETRIC GRANITE FACES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
