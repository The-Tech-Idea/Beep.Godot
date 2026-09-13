extends SceneTree

# FIX-04: a generated mountain-prefab art sprite must be OWNED, so a plain editor save keeps it.
# The owner has to be stamped AFTER the sprite is parented (TerrainAuthoring.Adopt); Godot rejects
# an owner that is not an ancestor, which is what made GenerateInEditor + Ctrl+S drop all the art.

const GENERATOR := "res://addons/beep_game_builder_cs/ecs/terrain/MountainPrefabGeneratorComponent.cs"
const MANIFEST := "res://tests/fixtures/mountain_prefab/prefab_manifest.json"
const GROUP := "generated_mountain_prefab_part"

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void: run.call_deferred()

func find_in_group(node: Node, group: String) -> Node:
	if node.is_in_group(group):
		return node
	for child in node.get_children():
		var found := find_in_group(child, group)
		if found != null:
			return found
	return null

func run() -> void:
	var host := Node2D.new()
	host.name = "MountainPrefabHost"
	root.add_child(host)

	var generator: Node = load(GENERATOR).new()
	generator.name = "Generator"
	generator.PrefabManifestPath = MANIFEST
	generator.UseSingleBakedPrefabImage = true
	host.add_child(generator)
	generator.owner = host

	var count: int = generator.GeneratePrefab()
	check(count > 0, "GeneratePrefab produced no art sprite")

	var sprite: Node = find_in_group(generator, GROUP)
	check(sprite != null, "No generated part sprite in group " + GROUP)
	if sprite != null:
		check(sprite.owner == host,
			"The generated art sprite was not adopted by the packed scene root (owner=%s)" % [sprite.owner])

	# Pack the host the way a plain editor save would, then read it back: an ownerless sprite is
	# not written to the scene file at all, so that is where "the art vanished on reload" shows.
	var packed := PackedScene.new()
	check(packed.pack(host) == OK, "Could not pack the generated host")
	host.free()

	var reloaded: Node = packed.instantiate()
	root.add_child(reloaded)
	# The group identifies the generated part before the save; PackedScene does not persist group
	# membership here, so what is asserted after the reload is that the sprite was written at all
	# and that it belongs to the packed scene root.
	var reloaded_sprite: Node = reloaded.find_child("baked_prefab", true, false)
	check(reloaded_sprite != null, "The packed scene dropped the generated art sprite; it had no owner")
	if reloaded_sprite != null:
		check(reloaded_sprite.owner == reloaded, "The reloaded art sprite's owner is not the packed scene root")
	reloaded.free()

	print("[mountain-prefab-owner] OK" if failures.is_empty() else "[mountain-prefab-owner] FAILED")
	quit(0 if failures.is_empty() else 1)
