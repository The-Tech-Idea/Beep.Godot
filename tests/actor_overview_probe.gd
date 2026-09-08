extends SceneTree

const ECS := "res://addons/beep_game_builder_cs/ecs/actors/"
var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func make(type: String, label: String, parent: Node, properties: Dictionary = {}) -> Node:
	var node: Node = load(ECS + type + ".cs").new()
	node.name = label
	for key in properties: node.set(key, properties[key])
	parent.add_child(node)
	node.set_process(false)
	node.set_physics_process(false)
	return node

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var host := Node2D.new()
	root.add_child(host)
	var registry := make("ActorRegistryComponent", "Registry", host, {"ActorsRootPath": NodePath("..")})
	var player := make("PlayerContextComponent", "Player", host, {"PlayerId": "local", "RegistryPath": NodePath("../Registry"), "ReadLocalInput": false})
	var bodies: Array[Node2D] = []
	for i in 4:
		var body := Node2D.new()
		body.position = Vector2(i * 200 - 200, 0) if i < 3 else Vector2(100000, 100000)
		host.add_child(body)
		body.set_process(true)
		make("ActorComponent", "Actor", body, {"ActorId": "unit%d" % i, "OwnerId": "enemy" if i == 2 else "local", "RegistryPath": NodePath("../../Registry")})
		bodies.append(body)
	var camera := Camera2D.new()
	camera.zoom = Vector2(0.1, 0.1)
	camera.rotation = 0.4
	host.add_child(camera)
	var overlay := make("ActorOverviewComponent", "Overview", host, {"PlayerPath": NodePath("../Player")})
	overlay.position = Vector2(30, 50)
	overlay.scale = Vector2(2, 0.8)
	await process_frame
	camera.force_update_scroll()
	overlay.RefreshOverview()
	check(overlay.IsOverviewActive and overlay.GetMarkerActors() == ["unit0", "unit1"], "Overview leaked enemies/offscreen units or required an avatar")
	check(player.PossessedActorId == "" and registry.ResidentActorCount == 4, "Overview changed possession/residency")
	check(overlay.PickActor(bodies[0].position + Vector2(20, 0)) == "unit0", "Screen-radius marker picking failed")
	check(overlay.PickActor(Vector2(1000, 1000)) == "", "Empty overview picked an actor")
	check(player.SelectActor("unit0", false), "Fixture selection failed")
	overlay.RefreshOverview()
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		var picture := root.get_texture().get_image()
		var green := 0
		var white := 0
		for y in picture.get_height():
			for x in picture.get_width():
				var color := picture.get_pixel(x, y)
				if color.g > 0.8 and color.r < 0.5: green += 1
				if color.r > 0.9 and color.g > 0.9 and color.b > 0.9: white += 1
		check(green > 15 and green < 100 and white > 25 and white < 150, "Native marker sizes/colors not screen-constant or blank")
		DirAccess.make_dir_recursive_absolute("res://tests/output/actor_overview")
		picture.save_png("res://tests/output/actor_overview/markers.png")
	camera.zoom = Vector2.ONE
	camera.force_update_scroll()
	overlay.RefreshOverview()
	check(not overlay.IsOverviewActive and overlay.MarkerCount == 0, "Overview remained enabled at detail zoom")
	camera.zoom = Vector2(0.1, 0.1)
	camera.force_update_scroll()
	overlay.RefreshOverview()
	check(overlay.MarkerCount == 2, "Overview did not return after zoom out")
	for body in bodies: check(body.is_processing(), "Overview suspended body processing")
	overlay.hide()
	overlay.RefreshOverview()
	check(overlay.MarkerCount == 0 and not overlay.IsOverviewActive, "Hidden overlay retained markers")
	var definition: Resource = load(ECS + "ActorDefinition.cs").new()
	definition.Id = "ambient"
	definition.Scene = load("res://tests/actor_residency_unit.tscn")
	definition.SimulationPolicy = 1
	registry.Definitions = [definition]
	registry.FindActor("unit1").Definition = definition
	check(registry.TrySleepActor("unit1"), "Fixture could not suspend idle unselected actor")
	await process_frame
	overlay.show()
	overlay.RefreshOverview()
	check("unit1" in overlay.GetMarkerActors() and registry.FindActor("unit1") == null and registry.DormantActorCount == 1, "Overview lost or woke dormant owned actor")
	host.free()
	print("[actor-overview] OK" if failures.is_empty() else "[actor-overview] FAILED")
	quit(0 if failures.is_empty() else 1)
