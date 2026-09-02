extends SceneTree

# The underground and liquid strata: deposits exist, lie under the terrain
# their definitions name, carry sane richness and depth, land in the right
# stratum, and repeat exactly for a seed.

const GENERATOR := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainGeneratorComponent.cs")
const DATA_LAYERS := preload("res://addons/beep_game_builder_cs/ecs/terrain/TerrainDataLayersComponent.cs")
const STORE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridSubsurfaceStoreComponent.cs")
const CATALOG := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceCatalog.cs")
const DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceDefinition.cs")
const EXTRACTOR := preload("res://addons/beep_game_builder_cs/ecs/grid/GridExtractorComponent.cs")
const WALLET := preload("res://addons/beep_game_builder_cs/ecs/grid/GridResourceWalletComponent.cs")
const GRID_OBJECT := preload("res://addons/beep_game_builder_cs/ecs/grid/GridObjectComponent.cs")
const SIZE := Vector2i(64, 40)
const SEED := 424242

# ResourceSet enum values, mirroring ResourceCatalogs.
const SET_HISTORICAL := 0
const SET_OIL_AND_GAS := 1

# Surface kinds each underground id may lie beneath (from ResourceCatalogs).
const OIL_UNDER := {
	"crude_oil": ["desert", "dry_grass", "swamp"],
	"offshore_oil": ["deep_water"],
	"natural_gas": ["desert", "tundra", "dry_grass"],
	"offshore_gas": ["shallow_water", "deep_water"],
	"shale": ["gravel", "rock", "dry_grass"],
	"oil_sands": ["tundra", "swamp"],
	"condensate": ["desert", "shallow_water"],
	"helium": ["desert", "rock"],
	"coalbed_methane": ["gravel", "rock"],
}

# ResourceDepth bands for a few known ids.
const OIL_DEPTH := { "oil_sands": 0, "crude_oil": 1, "offshore_oil": 2 }

