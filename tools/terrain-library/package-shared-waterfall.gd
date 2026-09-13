extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/"

func _initialize() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"projections_manifest.json"))
	var errors = []
	var parameters = JSON.parse_string(FileAccess.get_file_as_string(BASE+"motion_parameters.json"))
	var noise = Image.create(64,8,false,Image.FORMAT_RF)
	for salt in range(8):
		for lane in range(64): noise.set_pixel(lane,salt,Color(parameters.noise[salt][lane],0,0,1))
	if ResourceSaver.save(ImageTexture.create_from_image(noise),BASE+"motion_noise.res")!=OK: errors.append("Save deterministic motion noise")
	for entry in manifest.exports:
		var image = Image.load_from_file(BASE+entry.sheet)
		var size = Vector2i(entry.frameSize[0],entry.frameSize[1])
		if image.get_size()!=Vector2i(size.x*entry.profiles.size(),size.y*16):
			errors.append("Atlas dimensions: "+entry.sheet)
			continue
		var texture_path = BASE+str(entry.sheet).trim_suffix(".png")+".res"
		if ResourceSaver.save(ImageTexture.create_from_image(image),texture_path)!=OK:
			errors.append("Save texture: "+texture_path)
			continue
		var frames = SpriteFrames.new()
		frames.remove_animation("default")
		for profile in entry.profiles:
			frames.add_animation(profile.id)
			frames.set_animation_speed(profile.id,16.0/1.2)
			frames.set_animation_loop(profile.id,true)
			for index in range(16):
				var texture = AtlasTexture.new()
				texture.atlas = load(texture_path)
				texture.region = Rect2(Vector2(profile.column*size.x,index*size.y),Vector2(size))
				texture.filter_clip = true
				frames.add_frame(profile.id,texture)
		var path = str(entry.projection)+"_rise_"+str(entry.risePixels)+".tres"
		if ResourceSaver.save(frames,BASE+path)!=OK: errors.append("Save frames: "+path)
		var reloaded = ResourceLoader.load(BASE+path,"",ResourceLoader.CACHE_MODE_IGNORE) as SpriteFrames
		if reloaded==null or reloaded.get_animation_names().size()!=entry.profiles.size(): errors.append("Reload frames: "+path)
		entry.spriteFrames = path
	manifest.nativePackaging = {"status":"passed" if errors.is_empty() else "failed","errors":errors,"gpuReview":"pending"}
	if errors.is_empty():
		var file = FileAccess.open(BASE+"projections_manifest.json",FileAccess.WRITE)
		file.store_string(JSON.stringify(manifest,"  "))
	for error in errors: push_error(error)
	print("SHARED WATERFALL RESOURCES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
