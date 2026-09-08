# GridMinimapComponent

Lightweight HUD overview map (a `Control`, `[Tool][GlobalClass]`) for top-down/isometric grid worlds: it custom-draws (`_Draw`) a baked terrain background, road dots, job dots, selection dots, live unit positions, and the camera's viewport rectangle, all from existing Godot nodes without requiring a `TileMap` or a dedicated minimap scene. It is architecturally the odd one out in this UI batch — every other file here builds a tree of child `Control`s (`Label`/`Button`/`PanelContainer`); this one is a single `Control` that paints everything itself in `_Draw`.

Its whole design is built around avoiding per-frame allocation from Godot's marshalled collections. Road cells, job cells, and selected cells are each cached into a plain `List<Vector2I>` snapshot, refreshed only when a `_roadsDirty`/`_jobsDirty`/`_selectionDirty` flag is set by the owning system's change signal — the file's own doc comment says the previous version rebuilt these marshalled collections every single frame and "made an idle minimap one of the most allocation-heavy nodes in a scene." The terrain background is similarly baked once into an `ImageTexture` (`_terrainDirty`) rather than redrawn per cell per frame. What *is* read live every redraw is unit positions and the camera rectangle, since those move continuously; even so, the redraw itself is throttled to `RefreshIntervalSeconds` (default 0.1s) via an accumulator in `_Process`, because an overview map does not need frame-rate responsiveness.

## Public API

- `[Export] NodePath GridPath/NavigationPath/RoadPath/SelectionPath/JobQueuePath/UnitsRootPath/CameraPath/CellDataPath` — component/node wiring; `CellDataPath` is the source for the baked terrain background and is auto-found if left empty (documented as relevant only when a scene has more than one `GridCellDataComponent`).
- `[Export] bool AutoRefresh = true` — gates the `_Process` redraw-throttle loop (also runs while `Engine.IsEditorHint()`).
- `[Export(Range 0.02..2)] float RefreshIntervalSeconds = 0.1f` — minimum seconds between redraws while `AutoRefresh` is on.
- `[Export] Vector2I BoundsOrigin/BoundsSize`, `bool PreferNavigationBounds = true` — map bounds used for cell↔minimap conversion; when `PreferNavigationBounds` and a `GridNavigationComponent` with `UseBounds` is resolved, its bounds win over the local exports.
- `[Export] bool ShowTerrain/ShowRoads/ShowSelection/ShowJobs/ShowUnits/ShowCameraView` — per-layer visibility.
- `[Export] Color BackgroundColor/BorderColor/RoadColor/SelectionColor/JobColor/UnitColor/CameraColor` — per-layer draw colors.
- `public Vector2I EffectiveBoundsOrigin()` / `public Vector2I EffectiveBoundsSize()` — resolved bounds after the `PreferNavigationBounds` override and a minimum-1-per-axis clamp.
- `public override void _Ready()` — resolves references, sets `_Process` per `AutoRefresh`/editor-hint, queues an initial redraw.
- `public override void _ExitTree()` — disconnects all four signal sources.
- `public override void _Process(double delta)` — accumulates `delta` and calls `QueueRedraw()` once `RefreshIntervalSeconds` has elapsed.
- `public override string[] _GetConfigurationWarnings()` — warns if `BoundsSize` has a non-positive axis.
- `public override void _Draw()` — the actual paint: background, baked terrain texture, border, road/job/selection dots, unit dots, camera rectangle, each gated by its `Show*` flag.
- `public void RebuildMinimap()` — marks all four snapshots dirty and queues a redraw.
- `public void RefreshMinimap()` — alias for `RebuildMinimap()`.
- `public Vector2 CellToMinimap(Vector2I cell)` — converts a grid cell to a local pixel position inside the current map rect using the effective bounds.
- `public int VisibleRoadCount()` / `public int VisibleJobCount()` — snapshot counts (force a `RefreshSnapshots()` first).
- `public int VisibleUnitCount()` — live count of `Node2D` children directly under `_unitsRoot`.

## Dependencies

- Reads `GridRoadComponent.GetRoadCells()` and its `RoadChanged`/`RoadsChanged` signals (outside this batch).
- Reads `GridJobQueueComponent.GetJobs()` (same dictionary shape consumed by `GridJobBoardComponent` in this batch) and its `QueueChanged` signal (outside this batch).
- Reads `GridSelectionComponent.GetSelectedCells()` and its `SelectionChanged` signal (outside this batch).
- Reads `GridCellDataComponent.GetTerrainKind(Vector2I)` and its `CellChanged`/`CellsChanged` signals (outside this batch) to bake the terrain texture.
- Reads `GridProjectionComponent.WorldToCell(Vector2)` (outside this batch) to place units/camera on the map, falling back to a raw floor-based approximation when unresolved.
- Reads `GridNavigationComponent.UseBounds/.BoundsOrigin/.BoundsSize` (outside this batch) when `PreferNavigationBounds` is set.
- Uses `EntityComponent.FindComponent<T>(...)` as the scene-fallback lookup for grid/navigation/road/selection/jobs/cell-data, same pattern as every other file in this batch; `UnitsRootPath` and `CameraPath` have no such fallback (units root must be path-set; camera falls back to `GetViewport()?.GetCamera2D()`).
- Not established from this batch alone whether anything calls into `GridMinimapComponent` — none of the other 12 UI files reference it.

## Notes

- `TerrainColors` is a local, hardcoded `Dictionary<string, Color>` mapping ~20 terrain-kind strings (`"grass"`, `"desert"`, `"sea"`, etc.) to muted map colors — a second, minimap-only owner of "what does terrain kind X look like," independent of whatever the terrain engine's own resource/biome catalogs define; an unlisted kind silently renders as fully transparent rather than falling back to `BackgroundColor` or warning.
- `BakeTerrain()` silently skips baking (leaves `_terrainTexture` null, so `ShowTerrain` draws nothing) whenever either bounds axis exceeds 1024 — no warning is raised for a map bounds that's simply too large to bake at 1px/cell.
- `WorldToApproxCell` is an explicit, named fallback used whenever `GridProjectionComponent` isn't resolved — it floors raw world coordinates as if 1 world unit == 1 cell, which will misplace unit/camera dots on any grid whose cell size isn't 1.
- `ConnectSignals()`'s own comment calls it "idempotent, like the TileMapLayer bridge" — it re-diffs each of the four sources against its last-connected reference every call, so re-resolving the same node repeatedly is safe and cheap.
