# TerrainNoiseSet

Generation stage / support utility: an internal factory that builds every `FastNoiseLite` instance one generation run needs, consumed at the start of the generation pipeline (by `TerrainFieldBuilder`).

`TerrainNoiseSet` is a small immutable holder of six `FastNoiseLite` channels:
- ridge
- roughness
- moisture
- temperature
- lake
- vegetation

Each is seeded with a distinct offset from the run's base seed. Five run at frequencies derived from the map's size; vegetation runs at a frequency set in tiles. Keeping every channel on its own seed offset guarantees that retuning one stage's frequency can never shift another stage's noise pattern (they would otherwise all sample the same underlying field at different rates and drift together). It is `internal` — not part of the addon's public surface — and is a pure construction helper with no per-frame or mutable state after `Create` returns. It is `IDisposable`, and `Dispose` releases all six channels.

## Public API

This class is `internal sealed`, so nothing here is part of the addon's external API; documented for completeness since it is a construction/config chokepoint every generation run passes through.

- `static TerrainNoiseSet Create(TerrainGenerationSettings settings)` — the only entry point. Computes `shapeFrequency = 1 / TerrainLandmassStage.FeatureTiles(settings)`. This is the continental frequency, scaled so one landmass spans roughly the whole map and N landmasses each span about 1/√N of it. It then builds the six channels.
- `FastNoiseLite Ridge { get; }` — ridged-transform input driving mountain ranges, at 3.0× shape frequency.
- `FastNoiseLite Roughness { get; }` — at 3.1× shape frequency; general small-scale variation.
- `FastNoiseLite Moisture { get; }` — at `max(0.004, shapeFrequency * 1.25 * settings.MoistureFrequencyMultiplier)`.
- `FastNoiseLite Temperature { get; }` — at `max(0.004, shapeFrequency * 0.85 * settings.TemperatureFrequencyMultiplier)`.
- `FastNoiseLite Lake { get; }` — at `max(0.02, shapeFrequency * 2.4 * settings.LakeFrequencyMultiplier)`.
- `FastNoiseLite Vegetation { get; }` — at `settings.FeatureFrequencyMultiplier / TerrainFeatureStage.StandWavelengthTiles`. This is a coherent field, so woodland forms connected stands with edges instead of independent per-tile dice rolls.
  - The frequency is set in tiles, not scaled to the map: a forest is so many tiles across whatever the map's size. At the standard 16 tiles a wavelength, a temperate basin's woodland is forests with a median of 22–23 tiles.
  - A wavelength has to fit within `TerrainFeatureStage`'s 24-tile ranking blocks, one and a half times over at 16 tiles.
  - It used to be `max(0.01, shapeFrequency * 2.2 * FeatureFrequencyMultiplier)` with a multiplier of 0.18. That is coarser than the continents, about a hundred tiles a wavelength on a 144-tile map. Each block's percentile then took the high side of a flat slope, and the block grid came out as straight bands of forest (FIX-15).

Each channel is built by the private `Create(settings, seedOffset, frequency)` helper:
- It copies `NoiseType`, `FractalType`, `FractalOctaves` (from `settings.Octaves`), `FractalLacunarity` and `FractalGain` from `settings` onto every channel uniformly.
- It sets `Seed = settings.Seed + seedOffset`, with six distinct literal offsets: 92221, 92251, 9719, 19739, 51053 and 33427.
- It sets `Frequency = Mathf.Max(0.0001f, frequency)`.

## Dependencies

- Reads `TerrainGenerationSettings` (the `internal readonly record struct` defined in `TerrainGenerationSettings.cs`): `Seed`, `NoiseType`, `FractalType`, `Octaves`, `Lacunarity`, `Gain`, `MoistureFrequencyMultiplier`, `TemperatureFrequencyMultiplier`, `LakeFrequencyMultiplier`, `FeatureFrequencyMultiplier`.
- Calls `TerrainLandmassStage.FeatureTiles(settings)` to derive the base continental frequency from map size and landmass count.
- Reads `TerrainFeatureStage.StandWavelengthTiles` for the vegetation channel.
- Writes nothing to any other terrain file; returns a self-contained instance to its caller (`TerrainFieldBuilder`).

## Notes

- The six integer seed offsets are magic numbers with no named constants; they only need to be mutually distinct (so channels do not correlate), which they are, but nothing documents that constraint beyond the class's own summary comment about "each on its own seed offset."
