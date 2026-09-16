# GridMinimapComponent

Lightweight HUD overview map (a `Control`, `[Tool][GlobalClass]`) for top-down/isometric grid worlds: it custom-draws (`_Draw`) a baked terrain background, road dots, job dots, selection dots, live unit positions, and the camera's viewport rectangle, all from existing Godot nodes without requiring a `TileMap` or a dedicated minimap scene. It is architecturally the odd one out in this UI batch — every other file here builds a tree of child `Control`s (`Label`/`Button`/`PanelContainer`); this one is a single `Control` that paints everything itself in `_Draw`.

Its whole design is built around avoiding per-frame allocation from Godot's marshalled collections. Road cells, job cells, and selected cells are each cached into a plain `List<Vector2I>` snapshot, refreshed only when a `_roadsDirty`/`_jobsDirty`/`_selectionDirty` flag is set by the owning system's change signal — the file's own doc comment says the previous version rebuilt these marshalled collections every single frame and "made an idle minimap one of the most allocation-heavy nodes in a scene." The terrain background is similarly baked once into an `ImageTexture` (`_terrainDirty`) rather than redrawn per cell per frame. What *is* read live every redraw is unit positions and the camera rectangle, since those move continuously; even so, the redraw itself is throttled to `RefreshIntervalSeconds` (default 0.1s) via an accumulator in `_Process`, because an overview map does not need frame-rate responsiveness.

## Public API

