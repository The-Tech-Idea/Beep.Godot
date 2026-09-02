# TerrainShapePresets

World-data model / configuration catalogue: a static lookup table of named world-shape presets consumed by the generation pipeline and the editor UI.

Defines `TerrainShape`, an enum of five named landmass layouts (Continents, Pangaea, Archipelago, IslandChain, OceanWorld), and `TerrainShapeDefinition`, a record struct bundling the generation parameters each layout implies (landform mode, land coverage, island count, sea coverage, hills/mountains fraction, start-position count). The file's own doc comment explains the design rationale: shape (land layout) and climate are kept as separate, independently-choosable axes rather than combined into monolithic "world type" presets, because a combined list of N shapes × M climates would need N×M presets to cover every combination and previously covered only a fraction of them.

## Public API

- `enum TerrainShape { Continents, Pangaea, Archipelago, IslandChain, OceanWorld }` — the five selectable shape identifiers.
- `readonly record struct TerrainShapeDefinition(string DisplayName, TerrainGeneratorComponent.LandformMode Landform, float LandCoverage, int IslandCount, float SeaCoverage, float HillsFraction, float MountainsFraction, int StartPositions)` — the generation-parameter bundle for one shape.
- `static TerrainShapeDefinition Get(TerrainShape shape)` — looks up `shape` in the internal catalogue dictionary; if not found (not reachable for any current enum value, since all five are populated), falls back to the `Continents` definition rather than throwing.
- `static readonly TerrainShape[] Order` — the five shapes in the order they should be presented in a menu/dropdown.
- `static string[] DisplayNames()` — returns `Get(shape).DisplayName` for every shape in `Order`, in order; used to populate a UI list.

## Dependencies

- References `TerrainGeneratorComponent.LandformMode` (an enum defined in `TerrainGeneratorComponent.cs`) as the `Landform` field type in `TerrainShapeDefinition`.
- Consumed by `TerrainGeneratorComponent` (`TerrainShapePresets.Get(...)` and `.Order.Length` to resolve a numeric "map type" setting into a shape definition) and by `TerrainLabComponent` (`TerrainShapePresets.DisplayNames()` to populate its map-type dropdown). No other terrain-directory file reads from or writes to this one.

## Notes

- Every enum value in `TerrainShape` has a corresponding `Catalogue` entry, so the `Get` fallback-to-`Continents` branch is currently dead in practice — it only fires if a future enum member is added without a matching catalogue entry. It is a reasonable defensive default given `Continents` is described as "the default 4X arrangement," but worth confirming that stays true when the enum changes.
- Values are hand-tuned magic numbers with no validation that, e.g., `LandCoverage + SeaCoverage <= 1.0` or that `HillsFraction + MountainsFraction` stays within the land fraction — any such constraint is enforced (or not) by whatever generation stage consumes the definition, not by this file.
- Pure data/lookup file: no state, no I/O, no side effects.
