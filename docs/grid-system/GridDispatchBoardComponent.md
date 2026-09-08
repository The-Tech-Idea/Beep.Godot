# GridDispatchBoardComponent

`GridDispatchBoardComponent` runs a single-vehicle "settlers" dispatch loop: a button requests a task, a shared vehicle tweens out to a target position, the task's effects apply on arrival, and the vehicle tweens back. It is a self-contained scripted sequence, not a consumer of `GridJobQueueComponent`/`GridWorkerComponent` — it is a second, independent way this grid system models "dispatch work and react when it finishes," built for the case of one demonstrative vehicle and a handful of named tasks rather than a scalable worker pool.

The class doc comment explains why it looks the way it does: it replaces a controller that "hardcoded eight [tasks] as a switch over button names, with screen coordinates as literals in the cases and one private method per outcome." Every task is now a `GridDispatchTaskDefinition` resource, and a `Button`'s **name** is matched against a task's `Action` field, so "the panel and the task list are joined by data rather than by a switch" — adding a ninth task means adding a resource, not editing this file. The single-task-at-a-time restriction (`_isWorking`) is deliberate for the same reason the vehicle is shared: "two overlapping tweens on one node fight rather than queue."

## Public API
- `void Request(string action)` — the entry point a button's `Pressed` signal calls; if busy, sets `BusyPrompt` and returns; if no task's `Action` matches, sets an explicit `"No task is configured for {action}."` status rather than doing nothing silently ("a button whose name no task matches is a typo, and silently doing nothing looks like a broken loop").
- Signal `TaskCompleted(action)` — raised after the vehicle returns and effects have applied.
- Exports: `SpawnerPath`, `ResourceWalletPath`, `StatusLabelPath`, `WorkMarkerPath` (pulses at the work site while a task runs), `ToolButtonPaths`, `Tasks` (the `GridDispatchTaskDefinition` list), `HiddenAtStart`, `TravelSeconds`/`WorkSeconds` (timing), `IdlePrompt`/`BusyPrompt` (status text).

## Dependencies

- Subscribes to `GridWorkerSpawnerComponent.UnitSpawned`/`SpawnRejected` (this batch) purely for status text and an arrival nudge animation (`AnimateArrival`) — it does not use the spawner to create the dispatched vehicle itself; the vehicle is a fixed node referenced per-task via `GridDispatchTaskDefinition.VehiclePath`. This shows the spawner is reusable by both the job-queue worker system and this independent dispatch board within the same scene.
- Reads every field off `GridDispatchTaskDefinition` (this batch): `Action`, `Label`, `VehiclePath`, `Target`, `Show`/`Hide`, `RecolourTarget`/`Recolour`, `RewardResourceId`/`RewardAmount`.
- Calls `GridResourceWalletComponent.AddAmount` (outside this batch) to pay out a task's reward.

## Notes

- This is a duplicate-looking pattern against `GridJobQueueComponent` + `GridWorkerComponent` in the same batch: both are ways to "dispatch work, then react to completion," but they don't share any code or state — this one is Tween-driven, single-vehicle, and data-configured per task; the other is a claimable multi-worker job queue. They appear intentionally separate (one demo-scale scripted sequence, one general worker system) rather than an unresolved duplication, since neither references the other.
- `Dispatch` resolves `task.VehiclePath` via `GetNodeOrNull` relative to **this** node, not relative to the task resource (resources have no tree position) — so every task's `VehiclePath` must be authored as a path from the `GridDispatchBoardComponent` node.
- `Apply` special-cases `Polygon2D` for `RecolourTarget` (sets `.Color`) versus any other `CanvasItem` (sets `.Modulate`), since a `Polygon2D`'s fill color isn't driven by modulate.
- `ShowWorkMarker` always plays its pulse tween even if `WorkMarkerPath` was never set — `_workMarker` being null is guarded, so this is safe, just worth knowing the pulse is unconditional when a marker is wired.
