extends SceneTree

# A renderer that cannot draw must SAY SO.
#
# Every renderer finds its generator by NodePath, so one typo leaves it with
# nothing to read. Eight of the nine then returned quietly: the node existed, the
# map was empty, and nothing said why. An empty view and an empty world look
# identical, which is the whole problem - the failure is invisible exactly when
# you need to be told.
#
# This DRIVES each renderer with no generator wired. It cannot judge the result
# itself: Godot routes push_warning through the engine's error handler, which a
# script cannot observe. The assertion therefore lives in
# tests/renderer_reporting_probe.ps1, which reads the process output and requires
# a warning naming every renderer below.

const RENDERERS := [
	["TerrainPaintedRendererComponent", "Rebuild"],
	["TerrainTileRendererComponent", "Rebuild"],
	["TerrainIsometricRendererComponent", "Rebuild"],
	["TerrainIsometricAutotileRendererComponent", "Rebuild"],
	["TerrainFeatureRendererComponent", "Rebuild"],
	["TerrainIsometricFeatureRendererComponent", "Rebuild"],
	["TerrainReliefRendererComponent", "Rebuild"],
	["TerrainResourceRendererComponent", "Rebuild"],
	["TerrainMapOverlayComponent", "Rebuild"],
]

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	for entry in RENDERERS:
		var renderer_name: String = entry[0]
		var method: String = entry[1]
		var script: Resource = load("res://addons/beep_game_builder_cs/ecs/terrain/%s.cs" % renderer_name)
		if script == null:
			print("MISSING %s" % renderer_name)
			continue

		var node: Node = script.new()
		# The node's name is what the warning prints in its [brackets], so the
		# probe can tell which renderer stayed quiet.
		node.name = renderer_name
		root.add_child(node)
		await process_frame

		node.call(method)          # nothing wired: this renderer cannot draw
		await process_frame
		print("DRIVEN %s" % renderer_name)

		node.queue_free()
		await process_frame

	print("[renderer-reporting] OK: drove %d renderers with no generator." % RENDERERS.size())
	quit(0)
