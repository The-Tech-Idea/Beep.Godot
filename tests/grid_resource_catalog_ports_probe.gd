extends SceneTree

# GridStorageComponent/GridHaulerComponent gain an optional link to the
# resource-type catalog: AllowedResourceTags, checked against Catalog,
# accepts a resource by TYPE (ResourceDefinition.Tags) instead of listing
# every id by hand - a tank tagged "ore" accepts a new ore the catalog adds
# later with no scene edit - and closes a typo'd resource id out, since an
# id the catalog does not recognize can never match a tag. AllowedResourceIds
# keeps working exactly as before - checked first, catalog or not - and a
# storage/hauler with neither list set still accepts anything, unchanged.

const RESOURCE_CATALOG := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceCatalog.cs")
const RESOURCE_DEFINITION := preload("res://addons/beep_game_builder_cs/ecs/terrain/ResourceDefinition.cs")
const STORAGE := preload("res://addons/beep_game_builder_cs/ecs/grid/GridStorageComponent.cs")
const HAULER := preload("res://addons/beep_game_builder_cs/ecs/grid/GridHaulerComponent.cs")

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if ok:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		failures.append(message)

func make_definition(id: String, tags: Array) -> Resource:
	var d: Resource = RESOURCE_DEFINITION.new()
	d.set("Id", id)
	d.set("Tags", tags)
	return d

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var catalog: Resource = RESOURCE_CATALOG.new()
	var iron := make_definition("iron", ["ore", "metal"])
	var wood := make_definition("wood", ["organic"])
	catalog.set("Resources", [iron, wood])

	# --- storage: default (no filters) still accepts anything, unchanged ---
	var open_storage: Node = STORAGE.new()
	open_storage.name = "OpenStorage"
	root.add_child(open_storage)
	await process_frame
	check(bool(open_storage.call("CanAccept", "anything_at_all")),
		"a storage with no AllowedResourceIds/Tags still accepts anything, unchanged")

	# --- storage: AllowedResourceIds alone still works exactly as before ---
	var id_only_storage: Node = STORAGE.new()
	id_only_storage.name = "IdOnlyStorage"
	id_only_storage.set("AllowedResourceIds", ["wood"])
	root.add_child(id_only_storage)
	await process_frame
	check(bool(id_only_storage.call("CanAccept", "wood")) and not bool(id_only_storage.call("CanAccept", "iron")),
		"AllowedResourceIds alone, with no Catalog wired, behaves exactly as before")

	# --- storage: AllowedResourceTags with Catalog wired accepts by type ---
	var ore_tank: Node = STORAGE.new()
	ore_tank.name = "OreTank"
	ore_tank.set("Catalog", catalog)
	ore_tank.set("AllowedResourceTags", ["ore"])
	root.add_child(ore_tank)
	await process_frame

	check(bool(ore_tank.call("CanAccept", "iron")),
		"a tank tagged 'ore' accepts iron via the catalog, with iron never listed by id")
	check(not bool(ore_tank.call("CanAccept", "wood")),
		"the same tank rejects wood - wood is catalog-known but not tagged 'ore'")
	check(not bool(ore_tank.call("CanAccept", "unobtainium")),
		"an id the catalog does not recognize at all is rejected - this is what closes a typo'd resource id out")

	# --- falsify: clearing the tag on the catalog definition must make the
	# guard actually FAIL, proving it can fail before trusting it passes ---
	var accepted_before: bool = bool(ore_tank.call("CanAccept", "iron"))
	iron.set("Tags", [])
	var accepted_after_clearing_tag: bool = bool(ore_tank.call("CanAccept", "iron"))
	check(accepted_before and not accepted_after_clearing_tag,
		"falsified: removing iron's 'ore' tag from the catalog makes the same tank reject it")
	iron.set("Tags", ["ore", "metal"])
	check(bool(ore_tank.call("CanAccept", "iron")), "restoring the tag makes the tank accept iron again")

	# --- storage: AllowedResourceTags with NO catalog wired rejects
	# everything not id-listed, rather than silently ignoring the filter ---
	var unwired_tank: Node = STORAGE.new()
	unwired_tank.name = "UnwiredTagTank"
	unwired_tank.set("AllowedResourceTags", ["ore"])
	root.add_child(unwired_tank)
	await process_frame
	check(not bool(unwired_tank.call("CanAccept", "iron")),
		"AllowedResourceTags with no Catalog wired rejects everything not also id-listed")

	# --- Load actually respects the new acceptance rule end to end, not just CanAccept ---
	check(int(ore_tank.call("Load", "iron", 5)) == 5, "Load actually delivers iron into the ore tank")
	check(int(ore_tank.call("Load", "wood", 5)) == 0, "Load refuses wood into the ore tank - CanAccept is honored by Load, not just queried")

	# --- hauler: identical shape, proven independently on the transport side ---
	var truck_body := Node2D.new()
	truck_body.name = "OreTruck"
	root.add_child(truck_body)
	var truck: Node = HAULER.new()
	truck.set("RegisterOnReady", false)
	truck.set("Catalog", catalog)
	truck.set("AllowedResourceTags", ["ore"])
	truck_body.add_child(truck)
	await process_frame

	check(bool(truck.call("CanAccept", "iron")), "a hauler tagged 'ore' accepts iron via the catalog")
	check(not bool(truck.call("CanAccept", "wood")), "the same hauler rejects wood")
	check(int(truck.call("Load", "iron", 10)) == 10, "Load actually delivers iron into the ore truck's hold")

	print("RESULT: ", "all checks passed" if failures.is_empty() else "%d FAILED" % failures.size())
	quit(1 if failures.size() > 0 else 0)
