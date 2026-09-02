# TerrainWorldComponent.Drawing

Renderer: the half of `TerrainWorldComponent` (a `partial class`) responsible for turning a generated world into visible layers, plus the screen-space geometry queries (`StartPositionView`, `PreviewExtent`) other components read.

This file has no state of its own — every method operates on private fields declared in `TerrainWorldComponent.cs` (the renderer node references, `Projection`, `Seed`, `MapSize`, `BuiltSize`, `_generator`). It cannot be understood or documented in isolation from that file.

## Public API

- `Vector2 StartPositionView()` — resolves the generator, takes its first `GetStartPositions()` entry (or the map centre if none exist), and returns that cell's on-screen point: `_iso.SurfacePosition(cell)` when `Projection == Isometric`, otherwise the tile centre in painted-tile pixels (`_painted?.TileSize ?? 64`).
- `Rect2 PreviewExtent()` — resolves, then returns the world's bounding rectangle in renderer-node coordinates. For `Isometric`, a diamond-shaped rect derived from `_iso.CellSize`/`_iso.LevelHeight` (extending left of the origin, to account for the diamond's shape). For every other projection, `size * tileSize` anchored at the origin (`_painted?.TileSize ?? 64`). Falls back to `TerrainMapSetup.BoundsFor(MapSize)` when `BuiltSize` is still zero, i.e. before `Build()` has ever run.
- `Draw(Vector2I size)` *(private)* — the per-projection dispatch, called at the end of `Build()`:
  - rebuilds `_dataLayers` unconditionally, for every projection (cell data is what a game reads, so it can't depend on which view is on screen);
  - shows/rebuilds `_painted` only when `Projection == Painted`;
  - shows/rebuilds `_features` (top-down vegetation) for every *flat* projection (`Projection` is neither `Isometric` nor `IsometricAutotile`);
  - shows/rebuilds `_tiles` only for `Tiles`;
  - shows/rebuilds `_iso` and `_isometricFeatures` only for `Isometric`;
  - shows/rebuilds `_isometricAutotile` only for `IsometricAutotile`;
  - shows/rebuilds `_relief` and `_resources` for every flat projection;
  - shows/refreshes `_overlayNode`/`_overlay` for every flat projection only.
  Every renderer field gets an explicit `.Visible = ...` assignment, even the ones staying on — per the method's own header comment, this is deliberate: a renderer left out of this method previously stayed hidden by accident (default `Node2D` z-order put it behind the isometric sea) until an unrelated change exposed the bug as flat trees floating on open ocean.

## Dependencies

Reads/writes, through the private fields it shares with `TerrainWorldComponent.cs`:
- `TerrainDataLayersComponent` — `BoundsSize`, `TileSize`, `Rebuild()` (`TerrainDataLayersComponent.cs`)
- `TerrainPaintedRendererComponent` — `BoundsSize`, `TileSize`, `Rebuild()` (`TerrainPaintedRendererComponent.cs`)
- `TerrainFeatureRendererComponent` — `BoundsSize`, `Seed`, `Visible`, `Rebuild()` (`TerrainFeatureRendererComponent.cs`)
- `TerrainTileRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainTileRendererComponent.cs`)
- `TerrainIsometricRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()`, `CellSize`, `LevelHeight`, `SurfacePosition(cell)` (`TerrainIsometricRendererComponent.cs`)
- `TerrainIsometricAutotileRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainIsometricAutotileRendererComponent.cs`)
- `TerrainIsometricFeatureRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainIsometricFeatureRendererComponent.cs`)
- `TerrainReliefRendererComponent` — `BoundsSize`, `Seed`, `Visible`, `Rebuild()` (`TerrainReliefRendererComponent.cs`)
- `TerrainResourceRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainResourceRendererComponent.cs`)
- `TerrainMapOverlayComponent` — `BoundsSize`, `TileSize`, `Visible`, `Refresh()` (`TerrainMapOverlayComponent.cs`)
- `TerrainGeneratorComponent.GetStartPositions()` (`TerrainGeneratorComponent.cs`)
- `TerrainMapSetup.BoundsFor(MapSize)` (`TerrainMapSetup.cs`)

Within this batch: entirely dependent on `TerrainWorldComponent.cs` for its fields (`_dataLayers`, `_painted`, `_paintedNode`, `_features`, `_tiles`, `_iso`, `_isometricAutotile`, `_isometricFeatures`, `_relief`, `_resources`, `_overlay`, `_overlayNode`, `Projection`, `Seed`, `MapSize`, `BuiltSize`, `_generator`).

## Notes

- `_dataLayers.TileSize` and `_overlay.TileSize` are each only assigned `if (_painted is not null)` — if no painted renderer is wired at all (e.g. a scene that only uses Tiles/Isometric), the data-layer and overlay tile sizes keep whatever they last had rather than being driven by the active projection.
- `StartPositionView()` and `PreviewExtent()` both special-case only `Projection == Isometric`, not `IsometricAutotile`, even though the `TerrainProjection` enum's own doc comment says `IsometricAutotile` is "Same projection as Isometric, different art" — a scene using the autotile projection falls through to the flat/painted-tile-size math in both methods, which reads as an oversight (the camera would frame/focus it like a square grid, not the isometric diamond it actually renders as). Flagged as a likely gap, not independently verified at runtime.
