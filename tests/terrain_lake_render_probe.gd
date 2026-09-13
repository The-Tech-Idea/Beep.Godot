extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func run() -> void:
	var results: Array = []
	var river_sections = "--river-sections" in OS.get_cmdline_user_args()
	var sea = "--sea" in OS.get_cmdline_user_args()
	var sea_connectors = "--river-sea" in OS.get_cmdline_user_args()
	var shared_sea = "--shared-sea" in OS.get_cmdline_user_args()
	var adapters = "--river-adapters" in OS.get_cmdline_user_args()
	var lake_connectors = "--river-lake" in OS.get_cmdline_user_args()
	var adapter_direction = "north_south"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--adapter-direction="): adapter_direction = argument.trim_prefix("--adapter-direction=")
	var output_prefix = "river_widths_" if river_sections else "lake_"
	if sea: output_prefix = "sea_"
	if sea_connectors: output_prefix = "river_sea_"
	if lake_connectors: output_prefix = "river_lake_"
	if adapters: output_prefix = "river_adapters_"
	if adapters and adapter_direction!="north_south": output_prefix += adapter_direction+"_"
	if shared_sea: output_prefix = "shared_"+output_prefix
	var viewport_width = 1500 if river_sections else 1000
	var viewport_height = 1400 if adapters else 850
	if adapters: viewport_width = 1500
	for projection in ["square","isometric"]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(viewport_width,viewport_height)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.canvas_transform = Transform2D(0,Vector2(400,100) if projection=="isometric" else Vector2(60,60))
		if adapters and projection=="isometric": viewport.canvas_transform = Transform2D(0,Vector2(700,100))
		var scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/"+projection+"/river_widths_review.tscn" if river_sections else BASE+projection+"/lake_review.tscn"
		if sea: scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_banks_v1/"+projection+"/sea_review.tscn"
		if sea_connectors: scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sea_connectors_v1/"+projection+"/river_sea_review.tscn"
		if shared_sea:
			check(sea or sea_connectors,"Shared sea requires --sea or --river-sea")
			scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"+projection+("/river_sea_review.tscn" if sea_connectors else "/sea_review.tscn")
		if adapters: scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_adapters_v1/"+projection+"/river_adapter_review.tscn"
		if lake_connectors: scene_path = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/"+projection+"/river_lake_review.tscn"
		if adapters and adapter_direction!="north_south":
			scene_path = scene_path.replace("river_adapter_review.tscn","river_adapter_"+adapter_direction+".tscn")
			if projection=="isometric" and adapter_direction in ["west_east","east_west"]:
				viewport.canvas_transform = Transform2D(0,Vector2(300,100))
		var scene = load(scene_path).instantiate()
		viewport.add_child(scene)
		var layer = scene.get_node("Water") as TileMapLayer
		var atlas = layer.tile_set.get_source(0) as TileSetAtlasSource
		if lake_connectors or sea_connectors:
			var ports = layer.tile_set.get_source(1) as TileSetAtlasSource
			var port_count = 4 if sea_connectors else 8
			check(ports.get_tiles_count()==port_count,"Missing explicit inlet/outlet regions")
			for index in range(port_count):
				check(ports.get_tile_animation_frames_count(Vector2i(index,0))==16,"Missing connector frames")
				check(is_equal_approx(ports.get_tile_animation_speed(Vector2i(index,0)),16.0/1.2),"Connector phase mismatch")
			check(layer.get_cell_source_id(Vector2i(5,3))==1,"Mouth overwritten by terrain matching")
		check(atlas.get_tiles_count()==(64 if adapters else (16 if river_sections else 48)),"Missing water regions")
		var columns = 4 if river_sections else 8
		for index in range(64 if adapters else (16 if river_sections else 47)):
			var coords = Vector2i(index%columns,index/columns)
			check(atlas.get_tile_animation_frames_count(coords)==16,"Wrong animation frame count")
			check(is_equal_approx(atlas.get_tile_animation_speed(coords),16.0/1.2),"Wrong loop period")
		var before = await capture(viewport)
		await create_timer(0.35).timeout
		var after = await capture(viewport)
		var water_changes = 0
		var land_changes = 0
		var land_samples = 0
		var missing_cells = 0
		if adapters or lake_connectors:
			for cell in layer.get_used_cells():
				var screen = viewport.canvas_transform * layer.to_global(layer.map_to_local(cell))
				if before.get_pixelv(Vector2i(screen)).a < 0.99:
					missing_cells += 1
			check(missing_cells==0,"Blank adapter or terrain cell in assembled route")
		for y in range(viewport_height):
			for x in range(viewport_width):
				var a = before.get_pixel(x,y)
				var b = after.get_pixel(x,y)
				if a.a < 0.99:
					continue
				var changed = abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.015
				var threshold = 0.03 if sea or sea_connectors else 0.2
				var water = a.b > a.r+threshold and a.g > a.r+threshold
				if water and changed:
					water_changes += 1
				if not water:
					land_samples += 1
					if changed:
						land_changes += 1
		check(water_changes>50,"Lake does not visibly change")
		check(land_samples>1000,"Missing bank/grass samples")
		check(land_changes==0,"Lake animation moves grass or banks")
		check(before.save_png(OUTPUT+output_prefix+projection+"_a.png")==OK,"Capture failed")
		check(after.save_png(OUTPUT+output_prefix+projection+"_b.png")==OK,"Capture failed")
		results.append({"projection":projection,"waterChangedPixels":water_changes,"landChangedPixels":land_changes,"landSamples":land_samples})
		viewport.free()
	var file = FileAccess.open(OUTPUT+output_prefix+"render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("LAKE RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
