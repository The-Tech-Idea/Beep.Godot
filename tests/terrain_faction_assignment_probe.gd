extends SceneTree

# FEAT-10: the faction catalog and the start assignment, so the same player has the same start
# everywhere.
#   - Assign refuses an unknown faction, a start off the map, a start another faction holds and a
#     start the catalog locks elsewhere, each with its own reason; without a catalog it refuses
#     everything rather than pretending to assign;
#   - AutoAssign seats locked factions first, skips unplayable ones, and gives the same table twice;
#   - ActiveStartIndex is the local faction's assigned start with a catalog, LocalStartIndex without;
#   - the assignment survives a save round-trip AND a catalog reordered between save and load,
#     because it is keyed by faction id and not by index;
#   - two spawners owned by two players spawn in their own factions' areas, not both in start 0;
#   - the overlay draws each start's border in that start's faction's colour.

const TERRAIN := "res://addons/beep_game_builder_cs/ecs/terrain/"
const GRID := "res://addons/beep_game_builder_cs/ecs/grid/"
const ACTORS := "res://addons/beep_game_builder_cs/ecs/actors/"
const SIZE := Vector2i(48, 48)
const RADIUS := 10
const RED := Color(0.95, 0.15, 0.1)
const BLUE := Color(0.1, 0.2, 0.95)
const GREEN := Color(0.15, 0.8, 0.3)
const GREY := Color(0.5, 0.5, 0.5)

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, values: Dictionary = {}, parent: Node = null) -> Node:
	var node: Node = load(path).new()
	node.name = label
	for key in values: node.set(key, values[key])
	(parent if parent != null else root).add_child(node)
	return node

