extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_route_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func seek(player: AnimationPlayer, frame: int) -> void:
	player.play("flow")
	player.pause()
	player.seek(frame*1.2/16+0.00001,true)

func run() -> void:
	var errors = []
	var results = []
	var lake_mode = "--lake" in OS.get_cmdline_user_args()
	var base = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_granite_lake_v1/" if lake_mode else BASE
	var prefix = "isometric_granite_lake" if lake_mode else "isometric_granite_route"
	var size = Vector2i(1152,640) if lake_mode else Vector2i(768,512)
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(base+"manifest.json"))
	for entry in manifest.cases:
		var viewport = SubViewport.new()
		viewport.size = size
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(base+entry.scene).instantiate()
		scene.position = Vector2(512 if lake_mode else 352,176)
		viewport.add_child(scene)
		for name in ["SurfacePlane","LowerSurfacePlane"]:
			if scene.get_node(name).refresh()!="": errors.append("Invalid surface binding")
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		var fall: AnimatedSprite2D = scene.get_node("Waterfall")
		var upper = Vector2(entry.upstreamAnchor[0],entry.upstreamAnchor[1])
		var lower = Vector2(entry.downstreamAnchor[0],entry.downstreamAnchor[1])
		var inlet = Vector2i(entry.inletCell[0],entry.inletCell[1])
		var outlet = Vector2i(entry.outletCell[0],entry.outletCell[1])
		if fall.position+upper!=top.map_to_local(inlet)-Vector2(0,64): errors.append("Inlet anchor mismatch")
		if fall.position+lower!=top.map_to_local(outlet): errors.append("Outlet anchor mismatch")
		var player: AnimationPlayer = scene.get_node("WaterClock")
		seek(player,0)
		var first = await capture(viewport)
		seek(player,5)
		var second = await capture(viewport)
		var synchronized = 0
		var animated_mask = Image.create(size.x,size.y,false,Image.FORMAT_L8)
		animated_mask.fill(Color.BLACK)
		for child in scene.get_children():
			if child is AnimatedSprite2D:
				if child.frame!=5: errors.append("Connected sheet phase mismatch")
				synchronized += 1
				var pixels = child.sprite_frames.get_frame_texture(child.animation,child.frame).get_image()
				var origin = Vector2i(scene.position+child.position)
				for y in range(pixels.get_height()):
					for x in range(pixels.get_width()):
						if pixels.get_pixel(x,y).a>0: animated_mask.set_pixelv(origin+Vector2i(x,y),Color.WHITE)
		if synchronized!=9+(entry.lakeCells.size() if lake_mode else 0): errors.append("Missing connected animation")
		var lake_changes = 0
		if lake_mode:
			var layout_scene = load(base+entry.authoringLayout).instantiate()
			var layout: TileMapLayer = layout_scene.get_node("Water")
			var mouth = Vector2i(entry.lakeInletCell[0],entry.lakeInletCell[1])
			if layout.get_cell_tile_data(mouth).get_custom_data("flow_profile")!=entry.lakeInletProfile: errors.append("Lake inlet profile mismatch")
			for record in entry.lakeCells:
				var cell = Vector2i(record.cell[0],record.cell[1])
				if layout.get_cell_source_id(cell)!=record.sourceId: errors.append("Lake native source mismatch")
				var sprite: AnimatedSprite2D = scene.get_node(record.sprite)
				if sprite.position!=layout.map_to_local(cell)-Vector2(32,16): errors.append("Lake projection mismatch")
				var origin = Vector2i(scene.position+sprite.position)
				for y in range(32):
					for x in range(64):
						var a = first.get_pixelv(origin+Vector2i(x,y))
						var b = second.get_pixelv(origin+Vector2i(x,y))
						if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.02: lake_changes += 1
			if lake_changes<100: errors.append("Missing lake ripple motion")
			layout_scene.free()
		var alpha_changes = 0
		var grass_changes = 0
		var changed = 0
		var clipped = 0
		var static_changes = 0
		for y in range(size.y):
			for x in range(size.x):
				var a = first.get_pixel(x,y)
				var b = second.get_pixel(x,y)
				var delta = abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)
				if abs(a.a-b.a)>0.001: alpha_changes += 1
				if delta>0.02:
					changed += 1
					if animated_mask.get_pixel(x,y).r<0.5: static_changes += 1
					if a.g>a.r*1.1 and a.g>a.b*1.4: grass_changes += 1
				if (x==0 or y==0 or x==size.x-1 or y==size.y-1) and a.a>0: clipped += 1
		var role_changes = {}
		for role in {"upstream":upper,"curtain":Vector2(48,56),"impact":Vector2(48,88),"pool":lower}:
			var centers = {"upstream":upper,"curtain":Vector2(48,56),"impact":Vector2(48,88),"pool":lower}
			var center = Vector2i(scene.position+fall.position+centers[role])
			var count = 0
			for dy in range(-6,7):
				for dx in range(-6,7):
					var a = first.get_pixelv(center+Vector2i(dx,dy))
					var b = second.get_pixelv(center+Vector2i(dx,dy))
					if abs(a.r-b.r)+abs(a.g-b.g)+abs(a.b-b.b)>0.02: count += 1
			role_changes[role] = count
			if count<5: errors.append("Missing motion in "+role)
		if alpha_changes or grass_changes or static_changes: errors.append("Static terrain or bounds changed")
		if clipped: errors.append("Route clipped")
		first.save_png(OUTPUT+prefix+"_"+str(entry.direction)+"_frame0.png")
		second.save_png(OUTPUT+prefix+"_"+str(entry.direction)+"_frame5.png")
		results.append({"direction":entry.direction,"synchronizedSprites":synchronized,"changedPixels":changed,"alphaChanges":alpha_changes,"grassChanges":grass_changes,"staticChanges":static_changes,"clippedPixels":clipped,"roleChanges":role_changes,"lakeChangedSamples":lake_changes})
		viewport.free()
	var file = FileAccess.open(OUTPUT+prefix+"_render.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","errors":errors,"results":results,"visualApproval":false},"  "))
	print("ISOMETRIC GRANITE ROUTE RENDER ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
