extends SceneTree

# One sea, in every view that draws one.
#
# shaders/water_common.gdshaderinc declares the dials the sea reads and
# TerrainWaterMaterial is the one place C# writes them. Since VIEW-04 the VALUES come from
# one TerrainWaterLook assigned to the world, so the four views cannot disagree: they used
# to export the same thirteen dials each, and the tile view's defaults differed from the
# other two - one map drawn twice grew two seas.
#
# The defect measured before TerrainWaterMaterial: the tile view wrote 13 of the 22 shared
# uniforms. It set use_foam_sheet from its own FoamSheetPath and then set none of the five
# dials that make the sheet behave, nor either swell dial. The look closes the second half
# of that: there is now nowhere for a view to keep its own number.

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const WATER_SHADER := "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader"
const ISO_TILESET := "res://addons/beep_game_builder_cs/textures/iso/lab_terrain_tileset.tres"
const AUTOTILE_BINDINGS := ["grass=0", "dry_grass=1", "sand=2", "desert=3", "jungle=4",
	"swamp,mud,dirt=5", "tundra=6", "snow=7", "ice=8", "gravel=9", "rock=10",
	"water,shallow_water,sea,ocean=11", "deep_water=12"]

# look property -> [uniform, authored value]. Every value is deliberately NOT the shader's
# own default: a probe that authors the default cannot tell "the view passed my value" from
# "the view passed nothing at all", which is exactly the failure here.
const DIALS := {
	"WaveIntensity": ["wave_intensity", 1.7],
	"FoamStrength": ["foam_strength", 0.23],
	"ShallowTiles": ["shallow_tiles", 3.4],
	"DeepTiles": ["deep_tiles", 9.25],
	"GroundTextureTiles": ["ground_texture_tiles", 17.0],
	"WaterTextureTiles": ["water_texture_tiles", 13.0],
	"FoamTilesAlong": ["foam_tiles_along", 21.0],
	"FoamTilesAcross": ["foam_tiles_across", 4.5],
	"FoamScroll": ["foam_scroll", 1.75],
	"FoamPulse": ["foam_pulse", 0.81],
	"FoamArrivalRate": ["foam_arrival_rate", 2.5],
	"SwellDirectionDegrees": ["swell_direction_degrees", 47.0],
	"SwellDirectionality": ["swell_directionality", 0.19],
}

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func configure(view: Node, look: Resource) -> void:
	view.set("RefreshOnReady", false)
	view.set("TerrainGeneratorPath", NodePath("../Generator"))
	view.set("BoundsSize", Vector2i(32, 32))
	view.set("WaterShaderPath", WATER_SHADER)
	view.set("CoastRangeTiles", 5.0)
	view.set("WaterLook", look)
	# The isometric view builds its block TileSet before it reaches the sea, and refuses the
	# whole rebuild without a sheet - so it needs the shipped art even though this probe only
	# ever reads the water material.
	view.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png")
	view.set("SheetColumns", 8)
	view.set("SheetRows", 7)

