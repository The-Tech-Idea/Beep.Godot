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
	node.set_process(false)
	node.set_physics_process(false)
	return node

func member(host: Node, id: String, owner_id: String, max_health: int) -> Node:
	var body := CharacterBody2D.new()
	body.name = id
	host.add_child(body)
	make("HealthComponent", body, "Health")
	make("LevelingComponent", body, "Leveling")
	make("RpgPartyComponent", body, "Stats", {"BaseMaxHealth": max_health})
	return make("actors/ActorComponent", body, "Actor", {"ActorId": id, "OwnerId": owner_id, "RegistryPath": NodePath("../../Registry")})

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("actors/ActorRegistryComponent", host, "Registry")
	var player := make("actors/PlayerContextComponent", host, "Player", {"RegistryPath": NodePath("../Registry"), "ControlMode": 0})
	make("actors/PlayerContextComponent", host, "Other", {"RegistryPath": NodePath("../Registry"), "PlayerId": "other"})
	var a := member(host, "a", "player_1", 80)
	var b := member(host, "b", "player_1", 90)
	var c := member(host, "c", "player_1", 100)
	member(host, "d", "other", 110)
	var e := member(host, "e", "player_1", 120)
	b.get_parent().get_node("Health").CurrentHealth = 0
	c.IsActive = false
	check(player.CyclePossession(1, -1) and player.PossessedActorId == "a", "Cycle without avatar did not choose first eligible owned actor")
	a.SetIntent(Vector2.RIGHT, Vector2.RIGHT, false, false)
	check(player.CyclePossession(1, -1) and player.PossessedActorId == "e" and a.MoveIntent == Vector2.ZERO, "Cycle failed to skip dead/inactive/foreign actors or clear previous intent")
	check(player.CyclePossession(1, -1) and player.PossessedActorId == "a", "Forward cycle did not wrap")
	check(player.CyclePossession(-2147483648, -1) and player.PossessedActorId == "e", "Reverse cycle failed with minimum integer direction")
	player.SelectActor("e", false)
	player.SelectActor("a", true)
	player.StoreGroup(2)
	player.ClearSelection()
	check(player.GetGroupActors(2) == ["a", "e"], "Party group ordering is not stable")
	var copy: Array = player.GetGroupActors(2)
	copy.clear()
	check(player.GetGroupActors(2).size() == 2, "Group API exposed mutable internal membership")
	check(player.CyclePossession(1, 2) and player.PossessedActorId == "a" and player.GetSelectedActors().is_empty(), "Group cycling mutated selection")
	check(not player.CyclePossession(0, 2) and not player.CyclePossession(1, 10) and not player.CyclePossession(1, 3), "Invalid/empty party cycling accepted")

	var hud := Control.new()
	hud.name = "Hud"
	host.add_child(hud)
	for key in ["Level", "Health", "Mana", "Quest"]:
		var label := Label.new()
		label.name = key
		hud.add_child(label)
	var binding := make("ui/hud/RpgHudComponent", hud, "Binding", {"PlayerPath": NodePath("../Player"), "LevelPath": NodePath("Level"), "HealthPath": NodePath("Health"), "ManaPath": NodePath("Mana"), "QuestPath": NodePath("Quest")})
	await process_frame
	await process_frame
	check(hud.get_node("Health").tooltip_text == "HP 80 / 80", "HUD did not bind possessed actor")
	a.get_parent().get_node("Stats").StartQuest("quest_a", "A quest", 1)
	player.CyclePossession(1, 2)
	check(hud.get_node("Health").tooltip_text == "HP 120 / 120" and hud.get_node("Quest").text == "No active quest", "HUD retained previous actor stats/quest")
	a.get_parent().get_node("Health").CurrentHealth = 70
	a.get_parent().get_node("Health").emit_signal("HealthChanged", 70.0, 80.0)
	check(hud.get_node("Health").tooltip_text == "HP 120 / 120", "Old actor still drives HUD after switch")
	e.get_parent().get_node("Stats").SpendMana(5)
	check(hud.get_node("Mana").tooltip_text == "MP 35 / 40", "New actor stats signals were not connected")
	var saved: Dictionary = JSON.parse_string(JSON.stringify(registry.CaptureState()))
	player.SelectActor("a", false)
	player.StoreGroup(2)
	player.Possess("a")
	registry.RestoreState(saved)
	check(player.PossessedActorId == "e" and player.GetGroupActors(2) == ["a", "e"] and hud.get_node("Health").tooltip_text == "HP 120 / 120", "Save restore lost party/possession/HUD binding")
	registry.TransferOwnership("e", "other")
	check(player.PossessedActorId == "" and hud.get_node("Health").text == "" and hud.get_node("Health").tooltip_text == "", "Revoked possession left stale HUD")
	check(player.CyclePossession(1, 2) and player.PossessedActorId == "a", "Party did not reject transferred member")
	check(not player.CyclePossession(1, 2), "One-member party unnecessarily repossessed/canceled orders")
	player.ControlMode = 1
	check(not player.CyclePossession(1, -1), "RTS orders mode acquired avatar through party cycle")
	player.ControlMode = 0
	InputMap.add_action("test_party_next")
	player.NextActorAction = "test_party_next"
	player.PossessionGroup = -1
	b.get_parent().get_node("Health").CurrentHealth = 100
	var event := InputEventAction.new()
	event.action = "test_party_next"
	event.pressed = true
	player._UnhandledInput(event)
	check(player.PossessedActorId == "b", "Configured party input did not cycle")
	hud.remove_child(binding)
	player.Possess("a")
	hud.add_child(binding)
	await process_frame
	await process_frame
	check(hud.get_node("Health").tooltip_text == "HP 70 / 80", "Reattached HUD did not bind current possession")
	var definition: Resource = load(ECS + "actors/ActorDefinition.cs").new()
	definition.Id = "ambient"
	definition.Scene = load("res://tests/actor_residency_unit.tscn")
	definition.SimulationPolicy = 1
	registry.Definitions.append(definition)
	registry.ActorsRootPath = NodePath("..")
	registry.SpawnActor("ambient", "player_1", Vector2(300, 0), "sleep_a")
	registry.SpawnActor("ambient", "player_1", Vector2(400, 0), "sleep_b")
	player.SelectActor("sleep_a", false)
	player.SelectActor("sleep_b", true)
	player.StoreGroup(4)
	player.ClearSelection()
	check(registry.TrySleepActor("sleep_a") and registry.TrySleepActor("sleep_b"), "Dormant party fixture could not sleep")
	check(player.GetGroupActors(4) == ["sleep_a", "sleep_b"] and registry.DormantActorCount == 2, "Inspecting party woke its members")
	check(player.CyclePossession(1, 4) and player.PossessedActorId == "sleep_a" and registry.DormantActorCount == 1 and registry.IsDormant("sleep_b"), "Party cycle woke more than its chosen actor")
	check(hud.get_node("Health").text == "" and hud.get_node("Quest").text == "", "Actor without RPG stats inherited another actor's HUD")
	InputMap.erase_action("test_party_next")
	host.free()
	await process_frame
	print("[actor-party] OK" if failures.is_empty() else "[actor-party] FAILED")
	quit(0 if failures.is_empty() else 1)
