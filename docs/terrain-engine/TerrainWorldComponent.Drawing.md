# TerrainWorldComponent.Drawing

Renderer: the half of `TerrainWorldComponent` (a `partial class`) responsible for turning a generated world into visible layers, plus the screen-space geometry queries (`StartPositionView`, `PreviewExtent`) other components read.

This file has no state of its own — every method operates on private fields declared in `TerrainWorldComponent.cs` (the renderer node references, `Projection`, `Seed`, `MapSize`, `BuiltSize`, `_generator`). It cannot be understood or documented in isolation from that file.

## Public API

- `Vector2 StartPositionView()` — `StartPositionViewAt(0)`.
- `Vector2 StartPositionViewAt(int index)` — resolves the generator, takes `GetStartPositions()[index]`, and returns that cell's on-screen point: `_iso.SurfacePosition(cell)` under `Isometric`, `_isometricAutotile.CellPosition(cell)` under `IsometricAutotile`, otherwise the cell centre of the active flat view's logical layer (`MapToLocal`). Index k is start k of the generator's order, the same k `GridStartAreaComponent` and the cells' `terrain_start_area` use. A map with no starts answers the middle of the map (`BuiltSize / 2`). An index past the last start (or negative) on a map that has starts pushes a warning and answers the middle of the map too.
- `Vector2 StartPositionGlobal()` — `StartPositionGlobalAt(0)`.
- `Vector2 StartPositionGlobalAt(int index)` — `StartPositionViewAt(index)` in global coordinates, for a camera. `TerrainWorldCameraComponent.FrameStartPosition` calls it with its start-area component's `ActiveStartIndex`.
- `Rect2 PreviewExtent()` — resolves, then returns the world's bounding rectangle in renderer-node coordinates: `_isometricAutotile.GridExtent(size)` under `IsometricAutotile`, `_iso.SurfaceExtent` under `Isometric`, otherwise `size * tileSize` from the active flat renderer. Falls back to `TerrainMapSetup.BoundsFor(MapSize)` when `BuiltSize` is still zero.
- `Draw(Vector2I size, ...)` *(private)* — the per-projection dispatch, called after a build, a restore or a `Redraw()`. It rebuilds `_dataLayers` for every projection (cell data is what a game reads, so it cannot depend on the view). Which renderer is shown and rebuilt under each projection:

  | Renderer | Painted | Tiles | Isometric | IsometricAutotile |
  |---|---|---|---|---|
  | `_painted` | ✓ | | | |
  | `_tiles` | | ✓ | | |
  | `_iso` + `_isometricFeatures` | | | ✓ | |
  | `_isometricAutotile` | | | | ✓ |
  | `_features` (vegetation) | ✓ | ✓ | | ✓ |
  | `_relief` (rocks) | ✓ | ✓ | | ✓ |
  | `_resources` (icons) | ✓ | ✓ | | ✓ |
  | `_overlay` (starts, start areas, survey) | ✓ | ✓ | | ✓ |

  The four companions follow one fact, `gridBound = Projection is not Isometric`. `BindGameplayGrid` binds the grid to a native layer under Painted, Tiles and IsometricAutotile (the autotile view's diamond `GetTerrainLayer()`), and each companion places and sizes every stamp through the grid's `CellToWorld`/`CellCorners`. So they stand on square cells and diamonds alike. The block view binds its elevated surface instead and draws its vegetation with `_isometricFeatures`. Until VIEW-01 (2026-09-15) the companions were gated on `flat`, which also excluded IsometricAutotile and left that view a bare map. Guarded by `tests/examples/stack_order.gd`: the IsometricAutotile row, and every tree's anchor on a wooded diamond.
  `Draw` also pushes the world's three look resources onto the renderers before anything is rebuilt: `PropSizing` to the flat feature, flat relief and isometric feature renderers; `MapArt` to the painted view always and to the flat companions only under `Painted`; and, since VIEW-04 (2026-09-16), `WaterLook` to all four ground renderers. One sea for the world, pushed exactly as `PropSizing` is — which is also why a scene assigns the look on the world and never on a renderer: a renderer-level assignment is overwritten here on the next build.
  Every renderer field gets an explicit `.Visible = ...` assignment, even the ones staying on. Per the method's own header comment this is deliberate: a renderer left out of this method once stayed hidden by accident (default `Node2D` z-order put it behind the isometric sea), until an unrelated change exposed the bug as flat trees floating on open ocean.

- `EmitSpawnMarkers(GridProjectionComponent? grid)` *(private, FEAT-12)* — publishes this world's starts as scene nodes under `TerrainWorldComponent.SpawnsPath`, by handing `_generator.ResolveField().StartAreas`, the grid and `_generator.BoundsOrigin` to `TerrainSpawnMarkers.Emit`. `Draw` calls it **after** `BindGameplayGrid`, because a marker's position comes from the grid's own geometry, and only when `SpawnsPath` is non-empty. It rewrites the markers on every build and redraw: the starts belong to the world that was last built, so a map redrawn after a new world must not keep the previous one's markers. Two refusals, each with a warning and neither of them silent: a `SpawnsPath` that does not resolve to a `Node2D` writes nothing, and a missing generator or unbound grid writes nothing and — deliberately — clears nothing, rather than wiping an authored map's own markers.

## Dependencies

Reads/writes, through the private fields it shares with `TerrainWorldComponent.cs`:
- `TerrainDataLayersComponent` — `BoundsSize`, `TileSize`, `Rebuild()` (`TerrainDataLayersComponent.cs`)
- `TerrainPaintedRendererComponent` — `BoundsSize`, `TileSize`, `MapArt`, `WaterLook`, `Rebuild()` (`TerrainPaintedRendererComponent.cs`)
- `TerrainFeatureRendererComponent` — `BoundsSize`, `Seed`, `Visible`, `Rebuild()` (`TerrainFeatureRendererComponent.cs`)
- `TerrainTileRendererComponent` — `BoundsSize`, `Visible`, `WaterLook`, `Rebuild()` (`TerrainTileRendererComponent.cs`)
- `TerrainIsometricRendererComponent` — `BoundsSize`, `Visible`, `WaterLook`, `Rebuild()`, `CellSize`, `LevelHeight`, `SurfacePosition(cell)` (`TerrainIsometricRendererComponent.cs`)
- `TerrainIsometricAutotileRendererComponent` — `BoundsSize`, `Visible`, `WaterLook`, `Rebuild()` (`TerrainIsometricAutotileRendererComponent.cs`)
- `TerrainIsometricFeatureRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainIsometricFeatureRendererComponent.cs`)
- `TerrainReliefRendererComponent` — `BoundsSize`, `Seed`, `Visible`, `Rebuild()` (`TerrainReliefRendererComponent.cs`)
- `TerrainResourceRendererComponent` — `BoundsSize`, `Visible`, `Rebuild()` (`TerrainResourceRendererComponent.cs`)
- `TerrainMapOverlayComponent` — `BoundsSize`, `TileSize`, `Visible`, `Refresh()` (`TerrainMapOverlayComponent.cs`)
- `TerrainGeneratorComponent.GetStartPositions()`, `.ResolveField().StartAreas`, `.BoundsOrigin` (`TerrainGeneratorComponent.cs`)
- `TerrainSpawnMarkers.Emit(spawnsRoot, starts, grid, boundsOrigin)` (`TerrainSpawnMarkers.cs`)
- `TerrainMapSetup.BoundsFor(MapSize)` (`TerrainMapSetup.cs`)

