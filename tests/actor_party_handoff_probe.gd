extends SceneTree
const ECS := "res://addons/beep_game_builder_cs/ecs/"
var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)
func make(path: String, parent: Node, label: String, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + path + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	node.set_physics_process(false)
	return node
func _initialize() -> void: run.call_deferred()
func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", host, "Registry")
	var player := make("actors/PlayerContextComponent", host, "Player", {"RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	make("actors/PlayerContextComponent", host, "Other", {"RegistryPath": NodePath("../Registry"), "PlayerId": "other", "ReadLocalInput": false})
	make("grid/GridProjectionComponent", host, "Grid", {"DrawGrid": false, "TrackMouseCell": false})
	make("grid/GridNavigationComponent", host, "Navigation", {"GridPath": NodePath("../Grid"), "BoundsSize": Vector2i(32, 32)})
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Capabilities = 1
	var members: Array[Node] = []
	for id in ["a", "b", "c", "foreign"]:
		var body := Node2D.new()
		body.name = id
		host.add_child(body)
		make("grid/GridPathFollowerComponent", body, "Follower", {"GridPath": NodePath("../../Grid"), "NavigationPath": NodePath("../../Navigation"), "DriveCharacterBody": false})
		members.append(make("actors/ActorComponent", body, "Actor", {"ActorId": id, "OwnerId": "other" if id == "foreign" else "player_1", "Definition": definition, "RegistryPath": NodePath("../../Registry")}))
	var a := members[0]
	var b := members[1]
	var c := members[2]
	player.FollowPartyOnPossession = true
	check(not player.Possess("a") and not a.HasOrders and not b.HasOrders and player.RegroupParty() == 0, "Orders mode acquired automatic party control")
	player.ControlMode = 0
	player.SelectActor("a", false)
	player.SelectActor("b", true)
	player.StoreGroup(2)
	player.PossessionGroup = 2
	check(player.Possess("a") and b.FollowTargetActorId == "a" and not c.HasOrders, "Initial party following ignored group membership")
	player.SelectActor("c", true)
	player.StoreGroup(2)
	player.SelectActor("c", false)
	check(player.Possess("b") and a.FollowTargetActorId == "b" and c.FollowTargetActorId == "b" and not b.HasOrders, "Handoff did not redirect companions and release the new leader")
	check(player.GetSelectedActors() == ["c"], "Party handoff changed selection")
	player.SelectActor("a", false)
	player.IssueOrder(2, Vector2i.ZERO, "", false, "")
	check(player.Possess("c") and b.FollowTargetActorId == "c" and not a.HasOrders, "Handoff overrode a manual Stop")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	player.FollowPartyOnPossession = false
	check(not b.HasOrders, "Disabling automatic follow left owned policy orders active")
	registry.RestoreState(saved)
	check(player.FollowPartyOnPossession and player.PossessedActorId == "c" and b.FollowTargetActorId == "c", "Snapshot lost party policy state")
	check(player.Possess("b") and c.FollowTargetActorId == "b" and not a.HasOrders, "Restored manual hold was lost at handoff")
	check(player.RegroupParty() == 2 and a.FollowTargetActorId == "b" and c.FollowTargetActorId == "b", "Explicit regroup failed to release manual holds")
	var replaced := [false]
	registry.CommandAccepted.connect(func(id, action):
		if id == "c" and action == 5 and not replaced[0]:
			replaced[0] = true
			c.CancelOrders())
	player.RegroupParty()
	check(replaced[0] and not c.HasOrders, "Reentrant replacement fixture failed")
	player.Possess("a")
	check(b.FollowTargetActorId == "a" and not c.HasOrders, "Policy claimed an order replaced inside CommandAccepted")
	player.ControlMode = 1
	player._PhysicsProcess(0.0)
	check(not b.HasOrders and not c.HasOrders, "Leaving direct control retained party movement")
	player.ControlMode = 0
	player.Possess("b")
	check(a.FollowTargetActorId == "b" and not c.HasOrders, "Manual hold changed after control mode switch")
	host.remove_child(player)
	check(not a.HasOrders, "Detached player left automatic follower orders")
	host.add_child(player)
	player.set_physics_process(false)
	await process_frame
	await process_frame
	check(a.FollowTargetActorId == "b" and not c.HasOrders, "Reattachment did not restore policy ownership and manual holds")
	registry.TransferOwnership("a", "other")
	player.RegroupParty()
	check(not a.HasOrders and not members[3].HasOrders and c.FollowTargetActorId == "b", "Party policy controlled a foreign actor")
	b.get_parent().global_position = Vector2(512, 256)
	c.get_parent().global_position = Vector2(64, 256)
	b.SynchronizePosition()
	for i in 70:
		await physics_frame
		c._PhysicsProcess(0.1)
		c.get_parent().get_node("Follower").AdvancePath(0.1)
	check(c.FollowTargetActorId == "b" and c.get_parent().global_position.distance_to(b.get_parent().global_position) <= player.PartyFollowDistance, "Regrouped companion did not physically follow its new leader")
	var ambient := make("AnimalBehaviorComponent", c.get_parent(), "Ambient", {})
	check(not c.CanDrive(ambient) and c.CanDrive(c.get_parent().get_node("Follower")), "Settled Follow surrendered movement to ambient behavior")
	b.get_parent().free()
	check(player.PossessedActorId.is_empty() and not c.HasOrders, "Removed leader retained automatic party movement")
	host.free()
	print("[actor-party-handoff] OK" if failures.is_empty() else "[actor-party-handoff] FAILED")
	quit(0 if failures.is_empty() else 1)
