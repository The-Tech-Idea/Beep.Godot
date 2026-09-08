extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []

func check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func make(path: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void:
	create_timer(30.0).timeout.connect(func(): push_error("Actor residency timed out"); quit(1))
	call_deferred("run")

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var actors := Node2D.new()
	actors.name = "Actors"
	host.add_child(actors)
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Id = "ambient"
	definition.Scene = load("res://tests/actor_residency_unit.tscn")
	definition.SimulationPolicy = 1
	var continuous: Resource = definition.duplicate()
	continuous.Id = "continuous"
	continuous.SimulationPolicy = 0
	var registry := make("actors/ActorRegistryComponent", "Registry", host, {"ActorsRootPath": NodePath("../Actors"), "Definitions": [definition, continuous]})
	var player := make("actors/PlayerContextComponent", "Player", host, {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	make("grid/GridProjectionComponent", "Grid", host, {"DrawGrid": false, "TrackMouseCell": false})
	make("grid/GridNavigationComponent", "Navigation", host, {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(1024, 1024)})
	var ambient: Node = registry.SpawnActor("ambient", "player_1", Vector2(5000, 5000), "npc")
	var former_instance := ambient.get_instance_id()
	registry.SpawnActor("continuous", "player_1", Vector2(6000, 5000), "worker")
	check(not registry.TrySleepActor("worker"), "Continuous RTS actor was suspended")
	player.SelectActor("npc", false)
	check(not registry.TrySleepActor("npc"), "Selected actor was suspended")
	var orders := make("actors/ActorOrdersControllerComponent", "Orders", host, {"PlayerPath": NodePath("../Player"), "GridPath": NodePath("../Grid"), "NavigationPath": NodePath("../Navigation")})
	check(orders.MoveSelection(Vector2i(170, 170), false) == 1, "Ambient preflight not accepted")
	player.ClearSelection()
	check(not registry.TrySleepActor("npc"), "Pending preflight actor was suspended after deselection")
	orders.CancelFormation()
	player.SelectActor("worker", false)
	orders.MoveSelection(Vector2i(170, 170), false)
	player.SelectActor("npc", false)
	check(orders.MoveSelection(Vector2i(180, 180), true) == 1, "Ambient queued plan not accepted")
	player.ClearSelection()
	check(not registry.TrySleepActor("npc"), "Queued-plan actor was suspended")
	orders.CancelFormation()
	ambient.get_parent().get_node("Health").CurrentHealth = 50.0
	check(not registry.TrySleepActor("npc"), "Injured actor was suspended")
	ambient.get_parent().get_node("Health").CurrentHealth = 73.0
	check(registry.TrySleepActor("npc"), "Idle ambient actor could not sleep")
	await process_frame
	check(not is_instance_valid(ambient), "Sleeping scene instance was retained")
	check(registry.ActorCount == 2 and registry.ResidentActorCount == 1, "Sleeping actor lost its identity")
	check(player.GetOwnedActors().size() == 2, "Dormant actor disappeared from ownership")
	check(registry.QueryActors(Rect2(4900, 4900, 200, 200), "player_1", true).size() == 1, "Dormant spatial query lost the actor")
	registry.SpatialCellSize = 32.0
	check(registry.QueryActors(Rect2(4900, 4900, 200, 200), "", true).size() == 1, "Spatial reindex lost dormant records")
	var record: Dictionary = registry.GetActorRecord("npc")
	record.owner = "tampered"
	check(registry.GetActorOwner("npc") == "player_1", "Record inspection leaked mutable authoritative state")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	ambient = registry.WakeActor("npc")
	check(ambient != null and ambient.get_instance_id() != former_instance, "Wake reused a freed scene instance")
	check(ambient.get_parent().get_node("Health").CurrentHealth == 73.0, "Wake lost per-actor state")
	registry.RestoreState(saved)
	await process_frame
	check(registry.IsDormant("npc") and registry.ResidentActorCount == 1, "JSON restore eagerly instantiated dormant actors")
	check(player.SelectActor("npc", false), "Selection did not wake owned actor")
	check(not registry.IsDormant("npc"), "Selected actor remained dormant")
	player.ClearSelection()
	check(registry.TrySleepActor("npc"), "Actor could not sleep again")
	var camera := Camera2D.new()
	camera.name = "Camera"
	host.add_child(camera)
	var residency := make("actors/ActorResidencyComponent", "Residency", host, {"RegistryPath": NodePath("../Registry"), "CameraPath": NodePath("../Camera"), "TransitionsPerPoll": 1})
	residency.set_process(false)
	camera.position = Vector2(5000, 5000)
	camera.force_update_scroll()
	residency.RefreshResidency()
	check(not registry.IsDormant("npc"), "Camera did not wake visible actor")
	camera.position = Vector2.ZERO
	camera.force_update_scroll()
	residency.RefreshResidency()
	check(registry.IsDormant("npc") and not registry.IsDormant("worker"), "Camera residency violated simulation policy")
	check(registry.RemoveActor("npc"), "Dormant actor could not be destroyed")
	check(registry.ActorCount == 1 and registry.QueryActors(Rect2(4900, 4900, 200, 200), "", true).is_empty(), "Destroyed dormant actor left a stale record")
	host.free()
	print("[actor residency] OK" if failures.is_empty() else "[actor residency] FAILED")
	quit(0 if failures.is_empty() else 1)
