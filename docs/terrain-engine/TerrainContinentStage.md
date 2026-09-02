# TerrainContinentStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after the tile-grid water bodies (`TerrainWorld.CellWater`) have been decided.

`TerrainContinentStage` is a straightforward connected-components flood fill over the tile grid: every group of land tiles connected by tile-adjacency (not sample-adjacency) gets its own positive integer id in `TerrainWorld.CellContinent`, so gameplay code (start-position placement, spreading players across separate landmasses) can ask "is this the same landmass as that" in O(1) without re-deriving reachability itself. It deliberately runs on the reduced tile grid rather than the finer sample field the rest of generation works in, because "can a unit walk there" is a tile-resolution question — two shores a fraction of a sample apart but a full tile of water apart are not the same continent to a unit.

## Public API

- `internal static void Apply(TerrainWorld world)` — the only member in the class. For every unlabelled land tile, starts a new id (`nextId`, beginning at 1) and BFS-floods it across 4-connected land neighbours, writing the id into `world.CellContinent`. Idempotent to call once; calling it again after ids are already assigned is a no-op (every cell already has `CellContinent != 0`).

## Dependencies

- Reads and writes `TerrainWorld.CellContinent` (from `TerrainWorld.cs`) — the output array this stage exists to fill.
- Reads `TerrainWorld.CellWater` (from `TerrainWorld.cs`, `WaterBody` enum) — a cell is only eligible to seed or join a continent when `CellWater == WaterBody.None`.
- Reads `TerrainWorld.CellsWide`, `TerrainWorld.CellsHigh` (from `TerrainWorld.cs`) to size the tile grid it iterates.
- Calls `TerrainGeometry.Neighbours(x, y, width, height)` (from `TerrainGeometry.cs`) to enumerate the (up to 4) orthogonal tile neighbours for the flood fill.
- Called by `TerrainFieldBuilder.Build` (outside this batch); must run after whatever stage populates `TerrainWorld.CellWater` per tile, since land/water classification is this stage's only input besides the grid dimensions.

## Notes

- No `[Export]`ed fields, no settings dependency, no tunable parameters at all — behaviour is fully determined by the water/land tile grid it's handed.
- The class and its one method are both `internal`; nothing here is part of any public/editor-facing surface.
- No dead code, no silent failure path — a map with no land simply produces `nextId == 0` and every cell stays `CellContinent == 0`.