func water_material(view: Node, child: String) -> ShaderMaterial:
	var node: Node = view.get_node_or_null(child)
	check(node != null, "%s built no %s surface, so the sea was never bound" % [view.name, child])
	if node == null:
		return null
	var material = node.get("material")
	check(material is ShaderMaterial, "%s/%s carries no ShaderMaterial" % [view.name, child])
	return material as ShaderMaterial

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var cells: Node = load(BASE + "grid/GridCellDataComponent.cs").new()
	cells.name = "Cells"
	host.add_child(cells)
	var generator: Node = load(BASE + "terrain/TerrainGeneratorComponent.cs").new()
	generator.name = "Generator"
	generator.set("GenerateOnReady", false)
	generator.set("CellDataPath", NodePath("../Cells"))
	generator.set("BoundsSize", Vector2i(32, 32))
	generator.set("StartPositionCount", 0)
	host.add_child(generator)
	generator.call("GenerateTerrain")

	# ONE look, authored once, for every view.
	var look: Resource = load(BASE + "terrain/TerrainWaterLook.cs").new()
	for property in DIALS:
		look.set(property, DIALS[property][1])

	var tile: Node = load(BASE + "terrain/TerrainTileRendererComponent.cs").new()
	tile.name = "Tile"
	configure(tile, look)
	host.add_child(tile)
	var iso: Node = load(BASE + "terrain/TerrainIsometricRendererComponent.cs").new()
	iso.name = "Iso"
	configure(iso, look)
	host.add_child(iso)
	var autotile: Node = load(BASE + "terrain/TerrainIsometricAutotileRendererComponent.cs").new()
	autotile.name = "IsoAutotile"
	configure(autotile, look)
	autotile.set("Tiles", load(ISO_TILESET))
	autotile.set("UseTerrainConnections", false)
	autotile.set("TerrainBindings", PackedStringArray(AUTOTILE_BINDINGS))
	host.add_child(autotile)
	var painted: Node = load(BASE + "terrain/TerrainPaintedRendererComponent.cs").new()
	painted.name = "Painted"
	painted.set("RefreshOnReady", false)
	painted.set("TerrainGeneratorPath", NodePath("../Generator"))
	painted.set("BoundsSize", Vector2i(32, 32))
	painted.set("WaterLook", look)
	host.add_child(painted)

	tile.call("Rebuild")
	iso.call("Rebuild")
	autotile.call("Rebuild")
	painted.call("Rebuild")

	var views := {
		"tile": water_material(tile, "TileWater"),
		"iso": water_material(iso, "IsoWater"),
		"autotile": water_material(autotile, "TileWater"),
		"painted": water_material(painted, "SplatSurface"),
	}
	if failures.is_empty():
		# Every dial reaches every view. A uniform the view never set reads back as null -
		# which is precisely how the tile view used to answer for the seven dials it did not
		# carry, so the null case is the one that matters.
		for name in views:
			var material: ShaderMaterial = views[name]
			for property in DIALS:
				var uniform: String = DIALS[property][0]
				var expected: float = DIALS[property][1]
				var actual = material.get_shader_parameter(uniform)
				check(actual != null,
					"the %s view never set %s, so the world's %s runs on the shader's default" % [name, uniform, property])
				if actual != null:
					check(is_equal_approx(float(actual), expected),
						"the %s view sent %s = %s, not the world's %s" % [name, uniform, str(actual), str(expected)])

		# ...and every view agrees, which is the property one look exists for.
		for property in DIALS:
			var uniform: String = DIALS[property][0]
			var reference = views["tile"].get_shader_parameter(uniform)
			for name in views:
				var actual = views[name].get_shader_parameter(uniform)
				check(reference != null and actual != null and is_equal_approx(float(reference), float(actual)),
					"the views disagree about %s: tile=%s %s=%s" % [uniform, str(reference), name, str(actual)])

		# An empty sheet path must leave the shader on generated crests. Turning the
		# authored-surf path on with no sheet behind it draws no surf at all.
		for name in views:
			check(views[name].get_shader_parameter("use_foam_sheet") == false,
				"the %s view switched on the authored-surf path with no sheet loaded" % name)

		# The shared writer holds values to the shader's own hint_range. A property hint
		# constrains the Inspector and nothing else, so a script assigning past it used to
		# reach the shader unchecked in two of three views.
		look.set("DeepTiles", 999.0)
		look.set("FoamStrength", -3.0)
		for view in [tile, iso, autotile, painted]:
			view.call("Rebuild")
		for name in views:
			var material: ShaderMaterial = views[name]
			check(is_equal_approx(float(material.get_shader_parameter("deep_tiles")), 12.0),
				"the %s view let deep_tiles past the shader's ceiling of 12" % name)
			check(is_equal_approx(float(material.get_shader_parameter("foam_strength")), 0.0),
				"the %s view let foam_strength below the shader's floor of 0" % name)

	# No view may keep a water dial of its own: the look is the only place a number lives.
	for view in [tile, iso, autotile, painted]:
		for property in view.get_property_list():
			check(not (property.name in DIALS),
				"%s still exports %s; the look is the one owner of the sea's dials" % [view.name, property.name])

	host.free()
	if failures.is_empty():
		print("[terrain-water-material] OK")
		quit(0)
	else:
		print("[terrain-water-material] FAILED")
		quit(1)
