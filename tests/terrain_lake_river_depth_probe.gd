extends "res://tests/terrain_coastal_depth_render_probe.gd"

const CONTACT = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_river_depth_v1/"
const ORIGINAL = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/"
const PORTS = [Vector2i.UP,Vector2i.RIGHT,Vector2i.DOWN,Vector2i.LEFT]
const MOUTHS = [Vector2i(6,3),Vector2i(9,6),Vector2i(6,9),Vector2i(3,6)]
const FLOW_ROWS = [[0,1],[3,2],[1,0],[2,3]]

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(CONTACT+"manifest.json"))
	var results: Array = []
	for projection in ["square","isometric"]:
		var original_tiles = load(ORIGINAL+projection+"/river_lake.tres") as TileSet
		for index in range(8):
			var profile = manifest.profiles[index]
			var viewport = SubViewport.new()
			viewport.size = Vector2i(1040,940)
			viewport.transparent_bg = true
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			var scene = load(CONTACT+projection+"/"+profile.id+".tscn").instantiate()
			viewport.add_child(scene)
			var water: TileMapLayer = scene.get_node("Water")
			var depth: TileMapLayer = scene.get_node("Depth")
			water.tile_set = water.tile_set.duplicate(true)
			depth.tile_set = depth.tile_set.duplicate(true)
			for id in [1,2]:
				check(water.tile_set.get_source(id).texture.get_image().get_data()==original_tiles.get_source(id).texture.get_image().get_data(),"Contact/river frame pixels changed")
			check(water.tile_set.get_patterns_count()==8,"Native contact patterns missing")
			var binding = scene.get_node("CoastalDepthBinding")
			check(scene.get_node("SurfacePlane").refresh().is_empty() and binding.refresh().is_empty(),"Valid contact rejected")
			for i in range(4): await process_frame
			binding.set_process(false)
			var port: int = index/2
			var mouth: Vector2i = MOUTHS[port]
			var outside: Vector2i = mouth+PORTS[port]
			water.set_cell(outside,2,Vector2i(0,FLOW_ROWS[port][1-index%2]),0)
			check(not binding.refresh().is_empty(),"Wrong river flow accepted")
			check(water.get_cell_atlas_coords(outside).y==FLOW_ROWS[port][1-index%2],"Rejected flow paint was destroyed")
			water.set_cell(outside,2,Vector2i(0,FLOW_ROWS[port][index%2]),0)
			check(binding.refresh().is_empty(),"Corrected flow rejected")
			water.set_cell(mouth,1,Vector2i(index,0),TileSetAtlasSource.TRANSFORM_FLIP_H)
			check(not binding.refresh().is_empty(),"Unauthored flipped contact accepted")
			water.set_cell(mouth,1,Vector2i(index,0),0)
			check(binding.refresh().is_empty(),"Contact transform restoration rejected")
			var inside: Vector2i = mouth-PORTS[port]
			var original_region = water.get_cell_atlas_coords(inside)
			water.erase_cell(inside)
			check(not binding.refresh().is_empty(),"Missing lake contact accepted")
			water.set_cell(inside,0,original_region,0)
			check(binding.refresh().is_empty(),"Lake contact restore rejected")
			depth.set_cell(outside,0,Vector2i(6,5),0)
			check(not binding.refresh().is_empty(),"Depth paint on ordinary river accepted")
			depth.erase_cell(outside)
			check(binding.refresh().is_empty(),"Corrected depth paint rejected")
			var packed = PackedScene.new()
			var path = OUTPUT+"lake_river_depth_"+projection+"_"+profile.id+"_roundtrip.tscn"
			check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Contact save failed")
			var reopened = load(path).instantiate()
			root.add_child(reopened)
			check(reopened.get_node("CoastalDepthBinding").refresh().is_empty(),"Contact reopen failed")
			reopened.free()
			depth.clear()
			check(binding.refresh().is_empty(),"Deep mouth rendering rejected")
			speed(water,0.001)
			speed(depth,0.001)
			var size = Vector2(water.tile_set.tile_size)
			var cells = water.get_used_cells()
			var bounds = Rect2(water.map_to_local(cells[0])-size/2,size)
			for cell in cells: bounds = bounds.merge(Rect2(water.map_to_local(cell)-size/2,size))
			var origin = (Vector2(viewport.size)-bounds.size)/2-bounds.position
			viewport.canvas_transform = Transform2D(0,origin)
			var shaded = await capture(viewport)
			water.material.set_shader_parameter("depth_field_enabled",false)
			var original = await capture(viewport)
			var changed = difference(original,shaded)
			check(changed.changed>100 and changed.landChanged==0 and changed.alphaChanged==0,"Contact depth missing or changes bank/alpha")
			var river_changes = 0
			var mouth_changes = 0
			for y in range(original.get_height()):
				for x in range(original.get_width()):
					var a = original.get_pixel(x,y)
					var b = shaded.get_pixel(x,y)
					if a.a<0.99 or abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)<=0.015: continue
					var cell = water.local_to_map(Vector2(x+0.5,y+0.5)-origin)
					if water.get_cell_source_id(cell)==2: river_changes += 1
					if cell==mouth: mouth_changes += 1
			check(river_changes==0 and mouth_changes>0,"River shaded or mouth depth transition missing")
			var edge_samples = 0
			var edge_errors = 0
			var top_left = origin+water.map_to_local(mouth)-size/2
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
			check(edge_samples>0 and edge_errors==0,"River-facing mouth edge was tinted")
			check(binding.refresh().is_empty(),"Contact refresh rejected")
			viewport.canvas_transform.origin += Vector2(16,12)
			var camera = difference(shaded,await capture(viewport),Vector2i(16,12))
			check(camera.changed==0 and camera.alphaChanged==0,"Contact depth slides with camera")
			viewport.canvas_transform.origin = origin
			speed(water,16.0/1.2)
			speed(depth,16.0/1.2)
			var first = await capture(viewport)
			await create_timer(0.25).timeout
			var second = await capture(viewport)
			var motion = difference(first,second)
			check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Contact animation missing or moves terrain")
			if index<2:
				check(first.save_png(OUTPUT+"lake_river_depth_"+projection+"_"+profile.id+"_a.png")==OK,"Save contact capture")
				check(second.save_png(OUTPUT+"lake_river_depth_"+projection+"_"+profile.id+"_b.png")==OK,"Save contact motion capture")
			results.append({"projection":projection,"profile":profile.id,"sourceFramesUnchanged":true,"invalidFlowRejected":true,"saveReopen":true,"contrast":changed,"riverChanges":river_changes,"mouthChanges":mouth_changes,"riverEdgeSamples":edge_samples,"riverEdgeErrors":edge_errors,"camera":camera,"motion":motion})
			viewport.free()
	var file = FileAccess.open(OUTPUT+"lake_river_depth_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("LAKE RIVER DEPTH ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