Within this batch: entirely dependent on `TerrainWorldComponent.cs` for its fields (`_dataLayers`, `_painted`, `_paintedNode`, `_features`, `_tiles`, `_iso`, `_isometricAutotile`, `_isometricFeatures`, `_relief`, `_resources`, `_overlay`, `_overlayNode`, `Projection`, `Seed`, `MapSize`, `BuiltSize`, `_generator`).

## Notes

- `Draw()` owns no tile size (VIEW-02, 2026-09-15). It used to derive `flatTileSize` from the active flat renderer and copy it into the data layers' `TileSize` and the four companions' `TileSize` exports: five copies kept in step with the surface. Now each surface owns its geometry and the grid reports it. The companions place and size through the grid, so their `TileSize` export is only the no-grid value. The data layers' `TileSize` is their own metadata atlas size. `PreviewExtent`/`StartPositionView` read the active flat view's logical layer (`GetTerrainLayer()`, the same layer the grid binds), and return an empty rect or zero when the projection's renderer is not wired. Previously they used the painted size, or 64, for a view that was not on screen. Every shipped world that wires companions also wires a grid; `terrain_splat_demo.tscn` gained its `World/Grid` for this.
- Spawn markers are the one thing `Draw` writes that outlives the generator (FEAT-12). The design left emission to BGB-13's native publisher, which does not exist yet, so `SpawnsPath` is the emitter's caller today and BGB-13 will call the same helper. Guarded by `tests/terrain_spawn_markers_probe.gd`: a generated world writes one marker per start carrying its `start_index` and `hq_footprint`, a rebuild republishes them, and `GridStartAreaComponent` reads the same cells back with no generator and no data layers wired. The probe's `Spawns` node sits at a deliberate offset from the map root, so a marker written in the wrong space cannot land on the right cell by accident.
