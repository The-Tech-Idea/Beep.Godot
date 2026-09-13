extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_adapters_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE+"manifest.json"))
	var results: Array = []
	for projection in ["square","isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(256,256)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.canvas_transform = Transform2D(0,Vector2(96,64))
		var scene = load(BASE+projection+"/river_adapter_review.tscn").instantiate()
		viewport.add_child(scene)
		var layer = scene.get_node("Water") as TileMapLayer
		var overview = Image.create(1024,1024,false,Image.FORMAT_RGBA8)
		overview.fill(Color.TRANSPARENT)
		for profile in manifest.profiles:
			layer.clear()
			var pattern = layer.tile_set.get_pattern(profile.patternIndex)
			check(pattern.get_used_cells().size()==4,"Pattern must contain four cells")
			layer.set_pattern(Vector2i.ZERO,pattern)
			for cell in layer.get_used_cells():
				check(layer.get_cell_alternative_tile(cell)==0,"Invalid tile alternative")
			await process_frame
			await RenderingServer.frame_post_draw
			var image = viewport.get_texture().get_image()
			var gaps = 0
			var exposed = 0
			var samples = 0
			for y in range(256):
				for x in range(256):
					var cell = layer.local_to_map(Vector2(x+.5,y+.5)-Vector2(96,64))
					if cell.x<0 or cell.x>1 or cell.y<0 or cell.y>1: continue
					var color = image.get_pixel(x,y)
					samples += 1
					if color.a<0.99: gaps += 1
					if color.b>0.98 and color.r<0.03 and color.g<0.03: exposed += 1
			check(samples==(4096 if projection=="isometric" else 16384),"Incomplete projected pattern footprint")
			check(gaps==0,"Blank pixels in "+str(profile.id))
			check(exposed==0,"Unreplaced terrain mask in "+str(profile.id))
			var index = int(profile.patternIndex)
			overview.blit_rect(image,Rect2i(0,0,256,256),Vector2i(index%4,index/4)*256)
			results.append({"projection":projection,"profile":profile.id,"samples":samples,"gaps":gaps,"exposedMasks":exposed})
		check(overview.save_png(OUTPUT+"adapter_patterns_"+projection+".png")==OK,"Cannot save overview")
		viewport.free()
	var file = FileAccess.open(OUTPUT+"adapter_patterns.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("ADAPTER PATTERNS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
