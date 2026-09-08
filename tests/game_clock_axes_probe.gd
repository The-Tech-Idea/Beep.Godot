extends SceneTree

# One clock, two axes. The same component, with no axis branch of its own,
# must reach the same end state whether the game advances on frames or on an
# end-turn button - and a turn-based game must actually advance, which is the
# bug this replaced: the strategy genre declared "turns" and shipped nothing
# that could end one, so every duration in the game was frozen for the session.

const GAME_APP := preload("res://addons/beep_game_builder_cs/ecs/GameApp.cs")
const GAME_INFO := preload("res://addons/beep_game_builder_cs/core/GameInfo.cs")
const WORK := preload("res://addons/beep_game_builder_cs/ecs/WorkComponent.cs")
const STATS := preload("res://addons/beep_game_builder_cs/ecs/stats/StatsComponent.cs")
const TURN_DRIVER := preload("res://addons/beep_game_builder_cs/ecs/time/TurnDriverComponent.cs")
const GENRE_SCENE := preload("res://addons/beep_game_builder_cs/ecs/BeepGenreScene.cs")

const AXIS_REALTIME := 0
const AXIS_TURNS := 1

var _failures: int = 0

func check(condition: bool, message: String) -> void:
	if condition:
		print("  ok    %s" % message)
	else:
		print("  FAIL  %s" % message)
		_failures += 1

func _initialize() -> void:
	call_deferred("_run")

# Stands up the game master with a declared axis, exactly as a stamped project
# would - GameApp is the autoload, and it builds the clock itself.
func _make_game_app(axis: int, beats_per_day: float) -> Node:
	var info: Resource = GAME_INFO.new()
	info.set("TimeAxis", axis)
	info.set("BeatsPerDay", beats_per_day)

	var app: Node = GAME_APP.new()
	app.name = "GameApp"
	# Assigned BEFORE the node enters the tree, so _EnterTree's BuildSubsystems
	# configures the clock from THIS info and never loads game_info.tres.
	app.set("Info", info)
	root.add_child(app)
	app.call("SetGameRunning", true) # This fixture tests a running clock, not menu lifecycle.
	return app

func _make_worker(parent: Node, total_work: float, speed: float) -> Node:
	var work: Node = WORK.new()
	work.set("WorkSpeed", speed)
	parent.add_child(work)
	work.call("StartWork", total_work)
	return work

func _run() -> void:
	await _turn_axis()
	await _realtime_axis()
	await _turn_driver()
	await _clock_follows_declaration()
	await _pause_is_one_fact()
	await _clock_is_saved()

	print("RESULT: %s" % ("all checks passed" if _failures == 0 else "%d FAILED" % _failures))
	quit(1 if _failures > 0 else 0)

# A genre scene wired the README way: no generator run, just a BeepGenreScene
# applying its genre's tuning to the live GameInfo at its own _Ready.
func _make_genre_scene(genre_id: String) -> Node:
	var scene: Node = GENRE_SCENE.new()
	scene.set("GenreId", genre_id)
	scene.set("AutoInstantiateMainScene", false)
	scene.set("RegisterAsMainScene", false)
	root.add_child(scene)
	return scene

# ── The turn axis ────────────────────────────────────────────────────────────
func _turn_axis() -> void:
	var app := _make_game_app(AXIS_TURNS, 1.0)
	var clock: Node = app.get("Clock")
	check(clock != null, "GameApp owns a clock - it is a member, not a separate autoload")
	if clock == null:
		return

	check(int(clock.get("Axis")) == AXIS_TURNS,
		"the clock took the axis GameInfo DECLARED - nothing inferred it from the tree")

	# WorkSpeed 1 and 3 units of work: exactly three turns, so a mistake of one
	# beat either way is visible rather than rounded away.
	var work := _make_worker(root, 3.0, 1.0)
	await process_frame
	check(bool(work.get("IsWorking")), "the producer started")

	var before: float = float(work.get("AvailableWork"))
	for i in range(10):
		await process_frame
	check(is_equal_approx(float(work.get("AvailableWork")), before),
		"frames do NOT advance work on the turn axis - 10 frames passed and nothing moved")

	check(bool(clock.call("EndTurn")), "EndTurn advances a turn-axis clock")
	check(is_equal_approx(float(work.get("AvailableWork")), 2.0),
		"one turn burned exactly one beat of work (3 -> 2), not one frame's worth")
	check(int(clock.get("Turn")) == 1, "the turn counter advanced once")
	check(int(clock.get("Day")) == 1,
		"the day CASCADED from the beat - one turn is one day at BeatsPerDay 1, not a second clock")

	clock.call("EndTurn")
	clock.call("EndTurn")
	check(is_equal_approx(float(work.get("AvailableWork")), 0.0), "three turns finished three units of work")
	check(not bool(work.get("IsWorking")), "the producer stopped when its work ran out")
	check(int(clock.get("Day")) == 3, "three turns is three days")

	# The modifier ticker rides the same heartbeat, with no axis branch either.
	var stats: Node = STATS.new()
	root.add_child(stats)
	await process_frame
	check(stats != null, "StatsComponent attaches with no axis of its own to declare")

	work.free()
	stats.free()
	app.free()
	await process_frame

