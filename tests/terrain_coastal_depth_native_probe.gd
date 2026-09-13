extends SceneTree

var base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
var scene_name = "coastal_depth_review.tscn"
var report_prefix = "coastal_depth"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const DIRECTIONS = [Vector2i(0,-1),Vector2i(1,0),Vector2i(0,1),Vector2i(-1,0),Vector2i(1,-1),Vector2i(1,1),Vector2i(-1,1),Vector2i(-1,-1)]
var errors: Array[String] = []

func _initialize() -> void:
	if "--lake" in OS.get_cmdline_user_args():
		base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_depth_v1/"
		scene_name = "lake_depth_review.tscn"
		report_prefix = "lake_depth"
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func normalize(mask: int) -> int:
	for i in range(4):
		if not mask&(1<<i) or not mask&(1<<((i+1)%4)): mask &= ~(1<<(i+4))
	return mask

func paint(layer: TileMapLayer,mask: int) -> void:
	layer.clear()
	for y in range(-2,3):
		for x in range(-2,3): layer.set_cell(Vector2i(x,y),0,Vector2i(7,5),0)
	var cells: Array[Vector2i] = [Vector2i.ZERO]
	for b in range(8):
		if mask&(1<<b): cells.append(DIRECTIONS[b])
	layer.set_cells_terrain_connect(cells,0,0,false)

func map_state(layer: TileMapLayer) -> Dictionary:
	var state: Dictionary = {}
	for cell in layer.get_used_cells():
		var source = layer.get_cell_source_id(cell)
		if source>=0: state[cell] = [source,layer.get_cell_atlas_coords(cell),layer.get_cell_alternative_tile(cell)]
	return state

func run() -> void:
	var results: Array = []
	var masks: Array[int] = []
	for mask in range(256):
		if normalize(mask)==mask: masks.append(mask)
	for projection in ["square","isometric"]:
		var scene = load(base+projection+"/"+scene_name).instantiate()
		root.add_child(scene)
		var water: TileMapLayer = scene.get_node("Water")
		var depth: TileMapLayer = scene.get_node("Depth")
		var binding = scene.get_node("CoastalDepthBinding")
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Surface plane failed")
		check(binding.refresh().is_empty(),"Coastal fixture rejected")
		var original_water = water.tile_map_data
		var original_water_cells = map_state(water)
		var original_depth = depth.tile_map_data
		var combinations = 0
		for sea_mask in masks:
			paint(water,sea_mask)
			for depth_mask in masks:
				if depth_mask&~sea_mask: continue
				paint(depth,depth_mask)
				check(binding.refresh().is_empty(),"Valid coast/depth pair rejected: "+str(sea_mask)+"/"+str(depth_mask))
				var texture = water.material.get_shader_parameter("depth_field_texture") as Texture2D
				check(texture!=null,"Depth field texture missing")
				if texture!=null:
					var value = texture.get_image().get_pixelv(-binding.field_origin)
					var expected = depth.get_cell_atlas_coords(Vector2i.ZERO)
					check(roundi(value.r*255)==expected.x and roundi(value.g*255)==expected.y and value.b>0.99,"Cell field lost native mask region")
				combinations += 1
		water.tile_map_data = original_water
		depth.tile_map_data = original_depth
		check(binding.refresh().is_empty(),"Restored fixture rejected")
		check(map_state(water)==original_water_cells,"Fixture restoration changed logical water cells")
		for i in range(5): await process_frame
		var uploads = binding.field_uploads
		for i in range(5): await process_frame
		check(binding.field_uploads==uploads,"Unchanged fields are repeatedly uploaded")
		depth.set_cell(Vector2i.ZERO,0,Vector2i(6,5),0)
		for i in range(3): await process_frame
		check(not binding.last_error.is_empty() and water.material.get_shader_parameter("depth_field_enabled")==false,"Invalid land paint not rejected automatically")
		check(depth.get_cell_source_id(Vector2i.ZERO)==0 and map_state(water)==original_water_cells,"Binding destroyed logical map data")
		depth.erase_cell(Vector2i.ZERO)
		for i in range(3): await process_frame
		check(binding.last_error.is_empty() and water.material.get_shader_parameter("depth_field_enabled")==true,"Corrected land paint did not recover")
		binding.max_field_side = 1
		check(not binding.refresh().is_empty(),"Oversized field allocated")
		binding.max_field_side = 1024
		depth.position.x = 64
		check(not binding.refresh().is_empty(),"Mismatched layer transform accepted")
		depth.position = Vector2.ZERO
		check(binding.refresh().is_empty(),"Restored transform rejected")
		var packed = PackedScene.new()
		var role = water.tile_set.get_meta("terrain_library_role")
		water.tile_set.set_meta("terrain_library_role","incompatible_water_family")
		check(not binding.refresh().is_empty(),"Incompatible water family accepted")
		water.tile_set.set_meta("terrain_library_role",role)
		check(binding.refresh().is_empty(),"Restored water family rejected")
		var path = OUTPUT+report_prefix+"_"+projection+"_roundtrip.tscn"
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Coastal depth save failed")
		var reopened = load(path).instantiate()
		root.add_child(reopened)
		check(reopened.get_node("CoastalDepthBinding").refresh().is_empty(),"Coastal depth reload failed")
		check(map_state(reopened.get_node("Depth"))==map_state(depth),"Depth paint changed on reopen")
		reopened.free()
		results.append({"projection":projection,"validNeighborhoodPairs":combinations,"steadyStateUploads":0,"invalidPaintRetained":true,"sizeLimit":true,"saveReopen":true})
		scene.free()
	var file = FileAccess.open(OUTPUT+report_prefix+"_native.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("COASTAL DEPTH NATIVE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
