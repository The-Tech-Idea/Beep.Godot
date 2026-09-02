# TerrainWorld

World-data model: the single mutable working set every generation stage in the terrain pipeline reads from and writes to.

`TerrainWorld` is a plain (`internal sealed`, not a Godot node) struct-of-arrays: one array per fact (land, water, elevation, temperature, moisture, relief, coast distance, terrain kind, shade, continent id, resources, feature) indexed either by sample (`Width * Height`, at `SamplesPerCell` resolution) or by gameplay cell (`CellsWide * CellsHigh`). Keeping every fact as a same-length array sharing one index space is what lets gameplay cells, continuous rendering, lake carving and prop placement all agree on what is at a position, instead of each stage inventing its own lookup.

## Public API

- `TerrainWorld(int width, int height, int samplesPerCell)` — allocates every sample- and cell-resolution array; fills `Shade`/`CellShade` with `1.0f` (unlit multiplier), `CellTerrain` with `"grass"`, and `Resource`/`Feature` with `string.Empty`.
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

## Dependencies

None on the other 4 files in this batch — `TerrainWorld` is a pure data holder and is never referenced by `TerrainWorldComponent.cs`, `TerrainWorldComponent.Drawing.cs`, `TerrainWorldCameraComponent.cs`, or `TerrainWorldStatusComponent.cs` (those go through `TerrainGeneratorComponent` instead, which wraps a `TerrainWorld` internally but lives outside this batch).

Outside this batch, `TerrainWorld` is the shared working set read and written by essentially every generation-stage file in the folder: `TerrainElevationStage.cs`, `TerrainClimateStage.cs`, `TerrainLandmassStage.cs`, `TerrainWaterStage.cs`, `TerrainRiverStage.cs`, `TerrainContinentStage.cs`, `TerrainCoherenceStage.cs`, `TerrainErosionStage.cs`, `TerrainFeatureStage.cs`, `TerrainResourceStage.cs`, `TerrainScaleConstraintStage.cs`, `TerrainStartPositionStage.cs`, `TerrainTileReductionStage.cs`, `TerrainShadingStage.cs`, `TerrainBiomeStage.cs`, `TerrainFieldBuilder.cs`, `TerrainFlow.cs`, `GeneratedTerrainField.cs`.

## Notes

- `CellShade`'s doc comment is orphaned: the `<summary>Averaged hillshade per gameplay tile.</summary>` on the line immediately above `CellElevation` is immediately followed by a second, unrelated `<summary>` block that actually documents `CellElevation` — so `CellElevation` ends up documented twice in a row and `CellShade` (declared two lines later) ships with no doc comment of its own at all.
- `Latitude(...)` carries two stacked `<summary>` blocks with a `<param name="offsetSamples">` sandwiched between them — leftover text from an earlier revision. Only `offsetSamples` is documented via `<param>`; `span` and `centre` are described only in prose inside the second summary, not as `<param>` tags.
- No `[Export]` fields anywhere in this file — it is a plain internal C# class, not a Godot node/component, so it never appears in the editor's Add Node dialog and has no node lifecycle.
