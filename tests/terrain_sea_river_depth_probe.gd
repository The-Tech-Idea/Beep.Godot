extends "res://tests/terrain_coastal_depth_render_probe.gd"

const CONTACT = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_river_depth_v1/"
const SOURCE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const PORT_NAMES = ["north","east","south","west"]

func route_cell(port: int,cross: int,along: int) -> Vector2i:
	if port==1: return Vector2i(9-along,cross)
	if port==2: return Vector2i(cross,9-along)
	if port==3: return Vector2i(along,cross)
	return Vector2i(cross,along)

func run() -> void:
	var results: Array = []
	var lake = "--lake-wide" in OS.get_cmdline_user_args()
	var contact_base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_mouth_sections_v1/" if lake else CONTACT
	var source_base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/" if lake else SOURCE
	var report_prefix = "lake_mouth_sections" if lake else "sea_river_depth"
	for projection in ["square","isometric"]:
		for case in range(8 if lake else 4):
			var port: int = case/2 if lake else case
			var profile = PORT_NAMES[port]+("_inlet" if case%2==0 else "_outlet") if lake else PORT_NAMES[port]
			var viewport = SubViewport.new()
			viewport.size = Vector2i(1700,1700) if lake else Vector2i(1600,1600)
			viewport.transparent_bg = true
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			var scene_path = contact_base+projection+"/"+("widths_" if lake else "mouth_depth_")+profile+".tscn"
			var scene = load(scene_path).instantiate()
			viewport.add_child(scene)
			var water: TileMapLayer = scene.get_node("Water")
			var depth: TileMapLayer = scene.get_node("Depth")
			water.tile_set = water.tile_set.duplicate(true)
			depth.tile_set = depth.tile_set.duplicate(true)
			var binding = scene.get_node("CoastalDepthBinding")
			check(scene.get_node("SurfacePlane").refresh().is_empty() and binding.refresh().is_empty(),"Valid sea depth mouths rejected")
			check(water.tile_set.get_patterns_count()==(32 if lake else 16),"Native mouth patterns missing")
			var original_tiles = load(source_base+projection+("/lake_river_depth.tres" if lake else "/shared_sea.tres")) as TileSet
			for id in ([0,1,2] if lake else [0,1,2,3]):
				check(water.tile_set.get_source(id).texture.get_image().get_data()==original_tiles.get_source(id).texture.get_image().get_data(),"Original sea/river control frames changed")
			for i in range(4): await process_frame
			binding.set_process(false)
			var cursor = 1
			var mouths: Dictionary = {}
			for width in [1,2,3,5]:
				var cell = route_cell(port,cursor,4)
				var source_id = water.get_cell_source_id(cell)
				var region = water.get_cell_atlas_coords(cell)
				water.erase_cell(cell)
				check(not binding.refresh().is_empty(),"Missing width section accepted")
				var pattern_path = contact_base+projection+"/"+profile+"_"+str(width)+".tres" if lake else SOURCE+projection+"/mouth_"+PORT_NAMES[port]+"_"+str(width)+".tres"
				var pattern = load(pattern_path) as TileMapPattern
				water.set_pattern(cell,pattern)
				check(binding.refresh().is_empty(),"Native pattern repaint rejected")
				water.set_cell(cell,source_id,region,TileSetAtlasSource.TRANSFORM_FLIP_H)
				check(not binding.refresh().is_empty(),"Flipped mouth accepted")
				water.set_cell(cell,source_id,region,0)
				var river = route_cell(port,cursor,3)
				var river_region = water.get_cell_atlas_coords(river)
				water.set_cell(river,2,Vector2i(river_region.x,(river_region.y+1)%4),0)
				check(not binding.refresh().is_empty(),"Wrong upstream width flow accepted")
				water.set_cell(river,2,river_region,0)
				check(binding.refresh().is_empty(),"Corrected mouth rejected")
				for i in range(width): mouths[route_cell(port,cursor+i,4)] = width
				cursor += width+2
			var packed = PackedScene.new()
			var path = OUTPUT+report_prefix+"_"+projection+"_"+profile+"_roundtrip.tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Sea mouth depth save failed")
			var reopened = load(path).instantiate()
			root.add_child(reopened)
			check(reopened.get_node("CoastalDepthBinding").refresh().is_empty(),"Sea mouth depth reopen failed")
			reopened.free()
			speed(water,0.001)
			speed(depth,0.001)
			var size = Vector2(water.tile_set.tile_size)
			var cells = water.get_used_cells()
			var bounds = Rect2(water.map_to_local(cells[0])-size/2,size)
			for cell in cells: bounds = bounds.merge(Rect2(water.map_to_local(cell)-size/2,size))
			var origin = ((Vector2(viewport.size)-bounds.size)/2-bounds.position).floor()
			viewport.canvas_transform = Transform2D(0,origin)
			var shaded = await capture(viewport)
			water.material.set_shader_parameter("depth_field_enabled",false)
			var original = await capture(viewport)
			var contrast = difference(original,shaded)
			check(contrast.changed>100 and contrast.landChanged==0 and contrast.alphaChanged==0,"Sea mouth depth alters banks or is absent")
			var river_changes = 0
			var mouth_changes = {1:0,2:0,3:0,5:0}
			for y in range(original.get_height()):
				for x in range(original.get_width()):
					var a = original.get_pixel(x,y)
					var b = shaded.get_pixel(x,y)
					if a.a<0.99 or abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)<=0.015: continue
					var cell = water.local_to_map(Vector2(x+0.5,y+0.5)-origin)
					if water.get_cell_source_id(cell)==2: river_changes += 1
					if mouths.has(cell): mouth_changes[mouths[cell]] += 1
			check(river_changes==0,"Depth changed upstream river pixels")
			for width in mouth_changes:
				check(mouth_changes[width]==0 if lake else mouth_changes[width]>0,"Incorrect painted shallow mouth width "+str(width))
			var deep_mouth_changes = {1:0,2:0,3:0,5:0}
			if lake:
				var painted = depth.tile_map_data
				depth.clear()
				check(binding.refresh().is_empty(),"Deep lake mouth rejected")
				var deep_image = await capture(viewport)
				var deep_river_changes = 0
				for y in range(original.get_height()):
					for x in range(original.get_width()):
						var a = original.get_pixel(x,y)
						var b = deep_image.get_pixel(x,y)
						if a.a<0.99 or abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)<=0.015: continue
						var cell = water.local_to_map(Vector2(x+0.5,y+0.5)-origin)
						if mouths.has(cell): deep_mouth_changes[mouths[cell]] += 1
						if water.get_cell_source_id(cell)==2: deep_river_changes += 1
				check(deep_river_changes==0,"Deep lake shading changed river pixels")
				for width in deep_mouth_changes: check(deep_mouth_changes[width]>0,"Deep lake mouth has no shading")
				depth.tile_map_data = painted
				check(binding.refresh().is_empty(),"Shallow lake restoration rejected")
			var edge_samples = 0
			var edge_errors = 0
			for cell in mouths:
				var top_left = origin+water.map_to_local(cell)-size/2
				for y in range(int(size.y)):
					for x in range(64):
						var local = Vector2(x+0.5,y+0.5)
						if projection=="isometric":
							var dx = (local.x-32)/64
							var dy = (local.y-16)/32
							local = Vector2(dx+dy+0.5,-dx+dy+0.5)*64
						if local.x<0 or local.y<0 or local.x>=64 or local.y>=64: continue
						var along: float = [local.y,64-local.x,64-local.y,local.x][port]
						if along>0.5: continue
						var point = Vector2i(top_left)+Vector2i(x,y)
						var a = original.get_pixelv(point)
						var b = shaded.get_pixelv(point)
						if not (a.b>a.r+0.03 and a.g>a.r+0.03): continue
						if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015: edge_errors += 1
						edge_samples += 1
			check(edge_samples>0 and edge_errors==0,"Sea mouth river-facing edge changed")
			check(binding.refresh().is_empty(),"Sea depth restore failed")
			viewport.canvas_transform.origin += Vector2(16,12)
			var camera = difference(shaded,await capture(viewport),Vector2i(16,12))
			check(camera.changed==0 and camera.alphaChanged==0,"Sea mouth depth slides with camera")
			viewport.canvas_transform.origin = origin
			speed(water,16.0/1.2)
			speed(depth,16.0/1.2)
			var first = await capture(viewport)
			await create_timer(0.35).timeout
			var second = await capture(viewport)
			var motion = difference(first,second)
			check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Sea mouth animation missing or moves banks")
			if case==0:
				check(first.save_png(OUTPUT+report_prefix+"_"+projection+"_a.png")==OK and second.save_png(OUTPUT+report_prefix+"_"+projection+"_b.png")==OK,"Save depth mouth captures")
			results.append({"projection":projection,"port":PORT_NAMES[port],"profile":profile,"widths":[1,2,3,5],"patternRepaint":true,"saveReopen":true,"contrast":contrast,"riverChanges":river_changes,"mouthChanges":mouth_changes,"deepMouthChanges":deep_mouth_changes,"riverEdgeSamples":edge_samples,"riverEdgeErrors":edge_errors,"camera":camera,"motion":motion})
			viewport.free()
	var file = FileAccess.open(OUTPUT+report_prefix+"_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("SEA RIVER DEPTH ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
