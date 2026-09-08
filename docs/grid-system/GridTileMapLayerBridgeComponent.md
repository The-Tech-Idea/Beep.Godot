# GridTileMapLayerBridgeComponent

Synchronizes `GridCellDataComponent` state and `GridRoadComponent` road cells into a real Godot `TileMapLayer`, mapping each cell's flags (and whether it has a road) onto an atlas coordinate on one tile source. It is the authored-art counterpart to `GridCellOverlayComponent`'s immediate-mode debug drawing — use this once a project has real tile art and wants Godot's own tile renderer to show map state instead of colored overlay polygons.

The file's comments are almost entirely about batching cost. The private `PaintCell` writes a cell via `TileMapLayer.SetCell` but deliberately does *not* call `UpdateInternals()` — that is left to the caller, so `Rebuild()` can paint every stored cell and every road cell in a loop and call `UpdateInternals()` exactly once at the end; before this, per-cell internals updates made a full-map rebuild's cost grow with the square of the map size (`UpdateInternals` once per painted cell, then again after the loop). The public single-cell path, `RefreshCell`, is the opposite: it paints and immediately calls `UpdateInternals()`, for reacting to one `CellChanged`/`RoadChanged` signal. `ResolveReferences` also re-runs `ConnectSignals()` on every call (not only once in `_Ready`), because a dependency resolved after `_Ready` — a `NodePath` assigned at runtime, or a sibling component added later — used to be painted once from the initial resolve and then silently never listened to again; `ConnectSignals` is idempotent so this costs nothing when the resolved components haven't changed.

## Public API

- `[Export] public NodePath TileMapLayerPath / CellDataPath / RoadPath` — dependency wiring.
- `[Export] public bool PaintCells` / `public bool PaintRoads` / `public bool RoadsOverrideCells` — whether roads are checked *before* cell-flag state (roads win) or only fall through to `RoadAtlas` when a cell has no flag-derived atlas of its own.
- `[Export] public bool ClearBeforeRebuild` / `public bool FillDefaultTerrainInBounds` / `public Vector2I BoundsMin` / `public Vector2I BoundsMax` — optional full-layer clear and/or a one-time default-terrain fill over a bounding rectangle before real data is painted.
- `[ExportGroup("Tile Source")] [Export] public int SourceId` / `public int AlternativeTile`.
- `[ExportGroup("Atlas Coordinates")] [Export] public Vector2I DefaultTerrainAtlas / ClearedAtlas / TilledAtlas / WateredAtlas / PlantedAtlas / HarvestReadyAtlas / BlockedAtlas / RoadAtlas` — one atlas coordinate per cell state.
- `public override void _Ready()` — resolves references, connects signals, and (outside the editor) runs an initial `Rebuild()`.
- `public override void _ExitTree()` — disconnects signals.
- `public override string[] _GetConfigurationWarnings()` — warns on missing `TileMapLayerPath`, missing `CellDataPath`/`RoadPath` when their paint flag is on, or `BoundsMax` less than `BoundsMin`.
- `public void Rebuild()` — full repaint: optional clear, optional default-terrain fill, then every stored cell and every road cell, then one `UpdateInternals()`.
- `public void RefreshCell(Vector2I cell)` — repaints and publishes one cell immediately.
- `public void EraseCell(Vector2I cell)` — clears one cell on the layer.
- `public int PaintedCellCount()` — `TileMapLayer.GetUsedCells().Count`.
- `public Vector2I AtlasForCell(Vector2I cell)` — the resolution order: road wins outright if `PaintRoads && RoadsOverrideCells` and the cell has a road; otherwise the same `Blocked > HarvestReady > Planted > Watered > Tilled > Cleared` flag priority as elsewhere; otherwise a road atlas if `PaintRoads && !RoadsOverrideCells` and the cell has a road; otherwise `DefaultTerrainAtlas`.

## Dependencies

- Resolves a Godot `TileMapLayer` directly by `NodePath` only (no scene-wide fallback, unlike its other two dependencies — there is no sensible "find any TileMapLayer" default).
- Resolves `GridCellDataComponent` (`GetCells()`, `GetFlags(Vector2I)`, `CellChanged`/`CellsChanged` signals) and `GridRoadComponent` (`GetRoadCells()`, `HasRoad(Vector2I)`, `RoadChanged`/`RoadsChanged` signals — the last two outside this batch, read here as calls only) by `NodePath` or scene-wide `EntityComponent.FindComponent`.
- Uses `GridVariantReader.Vector2I` to pull the `"cell"` key back out of each dictionary `GetCells()` returns.
- Not established from this batch alone whether anything calls into this component — none of the other six files in this batch reference it.

## Notes

- `AtlasForCell`'s flag-priority branch (`Blocked > HarvestReady > Planted > Watered > Tilled > Cleared`) duplicates `GridCellOverlayComponent.ColorForFlags`'s priority table exactly, just mapped to `Vector2I` atlas coordinates instead of `Color` — the same "which state wins" decision implemented independently in both renderers read in this batch.
- `Rebuild()`'s early-out (`_tileMapLayer == null || _tileMapLayer.TileSet == null`) is silent — no warning, no exception — beyond the one-time `_GetConfigurationWarnings` check for an empty `TileMapLayerPath`; a `TileMapLayer` resolved but missing a `TileSet` at runtime produces no diagnostic at all when `Rebuild()`/`RefreshCell` are called against it.
