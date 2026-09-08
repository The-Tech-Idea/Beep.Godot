# GridWorkerSpawnerPanelComponent

Simple HUD panel (a `Control`, `[Tool][GlobalClass]`) for a base/depot/garage/barracks that spawns worker/truck/NPC units through `GridWorkerSpawnerComponent`: shows a title, a "count/max" readout, and a spawn button.

It follows the standard two-tier binding shape of this batch (bind an authored `Title`/`Count`/`SpawnButton` set of children, or generate a `PanelContainer` with the same three controls when `GenerateControlsWhenPathsEmpty` is set), and subscribes to the spawner's `UnitSpawned`/`SpawnRejected` signals so the count and button-disabled state stay live without polling.

## Public API

- `[Signal] SpawnButtonPressedEventHandler()` — fired unconditionally at the start of `RequestSpawn()`, before the spawner is asked to spawn anything.
- `[Export] NodePath SpawnerPath/TitleLabelPath/CountLabelPath/SpawnButtonPath` — component/control wiring.
- `[Export] bool BuildInEditor = true`, `bool GenerateControlsWhenPathsEmpty = false`.
- `[Export] bool HideWhenMissingSpawner = false` — hide the whole panel when no spawner is resolved.
- `[Export] string TitleText = "Base"`, `string SpawnButtonText = "Spawn Worker"`.
- `[Export] string CountTextFormat = "Workers: {0}/{1}"` — a `string.Format` template consumed by `FormatCount(count, max)`; a malformed template is caught (`FormatException`) and falls back to the hardcoded `"Workers: {count}/{max}"` string rather than throwing.
- `[Export] Vector2 PanelMinimumSize = new(176, 78)`.
- `public override void _Ready()` — resolves references, defers `RebuildPanel()` per `BuildInEditor`, subscribes to `UnitSpawned`/`SpawnRejected` at runtime.
- `public override void _ExitTree()` — unsubscribes both spawner signals and the spawn button.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `SpawnerPath` or (without generation enabled) no `Title`/`Count`/`SpawnButton` surface.
- `public void RebuildPanel()` — binds or generates the panel, then calls `RefreshPanel()`.
- `public bool RequestSpawn()` — emits `SpawnButtonPressed`, then calls `GridWorkerSpawnerComponent.SpawnWorker()`; returns whether a unit was actually spawned.
- `public void RefreshPanel()` — updates `Visible` (per `HideWhenMissingSpawner`), the count text, and the spawn button's `Disabled` state (`count >= max`, or no spawner at all).
- `public string CountText()` — forces a `RefreshPanel()`, then returns the count label's text.
- `public bool UsesSceneControls()` — true if any of the three label/button paths are set or found by convention.

## Dependencies

- Reads `GridWorkerSpawnerComponent.SpawnedCount/.MaxWorkers`, `.SpawnWorker()`, and its `UnitSpawned`/`SpawnRejected` signals (outside this batch).
- Uses `EntityComponent.FindComponent<GridWorkerSpawnerComponent>(...)` as the scene-fallback lookup, same pattern as every other file in this batch.
- Uses `KitChrome.SetConstantOverrideIfChanged`/`SetColorOverrideIfChanged` (outside this batch) for generated-control styling.
- Not established from this batch alone whether anything calls into `GridWorkerSpawnerPanelComponent` — none of the other 12 UI files reference it, though `GridWorkerStatusPanelComponent` in this same batch separately scans for `GridWorkerComponent` instances that a spawn from this panel would presumably create, without either panel referencing the other.

## Notes

- `RequestSpawn()` emits `SpawnButtonPressed` before knowing whether the spawn succeeds — the same "signal fires regardless of outcome" shape as `GridCalendarHudComponent.RequestAdvanceDay()` and `GridProductionPanelComponent.StartMachine()` in this batch.
- `FormatCount`'s `try/catch (FormatException)` around a user-editable `[Export] string` format template is a deliberate, narrow, safe fallback (a malformed export degrades the display text rather than throwing at runtime) — a good example of failing safe rather than failing silently, since the fallback text still communicates the same count/max values.
- Unlike `GridJobBoardComponent`/`GridObjectivePanelComponent`/`GridProductionPanelComponent`/`GridWorkerStatusPanelComponent`, this panel has nothing to list — it shows a single aggregate count rather than one row per unit, so it has no row-diffing logic and no `MaxVisible*` export.
