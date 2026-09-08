extends SceneTree

const BASE := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func make(path: String, parent: Node, node_name: String, values: Dictionary) -> Node:
	var node: Node = load(BASE + path + ".cs").new()
	node.name = node_name
	for key in values:
		node.set(key, values[key])
	parent.add_child(node)
	return node

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func settle() -> void:
	await process_frame
	await process_frame

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	make("terrain/TerrainGeneratorComponent", host, "Generator", {"GenerateOnReady": false})
	var layers := make("terrain/TerrainDataLayersComponent", host, "Layers", {
		"TerrainGeneratorPath": NodePath("../Generator"), "RefreshOnReady": false})
	var world := make("terrain/TerrainWorldComponent", host, "World", {
		"GeneratorPath": NodePath("../Generator"), "DataLayersPath": NodePath("../Layers"),
		"BuildOnReady": false, "ParticipatesInSave": false, "MapSize": 0,
		"Resources": 1, "ResourceLevel": 2, "Seed": 424242})
	world.call("NewWorld")
	var deposit := Vector2i(-1, -1)
	for y in range(32):
		for x in range(32):
			if layers.call("UndergroundResourceAt", Vector2i(x, y)) != "":
				deposit = Vector2i(x, y)
	check(deposit.x >= 0, "Fixture contains no underground deposit")
	var survey := make("grid/GridProspectingComponent", host, "Survey", {
		"DataLayersPath": NodePath("../Layers"), "ParticipatesInSave": false,
		"AutoConnect": false, "RevealAll": false, "SurveyRadius": 0})
	var store := make("grid/GridSubsurfaceStoreComponent", host, "Store", {
		"DataLayersPath": NodePath("../Layers"), "ParticipatesInSave": false})
	var overlay := make("terrain/TerrainMapOverlayComponent", host, "Overlay", {
		"TerrainGeneratorPath": NodePath("../Generator"), "ProspectingPath": NodePath("../Survey"),
		"SubsurfaceStorePath": NodePath("../Store"), "BoundsSize": Vector2i(32, 32),
		"RefreshOnReady": false, "ShowResources": false, "ShowStartPositions": false})
	overlay.call("Rebuild")
	check(overlay.get("UndergroundPatchCount") == 0, "Unsurveyed map leaked deposits")
	survey.call("Survey", deposit)
	await settle()
	check(overlay.get("UndergroundPatchCount") == 1, "Survey did not refresh overlay")
	store.call("Draw", deposit, 999999)
	await settle()
	check(overlay.get("UndergroundPatchCount") == 0, "Depleted deposit remained visible")
	store.call("RestoreState", {"cells": []})
	await settle()
	check(overlay.get("UndergroundPatchCount") == 1, "Restored deposit did not refresh overlay")
	survey.call("RestoreState", {"reveal_all": false, "cells": []})
	await settle()
	check(overlay.get("UndergroundPatchCount") == 0, "Restored discovery state leaked old patches")
	survey.set("RevealAll", true)
	await settle()
	check(overlay.get("UndergroundPatchCount") > 0, "RevealAll toggle did not refresh overlay")
	var native := TileMapLayer.new()
	native.tile_set = TileSet.new()
	native.tile_set.tile_size = Vector2i(96, 48)
	native.position = Vector2(92, -41)
	native.rotation = 0.25
	native.scale = Vector2(1.3, 0.8)
	host.add_child(native)
	var grid := make("grid/GridProjectionComponent", host, "Grid", {
		"TileMapLayerPath": native.get_path(), "DrawGrid": false, "TrackMouseCell": false})
	overlay.position = Vector2(-60, 70)
	overlay.rotation = -0.2
	overlay.set("GridPath", grid.get_path())
	overlay.set("BoundsOrigin", Vector2i(-7, 9))
	overlay.set("ProspectingPath", NodePath(""))
	overlay.set("SubsurfaceStorePath", NodePath(""))
	for shape in [TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_SHAPE_ISOMETRIC]:
		native.tile_set.tile_shape = shape
		overlay.call("Rebuild")
		check(overlay.get("UndergroundPatchCount") > 0, "Native grid dropped underground patches")
		var at := deposit + Vector2i(-7, 9)
		var expected := native.to_global(native.map_to_local(at))
		check(overlay.to_global(overlay.call("CellPosition", at)).distance_to(expected) < 0.001,
			"Overlay marker does not match native gameplay centre")
		var actual = overlay.call("CellOutline", at)
		var corners = grid.call("CellCorners", at)
		check(actual.size() == corners.size(), "Overlay patch corner count differs from grid")
		for i in range(actual.size()):
			check(overlay.to_global(actual[i]).distance_to(grid.to_global(corners[i])) < 0.001,
				"Overlay patch outline does not match transformed native cell")
	overlay.set("GridPath", NodePath("../MissingGrid"))
	overlay.call("Rebuild")
	check(overlay.get("UndergroundPatchCount") == 0, "Missing grid retained stale patch geometry")
	overlay.set("GridPath", grid.get_path())
	overlay.set("ProspectingPath", NodePath("../Missing"))
	overlay.call("Rebuild")
	check(overlay.get("UndergroundPatchCount") == 0, "Missing configured prospecting source revealed everything")
	overlay.set("ProspectingPath", NodePath(""))
	overlay.set("SubsurfaceStorePath", NodePath("../Missing"))
	overlay.call("Rebuild")
	check(overlay.get("UndergroundPatchCount") == 0, "Missing configured store showed deposits")
	host.free()
	print("[terrain-survey-overlay] OK" if failures.is_empty() else "[terrain-survey-overlay] FAILED")
	quit(0 if failures.is_empty() else 1)
