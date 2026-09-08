extends SceneTree

func _initialize() -> void: run.call_deferred()

func run() -> void:
	var smoke: Node = load("res://tests/CoreGameplaySmoke.cs").new()
	root.add_child(smoke)
	var passed: bool = smoke.RunRpgHealthIntegrationCheck()
	if not passed: push_error(smoke.Failure)
	smoke.free()
	await process_frame
	print("[actor-rpg-health] OK" if passed else "[actor-rpg-health] FAILED")
	quit(0 if passed else 1)
