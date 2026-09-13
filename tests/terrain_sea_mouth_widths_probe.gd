extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const PORTS = ["north","east","south","west"]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func route_cell(port: int,cross: int,along: int) -> Vector2i:
	match port:
		1: return Vector2i(9-along,cross)
		2: return Vector2i(cross,9-along)
		3: return Vector2i(along,cross)
	return Vector2i(cross,along)

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func check_mouths(layer: TileMapLayer,port: int) -> void:
	var cursor = 1
	for width in [1,2,3,5]:
		for index in range(width):
			var cell = route_cell(port,cursor+index,4)
			var source = 1 if width==1 else 3
			var coords = Vector2i(port,0) if width==1 else Vector2i(0 if index==0 else (2 if index==width-1 else 1),port)
			check(layer.get_cell_source_id(cell)==source and layer.get_cell_atlas_coords(cell)==coords,"Incorrect mouth module: "+str(cell))
			var kind = "narrow" if width==1 else ("low_bank" if index==0 else ("high_bank" if index==width-1 else "middle"))
			var data = layer.get_cell_tile_data(cell)
			var expected = PORTS[port]+"_inlet"+("" if width==1 else "."+kind)
			check(data!=null and data.get_custom_data("flow_profile")==expected,"Inlet flow profile missing")
			var river = route_cell(port,cursor+index,3)
			var river_column = 0 if width==1 else (1 if index==0 else (3 if index==width-1 else 2))
			check(layer.get_cell_source_id(river)==2 and layer.get_cell_atlas_coords(river)==Vector2i(river_column,[0,3,1,2][port]),"Wrong incoming river section")
		cursor += width+2

func run() -> void:
	var results: Array = []
	for projection in ["square","isometric"]:
		for port in range(4):
			var folder = BASE+projection+"/"
			var scene = load(folder+"mouth_widths_"+PORTS[port]+".tscn").instantiate()
			var viewport = SubViewport.new()
			viewport.size = Vector2i(1536,1100)
			viewport.transparent_bg = true
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			viewport.add_child(scene)
			var layer: TileMapLayer = scene.get_node("Water")
			check(layer.tile_set.get_patterns_count()==16,"Missing saved mouth patterns")
			var atlas: TileSetAtlasSource = layer.tile_set.get_source(3)
			check(atlas.get_tiles_count()==12,"Width modules were duplicated or omitted")
			for y in range(4):
				for x in range(3):
					var coords = Vector2i(x,y)
					check(atlas.get_tile_animation_frames_count(coords)==16,"Missing width animation frames")
					check(is_equal_approx(atlas.get_tile_animation_speed(coords),16.0/1.2),"Wrong width animation speed")
					check(atlas.get_tile_animation_separation(coords)==Vector2i(0,3),"Wrong frame-band stride")
			check_mouths(layer,port)
			var cursor = 1
			for width in [1,2,3,5]:
				var pattern: TileMapPattern = load(folder+"mouth_"+PORTS[port]+"_"+str(width)+".tres")
				check(pattern.get_used_cells().size()==width,"Pattern footprint mismatch")
				layer.erase_cell(route_cell(port,cursor,4))
				layer.set_pattern(route_cell(port,cursor,4),pattern)
				cursor += width+2
			check_mouths(layer,port)
			var packed = PackedScene.new()
			var roundtrip = OUTPUT+"mouth_"+projection+"_"+PORTS[port]+"_roundtrip.tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,roundtrip)==OK,"Save/reopen failed")
			var reopened = load(roundtrip).instantiate()
			check_mouths(reopened.get_node("Water"),port)
			reopened.free()
			var used = layer.get_used_cells()
			var size = Vector2(layer.tile_set.tile_size)
			var bounds = Rect2(layer.map_to_local(used[0])-size/2,size)
			for cell in used: bounds = bounds.merge(Rect2(layer.map_to_local(cell)-size/2,size))
			var scale = minf(1.0,minf((viewport.size.x-80)/bounds.size.x,(viewport.size.y-80)/bounds.size.y))
			var origin = (Vector2(viewport.size)-bounds.size*scale)/2-bounds.position*scale
			viewport.canvas_transform = Transform2D(Vector2(scale,0),Vector2(0,scale),origin)
			var before = await capture(viewport)
			await create_timer(0.35).timeout
			var after = await capture(viewport)
			var water_changes = 0
			var land_changes = 0
			var alpha_changes = 0
			var missing = 0
			for cell in used:
				var pixel = Vector2i(viewport.canvas_transform*layer.to_global(layer.map_to_local(cell)))
				if before.get_pixelv(pixel).a<0.99: missing += 1
			for y in range(viewport.size.y):
				for x in range(viewport.size.x):
					var a = before.get_pixel(x,y)
					var b = after.get_pixel(x,y)
					if abs(a.a-b.a)>0.01: alpha_changes += 1
					if a.a<0.99: continue
					if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)<=0.015: continue
					if a.b>a.r+0.03 and a.g>a.r+0.03: water_changes += 1
					else: land_changes += 1
			check(missing==0,"Blank cells in width assembly")
			check(water_changes>100,"Width assembly has no water motion")
			check(land_changes==0 and alpha_changes==0,"Width animation moves banks or bounds")
			var prefix = OUTPUT+"mouth_widths_"+projection+"_"+PORTS[port]
			check(before.save_png(prefix+"_a.png")==OK and after.save_png(prefix+"_b.png")==OK,"Width capture failed")
			results.append({"projection":projection,"port":PORTS[port],"widths":[1,2,3,5],"waterChangedPixels":water_changes,"landChangedPixels":land_changes,"alphaChangedPixels":alpha_changes,"missingCells":missing,"patternRoundtrip":true})
			viewport.free()
	var file = FileAccess.open(OUTPUT+"sea_mouth_widths_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("SEA MOUTH WIDTHS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