# ── The real-time axis ───────────────────────────────────────────────────────
func _realtime_axis() -> void:
	var app := _make_game_app(AXIS_REALTIME, 45.0)
	var clock: Node = app.get("Clock")
	check(int(clock.get("Axis")) == AXIS_REALTIME, "a real-time game declares the other axis")

	# The SAME component, unchanged, on the other axis. Deliberately slow (0.5
	# units/second against 3 units of work) so the sample lands mid-job: a fast
	# producer finishes inside the first frame and "did it move?" reads as "no".
	var work := _make_worker(root, 3.0, 0.5)
	var start: float = float(work.get("AvailableWork"))
	for i in range(10):
		await process_frame
	var now: float = float(work.get("AvailableWork"))
	check(now < start,
		"frames DO advance work on the real-time axis - the same component, no branch in it (%.4f -> %.4f)" % [start, now])
	check(bool(work.get("IsWorking")), "and it is still mid-job, so that reading was genuinely mid-flight")
	check(float(clock.get("Elapsed")) > 0.0, "the clock advanced itself, with nobody calling EndTurn")

	check(not bool(clock.call("EndTurn")),
		"EndTurn REPORTS refusal on a real-time clock instead of silently doing nothing")
	check(int(clock.get("Turn")) == 0, "a refused EndTurn did not move the turn counter")

	work.free()
	app.free()
	await process_frame

# ── The regression that shipped: a turn game with no way to end a turn ───────
func _turn_driver() -> void:
	var app := _make_game_app(AXIS_TURNS, 1.0)
	var clock: Node = app.get("Clock")

	var driver: Node = TURN_DRIVER.new()
	root.add_child(driver)
	var work := _make_worker(root, 2.0, 1.0)
	await process_frame

	check(bool(driver.get("visible")), "the driver shows itself on the turn axis")
	check(bool(driver.call("RequestEndTurn")), "the driver ends a turn")
	check(is_equal_approx(float(work.get("AvailableWork")), 1.0), "the driver's turn reached the producer")

	driver.call("RequestEndTurn")
	check(is_equal_approx(float(work.get("AvailableWork")), 0.0),
		"a turn-based producer actually FINISHES - strategy shipped an axis with no driver and froze here")

	driver.free()
	work.free()
	app.free()
	await process_frame

# ── The clock follows the declaration, even when the declaration changes ─────
func _clock_follows_declaration() -> void:
	# The master builds its clock from the Info it has at _EnterTree. A
	# BeepGenreScene then rewrites THAT Info's axis at its _Ready. The strategy
	# genre declares turns; the clock used to stay on the axis it was built with.
	var app := _make_game_app(AXIS_REALTIME, 45.0)
	var clock: Node = app.get("Clock")
	check(int(clock.get("Axis")) == AXIS_REALTIME, "before the genre is applied the clock runs the default real-time axis")

	var strategy := _make_genre_scene("strategy")
	await process_frame
	check(int(app.get("Info").get("TimeAxis")) == AXIS_TURNS, "the strategy genre declared turns on the live GameInfo")
	check(int(clock.get("Axis")) == AXIS_TURNS,
		"and the clock FOLLOWED the declaration - it used to keep the real-time axis it was configured with once")
	check(is_equal_approx(float(clock.get("BeatsPerDay")), 1.0), "declaring turns re-declared the cascade: one turn is one day")
	check(bool(clock.call("EndTurn")), "so the turn-based genre can actually end a turn")

	strategy.free()
	app.free()
	await process_frame

	# A genre that says nothing about time must leave an authored day length
	# alone. Every genre with a tuning block used to reset BeatsPerDay.
	var quiet := _make_game_app(AXIS_REALTIME, 120.0)
	var quiet_clock: Node = quiet.get("Clock")
	var platformer := _make_genre_scene("platformer")
	await process_frame
	check(is_equal_approx(float(quiet.get("Info").get("BeatsPerDay")), 120.0),
		"a genre with no time_axis leaves the authored BeatsPerDay alone (120 stays 120)")
	check(is_equal_approx(float(quiet_clock.get("BeatsPerDay")), 120.0), "and the clock still runs the authored day length")

	platformer.free()
	quiet.free()
	await process_frame

