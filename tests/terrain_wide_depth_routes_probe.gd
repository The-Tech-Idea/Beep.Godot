extends "res://tests/terrain_coastal_depth_render_probe.gd"

func infer_reference(rendered: Image,reference: Dictionary,origin: Vector2i) -> Dictionary:
	var scores = []
	var maxima = []
	for frame in range(16):
		var score = 0.0
		var maximum = 0.0
		for index in range(reference.points.size()):
			var p = reference.points[index]
			var actual = rendered.get_pixelv(origin+Vector2i(p[0],p[1]))
			var rgb = [round(actual.r*255),round(actual.g*255),round(actual.b*255)]
			for channel in range(3):
				var delta = abs(rgb[channel]-reference.frames[frame][index][channel])
				score+=delta
				maximum=maxf(maximum,delta)
		scores.append(score)
		maxima.append(maximum)
	var frame = scores.find(scores.min())
	var sorted = scores.duplicate()
	sorted.sort()
	return {"frame":frame,"maxByteError":maxima[frame],"unique":sorted[1]>sorted[0]}

func run() -> void:
	var cases = []
	var references = JSON.parse_string(FileAccess.get_file_as_string(OUTPUT+"wide_route_reference.json"))
	for width in [1,2,3,5,9]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(maxi(704,(width+4)*64+64),800)
		viewport.transparent_bg = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var scene = load("res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/square/width_"+str(width)+".tscn").instantiate()
		scene.position = Vector2(32,96)
		viewport.add_child(scene)
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Width surface")
		check(scene.get_node("LakeDepthBinding").refresh().is_empty(),"Width depth")
		for x in range(width):
			check(scene.get_node("Fall%d"%x).position==Vector2((x+2)*64,0),"Fall position")
			check(scene.get_node("River%d_192"%x).position==Vector2((x+2)*64,192),"Downstream position")
		var first = await capture(viewport)
		await create_timer(0.3).timeout
		var second = await capture(viewport)
		var motion = difference(first,second)
		check(motion.changed>100 and motion.landChanged==0 and motion.alphaChanged==0,"Width motion affects terrain or is absent")
		first.save_png(OUTPUT+"wide_depth_route_"+str(width)+".png")
		var refs = []
		for reference_case in references.cases:
			if reference_case.width==width: refs=reference_case.refs
		check(refs.size()==width*2,"Missing independent phase references")
		var seen = {}
		var max_error = 0.0
		var samples = []
		for sample in range(256):
			await create_timer(0.04).timeout
			var rendered = await capture(viewport)
			var phase = -1
			for reference in refs:
				var y = 0 if reference.role=="fall" else 192
				var origin = Vector2i(32+(int(reference.section)+2)*64,96+y)
				var match = infer_reference(rendered,reference,origin)
				if phase<0: phase=match.frame
				check(match.frame==phase and match.unique and match.maxByteError<=1,"Wide sprite phase/connector mismatch: "+str(width)+" "+str(match))
				max_error=maxf(max_error,match.maxByteError)
				if reference.role=="river":
					var native_match = infer_reference(rendered,reference,origin+Vector2i(0,64))
					check(native_match.frame==phase and native_match.unique and native_match.maxByteError<=1,"Native river phase mismatch width=%d section=%d sample=%d sprite=%d native=%s"%[width,reference.section,sample,phase,str(native_match)])
					max_error=maxf(max_error,native_match.maxByteError)
			seen[phase]=true
			samples.append(phase)
		check(seen.size()==16,"Wide route phase coverage incomplete")
		cases.append({"width":width,"motion":motion,"phaseSamples":samples,"distinctFrames":seen.size(),"maxByteError":max_error})
		viewport.free()
	var report = FileAccess.open(OUTPUT+"wide_depth_routes_render.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"status":"passed" if errors.is_empty() else "failed","cases":cases,"errors":errors,"joinPhase":"independent_CPU_water_samples","visualApproval":false},"  "))
	print("WIDE DEPTH ROUTES GPU ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
