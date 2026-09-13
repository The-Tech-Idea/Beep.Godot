extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"
const PLANE = preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSurfacePlane.gd")
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func capture(viewport: SubViewport) -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	return viewport.get_texture().get_image()

func compare(a: Image,b: Image,shift: Vector2i = Vector2i.ZERO,interior: int = 0) -> Dictionary:
	var count = 0
	var mismatches = 0
	var alpha_mismatches = 0
	for y in range(40,a.get_height()-40):
		for x in range(40,a.get_width()-40):
			var p = a.get_pixel(x,y)
			var q = b.get_pixelv(Vector2i(x,y)+shift)
			if p.a < 0.99: continue
			var edge = false
			if interior>0:
				for offset in [Vector2i(interior,0),Vector2i(-interior,0),Vector2i(0,interior),Vector2i(0,-interior)]:
					if a.get_pixelv(Vector2i(x,y)+offset).a<0.99: edge = true
			if edge: continue
			count += 1
			if q.a<0.99: alpha_mismatches += 1
			if abs(p.r-q.r)+abs(p.g-q.g)+abs(p.b-q.b)>0.012 or q.a<0.99: mismatches += 1
	return {"samples":count,"mismatches":mismatches,"alphaMismatches":alpha_mismatches}

func run() -> void:
	var results: Array = []
	for projection in ["square","isometric"]:
		var iso = projection=="isometric"
		var height = 32 if iso else 64
		var viewport = SubViewport.new()
		viewport.size = Vector2i(800,700)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(BASE+projection+"/sea_review.tscn").instantiate()
		var material: ShaderMaterial = scene.get_node("Water").material.duplicate()
		scene.free()
		var group = Node2D.new()
		group.position = Vector2(400,160) if iso else Vector2(120,90)
		viewport.add_child(group)
		var plane = PLANE.new()
		plane.projection = 1 if iso else 0
		plane.cell_size = Vector2(64,height)
		group.add_child(plane)
		var sprites: Array[Sprite2D] = []
		var texture: Texture2D = load(BASE+projection+"/sea_control_16.res")
		for y in range(8):
			for x in range(8):
				var sprite = Sprite2D.new()
				sprite.name = "Cell_%d_%d" % [x,y]
				sprite.texture = texture
				sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
				sprite.region_enabled = true
				sprite.region_rect = Rect2(6*64,5*height,64,height)
				sprite.position = Vector2((x-y)*32,(x+y)*16) if iso else Vector2((x+0.5)*64,(y+0.5)*64)
				sprite.material = material
				group.add_child(sprite)
				plane.layer_paths.append(plane.get_path_to(sprite))
				sprites.append(sprite)
		check(plane.refresh().is_empty(),"Surface plane failed")
		var first = await capture(viewport)
		await create_timer(0.2).timeout
		var still = compare(first,await capture(viewport))
		check(still.mismatches==0,"Pinned native frame drifts with time")
		plane.repeat_cells = 8
		check(plane.refresh().is_empty(),"Repeat change failed")
		var repeated = compare(first,await capture(viewport))
		check(repeated.mismatches==0,"Texture repeat setting rescales water motion")
		plane.repeat_cells = 4
		check(plane.refresh().is_empty(),"Restore repeat failed")
		viewport.canvas_transform = Transform2D(0,Vector2(16,12))
		var camera = compare(first,await capture(viewport),Vector2i(16,12))
		check(camera.samples>50000 and camera.mismatches==0,"Camera slides shared sea field")
		viewport.canvas_transform = Transform2D.IDENTITY
		group.position += Vector2(24,8)
		check(plane.refresh().is_empty(),"Translated surface plane failed")
		var moved = compare(first,await capture(viewport),Vector2i(24,8))
		check(moved.mismatches==0,"Moving the map slides its own field")
		group.position -= Vector2(24,8)
		check(plane.refresh().is_empty(),"Restore surface plane failed")
		# A single enlarged constant mask must shade identically to 64 adjoining cells.
		for sprite in sprites: sprite.visible = false
		var whole = sprites[0].duplicate() as Sprite2D
		whole.name = "WholeSurface"
		whole.visible = true
		whole.position = Vector2(0,112) if iso else Vector2(256,256)
		whole.scale = Vector2(8,8)
		group.add_child(whole)
		plane.layer_paths.append(plane.get_path_to(whole))
		check(plane.refresh().is_empty(),"Reference surface binding failed")
		var continuous = await capture(viewport)
		check(continuous.save_png(OUTPUT+"shared_sea_plane_"+projection+"_continuous.png")==OK,"Save continuous reference")
		var full_comparison = compare(first,continuous)
		# Nearest scaling enlarges the reference diamond's outer staircase by 8x.
		# Exclude only that outer silhouette, not interior tile boundaries.
		var tiled = compare(first,continuous,Vector2i.ZERO,5 if iso else 0)
		check(tiled.mismatches==0,"Tile boundaries interrupt the shared wave field")
		whole.visible = false
		for sprite in sprites:
			sprite.visible = true
			sprite.region_rect.position.y += 5*6*height
		var frame5 = await capture(viewport)
		var motion = compare(first,frame5)
		check(motion.mismatches>100,"Native atlas frame does not advance swells")
		check(first.save_png(OUTPUT+"shared_sea_plane_"+projection+"_frame0.png")==OK,"Save surface capture")
		check(frame5.save_png(OUTPUT+"shared_sea_plane_"+projection+"_frame5.png")==OK,"Save motion capture")
		results.append({"projection":projection,"fixedFrame":still,"repeatIndependentWater":repeated,"camera":camera,"translatedMap":moved,"tiledVsContinuous":tiled,"referenceOuterSilhouette":full_comparison,"motion":motion})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"shared_sea_plane_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","results":results,"errors":errors,"visualApproval":false},"  "))
	print("SHARED SEA PLANE ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
