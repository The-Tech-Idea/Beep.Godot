extends "res://tests/terrain_coastal_depth_render_probe.gd"

const SHARED = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/"

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(SHARED+"projections_manifest.json"))
	var results = []
	var procedural = "--procedural" in OS.get_cmdline_user_args()
	var parameters = JSON.parse_string(FileAccess.get_file_as_string(SHARED+"motion_parameters.json")) if procedural else {}
	for entry in manifest.exports:
		for connector in entry.connectors:
			for width in [1,2,3,5]:
				var viewport = SubViewport.new()
				viewport.size = Vector2i(512,384)
				viewport.transparent_bg = true
				viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
				root.add_child(viewport)
				var frames = load(SHARED+entry.spriteFrames) as SpriteFrames
				var sprites = []
				var first_alpha = PackedByteArray()
				var previous = PackedByteArray()
				var mismatch = 0
				var alpha_changes = 0
				var changes = 0
				for section in range(width):
					var kind = "narrow" if width==1 else ("low_bank" if section==0 else ("high_bank" if section==width-1 else "middle"))
					var name = "%s.%s.offset_%d"%[connector.direction,kind,section]
					check(frames.has_animation(name) and frames.get_frame_count(name)==16,"Missing shared animation")
					check(abs(frames.get_animation_speed(name)-16.0/1.2)<0.001 and frames.get_animation_loop(name),"Animation timing")
					var sprite = Sprite2D.new()
					sprite.centered = false
					sprite.texture = frames.get_frame_texture(name,0)
					sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
					var step = Vector2(64,0) if entry.projection=="square" else Vector2(32 if connector.direction=="north_south" else -32,16)
					sprite.position = Vector2(160 if entry.projection=="isometric" else 48,32)+step*section
					var texture = sprite.texture as AtlasTexture
					var second = frames.get_frame_texture(name,1) as AtlasTexture
					var m = ShaderMaterial.new()
					m.shader = load("res://addons/beep_game_builder_cs/shaders/terrain_native_clock_sprite.gdshader")
					m.set_shader_parameter("animation_atlas",texture.atlas)
					m.set_shader_parameter("animation_origin",texture.region.position)
					m.set_shader_parameter("animation_stride",second.region.position-texture.region.position)
					m.set_shader_parameter("animation_size",texture.region.size)
					m.set_shader_parameter("animation_atlas_size",texture.atlas.get_size())
					if procedural:
						m.set_shader_parameter("shared_waterfall_enabled",true)
						m.set_shader_parameter("waterfall_noise",load(SHARED+"motion_noise.res"))
						m.set_shader_parameter("waterfall_base_bytes",Vector3(parameters.baseColorBytes[0],parameters.baseColorBytes[1],parameters.baseColorBytes[2]))
						m.set_shader_parameter("waterfall_offset",float(section*64))
						m.set_shader_parameter("waterfall_rise",float(entry.risePixels))
						m.set_shader_parameter("waterfall_kind",["narrow","low_bank","middle","high_bank"].find(kind))
						m.set_shader_parameter("waterfall_projection",0 if entry.projection=="square" else (1 if connector.direction=="north_south" else 2))
					sprite.material = m
					viewport.add_child(sprite)
					sprites.append({"sprite":sprite,"animation":name})
				for frame in range(16):
					var expected = Image.create(512,384,false,Image.FORMAT_RGBA8)
					for item in sprites:
						item.sprite.material.set_shader_parameter("frame_override",float(frame))
						var pixels = frames.get_frame_texture(item.animation,frame).get_image()
						expected.blit_rect_mask(pixels,pixels,Rect2i(Vector2i.ZERO,pixels.get_size()),Vector2i(item.sprite.position))
					var rendered = await capture(viewport)
					rendered.convert(Image.FORMAT_RGBA8)
					var actual = rendered.get_data()
					var wanted = expected.get_data()
					for i in range(0,actual.size(),4):
						if actual[i+3]!=wanted[i+3] or (wanted[i+3]>0 and (abs(actual[i]-wanted[i])>1 or abs(actual[i+1]-wanted[i+1])>1 or abs(actual[i+2]-wanted[i+2])>1)): mismatch+=1
						if frame>0:
							if actual[i+3]!=first_alpha[i+3]: alpha_changes+=1
							if actual[i]!=previous[i] or actual[i+1]!=previous[i+1] or actual[i+2]!=previous[i+2]: changes+=1
					if frame==0: first_alpha=actual
					previous=actual
					if frame==5 and width==5 and entry.risePixels==64: rendered.save_png(OUTPUT+"shared_fall_"+entry.projection+"_"+connector.direction+".png")
				check(mismatch==0 and alpha_changes==0 and changes>100,"Shared assembly pixel/alpha/motion mismatch")
				results.append({"projection":entry.projection,"direction":connector.direction,"rise":entry.risePixels,"width":width,"frames":16,"pixelMismatches":mismatch,"alphaChanges":alpha_changes,"motionPixels":changes})
				viewport.free()
	var report = FileAccess.open(OUTPUT+("procedural_waterfall_render.json" if procedural else "shared_waterfall_render.json"),FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","cases":results,"errors":errors,"visualApproval":false,"nativeClockAudit":"separate_route_probes"},"  "))
	print("SHARED WATERFALL GPU ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
