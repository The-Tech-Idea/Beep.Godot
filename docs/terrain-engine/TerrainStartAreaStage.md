# TerrainStartAreaStage

Generation stage: the last step in the terrain-generation pipeline, run by `TerrainFieldBuilder` as `"Start areas"` straight after `TerrainStartPositionStage` (FEAT-09).

`TerrainStartPositionStage` chooses start tiles; a playable start is an area. This stage reserves an area around every chosen start, validates it, places the scenario's start kit in it, and writes one `TerrainStartAreaReport` per start. It follows how the genre does it: Ensemble's random-map patent gives each player one contiguous area with a minimum distance between areas and water excluded, then places a per-player object kit in a nested loop per area and per object type, relaxing a critical object's constraints in a fixed order; Age of Empires II grows every player land from its origin at once until they meet; 0 A.D. gives every base the same kit at authored distances. It never re-scores or moves a start. Unusable starts stay where they are and are reported with the reason.

## Public API

- `static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings, TerrainStartKitRules kit, TerrainResourceRules resources, CancellationToken cancellation = default)` — the sole entry point. Returns immediately, without allocating, when `settings.StartAreaRadius` is 0 or there are no start positions, so a map without start areas is built exactly as before.

Everything else (`Claimable`, `TooCloseToOtherArea`, `CountExits`, `PlaceEntry`, `Overwritable`, `SpacedFromSame`, `StampDeposit` and the constants `KitHashSalt`, `KitSpacing` = 4, `UndergroundDiscRadius` = 2) is private to the `internal static class TerrainStartAreaStage`.

### What Apply does

1. **Growth.** One queue is seeded with every start's headquarters footprint (`GridFootprint.Cells(origin, kit.HqFootprint)`) in start order, and every area grows from it at once over 4-neighbours. A contested cell goes to whichever area reached it first. A cell is claimed when it is unclaimed, dry, not `TerrainRelief.Mountains`, not `TerrainKindCatalog.Standard.BlockedByDefault`, within `radius²` of its own origin, and no other start's area holds a cell within `kit.AreaGap` (Chebyshev) of it. A footprint cell that is already claimed or too close to another area is skipped and the start gets the problem `footprint_overlap`. Every claimed cell is 4-connected to its own footprint by construction. The result is written to `world.CellStartArea` (0 none, k+1 start k).
2. **Exits and size.** Exits are area cells outside the footprint that are 4-adjacent to one of its sides (corners do not count). Fewer than `kit.ExitCount` gives `no_exit`. An area with fewer cells than `kit.MinAreaCells` (or, when that is 0, `ceil(0.6 × π × radius²)`) gives `area_too_small`.
3. **Kit, per start and per entry, in authored order.** The entry's resource is looked up with `resources.Find`. Not found gives `unknown_resource:<id>`; a Liquid resource gives `kit_entry_unplaceable:<id>`. Candidates are the area's cells outside the footprint whose terrain and relief the resource `Supports`, ordered by `TerrainGeometry.Hash01` of the cell, the seed and the entry index.
   - **Surface** entries write `world.Resource`. Relaxation levels, tried in order until `Count` is placed: 0 as authored (inside the `[MinDistance, MaxDistance]` band, on an empty cell, no same resource within 4 cells); 1 the band widened to the whole area; 2 the same-resource spacing dropped; 3 an existing Bonus-category resource that is not itself a kit placement may be overwritten. Supported ground, dryness and relief are never relaxed.
   - **Underground** entries are satisfied, with no placement recorded, if any area cell already holds that underground id. Otherwise each placement stamps a radius-2 deposit (richness 0.5, depth from the definition) on empty underground cells of this start's area, at level 0 inside the band, then level 1 anywhere in the area.
   - Still short: `missing_critical:<id>` for a critical entry, `missing:<id>` otherwise. Each placement records its relaxation level.
4. **Reports.** Appends one `TerrainStartAreaReport` per start to `world.StartAreas`, then pushes one `GD.PushWarning` listing every unusable start with its problems.

## Dependencies

- Reads `TerrainGenerationSettings.StartAreaRadius` and `.Seed`.
- Reads `TerrainGenerationBuffer.StartPositions`, `CellsWide`, `CellsHigh`, `CellIndex`, `CellWater`, `CellRelief`, `CellTerrain`, `IntScratchA` (the growth queue) and `CellUndergroundResource`.
- Writes `TerrainGenerationBuffer.CellStartArea`, `Resource`, `CellUndergroundResource`, `CellUndergroundRichness`, `CellUndergroundDepth` and `StartAreas`.
- Reads `TerrainStartKitRules` (footprint, exits, gap, minimum size, entries), `TerrainResourceRules.Find` and `TerrainResourceRules.Entry` (`Id`, `Stratum`, `Category`, `Depth`, `Supports`), `GridFootprint.Cells`, `TerrainGeometry.Neighbours4` and `Hash01`, and `TerrainKindCatalog.Standard.BlockedByDefault`.
- Called by `TerrainFieldBuilder.BuildPrepared` after `TerrainStartPositionStage`. Its output reaches consumers through `GeneratedTerrainField.StartAreaAtCell` and `StartAreas`: `TerrainGeneratorComponent.StartAreaAt`/`GetStartAreaReports`, the generated-cell handoff (`terrain_start_area` on `GridCellDataComponent`), `TerrainDataLayersComponent.StartAreaAt` and `TerrainMapOverlayComponent.ShowStartAreas`.

## Notes

- The Bonus test for relaxation level 3 reads the captured `TerrainResourceRules.Entry.Category` of the map's own catalog, not `TerrainResourceStage.CategoryOf`, which searches every shipped catalog and could answer for an id this map's catalog does not hold.
- Growth and distances use integer cell arithmetic, area cells are listed in scan order, and kit candidates are ordered by a seeded hash with the cell index as tie-break, so a seed and a kit produce the same areas and placements on every run. `tests/TerrainGenerationBaselineSmoke.cs` hashes the `start_area` and `start_reports` layers; its `SnapshotWithStartAreas` (radius 8, default kit plus two critical wheat at 2–6 cells and one stone at 3–8) backs the two `*_start_areas` baseline cases. Without start areas both layers hash an all-zero area and no reports, and every earlier layer of the existing cases is unchanged.
- `tests/terrain_start_area_probe.gd` checks, on a small Continents map with seed 31415 and radius 10, that every area cell is dry, not mountainous and 4-connected to its start, that no two areas are 8-adjacent, that each headquarters footprint is level and inside its area, that report counts match the map, that usable starts meet the exit count and hold a critical wheat entry placed on a one-cell band, that relaxation was used, and that the diagnostics agree with the reports.
