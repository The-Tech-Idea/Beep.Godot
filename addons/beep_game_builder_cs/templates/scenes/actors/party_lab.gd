extends "res://addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.gd"

var added_actions: Array[String] = []

func _enter_tree() -> void:
	var bindings := {"move_left": [KEY_A, KEY_LEFT], "move_right": [KEY_D, KEY_RIGHT],
		"move_up": [KEY_W, KEY_UP], "move_down": [KEY_S, KEY_DOWN],
		"party_lab_previous": [KEY_Q], "party_lab_next": [KEY_E]}
	for action in bindings:
		if InputMap.has_action(action): continue
		InputMap.add_action(action)
		added_actions.append(action)
		for key in bindings[action]:
			var event := InputEventKey.new()
			event.physical_keycode = key
			InputMap.action_add_event(action, event)

func _exit_tree() -> void:
	for action in added_actions: InputMap.erase_action(action)
	added_actions.clear()

func _ready() -> void:
	$HUD/Toolbar/Row/Previous.pressed.connect(func(): $Player.CyclePossession(-1, 0))
	$HUD/Toolbar/Row/Next.pressed.connect(func(): $Player.CyclePossession(1, 0))
	$HUD/Toolbar/Row/Hold.pressed.connect(hold_companions)
	$HUD/Toolbar/Row/Regroup.pressed.connect(func(): $Player.RegroupParty(); update_status())
	$Player.PossessionChanged.connect(possession_changed)
	super._ready()

func _process(_delta: float) -> void:
	if not ready_to_play: return
	var actor: Node = $Registry.FindActor($Player.PossessedActorId)
	if actor != null and actor.MoveIntent != Vector2.ZERO:
		$Camera2D/Controller.FocusWorld(actor.get_parent().global_position, false)

func world_built(size: Vector2i) -> void:
	super.world_built(size)
	if not ready_to_play: return
	$Player.StoreGroup(0)
	$Player.CyclePossession(1, 0)

func possession_changed(id: String) -> void:
	if id.is_empty(): return
	$Player.SelectActor(id, false)
	var actor: Node = $Registry.FindActor(id)
	if actor != null: $Camera2D/Controller.FocusWorld(actor.get_parent().global_position, true)
	update_status()

func hold_companions() -> void:
	var members: Array = $Player.GetGroupActors(0)
	members.erase($Player.PossessedActorId)
	var command: Resource = COMMAND.new()
	command.IssuerId = $Player.PlayerId
	command.Recipients = members
	command.Action = 2
	$Registry.Submit(command)
	update_status()

func update_status() -> void:
	if not ready_to_play: return
	var following := 0
	for id in $Player.GetOwnedActors():
		var actor: Node = $Registry.FindActor(id)
		if actor != null and not actor.FollowTargetActorId.is_empty(): following += 1
	$HUD/Status.text = "%d party members | %s | %d following" % [$Registry.ActorCount, $Player.PossessedActorId, following]
