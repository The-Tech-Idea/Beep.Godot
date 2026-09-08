extends SceneTree

# One sea, in both projections that draw a water SURFACE.
#
# shaders/water_common.gdshaderinc declares the dials the sea reads and
# TerrainWaterMaterial is the one place C# writes them. The contract scan pins that no
# view sets them itself; this pins the half a static read cannot see - that each view
# actually hands its authored value through, so one map's coast behaves the same way
# however it is drawn.
#
# The defect it guards against is measured: before TerrainWaterMaterial the tile view
# wrote 13 of the 22 shared uniforms. It set use_foam_sheet from its own FoamSheetPath
# and then set none of the five dials that make the sheet behave, nor either swell
# dial, so an authored coastline surfed in the isometric view and ran on shader
# defaults in the tile view of the same map.

const BASE := "res://addons/beep_game_builder_cs/ecs/"
const WATER_SHADER := "res://addons/beep_game_builder_cs/shaders/iso_water.gdshader"

# export name -> [uniform, authored value]. Every value is deliberately NOT the
# shader's own default: a probe that authors the default cannot tell "the view passed
# my value" from "the view passed nothing at all", which is exactly the failure here.
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

func configure(view: Node) -> void:
	view.set("RefreshOnReady", false)
	view.set("TerrainGeneratorPath", NodePath("../Generator"))
	view.set("BoundsSize", Vector2i(32, 32))
	view.set("WaterShaderPath", WATER_SHADER)
	view.set("CoastRangeTiles", 5.0)
	# The isometric view builds its block TileSet before it reaches the sea, and
	# refuses the whole rebuild without a sheet - so it needs the shipped art even
	# though this probe only ever reads the water material.
	view.set("BlockSheetPath", "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png")
	view.set("SheetColumns", 8)
	view.set("SheetRows", 7)
	for export_name in DIALS:
		view.set(export_name, DIALS[export_name][1])

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

	var tile: Node = load(BASE + "terrain/TerrainTileRendererComponent.cs").new()
	tile.name = "Tile"
	configure(tile)
	host.add_child(tile)
	var iso: Node = load(BASE + "terrain/TerrainIsometricRendererComponent.cs").new()
	iso.name = "Iso"
	configure(iso)
	host.add_child(iso)

	tile.call("Rebuild")
	iso.call("Rebuild")

	var views := {
		"tile": water_material(tile, "TileWater"),
		"iso": water_material(iso, "IsoWater"),
	}
	if failures.is_empty():
		# Every dial reaches every view. A uniform the view never set reads back as
		# null - which is precisely how the tile view used to answer for the seven
		# dials it did not carry, so the null case is the one that matters.
		for name in views:
			var material: ShaderMaterial = views[name]
			for export_name in DIALS:
				var uniform: String = DIALS[export_name][0]
				var expected: float = DIALS[export_name][1]
				var actual = material.get_shader_parameter(uniform)
				check(actual != null,
					"the %s view never set %s, so an authored %s runs on the shader's default" % [name, uniform, export_name])
				if actual != null:
					check(is_equal_approx(float(actual), expected),
						"the %s view sent %s = %s, not the authored %s" % [name, uniform, str(actual), str(expected)])

		# ...and both views agree, which is the property the shared include exists for.
		for export_name in DIALS:
			var uniform: String = DIALS[export_name][0]
			var a = views["tile"].get_shader_parameter(uniform)
			var b = views["iso"].get_shader_parameter(uniform)
			check(a != null and b != null and is_equal_approx(float(a), float(b)),
				"the two views disagree about %s: tile=%s iso=%s" % [uniform, str(a), str(b)])

		# An empty sheet path must leave the shader on generated crests. Turning the
		# authored-surf path on with no sheet behind it draws no surf at all.
		for name in views:
			check(views[name].get_shader_parameter("use_foam_sheet") == false,
				"the %s view switched on the authored-surf path with no sheet loaded" % name)

		# The shared writer holds values to the shader's own hint_range. An export's
		# PropertyHint.Range constrains the Inspector and nothing else, so a script
		# assigning past it used to reach the shader unchecked in two of three views.
		for view in [tile, iso]:
			view.set("DeepTiles", 999.0)
			view.set("FoamStrength", -3.0)
			view.call("Rebuild")
		for name in views:
			var material: ShaderMaterial = views[name]
			check(is_equal_approx(float(material.get_shader_parameter("deep_tiles")), 12.0),
				"the %s view let deep_tiles past the shader's ceiling of 12" % name)
			check(is_equal_approx(float(material.get_shader_parameter("foam_strength")), 0.0),
				"the %s view let foam_strength below the shader's floor of 0" % name)

	host.free()
	if failures.is_empty():
		print("[terrain-water-material] OK")
		quit(0)
	else:
		print("[terrain-water-material] FAILED")
		quit(1)
