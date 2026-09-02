# TerrainStartPositionStage

Generation stage: one step in the terrain-generation pipeline, run by `TerrainFieldBuilder`, after biome/relief/water data exists.

Chooses fair player start-tile candidates on the generated map. Each land cell not on a mountain/snow/ice/rock tile is scored by what a first city built there could actually work — food from nearby terrain, production from relief, whether fresh water (river/lake) or sea access is reachable within a small radius — then candidates are taken greedily by score subject to a minimum separation distance, spreading across continents first (one start per continent) before allowing a second start on any continent.

## Public API

- `static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the sole entry point. Clamps `settings.StartPositionCount` to `[0, 24]`; returns immediately (no positions added) if the clamped count is 0. Builds a candidate list of all cells that are not water, not `TerrainRelief.Mountains`, and whose `CellTerrain` is not `"snow"`, `"ice"`, or `"rock"`; scores each via `Score(...)`; sorts candidates by descending score; computes a minimum separation (`max(4, min(cellsWide, cellsHigh) / max(2, wanted) * 1.6)`); then runs `Take` twice — once restricted to one pick per continent, once unrestricted — appending accepted cells to `world.StartPositions` until `wanted` is reached or candidates run out.

## Dependencies

- Reads `TerrainWorld.CellsWide`, `TerrainWorld.CellsHigh`, `TerrainWorld.CellIndex(x,y)`, `TerrainWorld.CellWater`, `TerrainWorld.CellRelief`, `TerrainWorld.CellTerrain`, `TerrainWorld.CellContinent`.
- Writes `TerrainWorld.StartPositions` (appends `Vector2I` cell coordinates; does not clear it first, so a caller invoking `Apply` twice on the same `world` would accumulate positions rather than replace them).
- Reads `TerrainGenerationSettings.StartPositionCount`.
- References `WaterBody` and `TerrainRelief` enums (defined in `TerrainWorld.cs` / `ResourceDefinition.cs` per the codebase, not in this file).
- Called by `TerrainFieldBuilder.Apply` as a late pipeline step, after continent/biome/relief/water stages have populated `CellWater`, `CellRelief`, `CellTerrain`, `CellContinent`.

## Notes

- Private helper `Take(...)` mutates its `usedContinents` `HashSet<int>` speculatively: it calls `usedContinents.Add(on)` before checking separation, and only rolls back with `usedContinents.Remove(on)` if the candidate turns out too close to an existing start **and** `oncePerContinent` is true. This is correct as written (the add-then-maybe-remove is intentional, since `HashSet.Add` also serves as the "already used" test), but it is a non-obvious control-flow pattern worth a comment if touched again.
- `Score`'s terrain-food and relief-production tables are a second, independent set of terrain/relief weights alongside whatever a biome or economy stage elsewhere in the pipeline uses for actual gameplay yields (not verified against this batch, since those stages weren't read) — if a real production/food system exists elsewhere with its own per-terrain weights, this is a duplicate judgment of the same facts for a different purpose (start-site fairness vs. actual yield), which is fine if deliberate but worth checking is not literally the same table copy-pasted.
- No feedback or logging if `wanted` positions cannot all be placed (e.g. a very small or very water-heavy map) — `Apply` simply exhausts candidates and returns with fewer than `wanted` entries in `StartPositions`, silently.
- `minimumSeparation` uses `Mathf.Min(wide, high)` in cell units but the map could be non-square; no correction for aspect ratio beyond that.
