# GeneratedTerrainField

World-data model: the finished, queryable output of one terrain-generation run, sitting between the generation stages and every renderer/gameplay component that reads the map.

`GeneratedTerrainField` is the read-only result object `TerrainFieldBuilder.Build` hands back once all generation stages have run. It holds the map at two resolutions drawn from the same `TerrainWorld` data: gameplay-tile arrays (one value per cell — terrain kind, water body, continent id, resource, relief, elevation, feature) for anything that moves, paths or builds on the map, and finer sub-tile sample arrays (terrain, water, shade) for the painter, so a coastline or biome boundary can curve within a tile instead of stepping around tile corners. Both views come from the same run, so the two can differ in sub-tile detail but a tile-resolution query and the majority of its own sub-samples never disagree. It is `internal sealed`, so it is only ever handed out through a public wrapper such as `TerrainGeneratorComponent.ResolveField()`.

## Public API

- `GeneratedTerrainField(TerrainWorld world, TerrainGenerationDiagnostics diagnostics)` — constructor; copies references to `world`'s cell- and sample-resolution arrays and captures `world.StartPositions` and the diagnostics for the run.
- `TerrainGenerationDiagnostics Diagnostics { get; }` — the diagnostics object passed in at construction (timing/stats from the generation run).
- `IReadOnlyList<Vector2I> StartPositions { get; }` — fair player start tiles, in gameplay tile coordinates.
- `string TerrainAtCell(Vector2I cell)` — terrain kind string at a gameplay tile.
- `string WaterSourceAtCell(Vector2I cell)` — `"ocean"`, `"lake"`, `"river"`, or empty for dry land, derived from the cell's `WaterBody`.
- `int ContinentAtCell(Vector2I cell)` — landmass id at a tile; 0 means water.
- `string ResourceAtCell(Vector2I cell)` — the resource id at a tile, or empty.
- `TerrainRelief ReliefAtCell(Vector2I cell)` — Flat/Hills/Mountains at a tile.
- `float ElevationAtCell(Vector2I cell)` — land height 0–1 at a tile; water is 0.
- `string FeatureAtCell(Vector2I cell)` — the feature id at a tile, or empty.
- `string TerrainAtPosition(Vector2 position)` — terrain kind at full sub-tile sample resolution, for continuous-position queries (painting).
- `bool IsWaterAtPosition(Vector2 position)` — true where the nearest sample is water; used as a hard placement veto (props must never land here).
- `float WaterFractionAtPosition(Vector2 position)` — bilinear-weighted fraction (0–1) of the four surrounding samples that are water, so a renderer can fade the shoreline instead of drawing a hard sample-sized step.
- `float ShadeAtPosition(Vector2 position)` — hillshade bilinearly interpolated across the four surrounding samples, excluding any that are on the opposite side of the shoreline from the centre sample, so slopes read as smooth gradients and sea colour never bleeds inland.
- `Color BlendedBaseColour(Vector2 position, Func<string, Color> colourFor)` — base paint colour, bilinearly blended across the four surrounding samples (again excluding cross-shoreline samples) using a caller-supplied terrain-kind-to-colour function; throws `ArgumentNullException` if `colourFor` is null.

All indexing (`CellIndex`, `SampleIndex`, `SampleIndexAt`, `CornersAt`) clamps coordinates into range rather than throwing, so an out-of-bounds query returns the nearest edge value instead of failing.

## Dependencies

- Reads `TerrainWorld` (`CellsWide`, `CellsHigh`, `SamplesPerCell`, `Width`, `Height`, `CellTerrain`, `CellWater`, `CellContinent`, `Resource`, `CellRelief`, `CellElevation`, `Feature`, `Terrain`, `Water`, `Shade`, `StartPositions`) — all consumed once, at construction, and cached as private arrays.
- Reads `TerrainGenerationDiagnostics` only as an opaque value passed through the constructor to the `Diagnostics` property.
- Uses the `WaterBody` and `TerrainRelief` enums, both defined in `TerrainWorld.cs`.
- Constructed by `TerrainFieldBuilder.Build`/`Finish` (`TerrainFieldBuilder.cs`), which is the only place that calls `new GeneratedTerrainField(...)`.
- Consumed by `TerrainGeneratorComponent` (`ResolveField()` and its per-cell/per-position query wrappers) and by `TerrainCoastField` (`generator.ResolveField()`), both outside this batch.

## Notes

- `internal sealed` — this type never crosses the addon's public API surface directly; every external caller goes through `TerrainGeneratorComponent`'s public wrapper methods.
- The class-level doc comment's claim that "the tile value is the MAJORITY of the samples inside it" describes how `TerrainTileReductionStage` builds the cell arrays upstream, not something this file computes — this file only stores and queries the two resolutions it is handed; worth knowing if a reader expects majority-voting logic here.
- No caching/memoization: every position query recomputes `CornersAt` (4 sample lookups + weights) per call, which is deliberate per the `Corners` struct's own comment ("runs once per painted pixel and must not allocate") but means callers doing dense per-pixel painting are relying on this being cheap, not on any batching this class provides.
