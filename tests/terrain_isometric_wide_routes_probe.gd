extends "res://tests/terrain_wide_depth_routes_probe.gd"

const ROUTE_BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/"

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(ROUTE_BASE+"isometric/widths_manifest.json"))
	var cases = []
	var references = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT+"isometric_wide_route_reference.json"))
	for entry in manifest.cases:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1152,800)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load(ROUTE_BASE+entry.scene).instantiate()
		scene.position = Vector2(512,176)
		viewport.add_child(scene)
		for name in ["SurfacePlane","LowerSurfacePlane"]: check(scene.get_node(name).refresh().is_empty(),"Isometric wide surface")
		check(scene.get_node("LakeDepthBinding").refresh().is_empty(),"Isometric wide depth")
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		var water: TileMapLayer = scene.get_node("LakeWater")
		var ns = entry.direction=="north_south"
		check(top.get_used_cells().size()==(int(entry.width)+2)*4,"Plateau footprint")
		for index in range(int(entry.width)):
			var fall = scene.get_node("Fall_%d"%index) as Sprite2D
			var inlet = Vector2i(index+1,3) if ns else Vector2i(3,index+1)
			var outlet = Vector2i(index+1,4) if ns else Vector2i(4,index+1)
			check(fall.position+Vector2(64 if ns else 32,16)==top.map_to_local(inlet)-Vector2(0,64),"Fall inlet anchor")
			check(fall.position+Vector2(32 if ns else 64,96)==top.map_to_local(outlet),"Fall outlet anchor")
			check(water.get_cell_source_id(Vector2i(index+1,7) if ns else Vector2i(7,index+1))==2,"Native river stem")
		var first = await capture(viewport)
		await create_timer(0.3).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Isometric wide motion/terrain")
		var clipped = 0
		for x in range(first.get_width()):
			if first.get_pixel(x,0).a>0 or first.get_pixel(x,first.get_height()-1).a>0: clipped+=1
		for y in range(first.get_height()):
			if first.get_pixel(0,y).a>0 or first.get_pixel(first.get_width()-1,y).a>0: clipped+=1
		check(clipped==0,"Isometric wide route clipped")
		if entry.width==5: first.save_png(OUTPUT+"isometric_wide_route_"+entry.direction+".png")
		var packed = PackedScene.new()
		var path = OUTPUT+"isometric_wide_"+entry.direction+"_"+str(entry.width)+".tscn"
		check(packed.pack(scene)==OK and ResourceSaver.save(packed,path)==OK,"Wide route save")
		var reopened = load(path).instantiate()
		root.add_child(reopened)
		check(reopened.get_node("LakeDepthBinding").refresh().is_empty(),"Wide route reopen")
		reopened.free()
		var refs = []
		for reference_case in references.cases:
			if reference_case.direction==entry.direction and reference_case.width==entry.width: refs=reference_case.refs
		check(refs.size()==int(entry.width)*2,"Missing isometric references")
		var seen = {}
		var maximum = 0.0
		var samples = []
		for sample in range(256):
			await create_timer(0.04).timeout
			var rendered = await capture(viewport)
			var phase = -1
			for reference in refs:
				var name = "Fall_%d"%reference.section if reference.role=="fall" else "Flow_%d_6"%reference.section
				var origin = Vector2i(scene.get_node(name).global_position)
				var match = infer_reference(rendered,reference,origin)
				if phase<0: phase=match.frame
				check(match.frame==phase and match.unique and match.maxByteError<=1,"Isometric section phase: "+str(entry)+" "+str(match))
				maximum=maxf(maximum,match.maxByteError)
				if reference.role=="river":
					var native_cell = Vector2i(int(reference.section)+1,7) if ns else Vector2i(7,int(reference.section)+1)
					var native_origin = Vector2i(water.to_global(water.map_to_local(native_cell))-Vector2(32,16))
					var native = infer_reference(rendered,reference,native_origin)
					check(native.frame==phase and native.unique and native.maxByteError<=1,"Isometric native phase: "+str(native))
					maximum=maxf(maximum,native.maxByteError)
			seen[phase]=true
			samples.append(phase)
		check(seen.size()==16,"Isometric wide frames missing")
		cases.append({"direction":entry.direction,"width":entry.width,"motion":motion,"clippedPixels":clipped,"saveReopen":true,"phaseSamples":samples,"distinctFrames":seen.size(),"maxByteError":maximum})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"isometric_wide_routes_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","cases":cases,"errors":errors,"fullRoutePhaseAudit":"independent_projected_CPU_samples","visualApproval":false},"  "))
	print("ISOMETRIC WIDE ROUTES GPU ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
