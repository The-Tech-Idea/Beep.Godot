extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/"
var errors: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool,message: String) -> void:
	if not ok:
		errors.append(message)
		push_error(message)

func run() -> void:
	for projection in ["square","isometric"]:
		var folder = BASE+projection+"/"
		var scene = load(folder+"depth_review.tscn").instantiate()
		var old_guard = scene.get_node("DepthGuard")
		scene.remove_child(old_guard)
		old_guard.free()
		var depth: TileMapLayer = scene.get_node("Depth")
		var water: TileMapLayer = scene.get_node("Water")
		depth.clear()
		var cells: Array[Vector2i] = []
		for y in range(-5,16):
			for x in range(3,15):
				var data = water.get_cell_tile_data(Vector2i(x,y))
				if data!=null and data.terrain==0 and data.terrain_set==0:
					if x<9 or (x<13 and y>=4 and y<=11): cells.append(Vector2i(x,y))
		depth.set_cells_terrain_connect(cells,0,0,false)
		depth.material.set_shader_parameter("depth_permitted",false)
		var binding = load("res://addons/beep_game_builder_cs/ecs/terrain/TerrainSeaDepthBinding.gd").new()
		binding.name = "CoastalDepthBinding"
		binding.water_path = NodePath("../Water")
		binding.depth_path = NodePath("../Depth")
		scene.add_child(binding)
		binding.owner = scene
		root.add_child(scene)
		check(scene.get_node("SurfacePlane").refresh().is_empty(),"Surface binding failed")
		check(binding.refresh().is_empty(),"Coastal depth invalid: "+binding.last_error)
		# The field is derived on load, not stored as another embedded image draft.
		water.material.set_shader_parameter("depth_field_enabled",false)
		water.material.set_shader_parameter("depth_field_texture",null)
		scene.name = "CoastalDepthCandidate"
		scene.set_meta("pending","Visual approval, river/lake depth contacts, chunked fields and production engine integration")
		var packed = PackedScene.new()
		check(packed.pack(scene)==OK,"Pack coastal depth")
		check(ResourceSaver.save(packed,folder+"coastal_depth_review.tscn")==OK,"Save coastal depth")
		scene.free()
	print("COASTAL DEPTH ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
