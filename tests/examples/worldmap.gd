extends SceneTree

# Renders the generator's output as a plain top-down world map, one pixel block
# per tile, so the SHAPE of the land can be judged directly. The isometric scene
# is the product; this is the instrument - a continent that is really a web of
# tendrils is obvious here and easy to miss under blocks and props.
#
#   WORLDMAP_DIR=<dir> godot --headless --path <project> \
#       --script res://tests/examples/worldmap.gd

const SCALE := 6

const COLOURS := {
	"deep_water": Color8(24, 52, 94),
	"shallow_water": Color8(58, 108, 158),
	"sand": Color8(214, 194, 140),
	"grass": Color8(96, 140, 68),
	"dry_grass": Color8(150, 158, 92),
	"desert": Color8(206, 178, 118),
	"rock": Color8(126, 122, 116),
	"gravel": Color8(150, 146, 138),
	"snow": Color8(238, 242, 246),
	"tundra": Color8(158, 166, 152),
	"dirt": Color8(126, 100, 74),
	"forest": Color8(58, 96, 54),
	"swamp": Color8(78, 104, 76),
}

func paint(gen, size: Vector2i, image: Image, at: Vector2i) -> void:
	for y in range(size.y):
		for x in range(size.x):
			var kind: String = gen.TerrainKindAt(Vector2i(x, y))
			var colour: Color = COLOURS.get(kind, Color8(255, 0, 255))
			for py in range(SCALE):
				for px in range(SCALE):
					image.set_pixel(at.x + x * SCALE + px, at.y + y * SCALE + py, colour)

func _initialize() -> void:
	var out_dir: String = OS.get_environment("WORLDMAP_DIR")
	if out_dir == "":
		print("FAIL: WORLDMAP_DIR is not set")
		quit(1)
		return

	var root_node = load("res://tests/examples/terrain_iso_demo.tscn").instantiate()
	get_root().add_child(root_node)
	for i in range(20):
		await process_frame
	var gen = root_node.find_child("TerrainGenerator", true, false)

	var size := Vector2i(128, 80)
	gen.BoundsSize = size

	# landform, island count, label
	# landform, island count, coverage, label - the coverage each shape preset
	# actually ships with, so this shows the generator as a game would get it.
	var panels := [
		[0, 3, 0.42, "Continents"],
		[2, 7, 0.30, "Archipelago"],
		[2, 12, 0.22, "Island chain"],
		[1, 1, 0.66, "Pangaea"],
	]

	var cell := Vector2i(size.x * SCALE, size.y * SCALE)
	var image := Image.create(cell.x * 2, cell.y * 2, false, Image.FORMAT_RGBA8)
	image.fill(Color8(12, 14, 18))

	var index := 0
	for panel in panels:
		gen.Landform = panel[0]
		gen.ArchipelagoIslandCount = panel[1]
		# Land coverage is reached by two different dials depending on the mode:
		# Mainland derives it as 1 - SeaCoverage, everything else reads
		# LandmassScale directly.
		if panel[0] == 0:
			gen.SeaCoverage = 1.0 - float(panel[2])
		else:
			gen.LandmassScale = float(panel[2])
		gen.GenerateTerrain()
		var at := Vector2i((index % 2) * cell.x, (index / 2) * cell.y)
		paint(gen, size, image, at)
		print("%s: landform %d, %d asked, %d%% land" % [panel[3], panel[0], panel[1], int(panel[2] * 100.0)])
		index += 1

	var path := "%s/worldmap.png" % out_dir
	if image.save_png(path) != OK:
		print("FAIL: could not save %s" % path)
		quit(1)
		return
	print("saved %s  %dx%d" % [path, image.get_width(), image.get_height()])
	quit(0)
