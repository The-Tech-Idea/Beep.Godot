extends SceneTree

# The generator's output is pinned, layer by layer, and so is its allocation bill.
#
# ENH-16 rewrites how the generation stages walk neighbours, copy fields and rank
# percentiles. None of that may move a single cell: this probe hashes every layer the
# generator publishes for three seeds at two sizes and compares them with the recorded
# fixture, naming the case and the layer that moved. The fixture is rewritten only when
# the probe is run with `-- --record`, which is a deliberate act and prints as one.
#
# The second half is the reason the rewrite exists. A Huge build's managed allocation is
# read on the generating thread and held under a ceiling set at a quarter of what the
# stages allocated before the pass. The ceiling is a literal here, not a fixture value,
# so re-recording the hashes can never quietly move it.

const BASE := "res://addons/beep_game_builder_cs/"
const FIXTURE := "res://tests/fixtures/terrain_generation_baseline.json"
const SEEDS := [31415, 4242, 777]
const SIZES := {"small": Vector2i(48, 48), "huge": Vector2i(128, 80)}
# Recorded 2026-09-08 before ENH-16, Huge (128x80, 11 samples per cell), seed 31415,
# shape 0: 284,483,064 bytes allocated across TerrainFieldBuilder.BuildPrepared. The
# ceiling is a quarter of that.
const HUGE_ALLOCATION_BASELINE_BYTES := 284483064
const HUGE_ALLOCATION_CEILING_BYTES := HUGE_ALLOCATION_BASELINE_BYTES / 4

var failures: Array[String] = []

func check(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error(message)

func _initialize() -> void:
	call_deferred("run")

func case_name(seed: int, size_name: String, shape: int) -> String:
	return "seed%d_%s_shape%d" % [seed, size_name, shape]

func run() -> void:
	var recording := "--record" in OS.get_cmdline_user_args()
	var smoke: Node = load("res://tests/TerrainGenerationBaselineSmoke.cs").new()
	root.add_child(smoke)

	var snapshot := {}
	for i in SEEDS.size():
		var seed: int = SEEDS[i]
		for size_name in SIZES:
			var layers: Dictionary = smoke.call("Snapshot", SIZES[size_name], seed, i)
			snapshot[case_name(seed, size_name, i)] = layers

	if recording:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://tests/fixtures"))
		var out := FileAccess.open(FIXTURE, FileAccess.WRITE)
		check(out != null, "could not open %s for writing" % FIXTURE)
		if out != null:
			out.store_string(JSON.stringify({"cases": snapshot}, "  "))
			out.close()
		print("[terrain-generation-baseline] RECORDED %d cases to %s" % [snapshot.size(), FIXTURE])
	else:
		var text := FileAccess.get_file_as_string(FIXTURE)
		check(text.length() > 0, "baseline fixture %s is missing; run with `-- --record` on known-good code" % FIXTURE)
		var parsed: Variant = JSON.parse_string(text) if text.length() > 0 else null
		check(parsed is Dictionary and (parsed as Dictionary).has("cases"), "baseline fixture is not a {cases: ...} document")
		if parsed is Dictionary and (parsed as Dictionary).has("cases"):
			var expected: Dictionary = parsed["cases"]
			check(expected.keys().size() == snapshot.keys().size(),
				"fixture holds %d cases, probe produced %d" % [expected.keys().size(), snapshot.keys().size()])
			for name in snapshot:
				check(expected.has(name), "fixture has no case %s" % name)
				if not expected.has(name):
					continue
				var want: Dictionary = expected[name]
				var got: Dictionary = snapshot[name]
				for layer in got:
					check(want.has(layer), "%s: fixture has no layer %s" % [name, layer])
					if want.has(layer):
						check(want[layer] == got[layer], "%s: layer %s changed" % [name, layer])

	# The allocation bill, on the generating thread, for the largest authored size.
	var per_stage := {}
	var allocated: int = smoke.call("AllocatedBytes", SIZES["huge"], SEEDS[0], 0, per_stage)
	var names := per_stage.keys()
	names.sort_custom(func(a, b): return per_stage[a] > per_stage[b])
	print("[terrain-generation-baseline] huge build allocated %d bytes (%.1f MiB)" % [allocated, allocated / 1048576.0])
	for stage in names:
		print("[terrain-generation-baseline]   %-22s %12d bytes" % [stage, per_stage[stage]])
	if not recording:
		check(allocated < HUGE_ALLOCATION_CEILING_BYTES,
			"a Huge build allocated %d bytes; the ceiling is %d (a quarter of the %d recorded before the allocation pass)"
			% [allocated, HUGE_ALLOCATION_CEILING_BYTES, HUGE_ALLOCATION_BASELINE_BYTES])

	smoke.free()
	if failures.is_empty():
		print("[terrain-generation-baseline] OK")
		quit(0)
	else:
		print("[terrain-generation-baseline] FAILED")
		quit(1)
