# GridWorkerSpawnerComponent

`GridWorkerSpawnerComponent` is the factory for worker/truck/NPC units: given a cell, it validates the spawn is legal (in bounds, unblocked, unoccupied, on allowed terrain), instantiates a unit (a custom `PackedScene` or a built-in placeholder `CharacterBody2D`), ensures it has both a `GridPathFollowerComponent` and a `GridWorkerComponent`, and wires every NodePath those two components need to function — grid, navigation, job queue — before handing the finished unit back. The class doc comment states its job in one line: "Spawns worker, truck, or NPC units from a base/building and wires them to the reusable grid navigation and job systems."

It exists so that "spawn a worker" is one call instead of a multi-step recipe every project would otherwise repeat: instantiate, add a path follower, add a worker component, and cross-wire four or five NodePaths between them and the shared grid systems. `EnsurePathFollower`/`EnsureWorker` look for pre-existing components on the instantiated node before creating new ones, so a custom `UnitScene` that already carries a correctly configured follower/worker is respected rather than duplicated — this is what lets a project swap in real art and behavior while still getting the spawner's wiring and validation for free.

## Public API
- `Node2D? SpawnWorker()` / `Node2D? SpawnWorker(Vector2I cell)` — validates, instantiates, wires, and returns the new unit, or returns null and emits `SpawnRejected(reason)` on failure (`"missing_grid_navigation_or_jobs"`, `"missing_units_root"`, `"max_workers_reached"`, or a terrain/occupancy reason from `SpawnBlockReason`).
- `Array<Node> GetSpawnedUnits()` — the live, freed-unit-pruned list of everything this spawner has produced.
- `bool CanSpawnAt(Vector2I cell)` — dry-run predicate mirroring `SpawnWorker`'s validation without side effects.
- `int SpawnedCount` — prunes freed units, then returns the count.
- `int EffectiveMaxWorkers`, `int EffectiveInitialWorkers`, `float EffectiveDefaultUnitSpeed` — sanitized versions of the corresponding exports.
- Signals: `UnitSpawned(unit, workerId, x, y)`, `SpawnRejected(reason)`.
- Exports: `UnitScene`, `UnitsRootPath`, `GridPath`, `NavigationPath`, `JobQueuePath`, `CellDataPath`, `PlacementPath`, `SpawnCell`, `WorkerIdPrefix`, `AutoSpawnOnReady`, `InitialWorkers`, `MaxWorkers`, `DefaultUnitSpeed`, `DriveCharacterBody`, `SetZIndexFromY`, `TreatCellDataBlockedAsUnspawnable` (default on), `TreatBlockedTerrainKindsAsUnspawnable` (default on), `TreatPlacementOccupiedAsUnspawnable` (default on), `BlockedTerrainKinds` (defaults to `GridTerrainRules.DefaultBlockedTerrainKinds()`), `AllowedTerrainKinds`.

## Dependencies

- Resolves `GridProjectionComponent`, `GridNavigationComponent`, `GridCellDataComponent`, `GridPlacementComponent` (all outside this batch) to validate and place a spawn.
- Creates and configures `GridPathFollowerComponent` (outside this batch) and `GridWorkerComponent` (this batch) on every spawned unit — this is the file that sets `worker.JobQueuePath`, `worker.GridPath`, `worker.PathFollowerPath`, and `worker.WorkerId` via `GetPathTo`, which `GridWorkerComponent.ResolveReferences()` later reads back.
- Resolves its own `GridJobQueueComponent` (this batch) purely to hand its path to each spawned worker and as a precondition for spawning at all (`_jobs == null` rejects the spawn).
- Uses `GridTerrainRules.Normalize`/`IsAllowed`/`MatchesAny`/`DefaultBlockedTerrainKinds` (outside this batch) for terrain-kind eligibility.
- `GridDispatchBoardComponent` (this batch) subscribes to this spawner's `UnitSpawned`/`SpawnRejected` purely for status text and an arrival animation, without using it to actually create its own dispatched vehicle — established within this batch.

## Notes

- The terrain-kind normalization comment is a documented bug fix, not speculation: "`GridTerrainRules.Normalize` also replaces spaces and dashes — the private normalizer this replaced had quietly forgotten that, so this component alone treated 'Deep Water' and 'deep_water' as different kinds." Centralizing normalization in `GridTerrainRules` retired a second, divergent copy of the same logic.
- `CreateUnitNode`'s placeholder (a blue diamond `Polygon2D` on a `CharacterBody2D` with a `RectangleShape2D` collider) is real runtime behavior when `UnitScene` is unset, not test-only sample data — a project with no authored unit art still gets a spawnable, visible, collidable unit.
- `UniqueUnitName` appends `_2`, `_3`, … on a name collision under `_unitsRoot` rather than rejecting the spawn.
- `SpawnBlockReason` checks navigation bounds/blocked, then placement occupancy, then (only if `_cellData` is resolved) cell-data blocked flag and terrain-kind eligibility — so a scene with no `GridCellDataComponent` wired simply skips terrain-kind filtering entirely rather than failing closed.
