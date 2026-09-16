# TerrainStartAreaReport

Support type: an `internal sealed record` describing what `TerrainStartAreaStage` built for one start and whether that start is playable (FEAT-09). Also defines the `TerrainStartAreaPlacement` record struct.

One report exists per start position, in start order, on `TerrainGenerationBuffer.StartAreas` and `GeneratedTerrainField.StartAreas`. Unusable starts keep their report and stay in the start positions: dropping them would hide the fact that workflow review E01 requires reported ("report an unusable seed, never silently reroll it"). Games read the reports through `TerrainGeneratorComponent.GetStartAreaReports()`, which returns `ToDictionary()` for each.

## Public API

- `internal readonly record struct TerrainStartAreaPlacement(string ResourceId, Vector2I Cell, int Relaxation)` — one start-kit placement. `Cell` is generator-local. `Relaxation` is how far the constraints had to be relaxed: 0 as authored, 1 the distance band widened to the whole area, 2 the same-resource spacing dropped, 3 a Bonus resource overwritten. Underground placements use 0 or 1 only.
- `TerrainStartAreaReport(int Index, Vector2I Origin, Vector2I Footprint, int CellCount, int Exits, IReadOnlyList<TerrainStartAreaPlacement> Placements, IReadOnlyList<string> Problems)` — the start's index in start order, its origin (the headquarters anchor, generator-local), the kit's headquarters footprint size, the area's cell count, the exit count, every kit placement, and every problem found.
- `const string MissingPrefix = "missing:"` — prefix of a non-critical kit shortfall, the only problem a usable start may carry.
- `bool Usable { get; }` — true unless `Problems` holds something other than a `missing:` entry.
- `Godot.Collections.Dictionary ToDictionary()` — the report as GDScript reads it: `index`, `origin`, `footprint`, `cell_count`, `exits`, `placements` (an array of dictionaries with `resource`, `cell`, `relaxation`), `problems` (an `Array<string>`) and `usable`.

Problem strings the stage writes: `footprint_overlap`, `no_exit`, `area_too_small`, `unknown_resource:<id>`, `kit_entry_unplaceable:<id>`, `missing_critical:<id>` and `missing:<id>`.

## Dependencies

- Written by `TerrainStartAreaStage.Apply`.
- Read by `TerrainFieldBuilder` for the `start_area_count`, `start_area_usable_count`, `start_area_min_cells` and `start_area_max_cells` diagnostics, by `TerrainGeneratorComponent.GetStartAreaReports`, and by `TerrainMapOverlayComponent` for the headquarters outlines (`Index`, `Origin`, `Footprint`).

## Notes

- `Usable` is computed from `Problems` on every read, so the flag and the reasons cannot disagree.
- Cells in the report are generator-local, as `GetStartPositions` returns them. Add the generator's `BoundsOrigin` for absolute cells.
