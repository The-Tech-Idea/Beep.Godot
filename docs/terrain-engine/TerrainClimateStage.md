# TerrainClimateStage

Generation stage: an internal pipeline step that assigns per-sample `Temperature` and `Moisture`, the two axes `TerrainBiomeStage` later reads to classify terrain.

Temperature is modeled primarily as a function of latitude (via `world.Latitude`), reduced by an altitude/lapse-rate penalty and lightly wobbled by noise so the climate bands are not ruler-straight.

Moisture is built in this order:
1. A moisture fractal noise channel (weight 0.62) is blended with a maritime term (weight 0.38).
2. A rain-shadow term is subtracted. It dries land downwind of higher elevation, in the fixed `WindStepX = -1` direction.
3. A subtropical-aridity term is subtracted: a Gaussian bump centred on latitude 0.28, modelling the dry belts either side of the equator.
4. The result is scaled down at low temperature, since cold air holds less moisture.

Both outputs are clamped to `[0, 1]`. The Rainfall axis does not reach this stage: it scales lakes, rivers and vegetation (`TerrainMapSetup`), the way Civilization scopes it.

## The dry belt (FIX-15)

`SubtropicalAridity` is `exp(-((latitude − 0.28) / 0.13)²) × 0.35`, and it is where a Hot world's desert comes from. Latitude runs 0 at the equator to 1 at a pole, so the belt is centred on 25° and at half strength at 15° and 35°. That is where the deserts are: subtropical deserts "occur at a restricted latitudinal range between 15-35 degrees latitude" ([*ACC Physical Geology*, 2nd ed., §23.2](https://pressbooks.ccconline.org/accphysicalgeology/chapter/23-2-types-geography-of-deserts-physical-geology-2nd-edition/)).

It used to be centred on 0.34 (31°) with a width of 0.17, reaching 43° at half strength, and a peak of 0.20. That peak was chosen while every interior past 6.5 cells had no coastal moisture at all. Once the maritime reach was measured in kilometres, the old belt could not dry a hot map: the `biomes` guard's hot Continents map had no desert, and hot lab maps were greener than temperate ones (37–47% grass against 24–34%). Measured at normal rainfall with the new belt:

| Map | Temperate | Hot |
|---|---|---|
| Oilfield Days' basin, 144x144, seed 12345 | 98% grass, 2% dry grass | 2% desert, 82% dry grass, 15% grass |
| Standard lab map, 64x64, seed 2027 | 46% dry grass, 54% grass | 29% desert, 49% dry grass, 22% grass |
| Large lab map, 96x60, seed 31415 | 53% dry grass, 47% grass | 36% desert, 51% dry grass, 12% grass |

Shares are of inland cells (land minus beach sand). At span one, where the latitude centre is not read, a whole world is 27% desert, 36% dry grass, 19% grass and 18% tundra and snow, and its interior stays far drier than its coast.

## The maritime term (FIX-15)

The maritime term is `exp(-distance_km / 600)`. `distance_km` is the sample's distance to water, converted with `TerrainGenerationBuffer.KilometresPerSample(ClimateLatitudeSpan)`, the same span the latitude bands are drawn from:
- **Below span one:** the map's height is that fraction of the 10,000 km from equator to pole.
- **At span one:** the map runs pole to pole, 20,000 km.

The 600 km e-folding length is measured. On non-forested land, annual precipitation falls exponentially with distance from the ocean at a global mean of about 600 km ([Makarieva, Gorshkov & Li 2009](https://www.sciencedirect.com/science/article/abs/pii/S1476945X08000834), *Ecological Complexity* 6:302–307).

It replaced a linear ramp to zero at 6.5 cells. A cell has no size of its own, so that reach was about 1.1 km on Oilfield Days' 24 km, 144-cell basin and about 270 km on a scale-rules lab map. Measured on the game's recipe at temperate/normal before the change, 64 cells came out 20% desert, 96 cells 40% and 144 cells 44–52%. After it, all of them are 0% desert, and a whole world still has an interior far drier than its coast (see `terrain_climate_share_probe`).

## Public API

- `static void Apply(TerrainGenerationBuffer world, TerrainNoiseSet noise, TerrainGenerationSettings settings)` — the sole entry point. Iterates every `(x, y)` sample in `world`, computes `world.Temperature[index]` from latitude/altitude/noise and `world.Moisture[index]` from moisture noise/maritime/rain-shadow/aridity/temperature-scaling, and writes both back into `world`.

(`WindStepX`, `MaritimeEFoldingKilometres`, `Maritime`, `SubtropicalAridity`, `RainShadow` are private helpers, not public API.)

## Dependencies

- Reads `TerrainGenerationBuffer.Width`, `.Height`, `.Height` (for `bandWander`), `.Index(x,y)`, `.TileCentre(x,y)`, `.Latitude(y, offset, span, centre)`, `.KilometresPerSample(span)`, `.Elevation`, `.CoastDistance`, `.SamplesPerCell`, `.Land`, `.InBounds(x,y)`; **writes** `TerrainGenerationBuffer.Temperature[index]` and `TerrainGenerationBuffer.Moisture[index]` (`TerrainGenerationBuffer.cs`).
- Reads `TerrainNoiseSet.Temperature` and `.Moisture` (`FastNoiseLite.GetNoise2D`) (`TerrainNoiseSet.cs`).
- Reads `TerrainGenerationSettings.ClimateLatitudeSpan`, `.ClimateLatitudeCentre`, `.AltitudeCooling` (`TerrainGenerationSettings.cs`).
- Calls `TerrainGeometry.Normalized(float)` (`TerrainGeometry.cs`) to remap the signed moisture-noise sample into `[0, 1]`.
- Requires `world.Elevation`, `world.Land` and `world.CoastDistance` to already be populated (by earlier elevation/coastline stages) before this stage runs; it only reads them. `TerrainBiomeStage` depends on this stage's output (`world.Temperature`, `world.Moisture`) in turn.

## Notes

- `WindStepX` is a hardcoded `-1` (wind blowing from +X toward -X, i.e. rain shadows form to the west of high ground) with no setting to change prevailing wind direction; this is a fixed modeling choice, not a bug, but it is a hardcoded assumption worth knowing about if a map is meant to have configurable wind.
- The rain shadow still reaches `SamplesPerCell * 4` samples upwind, a distance in cells and so, like the old maritime reach, a different real distance on every map. FIX-15 left it unchanged: its strength is read off normalized elevation, and a regional basin's highest ground is not a mountain range, so converting only its reach to kilometres would change what it means without fixing that.
- No silent-failure paths — every sample is unconditionally assigned both a temperature and a moisture value each run.
- No overlap or duplicated logic with `TerrainBiomeStage`: this stage only produces the two continuous fields; classification into named terrain kinds happens entirely in `TerrainBiomeStage`.
