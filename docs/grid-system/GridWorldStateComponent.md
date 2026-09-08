# GridWorldStateComponent

`Node` implementing `ISaveable` that captures and restores the entire reusable grid toolkit's runtime state — cell data, roads, placement occupancy, navigation-blocked cells, every `GridObjectComponent`'s own state, selection, and jobs — as one versioned `Godot.Collections.Dictionary` snapshot. Because the snapshot is a plain Dictionary, it can be dropped into `GameStateData`, JSON, config files, or a custom save system without this component knowing which.

Each captured category is independently gated by its own export (`CaptureCellData`, `CapturePlacementOccupancy`, ...), so a project not using, say, jobs never has to carry an empty `jobs` array through its saves. Grid objects are handled differently from every other category: rather than storing raw state inline, the component walks every node in the `grid_objects` group (optionally filtered to descendants of `ObjectsRootPath`), records each one's tree path (`GetPathTo`) alongside its own `CaptureState()` dictionary, and on restore resolves each by that path and calls `RestoreState` on it — meaning this component restores *values* onto grid objects that must already exist in the tree (built by whatever scene-instancing the save system does elsewhere), it does not spawn them. `RestoreState`'s ordering is deliberate: it releases every grid object's footprint reservations *first* (`ReleaseGridObjectFootprints`), then clears and reloads the raw placement-occupancy and navigation-blocked sets from the snapshot, then reloads roads, and only then restores each grid object's own values — which will re-reserve its footprint internally if it was reserved (or `ReserveFootprintOnReady`) — a teardown-then-rebuild order chosen to avoid double-reserving a cell during the restore.

## Public API

- `[Signal] StateCapturedEventHandler()` / `StateRestoredEventHandler()`.
- `[Export] public bool ParticipatesInSave { get; set; } = true` — whether this node joins `SaveableHelper.Group` at runtime.
- `[Export] public string SaveKey { get; set; } = "grid_world.state"` — the key this snapshot is stored under in `GameStateData.GameData`.
- `[Export] public NodePath PlacementPath / NavigationPath / SelectionPath / JobQueuePath / CellDataPath / RoadPath / ObjectsRootPath` — dependency wiring; `ObjectsRootPath` also scopes/roots the grid-object search.
- `[Export] public bool CaptureCellData / CapturePlacementOccupancy / CaptureNavigationBlocks / CaptureRoads / CaptureGridObjects / CaptureSelection / CaptureJobs` — per-category on/off switches for both capture and restore.
- `public override void _Ready()` — resolves references and, outside the editor when `ParticipatesInSave`, joins `SaveableHelper.Group`.
- `public override void _ExitTree()` — leaves the group.
- `public override string[] _GetConfigurationWarnings()` — warns if `SaveKey` is empty.
- `public Godot.Collections.Dictionary CaptureState()` — builds the versioned snapshot dictionary (`"version"` = 1) from every enabled, resolved category; emits `StateCaptured`.
- `public void RestoreState(Godot.Collections.Dictionary state)` — applies a snapshot back onto every enabled, resolved category in the teardown-then-rebuild order above; emits `StateRestored`.
- `public void Save(GameBuilder.GameStateData state)` — `ISaveable`; writes `CaptureState()` under `SaveKey` (no-op if `SaveKey` is blank).
- `public void Load(GameBuilder.GameStateData state)` — `ISaveable`; reads back and calls `RestoreState` if `SaveKey` is present and holds a dictionary.

## Dependencies

- Implements `ISaveable` (`ISaveable.cs`) and joins `SaveableHelper.Group`; what actually iterates that group and invokes `Save`/`Load` is not established from this batch alone.
- Calls into `GridCellDataComponent` (`GetCells()`/`LoadCells()`) and `GridObjectComponent` (`CaptureState()`/`RestoreState()`/`ReleaseFootprint()`) — both confirmed by reading those two files in this same batch — plus, outside this batch, `GridPlacementComponent` (`GetOccupiedCells`/`SetOccupied`/`ClearOccupied`), `GridNavigationComponent` (`GetBlockedCells`/`SetBlocked`/`ClearBlocked`), `GridSelectionComponent` (`GetSelectedCells`/`SelectCell`/`ClearSelection`), `GridJobQueueComponent` (`GetJobs`/`LoadJobs`), and `GridRoadComponent` (`GetRoads`/`LoadRoads`).
- Uses `GridVariantReader.TryDictionary`/`.Array`/`.Vector2I` to parse the incoming snapshot.

## Notes

- Grid-object identity round-trips through `GetPathTo(gridObject).ToString()`, a `NodePath` string relative to this component. If the tree shape differs between capture and restore — a renamed/reparented node, or one not yet instantiated — the lookup (`GetNodeOrNull`) simply returns null and that object's entry is skipped with no warning; a partial grid-object restore is silent.
- `GridObjects()` prefers the engine-provided group lookup (`GetTree().GetNodesInGroup(GridObjectComponent.ComponentGroupName)`, filtered to `ObjectsRootPath` ancestry) and only falls back to a manual recursive tree walk (`CollectGridObjects`) when the group lookup returns zero results — a reasonable fast-path-with-fallback, not a duplicate implementation, since the two paths are used mutually exclusively per call.
- `ReadArray`/`ReadCells`/the private `DictString` helper here re-wrap the same `dict.ContainsKey(key) ? ... : fallback` pattern that `GridCellDataComponent` and `GridObjectComponent` each also define privately — all three ultimately delegate the real parsing to `GridVariantReader`, so the duplication is a trivial wrapper repeated three times rather than divergent logic.
