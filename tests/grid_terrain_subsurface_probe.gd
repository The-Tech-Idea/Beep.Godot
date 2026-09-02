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
const PROSPECTING := preload("res://addons/beep_game_builder_cs/ecs/grid/GridProspectingComponent.cs")
const TRANSPORT_MANAGER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridTransportManagerComponent.cs")
const EXTRACTION_MANAGER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridExtractionManagerComponent.cs")
const STORAGE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")

# A transporter written in PURE GDSCRIPT: no interface, no C# - just the
# contract's members by name. If this participates, the managers are truly
# open to both languages.
class GdTransporter extends Node:
	var IsBusy := false
	var hauls: Array = []
	var stored := {}
	var capacity := 999

	func CanAccept(_id: String) -> bool:
		return true

	func Load(id: String, amount: int) -> int:
		var total := 0
		for value in stored.values():
			total += int(value)
		var taken: int = min(capacity - total, amount)
		if taken <= 0:
			return 0
		stored[id] = int(stored.get(id, 0)) + taken
		return taken

	func Unload(id: String, amount: int) -> int:
		var held: int = int(stored.get(id, 0))
		var released: int = min(held, amount)
		stored[id] = held - released
		return released

	func RequestHaul(from_cell: Vector2i, id: String, amount: int) -> bool:
		hauls.append([from_cell, id, amount])
		Load(id, amount)
		return true
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

	# Prospecting: with RevealAll off, the underground is hidden until a
	# survey reveals it, the find is announced, and discovery survives saves.
	var prospecting: Node = PROSPECTING.new()
	prospecting.name = "Prospecting"
	prospecting.set("RevealAll", false)
	prospecting.set("DataLayersPath", NodePath("../FluidLayers"))
	prospecting.set("AutoConnect", false)
	root.add_child(prospecting)
	await process_frame

	var hidden_before: bool = not bool(prospecting.call("IsDiscovered", start))
	var found_ids: Array = []
	prospecting.connect("DepositDiscovered", func(_x, _y, id): found_ids.append(str(id)))
	prospecting.call("Survey", start)
	check(hidden_before and bool(prospecting.call("IsDiscovered", start)),
		"a cell is hidden until surveyed, then discovered")
	check(found_ids.has("probe_oil"),
		"surveying over a deposit announces the find (%s)" % str(found_ids))

	var snapshot: Dictionary = prospecting.call("CaptureState")
	var restored: Node = PROSPECTING.new()
	restored.set("RevealAll", false)
	restored.set("AutoConnect", false)
	root.add_child(restored)
	restored.call("RestoreState", snapshot)
	check(bool(restored.call("IsDiscovered", start)), "discovery survives a save round-trip")

	# The managers: expandable by registration, open to GDScript, and the
	# cargo-hold contract connects anything to anything.
	var transport: Node = TRANSPORT_MANAGER.new()
	transport.name = "Transport"
	root.add_child(transport)

	var bare := Node.new()
	bare.name = "NotATransporter"
	root.add_child(bare)
	transport.call("Register", bare)
	check(int(transport.get("TransporterCount")) == 0,
		"a node that does not answer the contract is refused registration")

	var gd_truck := GdTransporter.new()
	gd_truck.name = "GdTruck"
	root.add_child(gd_truck)
	transport.call("Register", gd_truck)
	check(int(transport.get("TransporterCount")) == 1,
		"a pure GDScript transporter registers - the contract is duck-typed, not C#-only")

	# The extractor delivers THROUGH the manager: yield reaches the GDScript
	# truck, not the wallet - and falls back to the wallet when no
	# transporter is free, so nothing is ever lost.
	var transport_store: Node = STORE.new()
	transport_store.name = "TransportStore"
	transport_store.set("DataLayersPath", NodePath("../FluidLayers"))
	transport_store.set("Catalog", probe_catalog)
	root.add_child(transport_store)

	var transport_wallet: Node = WALLET.new()
	transport_wallet.name = "TransportWallet"
	transport_wallet.set("ApplyStartingResourcesOnReady", false)
	root.add_child(transport_wallet)

	var rig2 := Node2D.new()
	rig2.name = "TransportRig"
	root.add_child(rig2)
	var rig2_object: Node = GRID_OBJECT.new()
	rig2_object.set("Cell", start)
	rig2_object.set("Footprint", Vector2i.ONE)
	rig2_object.set("BlocksNavigation", false)
	rig2.add_child(rig2_object)
	var rig2_extractor: Node = EXTRACTOR.new()
	rig2_extractor.set("SubsurfaceStorePath", NodePath("../../TransportStore"))
	rig2_extractor.set("ResourceWalletPath", NodePath("../../TransportWallet"))
	rig2_extractor.set("TransportManagerPath", NodePath("../../Transport"))
	rig2_extractor.set("Catalog", probe_catalog)
	rig2_extractor.set("DeliverVia", 1)
	rig2.add_child(rig2_extractor)
	await process_frame

	for i in 3:
		rig2_extractor.call("Tick", 0.6)
	var hauled_count: int = gd_truck.hauls.size()
	check(hauled_count > 0 and int(transport_wallet.call("GetAmount", "probe_oil")) == 0,
		"extractor yield routed through the transport manager to the GDScript truck (%d hauls)" % hauled_count)

	gd_truck.IsBusy = true
	for i in 3:
		rig2_extractor.call("Tick", 0.6)
	check(int(transport_wallet.call("GetAmount", "probe_oil")) > 0,
		"with every transporter busy the yield falls back to the wallet - never lost")
	gd_truck.IsBusy = false

	# Hand-offs: a tank fills, hands to a truck, the truck hands to a small
	# tank that cannot take it all - and the remainder returns to the truck.
	var tank: Node = STORAGE.new()
	tank.name = "Tank"
	root.add_child(tank)
	check(int(tank.call("Load", "crude_oil", 8)) == 8 and int(tank.call("Stored", "crude_oil")) == 8,
		"a storage tank loads material and reports it")

	var moved: int = int(transport.call("Transfer", tank, gd_truck, "crude_oil", 5))
	check(moved == 5 and int(tank.call("Stored", "crude_oil")) == 3 and int(gd_truck.Unload("crude_oil", 0)) == 0
		and int(gd_truck.stored.get("crude_oil", 0)) == 5,
		"Transfer hands cargo from a tank to a GDScript truck (%d moved)" % moved)

	var small_tank: Node = STORAGE.new()
	small_tank.name = "SmallTank"
	small_tank.set("Capacity", 4)
	root.add_child(small_tank)
	var second: int = int(transport.call("Transfer", gd_truck, small_tank, "crude_oil", 5))
	check(second == 4
		and int(small_tank.call("Stored", "crude_oil")) == 4
		and int(gd_truck.stored.get("crude_oil", 0)) == 1,
		"a full receiver takes what fits and the remainder returns to the giver (%d moved)" % second)
	check(int(small_tank.get("CurrentLoad")) == 4, "CurrentLoad reports the held total")

	# The extraction manager: the shipped extractor registers itself and
	# reports its rate; freeing the rig removes it.
	var extraction: Node = EXTRACTION_MANAGER.new()
	extraction.name = "ExtractionManager"
	root.add_child(extraction)
	var rig3 := Node2D.new()
	rig3.name = "ManagedRig"
	root.add_child(rig3)
	var rig3_object: Node = GRID_OBJECT.new()
	rig3_object.set("Cell", start)
	rig3_object.set("Footprint", Vector2i.ONE)
	rig3_object.set("BlocksNavigation", false)
	rig3.add_child(rig3_object)
	var rig3_extractor: Node = EXTRACTOR.new()
	rig3_extractor.set("SubsurfaceStorePath", NodePath("../../TransportStore"))
	rig3_extractor.set("ExtractionManagerPath", NodePath("../../ExtractionManager"))
	rig3_extractor.set("Catalog", probe_catalog)
	rig3.add_child(rig3_extractor)
	await process_frame
	rig3_extractor.call("Tick", 0.1)
	check(int(extraction.get("ExtractorCount")) == 1, "the shipped extractor registers with the extraction manager")
	check(float(extraction.call("EstimatedRatePerSecond", "probe_oil")) > 0.0,
		"the manager reports the fleet's extraction rate")
	rig3.free()
	check(int(extraction.get("ExtractorCount")) == 0, "a freed extractor leaves the registry")

	# The pipeline hookup: the extractor is BOTH PORTS. With DeliverVia =
	# Buffer its yield fills its own unload port, Transfer draws it into a
	# tank like any other hand-off, and a too-small buffer overflows to the
	# wallet so nothing is lost.
	var buffer_store: Node = STORE.new()
	buffer_store.name = "BufferStore"
	buffer_store.set("DataLayersPath", NodePath("../FluidLayers"))
	buffer_store.set("Catalog", probe_catalog)
	root.add_child(buffer_store)

	var buffer_wallet: Node = WALLET.new()
	buffer_wallet.name = "BufferWallet"
	buffer_wallet.set("ApplyStartingResourcesOnReady", false)
	root.add_child(buffer_wallet)

	var rig4 := Node2D.new()
	rig4.name = "BufferedRig"
	root.add_child(rig4)
	var rig4_object: Node = GRID_OBJECT.new()
	rig4_object.set("Cell", start)
	rig4_object.set("Footprint", Vector2i.ONE)
	rig4_object.set("BlocksNavigation", false)
	rig4.add_child(rig4_object)
	var rig4_extractor: Node = EXTRACTOR.new()
	rig4_extractor.set("SubsurfaceStorePath", NodePath("../../BufferStore"))
	rig4_extractor.set("ResourceWalletPath", NodePath("../../BufferWallet"))
	rig4_extractor.set("Catalog", probe_catalog)
	rig4_extractor.set("DeliverVia", 2)
	rig4_extractor.set("BufferCapacity", 2)
	rig4.add_child(rig4_extractor)
	await process_frame

	# One cycle yields 3: 2 fit the buffer, 1 overflows to the wallet.
	rig4_extractor.call("Tick", 0.6)
	check(int(rig4_extractor.call("Stored", "probe_oil")) == 2
		and int(rig4_extractor.get("CurrentLoad")) == 2,
		"a buffered extractor fills its own unload port (%d held)" % int(rig4_extractor.get("CurrentLoad")))
	check(int(buffer_wallet.call("GetAmount", "probe_oil")) == 1,
		"buffer overflow goes to the wallet - yield is never lost")

	var piped: int = int(transport.call("Transfer", rig4_extractor, tank, "probe_oil", 99))
	check(piped == 2
		and int(tank.call("Stored", "probe_oil")) == 2
		and int(rig4_extractor.get("CurrentLoad")) == 0,
		"Transfer pipes the extractor's buffer into a tank - extractor to storage, port to port (%d moved)" % piped)

	print("\nRESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