const WATER_KINDS := ["shallow_water", "deep_water", "water"]

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func make_generator(seed_value: int, set_value: int) -> Node:
	var generator: Node = GENERATOR.new()
	generator.set("BoundsSize", SIZE)
	generator.set("Seed", seed_value)
	generator.set("ResourceSet", set_value)
	generator.set("ResourceDensity", 1.5)
	root.add_child(generator)
	return generator

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var generator := make_generator(SEED, SET_OIL_AND_GAS)
	await process_frame

	var underground_cells := 0
	var wrong_ground := 0
	var bad_richness := 0
	var wrong_depth := 0
	var ids := {}
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			var id: String = str(generator.call("UndergroundResourceAt", cell))
			var richness: float = float(generator.call("UndergroundRichnessAt", cell))
			if id == "":
				if richness != 0.0:
					bad_richness += 1
				continue
			underground_cells += 1
			ids[id] = int(ids.get(id, 0)) + 1
			if richness <= 0.0 or richness > 1.0:
				bad_richness += 1
			var kind: String = str(generator.call("TerrainKindAt", cell))
			if OIL_UNDER.has(id) and not (kind in OIL_UNDER[id]):
				wrong_ground += 1
			if OIL_DEPTH.has(id) and int(generator.call("UndergroundDepthAt", cell)) != int(OIL_DEPTH[id]):
				wrong_depth += 1

	check(underground_cells > 0, "the map holds underground deposits (%d cells: %s)" % [underground_cells, str(ids)])
	check(ids.size() >= 2, "more than one underground resource generated (%s)" % str(ids))
	check(wrong_ground == 0, "every underground cell lies beneath a terrain its definition names (%d wrong)" % wrong_ground)
	check(bad_richness == 0, "richness is in (0,1] on deposits and 0 elsewhere (%d bad)" % bad_richness)
	check(wrong_depth == 0, "depth bands match the authored resource depths (%d wrong)" % wrong_depth)

	# Deposits are FIELDS: at least one deposit cell touches another cell of
	# the same id, which a sprinkle of independent markers would fail.
	var adjacent := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var id: String = str(generator.call("UndergroundResourceAt", Vector2i(x, y)))
			if id == "":
				continue
			if x + 1 < SIZE.x and str(generator.call("UndergroundResourceAt", Vector2i(x + 1, y))) == id:
				adjacent += 1
	check(adjacent > 0, "deposits form contiguous fields, not lone markers (%d adjacent pairs)" % adjacent)

	# Same seed, same strata - byte for byte.
	var twin := make_generator(SEED, SET_OIL_AND_GAS)
	await process_frame
	var diverged := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			if str(generator.call("UndergroundResourceAt", cell)) != str(twin.call("UndergroundResourceAt", cell)):
				diverged += 1
			if str(generator.call("LiquidResourceAt", cell)) != str(twin.call("LiquidResourceAt", cell)):
				diverged += 1
	check(diverged == 0, "the same seed lays the same strata (%d diverged)" % diverged)

	# The liquid stratum: Historical fish live on water cells, and the surface
	# array holds land resources only.
	var historical := make_generator(SEED, SET_HISTORICAL)
	await process_frame
	var liquid_cells := 0
	var liquid_on_land := 0
	var surface_on_water := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			var kind: String = str(historical.call("TerrainKindAt", cell))
			var is_water: bool = kind in WATER_KINDS
			if str(historical.call("LiquidResourceAt", cell)) != "":
				liquid_cells += 1
				if not is_water:
					liquid_on_land += 1
			if str(historical.call("ResourceAt", cell)) != "" and is_water:
				surface_on_water += 1
	check(liquid_cells > 0, "the liquid stratum holds resources - fish exist (%d cells)" % liquid_cells)
	check(liquid_on_land == 0, "liquid resources sit only on water (%d on land)" % liquid_on_land)
	check(surface_on_water == 0, "the surface array holds no water placements any more (%d on water)" % surface_on_water)

	# Publication: the data layers answer the same strata as the generator, so
	# a saved map needs no generator at runtime. Richness is banded on the
	# layer, so it is compared as "non-zero where the field is non-zero".
	var layers: Node = DATA_LAYERS.new()
	layers.set("TerrainGeneratorPath", NodePath("../" + str(generator.name)))
	layers.set("BoundsSize", SIZE)
	layers.set("RefreshOnReady", false)
	root.add_child(layers)
	await process_frame
	layers.call("Rebuild")

	var published_wrong := 0
	var richness_wrong := 0
	var depth_wrong := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			var id: String = str(generator.call("UndergroundResourceAt", cell))
			if str(layers.call("UndergroundResourceAt", cell)) != id:
				published_wrong += 1
			if id != "":
				if float(layers.call("UndergroundRichnessAt", cell)) <= 0.0:
					richness_wrong += 1
				if int(layers.call("UndergroundDepthAt", cell)) != int(generator.call("UndergroundDepthAt", cell)):
					depth_wrong += 1
	check(published_wrong == 0, "the underground layer publishes the generator's deposits (%d wrong)" % published_wrong)
	check(richness_wrong == 0, "published richness is non-zero on every deposit (%d wrong)" % richness_wrong)
	check(depth_wrong == 0, "published depth bands match the generator (%d wrong)" % depth_wrong)

	var liquid_layers: Node = DATA_LAYERS.new()
	liquid_layers.set("TerrainGeneratorPath", NodePath("../" + str(historical.name)))
	liquid_layers.set("BoundsSize", SIZE)
	liquid_layers.set("RefreshOnReady", false)
	root.add_child(liquid_layers)
	await process_frame
	liquid_layers.call("Rebuild")

	var liquid_published_wrong := 0
	for y in SIZE.y:
		for x in SIZE.x:
			var cell := Vector2i(x, y)
			if str(liquid_layers.call("LiquidResourceAt", cell)) != str(historical.call("LiquidResourceAt", cell)):
				liquid_published_wrong += 1
	check(liquid_published_wrong == 0, "the liquid layer publishes the generator's water column (%d wrong)" % liquid_published_wrong)

	# Extraction: the store owns the drawdown, the extractor is only the pump.
	var deposit_cell := Vector2i(-1, -1)
	for y in SIZE.y:
		for x in SIZE.x:
			if str(layers.call("UndergroundResourceAt", Vector2i(x, y))) != "":
				deposit_cell = Vector2i(x, y)
				break
		if deposit_cell.x >= 0:
			break
	check(deposit_cell.x >= 0, "a deposit cell exists to extract from")

	var store: Node = STORE.new()
	store.name = "Subsurface"
	store.set("DataLayersPath", NodePath("../" + str(layers.name)))
	root.add_child(store)

	var deposit_id: String = str(store.call("ResourceIdAt", deposit_cell))
	var remaining0: int = int(store.call("RemainingAt", deposit_cell))
	check(deposit_id != "", "the store reads the deposit id from the layers (%s)" % deposit_id)
	check(remaining0 > 0, "the store seeds remaining amount from richness (%d units)" % remaining0)

	var wallet: Node = WALLET.new()
	wallet.name = "Wallet"
	wallet.set("ApplyStartingResourcesOnReady", false)
	root.add_child(wallet)

	var rig := Node2D.new()
	rig.name = "Derrick"
	root.add_child(rig)
	var rig_object: Node = GRID_OBJECT.new()
	rig_object.set("Cell", deposit_cell)
	rig_object.set("Footprint", Vector2i.ONE)
	rig_object.set("BlocksNavigation", false)
	rig.add_child(rig_object)
	var extractor: Node = EXTRACTOR.new()
	extractor.set("SubsurfaceStorePath", NodePath("../../Subsurface"))
	extractor.set("ResourceWalletPath", NodePath("../../Wallet"))
	rig.add_child(extractor)
	await process_frame

	# Default cycle is 1.5s with no catalog; each 1.6s tick is one cycle.
	for i in remaining0 + 4:
		extractor.call("Tick", 1.6)
	var pumped: int = int(wallet.call("GetAmount", deposit_id))
	check(pumped == remaining0, "the extractor pumped the whole deposit into the wallet (%d of %d)" % [pumped, remaining0])
	check(int(store.call("RemainingAt", deposit_cell)) == 0, "the deposit is worked out")
	check(bool(extractor.get("IsExtracting")) == false, "the extractor stopped on depletion")

	# A FLUID deposit is one connected reservoir: a pump on one cell drains
	# the whole contiguous field, not just what it stands over.
	var fluid_def: Resource = DEFINITION.new()
	fluid_def.set("Id", "probe_oil")
	fluid_def.set("DisplayName", "Probe Oil")
	fluid_def.set("Stratum", 2)        # Underground
	fluid_def.set("Form", 1)           # Fluid
	fluid_def.set("Depth", 0)          # Shallow
	fluid_def.set("DepositScale", 0.9)
	fluid_def.set("Amount", 4)
	fluid_def.set("AmountPerGather", 3)
	fluid_def.set("GatherSeconds", 0.5)
	var kinds: Array[String] = ["grass", "dry_grass", "desert", "sand", "tundra", "gravel", "rock", "swamp"]
	fluid_def.set("TerrainKinds", kinds)
	var probe_catalog: Resource = CATALOG.new()
	var defs: Array[Resource] = [fluid_def]
	probe_catalog.set("Resources", defs)

	var fluid_generator: Node = GENERATOR.new()
	fluid_generator.set("BoundsSize", SIZE)
	fluid_generator.set("Seed", SEED)
	fluid_generator.set("Resources", probe_catalog)
	fluid_generator.set("ResourceDensity", 1.0)
	root.add_child(fluid_generator)
	await process_frame

	var fluid_layers: Node = DATA_LAYERS.new()
	fluid_layers.name = "FluidLayers"
	fluid_layers.set("TerrainGeneratorPath", NodePath("../" + str(fluid_generator.name)))
	fluid_layers.set("BoundsSize", SIZE)
	fluid_layers.set("RefreshOnReady", false)
	root.add_child(fluid_layers)
	await process_frame
	fluid_layers.call("Rebuild")

	var fluid_store: Node = STORE.new()
	fluid_store.name = "FluidStore"
	fluid_store.set("DataLayersPath", NodePath("../FluidLayers"))
	fluid_store.set("Catalog", probe_catalog)
	root.add_child(fluid_store)

	# Group every probe_oil cell into connected fields and take the LARGEST -
	# a field's rim can be a lone cell, and one cell cannot prove a reservoir.
	var assigned := {}
	var best_component: Array[Vector2i] = []
	for y in SIZE.y:
		for x in SIZE.x:
			var origin := Vector2i(x, y)
			if assigned.has(origin):
				continue
			if str(fluid_layers.call("UndergroundResourceAt", origin)) != "probe_oil":
				continue
			var component: Array[Vector2i] = []
			var frontier: Array[Vector2i] = [origin]
			assigned[origin] = true
			while frontier.size() > 0:
				var cell: Vector2i = frontier.pop_back()
				component.append(cell)
				for offset in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
					var next: Vector2i = cell + offset
					if assigned.has(next):
						continue
					if str(fluid_layers.call("UndergroundResourceAt", next)) == "probe_oil":
						assigned[next] = true
						frontier.append(next)
			if component.size() > best_component.size():
				best_component = component
	check(best_component.size() >= 3,
		"the fluid catalog generated a multi-cell probe_oil field (largest: %d cells)" % best_component.size())
	if best_component.size() == 0:
		print("\nRESULT: %d FAILED" % failures.size())
		quit(1)
		return

	var start: Vector2i = best_component[0]
	var reservoir_total := 0
	for cell in best_component:
		reservoir_total += int(fluid_store.call("RemainingAt", cell))
	var start_remaining: int = int(fluid_store.call("RemainingAt", start))

	var fluid_wallet: Node = WALLET.new()
	fluid_wallet.name = "FluidWallet"
	fluid_wallet.set("ApplyStartingResourcesOnReady", false)
	root.add_child(fluid_wallet)

	var pump := Node2D.new()
	pump.name = "Pumpjack"
	root.add_child(pump)
	var pump_object: Node = GRID_OBJECT.new()
	pump_object.set("Cell", start)
	pump_object.set("Footprint", Vector2i.ONE)
	pump_object.set("BlocksNavigation", false)
	pump.add_child(pump_object)
	var pumpjack: Node = EXTRACTOR.new()
	pumpjack.set("SubsurfaceStorePath", NodePath("../../FluidStore"))
	pumpjack.set("ResourceWalletPath", NodePath("../../FluidWallet"))
	pumpjack.set("Catalog", probe_catalog)
	pump.add_child(pumpjack)
	await process_frame

	var cycles: int = int(ceil(reservoir_total / 3.0)) + 4
	for i in cycles:
		pumpjack.call("Tick", 0.6)
	var reservoir_pumped: int = int(fluid_wallet.call("GetAmount", "probe_oil"))
	check(reservoir_pumped == reservoir_total,
		"one pump drained the whole connected reservoir (%d of %d from %d cells)"
			% [reservoir_pumped, reservoir_total, best_component.size()])
	check(reservoir_pumped > start_remaining,
		"the pump drew more than its own cell held (%d pumped, cell held %d) - the field is one reservoir"
			% [reservoir_pumped, start_remaining])

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
