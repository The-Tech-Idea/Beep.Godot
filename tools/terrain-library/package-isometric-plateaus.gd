extends SceneTree

const CORNER = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_iso_corner_v1/"
const GROUND = "res://addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/isometric/surface_candidate_v1/grass_dirt_review.tscn"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_plateaus_v1/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE)
	var errors = []
	var entries = []
	for dimensions in [Vector2i(1,1),Vector2i(2,2),Vector2i(3,2),Vector2i(5,3)]:
		var scene = load(CORNER+"corner_review.tscn").instantiate()
		scene.name = "LayeredIsometricPlateau"
		var walls: TileMapLayer = scene.get_node("Corner")
		walls.name = "CliffWalls"
		walls.y_sort_enabled = true
		walls.clear()
		var source = load(GROUND).instantiate()
		var reference: TileMapLayer = source.get_node("Ground")
		var top = TileMapLayer.new()
		top.name = "PlateauSurface"
		top.tile_set = reference.tile_set
		top.material = reference.material.duplicate()
		top.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		top.position.y = -64
		top.z_index = 1
		scene.add_child(top)
		top.owner = scene
		source.free()
		for y in range(dimensions.y):
			for x in range(dimensions.x):
				walls.set_cell(Vector2i(x,y),0,Vector2i.ZERO,0)
				top.set_cell(Vector2i(x,y),0,Vector2i(6,5),0)
		scene.get_node("SurfacePlane").layer_paths.assign([NodePath("../CliffWalls"),NodePath("../PlateauSurface")])
		scene.set_meta("dimensions_cells",dimensions)
		scene.set_meta("missing_artwork","Wall variations, concave faces, exposed ends and bottom overlays")
		var file = "plateau_%dx%d.tscn" % [dimensions.x,dimensions.y]
		var packed = PackedScene.new()
		var result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+file)
		if result!=OK: errors.append(file)
		entries.append({"scene":file,"dimensions":[dimensions.x,dimensions.y]})
		scene.free()
	var manifest = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","style":"cartoon","projection":"isometric","risePixels":64,"footprintCell":[64,32],"cases":entries,"resources":[CORNER+"runtime/corner.tres",GROUND],"approvalEvidence":null,"pending":["Wall repetition review","Concave shapes and holes","Exposed ends","Bottom contacts","Visual approval"],"productionReady":false},"  "))
	print("ISOMETRIC PLATEAUS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
