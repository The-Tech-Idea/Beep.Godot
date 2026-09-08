# TerrainContinentStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after tile reduction and `TerrainScaleConstraintStage.ApplyTerrain`, before resources and start positions. Cleanup may drain lakes or short rivers and reconnect dry land, so labelling earlier produces stale IDs, including zero IDs on drained land.

`TerrainContinentStage` is a straightforward connected-components flood fill over the tile grid: every group of land tiles connected by tile-adjacency (not sample-adjacency) gets its own positive integer id in `TerrainGenerationBuffer.CellContinent`, so gameplay code (start-position placement, spreading players across separate landmasses) can ask "is this the same landmass as that" in O(1) without re-deriving reachability itself. It deliberately runs on the reduced tile grid rather than the finer sample field the rest of generation works in, because "can a unit walk there" is a tile-resolution question — two shores a fraction of a sample apart but a full tile of water apart are not the same continent to a unit.

## Public API

- `internal static void Apply(TerrainGenerationBuffer world)` — the only member in the class. For every unlabelled land tile, starts a new id (`nextId`, beginning at 1) and BFS-floods it across 4-connected land neighbours, writing the id into `world.CellContinent`. Idempotent to call once; calling it again after ids are already assigned is a no-op (every cell already has `CellContinent != 0`).

## Dependencies

- Reads and writes `TerrainGenerationBuffer.CellContinent` (from `TerrainGenerationBuffer.cs`) — the output array this stage exists to fill.
- Reads `TerrainGenerationBuffer.CellWater` (from `TerrainGenerationBuffer.cs`, `WaterBody` enum) — a cell is only eligible to seed or join a continent when `CellWater == WaterBody.None`.
- Reads `TerrainGenerationBuffer.CellsWide`, `TerrainGenerationBuffer.CellsHigh` (from `TerrainGenerationBuffer.cs`) to size the tile grid it iterates.
- Calls `TerrainGeometry.Neighbours(x, y, width, height)` (from `TerrainGeometry.cs`) to enumerate the (up to 4) orthogonal tile neighbours for the flood fill.
- Called by `TerrainFieldBuilder.Build` (outside this batch); must run after whatever stage populates `TerrainGenerationBuffer.CellWater` per tile, since land/water classification is this stage's only input besides the grid dimensions.

## Notes

- IDs describe cardinally connected dry-land cells, not full navigation reachability. Wading, bridges, height transitions and dynamic blockers belong to grid navigation. Call this stage once on a fresh generation buffer after all water edits; it is not a relabelling API for edited buffers.
- `tests/terrain_final_topology_probe.gd` checks every label and the diagnostic count against an independent flood-fill of the final generated water grid across four seeds, with scale rules on and off. River/lake drain fixtures separately check tile/sample consistency.

- No `[Export]`ed fields, no settings dependency, no tunable parameters at all — behaviour is fully determined by the water/land tile grid it's handed.
- The class and its one method are both `internal`; nothing here is part of any public/editor-facing surface.
- No dead code, no silent failure path — a map with no land simply produces `nextId == 0` and every cell stays `CellContinent == 0`.
