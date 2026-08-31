extends SceneTree

# The Game tiles view must draw through the SHARED layer stack, and be checked
# for it rather than looked at.
#
# Its layering was wrong twice and a screenshot showed neither fault honestly.
# The sand layer had 436 tiles drawn and then buried under deep water, because
# this view ordered its layers by biome and drew water last; and the atlases
# imported without mipmaps, so a 64px tile drawn at 9px aliased into mush. Both
# are invisible in a small picture and obvious in the numbers.
#
# The order this asserts is TerrainLayers': base, sea, ground, hills, mountains,
# with the shader sea between the water tiles and the land.

const SEA_Z := 0        # TerrainLayers.ZFor(Sea)
const GROUND_Z := 2     # TerrainLayers.ZFor(Ground)
const HILLS_Z := 4
const MOUNTAIN_Z := 6

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func _initialize() -> void:
	var root_node = load("res://tests/examples/terrain_tilemap_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(45): await process_frame

	var gen = root_node.find_child("TerrainGenerator", true, false)
	var tr = root_node.find_child("TileRenderer", true, false)
	if tr == null:
		tr = root_node.find_child("BiomeTiles", true, false)
	if tr == null:
		for n in root_node.find_children("*", "Node2D", true, false):
			if n.find_child("BaseTiles", true, false) != null:
				tr = n
				break

	var layers := {}
	for n in tr.find_children("*", "TileMapLayer", true, false):
		layers[n.name] = n

	check(layers.size() > 0, "the view draws through TileMapLayers (%d of them)" % layers.size())

	# Water beneath land. This is the fault that buried the beach: drawing the
	# sea last let its transition tiles cover the shore, beach included.
	var deep = layers.get("DeepWaterTiles")
	var sand = layers.get("SandTiles")
	var grass = layers.get("GrassTiles")
	if deep != null and sand != null:
		check(deep.z_index < sand.z_index,
			"sea draws beneath the beach (water z%d < sand z%d)" % [deep.z_index, sand.z_index])
	if deep != null and grass != null:
		check(deep.z_index < grass.z_index,
			"sea draws beneath the land (water z%d < ground z%d)" % [deep.z_index, grass.z_index])

	# The levels themselves, against the shared stack.
	if grass != null:
		check(grass.z_index < GROUND_Z, "ground sits below the ground level's own z")
	var rock = layers.get("RockTiles")
	if rock != null and grass != null:
		check(rock.z_index > grass.z_index,
			"mountains draw above the ground (rock z%d > ground z%d)" % [rock.z_index, grass.z_index])

	# The sea surface, between the water tiles and the land.
	var water = tr.find_child("TileWater", true, false)
	check(water != null, "the sea has a shader surface, not just flat tiles")
	if water != null:
		check(water.z_index == SEA_Z,
			"the sea surface is at the shared sea level (z%d)" % water.z_index)
		if deep != null:
			check(deep.z_index < water.z_index, "the sea surface draws over its own bed")
		if grass != null:
			check(water.z_index < grass.z_index, "the land draws over the sea surface")
		var mat: ShaderMaterial = water.material
		check(mat != null and mat.get_shader_parameter("flat_projection") == 1.0,
			"the sea uses the shared shader in its top-down projection")

	# Every kind the generator produced must have somewhere to be drawn.
	var size: Vector2i = gen.BoundsSize
	var kinds := {}
	for y in range(size.y):
		for x in range(size.x):
			var k: String = gen.TerrainKindAt(Vector2i(x, y))
			kinds[k] = kinds.get(k, 0) + 1

	for kind in ["sand", "grass", "rock"]:
		if kinds.get(kind, 0) < 20:
			continue
		var node: String = "%sTiles" % kind.capitalize()
		var layer = layers.get(node)
		check(layer != null and layer.get_used_cells().size() > 0,
			"%d %s tiles reach a layer that draws them" % [kinds[kind], kind])

	# Rebuilding must keep the map, not empty it.
	#
	# This view reuses its layers when the configuration behind them has not
	# changed, and until now that branch could never run: the reuse test compared
	# _layers.Count against the configured biomes, while _layers also holds the
	# filled base, so with a base atlas set the counts never matched and every
	# rebuild tore down and recreated every layer. Fixing the test brought a code
	# path into use that had never executed, so it needs covering: a reuse that
	# repaints nothing looks exactly like a map that generated nothing.
	var before := {}
	for name in layers:
		before[name] = layers[name].get_used_cells().size()

	tr.Rebuild()
	for i in range(20): await process_frame

	var relayered := {}
	for n in tr.find_children("*", "TileMapLayer", true, false):
		relayered[n.name] = n

	check(relayered.size() == layers.size(),
		"rebuilding keeps the same %d layers" % layers.size())
	for name in before:
		if before[name] == 0:
			continue
		var again = relayered.get(name)
		check(again != null and again.get_used_cells().size() == before[name],
			"%s still has its %d tiles after a rebuild" % [name, before[name]])

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