# ── One pause fact: the tree's, through the master's one door ────────────────
func _pause_is_one_fact() -> void:
	var app := _make_game_app(AXIS_TURNS, 1.0)
	var clock: Node = app.get("Clock")
	var announced := {"paused": 0, "resumed": 0}
	app.connect("GamePaused", func(): announced["paused"] += 1)
	app.connect("GameResumed", func(): announced["resumed"] += 1)

	check(not paused and not bool(app.get("IsPaused")), "nothing is paused to begin with")
	app.call("SetPaused", true)
	check(paused, "the master's door sets the TREE's pause flag - the one pause fact")
	check(bool(app.get("IsPaused")), "GameApp.IsPaused is a view of that flag, not a second copy")
	check(announced["paused"] == 1, "pausing announced itself once (GamePaused)")
	check(not bool(clock.call("EndTurn")),
		"EndTurn REFUSES under the pause - the turn axis has no _Process for the tree pause to stop, so the button used to work straight through the pause menu")
	check(int(clock.get("Turn")) == 0, "and the turn counter did not move")

	app.call("SetPaused", false)
	check(not paused and announced["resumed"] == 1, "resuming clears the flag and announces itself once (GameResumed)")
	check(bool(clock.call("EndTurn")) and int(clock.get("Turn")) == 1, "a resumed game ends turns again")
	app.call("SetPaused", false)
	check(announced["resumed"] == 1, "resuming a game that is not paused announces nothing")

	app.free()
	await process_frame

	# The real-time axis gets the same fact for free: a pausable clock does not
	# process while the tree is paused. Nothing else in the clock knows about pause.
	var rt := _make_game_app(AXIS_REALTIME, 45.0)
	var rt_clock: Node = rt.get("Clock")
	for i in range(3):
		await process_frame
	rt.call("SetPaused", true)
	var frozen: float = float(rt_clock.get("Elapsed"))
	for i in range(5):
		await process_frame
	check(is_equal_approx(float(rt_clock.get("Elapsed")), frozen),
		"a paused real-time clock does not advance (%.4f held for 5 frames)" % frozen)
	rt.call("SetPaused", false)
	for i in range(3):
		await process_frame
	check(float(rt_clock.get("Elapsed")) > frozen, "and it runs again once resumed")

	rt.free()
	await process_frame

# ── The clock is saved: three facts, Day re-derived ──────────────────────────
func _clock_is_saved() -> void:
	var info: Resource = GAME_INFO.new()
	info.set("TimeAxis", AXIS_TURNS)
	info.set("BeatsPerDay", 1.0)
	info.set("EnableGameStateManager", true)
	info.set("SaveDirectory", "user://game_clock_axes_probe_saves")
	var app: Node = GAME_APP.new()
	app.name = "GameApp"
	app.set("Info", info)
	root.add_child(app)
	await process_frame   # _Ready: the master joins the saveables group

	var clock: Node = app.get("Clock")
	var saves: Node = app.get("Saves")
	check(saves != null, "the master owns the save manager")
	saves.call("NewGame", "clock probe")
	saves.call("BeginSession", "clock probe")
	await process_frame
	await process_frame
	for i in range(3):
		clock.call("EndTurn")
	check(bool(saves.call("Save", 0)), "a save is written with the clock at turn 3")

	for i in range(2):
		clock.call("EndTurn")
	check(int(clock.get("Turn")) == 5 and int(clock.get("Day")) == 5, "play went on to turn 5 after the save")

	check(bool(saves.call("Load", 0)), "the save loads back")
	saves.call("RestoreAllSaveables")
	check(int(clock.get("Turn")) == 3,
		"the clock came back at turn 3 - a turn-based save used to resume at turn 0 while every remaining duration kept its beats")
	check(int(clock.get("Day")) == 3, "Day was re-derived from the saved beats, not stored a second time")
	check(is_equal_approx(float(clock.get("Elapsed")), 3.0), "Elapsed is the saved beats")

	saves.call("DeleteSave", 0)
	app.free()
	await process_frame
