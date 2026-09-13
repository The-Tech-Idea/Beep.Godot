extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const DIRECTIONS = [Vector2i(0,-1),Vector2i(1,0),Vector2i(0,1),Vector2i(-1,0),Vector2i(1,-1),Vector2i(1,1),Vector2i(-1,1),Vector2i(-1,-1)]
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func normalize(mask: int) -> int:
	for i in range(4):
		if not mask&(1<<i) or not mask&(1<<((i+1)%4)): mask &= ~(1<<(i+4))
	return mask

func run() -> void:
	var results: Array = []
	for projection in ["square","isometric"]:
		var scene = load(BASE+projection+"/depth_review.tscn").instantiate()
		root.add_child(scene)
		var depth: TileMapLayer = scene.get_node("Depth")
		var water: TileMapLayer = scene.get_node("Water")
		var guard = scene.get_node("DepthGuard")
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Depth surface binding failed")
		check(guard.refresh().is_empty(),"Valid depth placement rejected")
		var iso = projection=="isometric"
		var bits = [TileSet.CELL_NEIGHBOR_TOP_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_SIDE,TileSet.CELL_NEIGHBOR_TOP_LEFT_SIDE,TileSet.CELL_NEIGHBOR_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_CORNER,TileSet.CELL_NEIGHBOR_LEFT_CORNER,TileSet.CELL_NEIGHBOR_TOP_CORNER] if iso else [TileSet.CELL_NEIGHBOR_TOP_SIDE,TileSet.CELL_NEIGHBOR_RIGHT_SIDE,TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,TileSet.CELL_NEIGHBOR_LEFT_SIDE,TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]
		var probe = TileMapLayer.new()
		probe.tile_set = depth.tile_set
		root.add_child(probe)
		var cases = 0
		for mask in range(256):
			if normalize(mask)!=mask: continue
			probe.clear()
			for y in range(-2,3):
				for x in range(-2,3): probe.set_cell(Vector2i(x,y),0,Vector2i(7,5),0)
			var cells: Array[Vector2i] = [Vector2i.ZERO]
			for b in range(8):
				if mask&(1<<b): cells.append(DIRECTIONS[b])
			probe.set_cells_terrain_connect(cells,0,0,false)
			var actual = 0
			var data = probe.get_cell_tile_data(Vector2i.ZERO)
			check(data!=null,"Missing native depth tile")
			if data!=null:
				for b in range(8):
					if data.get_terrain_peering_bit(bits[b])==0: actual |= 1<<b
			check(actual==mask,"Wrong depth mask "+str(mask)+" in "+projection)
			probe.set_cells_terrain_connect([Vector2i.ZERO],0,-1,false)
			var erased = probe.get_cell_tile_data(Vector2i.ZERO)
			check(erased==null or erased.terrain==-1,"Depth erasure failed")
			cases += 1
		probe.free()
		var atlas: TileSetAtlasSource = depth.tile_set.get_source(0)
		check(atlas.get_tiles_count()==48,"Depth region count mismatch")
		for index in range(48):
			var coords = Vector2i(index%8,index/8)
			check(atlas.get_tile_animation_frames_count(coords)==16,"Depth/background frame count mismatch")
			check(is_equal_approx(atlas.get_tile_animation_speed(coords),16.0/1.2),"Depth phase mismatch")
		var painted = depth.tile_map_data
		var base_water = water.tile_map_data
		depth.set_cell(Vector2i.ZERO,0,Vector2i(6,5),0)
		await process_frame
		await process_frame
		check(not guard.last_error.is_empty(),"Invalid paint was not automatically reported")
		check(depth.material.get_shader_parameter("depth_permitted")==false,"Invalid depth overlay not suppressed")
		check(depth.get_cell_source_id(Vector2i.ZERO)==0,"Guard discarded painted cells")
		check(water.tile_map_data==base_water,"Guard changed water data")
		depth.erase_cell(Vector2i.ZERO)
		await process_frame
		await process_frame
		check(guard.last_error.is_empty() and depth.material.get_shader_parameter("depth_permitted")==true,"Corrected paint did not restore: error="+guard.last_error+" dirty="+str(guard.get("_dirty"))+" permitted="+str(depth.material.get_shader_parameter("depth_permitted"))+" source="+str(depth.get_cell_source_id(Vector2i.ZERO)))
		depth.tile_map_data = painted
		check(guard.refresh().is_empty(),"Explicit bulk-data validation failed")
		water.erase_cell(Vector2i(13,0))
		check(not guard.refresh().is_empty(),"Missing supporting water was accepted")
		water.tile_map_data = base_water
		depth.position.x = 64
		check(not guard.refresh().is_empty(),"Misaligned depth layer was accepted")
		depth.position = Vector2.ZERO
		check(guard.refresh().is_empty(),"Restored transform rejected")
		check(not depth.collision_enabled and not depth.navigation_enabled,"Visual depth enabled gameplay geometry")
		var packed = PackedScene.new()
		var file = OUTPUT+"sea_depth_"+projection+"_roundtrip.tscn"
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,file)==OK,"Depth save failed")
		var reopened = load(file).instantiate()
		root.add_child(reopened)
		check(reopened.get_node("Depth").tile_map_data==painted,"Depth paint lost on reopen")
		check(reopened.get_node("DepthGuard").refresh().is_empty(),"Depth guard failed after reopen")
		reopened.free()
		results.append({"projection":projection,"nativeMasks":cases,"animatedRegions":48,"erasure":true,"invalidPlacementPreservesData":true,"saveReopen":true})
		scene.free()
	var report = FileAccess.open(OUTPUT+"sea_depth_native.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("SEA DEPTH NATIVE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