- `[Export] NodePath GridPath/NavigationPath/RoadPath/SelectionPath/JobQueuePath/UnitsRootPath/CameraPath/CellDataPath` — component/node wiring; `CellDataPath` is the source for the baked terrain background and is auto-found if left empty (documented as relevant only when a scene has more than one `GridCellDataComponent`).
- `[Export] bool AutoRefresh = true` — gates the `_Process` redraw-throttle loop (also runs while `Engine.IsEditorHint()`).
- `[Export(Range 0.02..2)] float RefreshIntervalSeconds = 0.1f` — minimum seconds between redraws while `AutoRefresh` is on.
- `[Export] Vector2I BoundsOrigin/BoundsSize`, `bool PreferNavigationBounds = true` — map bounds used for cell↔minimap conversion; when `PreferNavigationBounds` and a `GridNavigationComponent` with `UseBounds` is resolved, its bounds win over the local exports.
- `[Export] bool ShowTerrain/ShowRoads/ShowSelection/ShowJobs/ShowUnits/ShowCameraView` — per-layer visibility.
- `[Export] bool ShowStartAreas` (FEAT-10, **default off**) — tint the baked terrain where the map reserves ground for a start, in the colour of the faction that start was assigned. Off by default because a map without a faction catalog has no colour to tint with, and a sandbox showing one player's own reservation as a coloured patch is noise. The tint only runs when all three of `ShowStartAreas`, a resolved `StartAreaPath` and `GridCellDataComponent.HasStartAreas` are true.
- `[Export] NodePath StartAreaPath` (FEAT-10) — the `GridStartAreaComponent` whose assignment says which faction holds which start. Resolved with `fallbackWhenEmpty: false`, unlike the other component wires: the tint is something this minimap opts into, and finding a start component in the scene would colour a map nobody asked to colour.
- `[Export(Range 0,1,0.01)] float StartAreaTint` (FEAT-10, default `0.2`) — how far a start area's texel moves toward its faction's colour, through the shared `GridFactionCatalog.TintToward`. A tint, not a fill: the terrain underneath must still read, or the minimap stops being a map of the ground.
- `[Export] Color BackgroundColor/BorderColor/RoadColor/SelectionColor/JobColor/UnitColor/CameraColor` — per-layer draw colors.
- `public Vector2I EffectiveBoundsOrigin()` / `public Vector2I EffectiveBoundsSize()` — resolved bounds after the `PreferNavigationBounds` override and a minimum-1-per-axis clamp.
- `public override void _Ready()` — resolves references, sets `_Process` per `AutoRefresh`/editor-hint, queues an initial redraw.
- `public override void _ExitTree()` — disconnects all five signal sources (roads, jobs, selection, cell data, and the start area's `AssignmentChanged`).
- `public int TerrainScale` — the terrain bake's downsample factor: 1 = one texel per cell, 2 = one per 2×2 block, and so on. Readable so a HUD (or a guard) can see the scale the bake chose.
- `internal int TerrainTextureWidth` / `internal Color BakedTexel(int x, int y)` — test hooks: the baked texture's width in texels (0 when nothing is baked) and one baked texel's colour (transparent when nothing is baked). `tests/GridMinimapSmoke.cs` reads both.
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
- Reads `GridCellDataComponent.GetTerrainKind(Vector2I)` and its `CellChanged`/`CellsChanged` signals (outside this batch) to bake the terrain texture, plus `GetStartArea(Vector2I)` and `HasStartAreas` when the start-area tint is on.
- Reads `GridStartAreaComponent.ColourOfStart(int)` and its `AssignmentChanged` signal, and calls the static `GridFactionCatalog.TintToward`. It never resolves or reads a `GridFactionCatalog` itself — a contract pin refuses the bare call `ColourOf(` in this file, because only the start component knows which faction holds a start.
- Calls `TerrainGeometry.MostCommon` twice per baked texel: once over terrain kinds, once over start-area ids.
- Reads `GridProjectionComponent.WorldToCell(Vector2)` (outside this batch) to place units/camera on the map, falling back to a raw floor-based approximation when unresolved.
- Reads `GridNavigationComponent.UseBounds/.BoundsOrigin/.BoundsSize` (outside this batch) when `PreferNavigationBounds` is set.
- Uses `EntityComponent.FindComponent<T>(...)` as the scene-fallback lookup for grid/navigation/road/selection/jobs/cell-data, same pattern as every other file in this batch; `UnitsRootPath` and `CameraPath` have no such fallback (units root must be path-set; camera falls back to `GetViewport()?.GetCamera2D()`).
- Not established from this batch alone whether anything calls into `GridMinimapComponent` — none of the other 12 UI files reference it.

## Notes

- `TerrainColors` is a local, hardcoded `Dictionary<string, Color>` mapping ~20 terrain-kind strings (`"grass"`, `"desert"`, `"sea"`, etc.) to muted map colors — a second, minimap-only owner of "what does terrain kind X look like," independent of whatever the terrain engine's own resource/biome catalogs define; an unlisted kind silently renders as fully transparent rather than falling back to `BackgroundColor` or warning.
- `BakeTerrain()` no longer skips a large map (ENH-13). It picks the coarsest power-of-two scale that fits the texture under 1024 texels per axis, prints the chosen scale once (`_reportedDownsample`), and bakes one texel per s×s block. Below 1024 the scale is 1 and the bake is one texel per cell, unchanged.
- **The tint is majority-voted per block, exactly like the terrain kind** (FEAT-10). `BlockColour` counts terrain kinds *and* start-area ids over the same s×s block and settles both with `TerrainGeometry.MostCommon`, so a downsampled block cannot pick its terrain by majority and its owner by some other rule. The area vote includes id 0 — unreserved — so a mostly-open block reads as unreserved and is left alone. A block whose majority *is* reserved converts back (`reserved - 1` is the start index) and asks `GridStartAreaComponent.ColourOfStart`, which answers transparent for a start nobody holds; `TintToward` then returns the ground untouched rather than inventing faction 1's colour. Being the same vote is why `TerrainGeometry.MostCommon` became generic.
- The tint changes hue, not opacity: `TintToward` lerps toward `new Color(faction, source.A)`, so a reserved texel and an unreserved one are equally opaque.
- `AssignmentChanged` marks the **terrain** snapshot dirty, not a new one. The colours are baked into the texture, so a start changing hands has to re-bake; the bake otherwise runs only on cell changes, and a redraw alone would show the old owner's colour. Connecting or disconnecting the start-area source also marks it dirty, so switching `StartAreaPath` repaints.
- `WorldToApproxCell` is an explicit, named fallback used whenever `GridProjectionComponent` isn't resolved — it floors raw world coordinates as if 1 world unit == 1 cell, which will misplace unit/camera dots on any grid whose cell size isn't 1.
- `ConnectSignals()`'s own comment calls it "idempotent, like the TileMapLayer bridge" — it re-diffs each of the five sources against its last-connected reference every call, so re-resolving the same node repeatedly is safe and cheap.
- The `StartAreaTint` section of `tests/GridMinimapSmoke.cs` (run by `tests/grid_minimap_probe.ps1`) is the guard: a 6×6 block of cells is marked `terrain_start_area = 2` (start 1), `alpha` is catalog index **1** and is assigned start **1** on purpose — so catalog order and the assignment disagree, and a tint that read the catalog by start index would paint `bravo`'s blue. It then asserts the reserved texel equals the plain ground tinted 20 % toward `alpha`, that the tint actually changed the texel (an assertion that the check can fail at all), that unreserved ground at (20,20) is byte-identical with the tint on and off, and that after `AutoAssign()` — which leaves that start unheld in this fixture — the texel is back to plain.
- `tests/addon_contract_scan.ps1` requires this file to contain `ColourOfStart(` and refuses it containing the bare `ColourOf(`. The bare spelling is deliberate: the pin was first written as `FactionCatalog.ColourOf(` and a null-forgiving `FactionCatalog!.ColourOf(` slid straight past it.
