# TerrainGenerationBuffer

World-data model: the single mutable working set every generation stage in the terrain pipeline reads from and writes to.

`TerrainGenerationBuffer` is a plain (`internal sealed`, not a Godot node) struct-of-arrays: one array per fact (land, water, elevation, temperature, moisture, relief, coast distance, terrain kind, shade, continent id, resources, feature) indexed either by sample (`Width * Height`, at `SamplesPerCell` resolution) or by gameplay cell (`CellsWide * CellsHigh`). Keeping every fact as a same-length array sharing one index space is what lets gameplay cells, continuous rendering, lake carving and prop placement all agree on what is at a position, instead of each stage inventing its own lookup.

## Public API

- `TerrainGenerationBuffer(int width, int height, int samplesPerCell)` — allocates every sample- and cell-resolution array; fills `Shade`/`CellShade` with `1.0f` (unlit multiplier), `CellTerrain` with `"grass"`, and `Resource`/`Feature` with `string.Empty`.
- `int Width { get; }`, `int Height { get; }`, `int SamplesPerCell { get; }` — construction-time sample-grid dimensions.
- `int Count => Width * Height` — total sample count.
- `bool[] Land` — true where a sample is dry land (post lake-carving).
- `bool[] Footprint` — the landmass outline as the landmass stage first chose it, before lakes were cut out; this is what "one landmass" and "land coverage" mean, independent of any lake carved inside it later.
- `WaterBody[] Water` — per-sample water classification (`None`/`Ocean`/`Lake`/`River`); ocean reaches the map border, a lake never does.
- `float[] Elevation` — normalized 0..1 height above sea level on land.
- `float[] Temperature` — normalized 0..1, 1 hottest.
- `float[] Moisture` — normalized 0..1, 1 wettest.
- `TerrainRelief[] Relief` — Flat/Hills/Mountains per sample, assigned by elevation percentile.
- `int[] CoastDistance` — samples to nearest water; 0 in water itself.
- `string[] Terrain` — final per-sample terrain kind consumed by gameplay/rendering.
- `float[] Shade` — per-sample multiplier on the painted base colour (1 = unlit); keeps relief visually legible without baking it into the terrain kind.
- `int[] Continent` — landmass id per sample; 0 is water.
- `string[] Resource` — per **gameplay cell** (not per sample) resource id, empty = none.
- `List<Vector2I> StartPositions` — fair player start tiles, in gameplay cell coordinates.
- `byte[] CellStartArea` — per gameplay cell, which start's reserved area the cell belongs to: 0 none, k+1 start k. Allocated on first access, which only `TerrainStartAreaStage` makes, so a map without start areas carries no array. `internal byte[]? CellStartAreaIfGenerated` returns the array when it was allocated, else null (FEAT-09).
- `List<TerrainStartAreaReport> StartAreas` — one report per start position when start areas were generated, in start order.
- `ushort[] CellStartDistance` — per gameplay cell, the distance to the nearest start in whole cells, water included. Allocated on first access, and allocated only when `StartDistanceScaling` asks for it: two bytes a cell is a real cost on a huge world, and a map that scales nothing by distance has no reader for it. `internal ushort[]? CellStartDistanceIfGenerated` returns the array when it was allocated, else null (FEAT-14).
- `TerrainNeutralSitesReport NeutralSites { get; set; }` — what the kit's Neutral entries placed between the starts. `TerrainNeutralSitesReport.None` until `TerrainStartAreaStage` writes a report, which it does only when start areas are on and the kit has a Neutral entry (FEAT-14).
- `string[] CellTerrain`, `WaterBody[] CellWater`, `TerrainRelief[] CellRelief`, `float[] CellElevation`, `float[] CellShade`, `int[] CellContinent`, `string[] Feature` — the authoritative gameplay-tile-resolution reductions of the sample arrays above; these, not the sample arrays, are what a game actually moves/paths/builds on.
- `int CellIndex(int cellX, int cellY)` — `(cellY * CellsWide) + cellX`.
- `bool CellInBounds(int cellX, int cellY)` — bounds check against `CellsWide`/`CellsHigh`.
- `int CellsWide => Mathf.Max(1, Width / Mathf.Max(1, SamplesPerCell))`, `int CellsHigh` (same for Height) — derived cell-grid dimensions.
- `int CellCentreIndex(int cellX, int cellY)` — sample index at the centre of a gameplay cell (clamped to grid bounds).
- `int Index(int x, int y)` — `(y * Width) + x`, the sample-grid flat index.
- `bool InBounds(int x, int y)` — sample-grid bounds check.
- `Vector2 TileCentre(int x, int y)` — a sample's centre in tile-space (`(x+0.5)/SamplesPerCell`, `(y+0.5)/SamplesPerCell`).
- `float Latitude(int y, float offsetSamples, float span, float centre)` — latitude at a row, 0 at equator / 1 at a pole. At `span >= 1` (a whole-world map) returns the full pole-to-equator-to-pole gradient; below that it returns a narrow window centred on `centre`, i.e. one climate band instead of the whole range — this is what stops a 50-tile-tall regional map from getting an ice cap, a desert and a jungle all at once.

Also in this file: `internal enum WaterBody : byte { None, Ocean, Lake, River }` and `internal enum TerrainRelief : byte { Flat, Hills, Mountains }`.

## Ownership

`TerrainFieldBuilder` creates this buffer for one build. Generation stages mutate it in order;
`GeneratedTerrainField` takes the output arrays without copying them. The buffer is never a node,
save participant, or live map. Runtime cell edits belong to `GridCellDataComponent`.

## Dependencies

The world controller and its camera/status helpers do not hold this buffer. They reach generation
through `TerrainGeneratorComponent`, which delegates the build to `TerrainFieldBuilder`.

Outside this batch, `TerrainGenerationBuffer` is the shared working set read and written by essentially every generation-stage file in the folder: `TerrainElevationStage.cs`, `TerrainClimateStage.cs`, `TerrainLandmassStage.cs`, `TerrainWaterStage.cs`, `TerrainRiverStage.cs`, `TerrainContinentStage.cs`, `TerrainCoherenceStage.cs`, `TerrainErosionStage.cs`, `TerrainFeatureStage.cs`, `TerrainResourceStage.cs`, `TerrainScaleConstraintStage.cs`, `TerrainStartPositionStage.cs`, `TerrainStartAreaStage.cs`, `TerrainTileReductionStage.cs`, `TerrainShadingStage.cs`, `TerrainBiomeStage.cs`, `TerrainFieldBuilder.cs`, `TerrainFlow.cs`, `GeneratedTerrainField.cs`.

## Notes

- `CellShade` and `CellElevation` have separate documentation. `Latitude` documents each input.
- No `[Export]` fields anywhere in this file — it is a plain internal C# class, not a Godot node/component, so it never appears in the editor's Add Node dialog and has no node lifecycle.
