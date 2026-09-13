extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_waterfall_v1/"

func _initialize() -> void:
	var errors = []
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	var scene = Node2D.new()
	scene.name = "IsometricWaterOnlyReview"
	var cases = []
	var row = 0
	for rise in manifest.rises:
		var texture = ImageTexture.create_from_image(Image.load_from_file(BASE+rise.sheet))
		var texture_file = BASE+str(rise.sheet).trim_suffix(".png")+".res"
		if ResourceSaver.save(texture,texture_file)!=OK: errors.append("Texture save failed")
		var frames = SpriteFrames.new()
		frames.remove_animation("default")
		for index in range(manifest.profiles.size()):
			var profile = manifest.profiles[index]
			frames.add_animation(profile.id)
			frames.set_animation_speed(profile.id,16.0/1.2)
			frames.set_animation_loop(profile.id,true)
			for frame in range(16):
				var region = AtlasTexture.new()
				region.atlas = load(texture_file)
				region.region = Rect2(index*96,frame*int(rise.frameSize[1]),96,int(rise.frameSize[1]))
				region.filter_clip = true
				frames.add_frame(profile.id,region)
		if ResourceSaver.save(frames,BASE+rise.spriteFrames)!=OK: errors.append("SpriteFrames save failed")
		for connector in rise.connectors:
			var column = 0
			for width in manifest.widthExamples:
				var assembly = Node2D.new()
				assembly.name = "%s_Rise%d_Width%d" % [connector.direction,rise.risePixels,width.width]
				assembly.position = Vector2(column,row)
				assembly.set_meta("terrain_missing",true)
				assembly.set_meta("rise_pixels",rise.risePixels)
				scene.add_child(assembly)
				assembly.owner = scene
				for index in range(width.sections.size()):
					var sprite = AnimatedSprite2D.new()
					sprite.name = "Section%d" % index
					sprite.sprite_frames = load(BASE+rise.spriteFrames)
					sprite.animation = str(connector.direction)+"."+str(width.sections[index])
					sprite.autoplay = sprite.animation
					sprite.centered = false
					sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
					sprite.position = Vector2(connector.acrossCellStep[0],connector.acrossCellStep[1])*index
					if connector.direction=="west_east": sprite.position.x += (int(width.width)-1)*32
					assembly.add_child(sprite)
					sprite.owner = scene
				cases.append({"node":str(assembly.name),"direction":connector.direction,"width":width.width,"risePixels":rise.risePixels})
				column += 96+(int(width.width)-1)*32+48
			row += int(rise.frameSize[1])+64+48
	var packed = PackedScene.new()
	var result = packed.pack(scene)
	if result==OK: result = ResourceSaver.save(packed,BASE+"water_only_review.tscn")
	if result!=OK: errors.append("Scene save failed")
	scene.free()
	manifest.showcase = "water_only_review.tscn"
	manifest.cases = cases
	var file = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest,"  "))
	print("ISOMETRIC WATERFALL PACKAGE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
