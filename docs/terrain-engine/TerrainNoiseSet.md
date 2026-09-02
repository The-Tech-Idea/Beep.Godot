# TerrainNoiseSet

Generation stage / support utility: an internal factory that builds every `FastNoiseLite` instance one generation run needs, consumed at the start of the generation pipeline (by `TerrainFieldBuilder`).

`TerrainNoiseSet` is a small immutable holder of ten `FastNoiseLite` channels (shape, two shape-warp axes, ridge, roughness, moisture, temperature, lake, detail, vegetation), each seeded with a distinct offset from the run's base seed and scaled to a frequency derived from map size. Keeping every channel on its own seed offset guarantees that retuning one stage's frequency can never shift another stage's noise pattern (they'd otherwise all sample the same underlying field at different rates and drift together). It is `internal` — not part of the addon's public surface — and is a pure construction helper with no per-frame or mutable state after `Create` returns.

## Public API

This class is `internal sealed`, so nothing here is part of the addon's external API; documented for completeness since it's a construction/config chokepoint every generation run passes through.

- `static TerrainNoiseSet Create(TerrainGenerationSettings settings)` — the only entry point. Computes `shapeFrequency = 1 / TerrainLandmassStage.FeatureTiles(settings)` (continental frequency scaled to the map so one landmass spans roughly the whole map and N landmasses each span ~1/√N of it), then builds all ten noise channels from it, each at a distinct multiple of `shapeFrequency` (or, for moisture/temperature/lake/detail/vegetation, `Mathf.Max` against a hard floor combined with the relevant `*FrequencyMultiplier` field from `settings`).
- `FastNoiseLite Shape { get; }` — continental fractal; decides where land is.
- `FastNoiseLite ShapeWarpX { get; }`, `FastNoiseLite ShapeWarpY { get; }` — domain-warp offsets applied to the shape fractal, each at 1.7x shape frequency.
- `FastNoiseLite Ridge { get; }` — ridged-transform input driving mountain ranges, at 3.0x shape frequency.
- `FastNoiseLite Roughness { get; }` — at 3.1x shape frequency; general small-scale variation.
- `FastNoiseLite Moisture { get; }` — at `max(0.004, shapeFrequency * 1.25 * settings.MoistureFrequencyMultiplier)`.
- `FastNoiseLite Temperature { get; }` — at `max(0.004, shapeFrequency * 0.85 * settings.TemperatureFrequencyMultiplier)`.
- `FastNoiseLite Lake { get; }` — at `max(0.02, shapeFrequency * 2.4 * settings.LakeFrequencyMultiplier)`.
- `FastNoiseLite Detail { get; }` — at `max(0.05, settings.Frequency * 3.2)` — the one channel driven from the settings' own base `Frequency` rather than the map-derived `shapeFrequency`.
- `FastNoiseLite Vegetation { get; }` — at `max(0.01, shapeFrequency * 2.2 * settings.FeatureFrequencyMultiplier)`; a coherent field (coarser than `Detail`, finer than `Shape`) so forest/vegetation forms connected stands with edges instead of independent per-tile dice rolls.

Each channel is built by the private `Create(settings, seedOffset, frequency)` helper, which copies `NoiseType`, `FractalType`, `FractalOctaves` (from `settings.Octaves`), `FractalLacunarity`, `FractalGain` from `settings` onto every channel uniformly, and sets `Seed = settings.Seed + seedOffset` (ten distinct literal offsets: 91127, 91159, 91193, 92221, 92251, 9719, 19739, 51053, 71069, 33427) and `Frequency = Mathf.Max(0.0001f, frequency)`.

## Dependencies

- Reads `TerrainGenerationSettings` (the `internal readonly record struct` defined in `TerrainGenerationSettings.cs`): `Seed`, `NoiseType`, `FractalType`, `Frequency`, `Octaves`, `Lacunarity`, `Gain`, `MoistureFrequencyMultiplier`, `TemperatureFrequencyMultiplier`, `LakeFrequencyMultiplier`, `FeatureFrequencyMultiplier`.
- Calls `TerrainLandmassStage.FeatureTiles(settings)` to derive the base continental frequency from map size and landmass count.
- Writes nothing to any other terrain file; returns a self-contained instance to its caller (`TerrainFieldBuilder`, not in this batch).

## Notes

- No stale comments found; each XML doc comment matches the code beneath it (e.g. `Vegetation`'s comment about connected stands vs. per-tile dice rolls accurately describes why it uses a coherent frequency rather than pure randomness).
- The ten integer seed offsets are magic numbers with no named constants; they only need to be mutually distinct (so channels don't correlate), which they are, but nothing documents that constraint beyond the class's own summary comment about "each on its own seed offset."
- `Detail` is the only channel scaled from `settings.Frequency` (the painter/texture frequency) rather than `shapeFrequency` (the map-derived continental frequency) — consistent with the class comment's warning elsewhere in the codebase that "the continental fractal is scaled to the MAP, not the painter's texture frequency," since fine detail *should* track the painter's own frequency setting.
