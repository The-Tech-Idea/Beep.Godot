# TerrainReliefRendererComponent

Batched hill and mountain sprites for the flat terrain views. This component renders relief;
it does not generate terrain or change gameplay cells.

## Sources

- `CellDataPath`: authoritative live `GridCellDataComponent`. Reads `terrain_relief` through
  `LiveTerrainSurfaceData`; water cells draw no relief even when they retain old height metadata.
- `TerrainGeneratorPath`: generated-field source only when CellDataPath is empty.
- `GridPath`: optional gameplay grid. Cell anchors use its native geometry and global transforms,
  converted into this renderer's local coordinates. A missing explicit source clears the view;
  it never silently falls back to a different world.
- `BoundsOrigin` and `BoundsSize`: logical cell rectangle. Live queries use absolute cells;
  generated-field queries use local coordinates within the rectangle.
- `TileSize`: square spacing when no grid is configured. With a grid, icon fitting uses the
  smaller transformed cell-axis length, supporting rectangular flat tiles.

`TerrainWorldComponent` binds the shared live source and gameplay grid, propagates bounds and seed,
and rebuilds relief after binding the selected projection. Relief is hidden in isometric views;
those views use their own elevated geometry.

## Art And Drawing

`HillsTextures` / `MountainsTextures` are Inspector arrays of individual transparent
Texture2D resources. A populated array takes precedence over its corresponding sheet.
Each accepted stamp chooses one entry deterministically from its cell, seed and slot.

`HillsSheetPath` / `MountainsSheetPath` select equal-frame sprite sheets when the
corresponding array is empty. Columns and rows specify each sheet's layout.
Empty arrays and paths disable that relief level.
Changing a sheet path invalidates its cached texture on Rebuild.

`HillsCoverage` and `MountainsCoverage` are probabilities (0 to 1) per eligible
raised cell, not percentages of the entire map. Zero produces no stamps; one
accepts all dry eligible cells. Fine coast sampling rejects water anchors and
uses the shared bounded `TerrainFeatureScatter` placement routine.

`PropSizing`, `HillsPerTile`, `MountainsPerTile`,
`PositionJitter`, `ScaleJitter`, and `Seed` control the stamps. Deterministic variation
uses the shared `TerrainGeometry.Hash01`; relief values use the shared `TerrainRelief` enum.
These are visual settings, not terrain-generation parameters.

Stamps are sorted by local Y and drawn from one Node2D rather than creating a node per sprite.
TerrainLayers owns the Z order. Textures use linear filtering with mipmaps.

The authored `templates/scenes/terrain/terrain_rock_objects.tscn` prefab uses four
unaltered individual sprites from the user's `Art/Rocks/Objects_separately` folder:
Rock1_1, Rock1_2, Rock4_1 and Rock4_2, copied to the addon's `textures/rocks` folder.
Small sprites represent hills; large sprites represent mountains. Defaults are 35%
hill coverage and 55% mountain coverage, one object per accepted cell. The generator
lab and standalone painted demo instance this prefab. Ground cover remains beneath
the objects; warm grassland hills and mountains no longer become gravel or rock.
Visible rock dimensions follow `TerrainPropSizing`: 0.25-0.40 cells (small) and
0.45-0.65 cells (large), after jitter. Transparent frame padding is excluded.

## Lifecycle And API

Automatic refresh pauses while hidden, including callbacks queued before hiding. Showing a
previously attempted view catches up. Reattachment restores cell/grid subscriptions, and explicit
`Rebuild()` is still supported while hidden. World-managed bounds remain current while inactive.

Stamp size and jitter use the current cell's native top-face corners. They do not query adjacent
cell centers, which may be missing at the map boundary or lifted to a different elevation.

- `Rebuild()`: resolves current paths, loads sheets, reads the active source and rebuilds stamps.
- `CellPosition(Vector2I)`: returns the logical cell anchor in renderer-local coordinates.
- `StampCount`: current number of drawn relief sprites.
- `GetStampBounds()`: actual frame rectangles, in renderer-local units.
- `RefreshOnReady`: defer an initial rebuild at runtime; disable when a world controller drives it.

Cell changes invalidate nearby resident chunks when streaming; otherwise they coalesce into a
deferred rebuild, as do bulk cell reloads and grid geometry notifications. Exiting the tree
disconnects sources and retires stamps. Hidden views do not schedule edit rebuilds;
the world controller rebuilds them when selected. Standalone callers changing paths or art settings
must call Rebuild. No generation is performed when a live source is configured.

## Verification And Limits

`tests/terrain_live_relief_probe.gd` covers live flattening, flooding, save/restore, source changes,
nonzero origin, transformed rectangular-grid anchors, removed/reloaded sheets, and missing sources.
With `-- --capture`, it captures actual hill and flattened frames and checks that rendered pixels
change. Captures are written under `tests/output/live_relief/`.

Runtime maps above 65,536 cells stream through the shared `TerrainPropResidency` scheduler.
`StreamLargeMaps` defaults to true; `ReliefChunkSize` defaults to 32,
`ReliefPreloadChunks` to 1, and `ReliefCellsPerFrame` to 256. Smaller maps and editor
rebuilds retain full-rectangle rendering. `UpdateReliefResidency()` performs a budgeted update;
resident chunk/cell counts, pending chunks and cells processed last frame are exposed for diagnostics.
Hidden and distant chunks retire without suspending gameplay simulation.

`tests/terrain_relief_streaming_probe.gd` checks sparse 1024x1024 logical bounds,
bounded cell work, full-renderer position/size parity, water exclusion, live flattening,
camera jumps, rotated-camera edge bounds and hide/show recovery.
This does not qualify full million-cell terrain generation. Whole-map zoom still requests all
visible rocks; asset loading and stamp sorting are not time-budgeted. Arbitrary scene-transform
changes need a grid geometry notification or an explicit rebuild.

`tests/terrain_ground_cover_probe.gd` checks the actual lab scene's ground, preserved
relief, sparse deterministic object count, zero coverage, unchanged gameplay records
and imported/mipmapped art. Seed 31415 at Tiny yields 24 raised cells, zero gray
rock/gravel ground cells and nine rock objects.

These are decorative draws, not collision bodies, harvestable resources or buildings.
Dry anchors are checked; the full visible sprite footprint is not yet water-clipped.
Flat-view rocks are hidden in isometric modes, which still need equivalent object art.
