# TerrainShadingStage

Generation stage: one step in the terrain-generation pipeline, run by `TerrainFieldBuilder`.

Computes a per-cell hillshade multiplier from the elevation gradient so that slopes facing the (fixed, north-west) light are brightened and slopes facing away are darkened. This is the mechanism that lets a rendered map show relief without recoloring the biome underneath — a grassy hill stays green and still reads as a hill, instead of every relief level costing its own desaturated "rocky" biome. The stage writes into `world.Shade`, which a renderer later multiplies into the tile's colour.

## Public API

- `static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the sole entry point. If `settings.HillshadeStrength <= 0`, returns immediately without touching `world.Shade` (a caller relying on shading being flat/neutral in that case must have initialized `Shade` to `1.0` elsewhere, since this stage does not do it for the disabled case beyond water cells). Otherwise, for every sample in `world.Width × world.Height`: water cells (`!world.Land[index]`) get `Shade = 1.0` (unlit/flat); land cells get a central-difference slope (`ElevationAt` neighbours, clamped at field edges) dotted against the fixed light direction `(-1,-1)` normalized, scaled by `Strength (7.5) * settings.HillshadeStrength`, added to 1.0, and clamped to `[0.70, 1.30]`.
- `private static float ElevationAt(TerrainWorld world, int x, int y)` — reads `world.Elevation` at `(x,y)` with both coordinates clamped into `[0, Width-1] × [0, Height-1]`, i.e. edge-clamped sampling for the central-difference gradient.

## Dependencies

- Reads `TerrainWorld.Width`, `TerrainWorld.Height`, `TerrainWorld.Land`, `TerrainWorld.Elevation`, `TerrainWorld.Index(x,y)`.
- Writes `TerrainWorld.Shade`.
- Reads `TerrainGenerationSettings.HillshadeStrength`.
- Called by `TerrainFieldBuilder.Apply` (or equivalent orchestration method) as one step of the generation pipeline, alongside `TerrainStartPositionStage` and others.

## Notes

- When `HillshadeStrength <= 0` the early return skips writing `Shade` for land cells entirely — the field is left at whatever `TerrainWorld` initialized it to (not necessarily `1.0`), unlike the water-cell branch inside the loop which explicitly sets `1.0`. Whether this matters depends on `TerrainWorld`'s default array initialization (not read as part of this file), but it is an asymmetry worth flagging: the "disabled" path and the "in-range but flat slope" path do not produce shading through the same code, only the same likely value.
- The clamp range `[0.70, 1.30]` and gain `Strength = 7.5` are hardcoded constants, not exposed via `TerrainGenerationSettings` beyond the single `HillshadeStrength` multiplier — tuning the shading curve's floor/ceiling requires editing this file.
- The class is `internal`, consistent with `TerrainStartPositionStage` — both are pipeline-stage implementation details, not part of the addon's public surface.
