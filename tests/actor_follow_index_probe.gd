extends SceneTree
const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
var host: Node2D
var registry: Node
var followers: Array[Node] = []
var definition: Resource
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(path: String, parent: Node, name_: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = name_
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	node.set_physics_process(false)
	return node
func member(id: String, position_: Vector2, target: String = "", shape: Resource = null, distance: float = 64) -> Node:
	var body := Node2D.new()
	host.add_child(body)
	body.position = position_
	if not target.is_empty():
		make("grid/GridPathFollowerComponent", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false})
	var actor := make("actors/ActorComponent", body, "Actor", {"ActorId": id, "OwnerId": "player_1", "RegistryPath": NodePath("../../Registry"), "Definition": definition if shape == null else shape})
	if not target.is_empty():
		var command: Resource = load(ECS + "actors/ActorCommand.cs").new()
		command.IssuerId = "player_1"
		command.Action = 5
		command.TargetActorId = target
		command.FollowDistance = distance
		command.Recipients = [id]
		check(registry.Submit(command) == 1, "Follow index fixture rejected command")
		actor._PhysicsProcess(0.1)
		followers.append(actor)
	return actor
func _initialize() -> void: run.call_deferred()
func run() -> void:
	host = Node2D.new()
	root.add_child(host)
	registry = make("actors/ActorRegistryComponent", host, "Registry")
	make("actors/PlayerContextComponent", host, "Player", {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var grid := make("grid/GridProjectionComponent", host, "Grid", {"TileSize": Vector2(32, 32), "DrawGrid": false, "TrackMouseCell": false})
	make("grid/GridNavigationComponent", host, "Navigation", {"GridPath": NodePath("../Grid"), "BoundsOrigin": Vector2i(-100000, -100000), "BoundsSize": Vector2i(200000, 200000)})
	definition = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Capabilities = 1
	definition.Footprint = Vector2(20, 20)
	var candidates := 0
	var started := Time.get_ticks_usec()
	for i in 500:
		var origin := Vector2((i - 250) * 1024, (i % 4) * 512)
		member("leader_%d" % i, origin)
		member("follower_%d" % i, origin + Vector2(48, 0), "leader_%d" % i)
		candidates += registry.LastFollowRestCandidateCount
	check(registry.ActorCount == 1000 and registry.FollowRestReservationCount == 500, "Distributed groups lost identities or reservations")
	check(candidates <= 1000, "Distributed follow reservations still perform registry-wide scans")
	check(registry.FollowRestBucketCount <= 2000, "Small footprints use excessive index buckets")
	print("[follow index] 1000 actors / 500 groups: %d candidates, setup=%d ms" % [candidates, (Time.get_ticks_usec() - started) / 1000])
	var first: Node = followers[0]
	first.IsActive = false
	var replacement := member("replacement", first.get_parent().position, "leader_0")
	check(registry.FollowRestReservationCount == 500, "Local stale reservation was not retired before replacement")
	replacement.CancelOrders()
	first.IsActive = true
	first._PhysicsProcess(1.0)
	check(registry.FollowRestReservationCount == 500, "Reactivated follower could not reacquire its destination")
	var old_position: Vector2 = first.get_parent().position
	registry.FindActor("leader_0").get_parent().position += Vector2(512, 2048)
	first.get_parent().position += Vector2(512, 2048)
	first._PhysicsProcess(1.0)
	member("old_site_leader", old_position - Vector2(48, 0))
	member("old_site_follower", old_position, "old_site_leader")
	check(registry.FollowRestReservationCount == 501, "Relocation did not free the old destination")
	var big: Resource = definition.duplicate()
	big.Footprint = Vector2(4096, 4096)
	member("large_leader", Vector2(500000, 0))
	var before: int = registry.FollowRestBucketCount
	var oversized := member("oversized", Vector2(504000, 0), "large_leader", big, 6000)
	check(registry.FollowRestBucketCount == before, "Oversized footprint allocated an unbounded bucket grid")
	var nearby := member("nearby", oversized.get_parent().position, "large_leader", null, 6000)
	check(registry.LastFollowRestCandidateCount >= 1, "Normal query failed to include oversized reservations")
	check(nearby.get_parent().get_node("Follower").DestinationCell != grid.WorldToCell(nearby.get_parent().position), "Oversized reservation failed to reject an overlapping destination")
	for actor in followers: actor.CancelOrders()
	check(registry.FollowRestReservationCount == 0 and registry.FollowRestBucketCount == 0, "Cancellation leaked follow index entries")
	host.free()
	await process_frame
	print("[actor-follow-index] OK" if failures.is_empty() else "[actor-follow-index] FAILED")
	quit(0 if failures.is_empty() else 1)
