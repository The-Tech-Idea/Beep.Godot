extends "res://addons/beep_game_builder_cs/templates/scenes/actors/actor_lab.gd"

var diagnostic_elapsed := 0.0

func _ready() -> void:
	$Archive.ArchiveDirectory = "user://streaming_lab/%s_%s" % [OS.get_process_id(), Time.get_ticks_usec()]
	$World.GenerationProgress.connect(func(stage, fraction):
		$HUD/Loading/Row/Stage.text = stage
		$HUD/Loading/Row/Progress.value = fraction * 100.0
		$HUD/Loading/Row/Cancel.disabled = not $World.CanCancelGeneration)
	$World.GenerationFinished.connect(func(success, message):
		$HUD/Loading.visible = not success
		if not success: $HUD/Loading/Row/Stage.text = message
		$HUD/Loading/Row/Cancel.disabled = true)
	$HUD/Loading/Row/Cancel.pressed.connect(func(): $World.CancelGeneration())
	$HUD/Toolbar/Row/Home.pressed.connect(func():
		if ready_to_play:
			$Camera2D/Controller.FocusWorld($Grid.CellToWorld(home), true)
			$Camera2D/Controller.SetZoomLevel(0.8, true))
	$HUD/Toolbar/Row/Streaming.toggled.connect(set_streaming)
	await super._ready()

func world_built(size: Vector2i) -> void:
	super.world_built(size)
	if not ready_to_play: return
	# Capture the complete visual source before retiring any gameplay records.
	$Painted.Rebuild()
	$CameraDemand.RefreshDemand()
	$HUD/Toolbar/Row/Streaming.disabled = false
	set_streaming($HUD/Toolbar/Row/Streaming.button_pressed)

func set_streaming(enabled: bool) -> void:
	$Archive.AutoEnforceChunkBudget = enabled and ready_to_play
	# Archived records must remain demand-loadable when automatic eviction is paused.
	$Archive.AutoLoadPinnedChunks = ready_to_play

func _process(delta: float) -> void:
	diagnostic_elapsed += delta
	if diagnostic_elapsed < 0.25: return
	diagnostic_elapsed = 0.0
	var activity := "Reading" if $Archive.IsLoading else ("Writing" if $Archive.IsSaving else "Idle")
	var view := "Overview" if $CameraDemand.IsUsingOverview else "Detail"
	$HUD/Diagnostics.text = "%s | %s | Chunks %d / %d | Archived %d | Pinned %d\nCells %d / %d | Excess: %d chunks, %d cells" % [
		view, activity, $Cells.StoredChunkCount, $Archive.MaximumResidentChunks,
		$Cells.EvictedChunkCount, $Cells.GetPinnedChunks().size(), $Cells.CellCount,
		$Archive.MaximumResidentCells, $Archive.ResidentChunksOverBudget, $Archive.ResidentCellsOverBudget]
	if not $Archive.LastError.is_empty():
		$HUD/Diagnostics.text += "\n" + $Archive.LastError
