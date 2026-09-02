# TerrainGenerationSettings

World-data model: the immutable input record to the generation pipeline, plus the diagnostics record the pipeline reports back. Neither is a Node.

`TerrainGenerationSettings` bundles every knob that can affect a generated world — map bounds, mode/preset, noise parameters, landform, erosion, lakes/rivers, gameplay density dials, resource catalog choice, biome-coherence and climate-scale options — into a single `readonly record struct`. Because it is a value type with structural equality, generation is treated as a pure function of this value: `TerrainGeneratorComponent` caches the last `GeneratedTerrainField` keyed on settings equality and only rebuilds when a field actually changed. `TerrainGenerationDiagnostics` is the companion output record: a measured summary of one generation run (coverage percentages, counts, timing) that tooling (the lab UI, MCP commands) can read back without re-walking the field.

## Public API

- `TargetLandCoverage => LandmassScale` — read-only computed property; land-coverage target is `LandmassScale` and nothing else. The doc comment explains this was previously a second, disagreeing source (`1 - SeaCoverage`) that has been retired in favor of one owner.
- `RequestedLandmassCount` — computed property mapping `Landform` to a count: `Island` → 1, `Archipelago` → `Max(2, ArchipelagoIslandCount)`, anything else (`Mainland`) → `Clamp(ArchipelagoIslandCount, 2, 6)`. Mainland is explicitly documented as "a few continents," not one landmass filling the map.
- Every other member is a plain positional field of the record (`Origin`, `Size`, `Mode`, `Preset`, `Seed`, noise params, landform params, erosion/beach, lake/river params, gameplay/resource params, relief fractions, climate-map/scale-rule flags and their derived values) — these are data, not behavior; see `TerrainGeneratorComponent.md` for what sets each one and `TerrainFieldBuilder`/the `Terrain*Stage` files for what reads each one.
- `TerrainGenerationDiagnostics.ToDictionary() : Godot.Collections.Dictionary` — converts the diagnostics record to a Godot dictionary (snake_case keys) for GDScript/MCP consumption. Pure field-to-key mapping, no computation.

## Dependencies

- References `TerrainGeneratorComponent.LandformMode` (the enum lives on the generator component, not here) to compute `RequestedLandmassCount`.
- References `ResourceSet` (defined in `TerrainResourceStage.cs`) and `ResourceCatalog` (defined in `ResourceCatalog.cs`) as field types.
- Written (constructed) exclusively by `TerrainGeneratorComponent.CurrentSettings()`, which reads every exported field on the component and clamps it into one of these records.
- Read by `TerrainFieldBuilder.Build(TerrainGenerationSettings)`, which fans the settings out to the individual `Terrain*Stage` classes (continent, elevation, climate, biome, water, erosion, feature, resource, start-position stages, etc.).
- `TerrainGenerationDiagnostics` is produced by `TerrainFieldBuilder` (inside the built `GeneratedTerrainField.Diagnostics`) and surfaced by `TerrainGeneratorComponent.GetGenerationDiagnostics()`.

## Notes

- Both records are `internal`, so nothing outside the `Beep.ECS` assembly touches them directly — GDScript/tooling only ever sees the `Godot.Collections.Dictionary` produced by `ToDictionary()`.
- The XML doc comments on `TargetLandCoverage` and `RequestedLandmassCount` are unusually narrative (they describe a past bug and its fix) rather than plain API descriptions; they are accurate but read as commit-message prose, not reference docs.
- `ResourceCatalog` is nullable (`ResourceCatalog?`) with a comment stating null means the `ResourceSet` axis picks a shipped catalog instead — this contract is enforced wherever `ResourceCatalog` is consumed downstream (not in this file), so it can't be verified purely from this file.
