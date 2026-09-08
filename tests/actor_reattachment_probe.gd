extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(path: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	current_scene = host
	var registry := make("actors/ActorRegistryComponent", host)
	var player := make("actors/PlayerContextComponent", host, {"RegistryPath": registry.get_path(), "PlayerId": "owner", "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", host, {"DrawGrid": false})
	var nav := make("grid/GridNavigationComponent", host, {"GridPath": grid.get_path(), "BoundsSize": Vector2i(30, 30)})
	var destination := Node2D.new()
	host.add_child(destination)
	var body := Node2D.new()
	host.add_child(body)
	var health := make("HealthComponent", body)
	make("grid/GridPathFollowerComponent", body, {"GridPath": grid.get_path(), "NavigationPath": nav.get_path(), "Speed": 500.0, "SetZIndexFromY": false})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Capabilities = 1
	var actor := make("actors/ActorComponent", body, {"RegistryPath": registry.get_path(), "ActorId": "unit", "OwnerId": "owner", "Definition": definition})
	make("actors/ActorPresentationComponent", body, {"PlayerPath": player.get_path()})
	var selection_connections: int = player.get_signal_connection_list("SelectionChanged").size()
	check(selection_connections > 0, "Selection presentation did not subscribe")
	var events: Array = []
	actor.CommandFinished.connect(func(action, success, reason): events.append([action, success, reason]))
	check(player.SelectActor("unit", false), "Initial selection failed")
	check(player.IssueOrder(0, Vector2i(4, 0), "", false, "") == 1, "Move rejected")
	for i in 4:
		body.reparent(destination if body.get_parent() == host else host)
		check(registry.FindActor("unit") == actor and registry.ActorCount == 1, "Reparent lost actor registration")
		check(actor.HasOrders and actor.OwnerId == "owner", "Reparent reset orders or ownership")
		check(player.get_signal_connection_list("SelectionChanged").size() == selection_connections, "Reparent lost or duplicated presentation subscription")
	for i in 90: await physics_frame
	check(events.size() == 1 and events[0][1], "Reattached actor did not complete move exactly once")
	check(body.global_position.distance_to(grid.CellToWorld(Vector2i(4, 0))) < 1, "Reattached actor missed destination")
	host.remove_child(player)
	check(registry.FindPlayer("owner") == null, "Detached player retained registry entry")
	host.add_child(player)
	check(registry.FindPlayer("owner") == player, "Reattached player not registered")
	check(player.SelectActor("unit", false), "Reattached player cannot select owned actor")
	check(player.IssueOrder(0, Vector2i(5, 0), "", false, "") == 1, "Reattached player cannot issue orders")
	health.emit_signal("Died")
	check(not actor.HasOrders and player.GetSelectedActors().is_empty(), "Reattached death handler did not cancel orders and selection")
	body.get_parent().remove_child(body)
	check(registry.ActorCount == 0, "Detached actor retained registry entry")
	destination.add_child(body)
	check(registry.ActorCount == 1, "Delayed reattachment lost actor")
	body.free()
	check(registry.ActorCount == 0, "Freed actor retained registry entry")
	host.free()
	print("[actor-reattachment] OK" if failures.is_empty() else "[actor-reattachment] FAILED")
	quit(0 if failures.is_empty() else 1)
