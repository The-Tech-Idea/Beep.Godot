# TerrainClimateStage

Generation stage: an internal pipeline step that assigns per-sample `Temperature` and `Moisture`, the two axes `TerrainBiomeStage` later reads to classify terrain.

Temperature is modeled primarily as a function of latitude (via `world.Latitude`), reduced by an altitude/lapse-rate penalty and lightly wobbled by noise so the climate bands are not ruler-straight. Moisture blends a moisture fractal noise channel with a maritime term (falls off with distance from a coast), then subtracts a rain-shadow term (dries out land downwind — in the fixed `WindStepX = -1` direction — of higher elevation) and a subtropical-aridity term (a Gaussian bump centered near latitude 0.34 modeling the real-world dry belts either side of the equator), and finally scales the whole thing down at low temperature since cold air holds less moisture. Both outputs are clamped to `[0, 1]`.

## Public API

- `static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)` — the sole entry point. Iterates every `(x, y)` sample in `world`, computes `world.Temperature[index]` from latitude/altitude/noise and `world.Moisture[index]` from moisture noise/maritime/rain-shadow/aridity/temperature-scaling, and writes both back into `world`.

(`WindStepX`, `Maritime`, `SubtropicalAridity`, `RainShadow` are private helpers, not public API.)

## Dependencies

- Reads `TerrainWorld.Width`, `.Height`, `.Height` (for `bandWander`), `.Index(x,y)`, `.TileCentre(x,y)`, `.Latitude(y, offset, span, centre)`, `.Elevation`, `.CoastDistance`, `.SamplesPerCell`, `.Land`, `.InBounds(x,y)`; **writes** `TerrainWorld.Temperature[index]` and `TerrainWorld.Moisture[index]` (`TerrainWorld.cs`).
- Reads `TerrainNoiseSet.Temperature` and `.Moisture` (`FastNoiseLite.GetNoise2D`) (`TerrainNoiseSet.cs`).
- Reads `TerrainGenerationSettings.ClimateLatitudeSpan`, `.ClimateLatitudeCentre`, `.AltitudeCooling` (`TerrainGenerationSettings.cs`).
- Calls `TerrainGeometry.Normalized(float)` (`TerrainGeometry.cs`) to remap the signed moisture-noise sample into `[0, 1]`.
- Requires `world.Elevation`, `world.Land` and `world.CoastDistance` to already be populated (by earlier elevation/coastline stages) before this stage runs; it only reads them. `TerrainBiomeStage` depends on this stage's output (`world.Temperature`, `world.Moisture`) in turn.

## Notes

- `WindStepX` is a hardcoded `-1` (wind blowing from +X toward -X, i.e. rain shadows form to the west of high ground) with no setting to change prevailing wind direction; this is a fixed modeling choice, not a bug, but it is a hardcoded assumption worth knowing about if a map is meant to have configurable wind.
- No silent-failure paths — every sample is unconditionally assigned both a temperature and a moisture value each run.
- No overlap or duplicated logic with `TerrainBiomeStage`: this stage only produces the two continuous fields; classification into named terrain kinds happens entirely in `TerrainBiomeStage`.
