extends SceneTree

const TEMPLATE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_plateaus_v1/plateau_1x1.tscn"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_shapes_v1/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE)
	var errors = []
	var cases = []
	for name in ["notch","u_shape","ring"]:
		var extent = Vector2i(5,5) if name=="notch" else Vector2i(7,5) if name=="u_shape" else Vector2i(7,7)
		var scene = load(TEMPLATE).instantiate()
		scene.name = "IsometricModularShape"
		var walls: TileMapLayer = scene.get_node("CliffWalls")
		var top: TileMapLayer = scene.get_node("PlateauSurface")
		walls.clear()
		top.clear()
		var cells = []
		var openings = []
		for y in range(extent.y):
			for x in range(extent.x):
				var removed = x>=2 and y>=2 if name=="notch" else x>=2 and x<=4 and y>=2 if name=="u_shape" else x>=1 and x<=5 and y>=1 and y<=5
				if removed:
					openings.append([x,y])
					continue
				walls.set_cell(Vector2i(x,y),0,Vector2i.ZERO,0)
				top.set_cell(Vector2i(x,y),0,Vector2i(6,5),0)
				cells.append([x,y])
		# Save the changed native cells, rather than inheriting the template's one-cell footprint.
		walls.owner = scene
		top.owner = scene
		scene.set_meta("dimensions_cells",extent)
		scene.set_meta("missing_artwork","Natural concave wall treatment, wall variation, bottom contacts")
		var file = name+".tscn"
		var packed = PackedScene.new()
		var result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+file)
		if result!=OK: errors.append(file)
		cases.append({"scene":file,"dimensions":[extent.x,extent.y],"cells":cells,"openings":openings})
		scene.free()
	var manifest = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","style":"cartoon","projection":"isometric","cases":cases,"risePixels":64,"sourceScene":TEMPLATE,"productionReady":false,"approvalEvidence":null,"pending":["Visual approval","Natural concave wall treatment","Wall variation","Bottom contacts","Engine authoring integration"]},"  "))
	print("ISOMETRIC SHAPES ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