# id, colour, playable, locked start
func catalog_of(entries: Array) -> Resource:
	var catalog: Resource = load(GRID + "GridFactionCatalog.cs").new()
	var factions: Array = catalog.get("Factions")
	for entry in entries:
		var faction: Resource = load(GRID + "GridFactionDefinition.cs").new()
		faction.set("FactionId", entry[0])
		faction.set("Colour", entry[1])
		faction.set("Playable", entry[2])
		faction.set("LockedStart", entry[3])
		factions.append(faction)
	return catalog

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var cells := make(GRID + "GridCellDataComponent.cs", "Cells")
	var generator := make(TERRAIN + "TerrainGeneratorComponent.cs", "Generator", {
		"BoundsSize": SIZE, "Seed": 31415, "UseClimateBiomeMaps": true, "UseScaleRules": true,
		"CellDataPath": NodePath("../Cells"), "GenerateOnReady": false, "ClearExistingCells": true})
	generator.call("ApplyMapSetup", 0, 1, 1, 1, 1, 1)
	var kit: Resource = load(TERRAIN + "TerrainStartKit.cs").new()
	kit.set("MinAreaCells", 80)
	generator.set("StartKit", kit)
	generator.set("StartAreaRadius", RADIUS)
	generator.call("GenerateTerrain")
	var layers := make(TERRAIN + "TerrainDataLayersComponent.cs", "Layers", {
		"TerrainGeneratorPath": NodePath("../Generator"), "BoundsSize": SIZE, "RefreshOnReady": false})
	layers.call("Rebuild")
	var grid := make(GRID + "GridProjectionComponent.cs", "Grid", {"TileSize": Vector2(64, 64)})
	var navigation := make(GRID + "GridNavigationComponent.cs", "Navigation", {
		"GridPath": NodePath("../Grid"), "CellDataPath": NodePath("../Cells"), "BoundsSize": SIZE})
	var start_area := make(GRID + "GridStartAreaComponent.cs", "StartArea", {
		"CellDataPath": NodePath("../Cells"), "DataLayersPath": NodePath("../Layers"),
		"NavigationPath": NodePath("../Navigation"), "ParticipatesInSave": false})
	await process_frame

	var starts: Array = generator.call("GetStartPositions")
	var count: int = starts.size()
	check(count >= 3, "the fixture map needs three starts for the assignment checks (%d)" % count)
	check(int(start_area.get("StartCount")) == count, "StartCount is %d, the map has %d starts" % [int(start_area.get("StartCount")), count])

	# ── Assign: every refusal has its own reason ─────────────────────────────
	check(str(start_area.call("Assign", "alpha", 0)) == "no_faction_catalog",
		"without a catalog Assign refuses rather than pretending (%s)" % start_area.call("Assign", "alpha", 0))

	# alpha free, bravo locked to start 3, charlie unplayable, delta free. Bravo is locked to a
	# start catalog order would NOT have given it (second in the catalog would take start 1), so
	# seating the locked factions first is the only rule that produces this table.
	var catalog := catalog_of([["alpha", RED, true, -1], ["bravo", BLUE, true, 3],
		["charlie", GREY, false, -1], ["delta", GREEN, true, -1]])
	start_area.set("FactionCatalog", catalog)
	check(str(start_area.call("Assign", "echo", 0)) == "unknown_faction", "an id the catalog does not hold is unknown_faction")
	check(str(start_area.call("Assign", "alpha", -1)) == "start_out_of_range", "a negative start is out of range")
	check(str(start_area.call("Assign", "alpha", count)) == "start_out_of_range", "a start past the last one is out of range")
	check(str(start_area.call("Assign", "alpha", 0)) == "", "alpha takes start 0")
	check(str(start_area.call("Assign", "delta", 0)) == "start_taken:alpha", "a start alpha holds is refused, naming alpha")
	check(str(start_area.call("Assign", "bravo", 2)) == "start_locked:3", "a locked faction is refused any other start, naming the locked one")
	check(str(start_area.call("Assign", "bravo", 3)) == "", "a locked faction takes its own start")
	check(int(start_area.call("StartIndexOf", "delta")) == -1, "a faction that was refused holds nothing")
	check(int(start_area.call("FactionAtStart", 0)) == 1, "start 0 is held by catalog index 1 (alpha)")
	check(int(start_area.call("FactionAtStart", count - 1)) == 0 if count > 2 else true, "an unheld start is held by nobody (0)")

	# ── AutoAssign: locked first, playable only, deterministic ───────────────
	var seated: int = int(start_area.call("AutoAssign"))
	check(seated == 3, "AutoAssign seated %d playable factions, the catalog has 3" % seated)
	check(int(start_area.call("StartIndexOf", "bravo")) == 3, "bravo keeps its locked start 3, whatever catalog order says")
	check(int(start_area.call("StartIndexOf", "alpha")) == 0, "alpha takes the first free start")
	check(int(start_area.call("StartIndexOf", "delta")) == 1, "delta takes the next free start")
	check(int(start_area.call("StartIndexOf", "charlie")) == -1, "an unplayable faction is seated nowhere")
	var first: Dictionary = start_area.call("GetAssignments")
	start_area.call("AutoAssign")
	check(start_area.call("GetAssignments") == first, "AutoAssign gives the same table twice (%s then %s)" % [first, start_area.call("GetAssignments")])

	# ── ActiveStartIndex ─────────────────────────────────────────────────────
	start_area.set("LocalStartIndex", 0)
	start_area.set("LocalFaction", "bravo")
	check(int(start_area.get("ActiveStartIndex")) == 3, "with a catalog the local player's start is their faction's")
	start_area.set("FactionCatalog", null)
	check(int(start_area.get("ActiveStartIndex")) == 0, "without a catalog the local player's start is LocalStartIndex")
	start_area.set("FactionCatalog", catalog)

	# ── Save round-trip, keyed by id ─────────────────────────────────────────
	var saved: Dictionary = start_area.call("CaptureState")
	check(str(start_area.call("Assign", "alpha", count - 1)) == "", "alpha is moved before the restore")
	check(int(start_area.call("StartIndexOf", "alpha")) == count - 1, "alpha did move")
	start_area.call("RestoreState", saved)
	check(start_area.call("GetAssignments") == first, "the restore put the table back (%s)" % [start_area.call("GetAssignments")])

	# The same assignment against a catalog whose order changed: ids survive, indices would not.
	var reordered := catalog_of([["delta", GREEN, true, -1], ["charlie", GREY, false, -1],
		["bravo", BLUE, true, 3], ["alpha", RED, true, -1]])
	start_area.set("FactionCatalog", reordered)
	start_area.call("RestoreState", saved)
	check(int(start_area.call("StartIndexOf", "alpha")) == 0 and int(start_area.call("StartIndexOf", "bravo")) == 3
		and int(start_area.call("StartIndexOf", "delta")) == 1,
		"a reordered catalog still gives each faction its own start (%s)" % [start_area.call("GetAssignments")])
	start_area.set("FactionCatalog", catalog)
	start_area.call("RestoreState", saved)

	# ── A world build does not overwrite an assignment that fits it ──────────
	# Load order between saveables is not fixed: this component's Load can run BEFORE the world is
	# restored, and a WorldBuilt that always re-deals would then throw the restored table away with
	# nothing reporting it. A Redraw re-emits WorldBuilt too, so the same rule protects a lobby's
	# choices. Only a table that cannot be this world's is replaced.
	var world := make(TERRAIN + "TerrainWorldComponent.cs", "World", {
		"GeneratorPath": NodePath("../Generator"), "BuildOnReady": false, "ParticipatesInSave": false})
	start_area.set("WorldPath", NodePath("../World"))
	await process_frame
	check(str(start_area.call("Assign", "alpha", 4)) == "", "alpha is moved off its dealt start")
	world.emit_signal("WorldBuilt", SIZE)
	check(int(start_area.call("StartIndexOf", "alpha")) == 4,
		"a world build left an assignment that fits the map alone (alpha is on %d)" % int(start_area.call("StartIndexOf", "alpha")))
	start_area.call("RestoreState", saved)

	# A table that does NOT fit - a start this map does not have - is dealt again.
	start_area.call("Assign", "alpha", 0)
	var stale := {"version": 1, "assignments": {"alpha": 99}}
	start_area.call("RestoreState", stale)
	world.emit_signal("WorldBuilt", SIZE)
	check(int(start_area.call("StartIndexOf", "alpha")) == 0,
		"an assignment holding a start off this map was dealt again (alpha is on %d)" % int(start_area.call("StartIndexOf", "alpha")))
	start_area.set("WorldPath", NodePath(""))
	start_area.call("RestoreState", saved)

	# ── Two spawners, two players, two areas ─────────────────────────────────
	var units := Node2D.new()
	units.name = "Units"
	root.add_child(units)
	make(GRID + "GridJobQueueComponent.cs", "Jobs")
	make(ACTORS + "ActorRegistryComponent.cs", "Registry")
	make(ACTORS + "PlayerContextComponent.cs", "PlayerOne", {
		"RegistryPath": NodePath("../Registry"), "PlayerId": "player_1", "FactionId": "alpha", "ReadLocalInput": false})
	make(ACTORS + "PlayerContextComponent.cs", "PlayerTwo", {
		"RegistryPath": NodePath("../Registry"), "PlayerId": "player_2", "FactionId": "bravo", "ReadLocalInput": false})
	await process_frame

	var cells_by_owner := {}
	for owner in ["player_1", "player_2"]:
		var spawner := make(GRID + "GridWorkerSpawnerComponent.cs", "Spawner_" + owner, {
			"GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation"), "JobQueuePath": NodePath("../Jobs"),
			"CellDataPath": NodePath("../Cells"), "UnitsRootPath": NodePath("../Units"),
			"ActorRegistryPath": NodePath("../Registry"), "OwnerId": owner,
			"SpawnAtStartArea": true, "StartAreaPath": NodePath("../StartArea")})
		var landed: Array[Vector2i] = []
		spawner.connect("UnitSpawned", func(_unit, _id, x, y): landed.append(Vector2i(x, y)))
		check(spawner.call("SpawnWorker") != null, "%s's worker was rejected" % owner)
		check(landed.size() == 1, "%s spawned %d workers" % [owner, landed.size()])
		if not landed.is_empty(): cells_by_owner[owner] = landed[0]

	# Each player's unit stands in the area their own faction was assigned.
	for owner in cells_by_owner:
		var faction: String = "alpha" if owner == "player_1" else "bravo"
		var wanted: int = int(start_area.call("StartIndexOf", faction)) + 1
		var got: int = int(generator.call("StartAreaAt", cells_by_owner[owner]))
		check(got == wanted, "%s (%s) spawned in area %d, its faction holds start %d (area %d)" % [owner, faction, got, wanted - 1, wanted])
	check(cells_by_owner.size() == 2 and cells_by_owner["player_1"] != cells_by_owner["player_2"],
		"the two players spawned on different cells (%s)" % [cells_by_owner])

	# ── Overlay: a start's border is its faction's colour ────────────────────
	var overlay := make(TERRAIN + "TerrainMapOverlayComponent.cs", "Overlay", {
		"TerrainGeneratorPath": NodePath("../Generator"), "BoundsSize": SIZE, "RefreshOnReady": false,
		"ShowUndergroundResources": false, "ShowStartAreas": true, "StartAreaPath": NodePath("../StartArea")})
	overlay.call("Rebuild")
	var segments: int = int(overlay.get("StartAreaSegmentCount"))
	check(segments > 0, "the overlay baked start-area segments")
	var seen := {}
	for i in segments:
		var start: int = int(overlay.call("StartAreaSegmentStart", i))
		var colour: Color = overlay.call("StartAreaSegmentColour", i)
		var faction_colour: Color = start_area.call("ColourOfStart", start)
		if faction_colour.a > 0.0:
			check(colour.is_equal_approx(faction_colour),
				"segment %d of start %d is %s, its faction's colour is %s" % [i, start, colour, faction_colour])
			seen[start] = colour
	var alpha_start: int = int(start_area.call("StartIndexOf", "alpha"))
	var bravo_start: int = int(start_area.call("StartIndexOf", "bravo"))
	check(seen.has(alpha_start) and seen[alpha_start].is_equal_approx(RED),
		"alpha's start %d is drawn in alpha's red (%s)" % [alpha_start, seen])
	check(seen.has(bravo_start) and seen[bravo_start].is_equal_approx(BLUE),
		"bravo's locked start %d is drawn in bravo's blue (%s)" % [bravo_start, seen])

	# Unwired, the overlay keeps its own fixed palette rather than drawing nothing.
	overlay.set("StartAreaPath", NodePath(""))
	overlay.call("Rebuild")
	var plain: Color = overlay.call("StartAreaSegmentColour", 0)
	check(not plain.is_equal_approx(Color(0, 0, 0, 0)) and plain.a > 0.0,
		"without a start component the overlay still colours a start from its palette (%s)" % plain)

	print("[terrain-faction-assignment] %d starts, %d seated, %d overlay segments" % [count, seated, segments])
	print("[terrain-faction-assignment] OK" if failures.is_empty() else "[terrain-faction-assignment] FAILED")
	quit(0 if failures.is_empty() else 1)
