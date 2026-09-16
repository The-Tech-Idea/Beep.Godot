# TerrainStartPositionStage

Generation stage: one step in the terrain-generation pipeline, run by `TerrainFieldBuilder`, after biome/relief/water data exists.

Chooses fair player start-tile candidates on the generated map. Each eligible cell (see "Eligibility and start areas" below) is scored by what a first city built there could actually work — food from nearby terrain, production from relief, whether fresh water (river/lake) or sea access is reachable within a small radius — then candidates are taken greedily by score subject to a minimum separation distance, spreading across continents first (one start per continent) before allowing a second start on any continent.

## Public API

Lava is excluded from candidates alongside snow, ice and rock. The old selector
could choose it despite the live grid blocking movement and construction there.
The final-topology probe verifies no starts on an all-lava fixture and selection
of the sole habitable grass cell when one is present. A wholly volcanic map can
therefore have no suitable starts; this stage does not invent habitable land.

- `static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings, TerrainStartKitRules kit, CancellationToken cancellation = default)` — the sole entry point. Reads the wanted count from `settings.RequestedStartPositionCount` (the `[0, 24]` clamp diagnostics report as "requested"); returns immediately (no positions added) if it is 0. Builds a candidate list of every cell that passes the private `Eligible` predicate; scores each via `Score(...)`; sorts candidates by descending score; computes a minimum separation (`max(4, min(cellsWide, cellsHigh) / max(2, wanted) * 1.6)`); then runs `Take` twice — once restricted to one pick per continent, once unrestricted — appending accepted cells to `world.StartPositions` until `wanted` is reached or candidates run out.

### Eligibility and start areas (FEAT-09)

`Eligible` walks `GridFootprint.Cells(cell, footprint)` and requires every footprint cell to be in bounds, dry (`CellWater == WaterBody.None`), not `TerrainRelief.Mountains`, `TerrainKindCatalog.Standard.Startable`, and on the same relief as the anchor cell. That is the level-ground rule `GridPlacementComponent.WhyNot` enforces as `not_level`, over the same footprint walk.

- With `settings.StartAreaRadius > 0` the footprint is `kit.HqFootprint` (default 3×3), so a start is only chosen where the kit's headquarters fits on level ground.
- With the radius at 0 the footprint is 1×1, which is the single-cell test the predicate replaced. Starts are then exactly what they were before start areas existed.

Scoring, sorting, separation and the per-continent pass are the same in both cases. This stage never reserves an area. `TerrainStartAreaStage` runs next and grows an area from each chosen start.

## Dependencies

- Reads `TerrainGenerationBuffer.CellsWide`, `TerrainGenerationBuffer.CellsHigh`, `TerrainGenerationBuffer.CellIndex(x,y)`, `TerrainGenerationBuffer.CellWater`, `TerrainGenerationBuffer.CellRelief`, `TerrainGenerationBuffer.CellTerrain`, `TerrainGenerationBuffer.CellContinent`.
- Writes `TerrainGenerationBuffer.StartPositions` (appends `Vector2I` cell coordinates; does not clear it first, so a caller invoking `Apply` twice on the same `world` would accumulate positions rather than replace them).
- Reads `TerrainGenerationSettings.RequestedStartPositionCount` and `TerrainGenerationSettings.StartAreaRadius`.
- Reads `TerrainStartKitRules.HqFootprint` (only when the radius is above 0), `GridFootprint.Cells`, `TerrainGenerationBuffer.CellInBounds` and `TerrainKindCatalog.Standard.Startable`.
- References `WaterBody` and `TerrainRelief` enums (defined in `TerrainGenerationBuffer.cs` / `ResourceDefinition.cs` per the codebase, not in this file).
- Called by `TerrainFieldBuilder.Apply` as a late pipeline step, after continent/biome/relief/water stages have populated `CellWater`, `CellRelief`, `CellTerrain`, `CellContinent`.

## Notes

- Private helper `Take(...)` mutates its `usedContinents` `HashSet<int>` speculatively: it calls `usedContinents.Add(on)` before checking separation, and only rolls back with `usedContinents.Remove(on)` if the candidate turns out too close to an existing start **and** `oncePerContinent` is true. This is correct as written (the add-then-maybe-remove is intentional, since `HashSet.Add` also serves as the "already used" test), but it is a non-obvious control-flow pattern worth a comment if touched again.
- `Score`'s terrain-food and relief-production tables are a second, independent set of terrain/relief weights alongside whatever a biome or economy stage elsewhere in the pipeline uses for actual gameplay yields (not verified against this batch, since those stages weren't read) — if a real production/food system exists elsewhere with its own per-terrain weights, this is a duplicate judgment of the same facts for a different purpose (start-site fairness vs. actual yield), which is fine if deliberate but worth checking is not literally the same table copy-pasted.
- A partial placement emits a warning if candidates existed but cannot satisfy
  the requested count and spacing. No candidates returns early without a warning;
  generation diagnostics still report requested and actual start counts.
- `minimumSeparation` uses `Mathf.Min(wide, high)` in cell units but the map could be non-square; no correction for aspect ratio beyond that.
