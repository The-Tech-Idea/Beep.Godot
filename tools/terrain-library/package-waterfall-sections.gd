extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	var scene = Node2D.new()
	scene.name = "WaterOnlyConnectorReview"
	scene.set_meta("terrain_artwork_missing",true)
	var row = 0
	for entry in manifest.rises:
		var image = Image.load_from_file(BASE+entry.sheet)
		var texture = ImageTexture.create_from_image(image)
		var texture_path = BASE+entry.sheet.trim_suffix(".png")+".res"
		check(ResourceSaver.save(texture,texture_path)==OK,"Save water texture")
		var frames = SpriteFrames.new()
		frames.remove_animation("default")
		for kind in range(manifest.sections.size()):
			var animation = str(manifest.sections[kind])
			frames.add_animation(animation)
			frames.set_animation_speed(animation,16.0/1.2)
			frames.set_animation_loop(animation,true)
			for index in range(16):
				var region = AtlasTexture.new()
				region.atlas = load(texture_path)
				region.region = Rect2(kind*64,index*int(entry.frameSize[1]),64,int(entry.frameSize[1]))
				region.filter_clip = true
				frames.add_frame(animation,region)
		check(ResourceSaver.save(frames,BASE+entry.spriteFrames)==OK,"Save water SpriteFrames")
		var column = 0
		for example in manifest.widthExamples:
			var assembly = Node2D.new()
			assembly.name = "Rise%dWidth%d" % [entry.risePixels,example.width]
			assembly.position = Vector2(column,row)
			assembly.set_meta("rise_pixels",entry.risePixels)
			assembly.set_meta("upstream_elevation_pixels",entry.risePixels)
			assembly.set_meta("downstream_elevation_pixels",0)
			assembly.set_meta("terrain_artwork_missing",true)
			scene.add_child(assembly)
			assembly.owner = scene
			for index in range(example.sections.size()):
				var sprite = AnimatedSprite2D.new()
				sprite.name = "Section%d" % index
				sprite.sprite_frames = load(BASE+entry.spriteFrames)
				sprite.animation = str(example.sections[index])
				sprite.autoplay = sprite.animation
				sprite.centered = false
				sprite.position.x = index*64
				sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
				assembly.add_child(sprite)
				sprite.owner = scene
			column += int(example.width)*64+48
		row += int(entry.frameSize[1])+48
	var packed = PackedScene.new()
	check(packed.pack(scene)==OK,"Pack waterfall review")
	check(ResourceSaver.save(packed,BASE+"water_only_review.tscn")==OK,"Save waterfall review")
	scene.free()
	print("WATERFALL SECTIONS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
