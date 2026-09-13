extends SceneTree

const PLATEAUS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_plateaus_v1/"
const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_tiers_v1/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(BASE)
	var errors = []
	var cases = []
	for count in [2,3]:
		var scene = Node2D.new()
		scene.name = "IsometricTieredPlateau"
		var definitions = [
			{"size":Vector2i(5,3),"cell":Vector2i.ZERO},
			{"size":Vector2i(3,2),"cell":Vector2i(1,0)},
			{"size":Vector2i(1,1),"cell":Vector2i(2,0)}]
		var tiers = []
		for level in range(count):
			var definition = definitions[level]
			var size: Vector2i = definition.size
			var tier = load(PLATEAUS+"plateau_%dx%d.tscn" % [size.x,size.y]).instantiate()
			tier.name = "Tier%d" % level
			var walls: TileMapLayer = tier.get_node("CliffWalls")
			var offset = walls.map_to_local(definition.cell)-walls.map_to_local(Vector2i.ZERO)
			tier.position = offset-Vector2(0,level*64)
			tier.z_index = level*2
			# Each elevated surface uses the same logical material origin, not a restarted texture.
			tier.get_node("SurfacePlane").position = Vector2(0,-64)-offset
			tier.set_meta("base_elevation_pixels",level*64)
			scene.add_child(tier)
			tier.owner = scene
			scene.set_editable_instance(tier,true)
			tier.get_node("SurfacePlane").owner = scene
			tiers.append({"node":str(tier.name),"cell":[definition.cell.x,definition.cell.y],"dimensions":[size.x,size.y],"baseElevationPixels":level*64,"topElevationPixels":(level+1)*64})
		var file = "tiers_%d.tscn" % count
		var packed = PackedScene.new()
		var result = packed.pack(scene)
		if result==OK: result = ResourceSaver.save(packed,BASE+file)
		if result!=OK: errors.append(file)
		cases.append({"scene":file,"tiers":tiers})
		scene.free()
	var manifest = FileAccess.open(BASE+"manifest.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"schemaVersion":1,"status":"candidate","style":"cartoon","projection":"isometric","cases":cases,"risePerTierPixels":64,"sourceManifest":PLATEAUS+"manifest.json","productionReady":false,"approvalEvidence":null,"pending":["Visual approval","Wall variation","Concave tiers","Bottom contacts","Navigation authored separately"]},"  "))
	print("ISOMETRIC TIERS ","PASSED" if errors.is_empty() else "FAILED")
	quit(0 if errors.is_empty() else 1)
