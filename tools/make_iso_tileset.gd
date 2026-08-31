extends SceneTree

# Builds a starter isometric TileSet from a 17-piece atlas, so the only thing
# left to do by hand is the part a human has to do: paint the terrain peering
# bits on each tile.
#
# Everything mechanical is done here - isometric tile shape, diamond-down
# layout, the atlas source, all 17 tiles created, one terrain set with two
# terrains named. Deriving the corner bits from the pixels is what does NOT
# work: on a textured sheet two shades of the same terrain are not separable,
# and a mapping guessed that way agreed with the known one on a third of tiles.
#
#   godot --headless --path <project> --script res://tools/make_iso_tileset.gd
#
# Then open the generated .tres, select the TileSet, and in the Terrains tab
# paint the corners of each tile. Godot does the matching from there.

const ATLASES := [
	{
		"texture": "res://addons/beep_game_builder_cs/textures/iso/grassland.png",
		"output": "res://addons/beep_game_builder_cs/textures/iso/grassland_tileset.tres",
		"lower": "Grass",
		"upper": "Meadow",
	},
]

# Measured from the supplied sheets: the diamond art is 324x181, not the 396x198
# atlas cell it sits in. Using the cell leaves a gap on every edge and the map
# seams; using the diamond is what makes the ground continuous.
const CELL := Vector2i(324, 181)
const COLUMNS := 5
const ROWS := 4
const TILE_COUNT := 17

func _initialize() -> void:
	var made := 0
	for entry in ATLASES:
		if _build(entry):
			made += 1
	print("built %d of %d tilesets" % [made, ATLASES.size()])
	quit(0 if made == ATLASES.size() else 1)

func _build(entry: Dictionary) -> bool:
	var texture: Texture2D = load(entry["texture"])
	if texture == null:
		print("FAIL: could not load ", entry["texture"])
		return false

	var source := TileSetAtlasSource.new()
	source.texture = texture
	source.texture_region_size = CELL
	var created := 0
	for i in range(TILE_COUNT):
		var coords := Vector2i(i % COLUMNS, i / COLUMNS)
		# Guard on the atlas grid rather than has_room_for_tile: that overload
		# reports false for every coordinate here and silently produced a
		# tileset with no tiles at all.
		var grid := source.get_atlas_grid_size()
		if coords.x < grid.x and coords.y < grid.y:
			source.create_tile(coords)
			created += 1

	var tile_set := TileSet.new()
	tile_set.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	tile_set.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	tile_set.tile_size = CELL
	tile_set.add_source(source, 0)

	# Corner matching, because these are corner sets: each tile shows which of
	# its four quadrants belong to the upper terrain.
	tile_set.add_terrain_set()
	tile_set.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS)
	tile_set.add_terrain(0)
	tile_set.set_terrain_name(0, 0, entry["lower"])
	tile_set.add_terrain(0)
	tile_set.set_terrain_name(0, 1, entry["upper"])

	var err := ResourceSaver.save(tile_set, entry["output"])
	if err != OK:
		print("FAIL: could not save %s (error %d)" % [entry["output"], err])
		return false
	print("saved %s  (%d tiles, paint the corners in the Terrains tab)" % [entry["output"], created])
	return true
