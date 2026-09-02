# TerrainRiverStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after `TerrainClimateStage.Apply` and before `TerrainShadingStage.Apply`/`TerrainBiomeStage.Apply` — at full sub-tile sample resolution, before the map is reduced to one value per gameplay tile.

`TerrainRiverStage` carves rivers as a single drainage network rather than a set of independently-walked traces: it reuses the shared D8 flow accumulation (`TerrainFlow.Accumulate`, also used by `TerrainErosionStage`) to know how much land drains through every sample, picks an accumulation threshold that makes a `settings.RiverDensity`-scaled *percentile* of the land count as river (not an absolute cell count, since raw accumulation scales with map area), and then carves a disc of radius 1–3 samples — wider for cells carrying more accumulated flow — around every sample at or above that threshold, turning it from land into `WaterBody.River`. Because it works on shared accumulation rather than independent walks, tributaries fall out naturally as merging flow rather than needing special-case handling, and every drop of water reaches the sea (pits routed toward the coast by `TerrainFlow.Downhill`) so no river dead-ends mid-map.

## Public API

- `internal static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the stage's only member. Clamps `settings.RiverDensity` to 0–4 and returns immediately if it is ≤0. Computes the drainage network via `TerrainFlow.Accumulate`; returns if there is no land. Computes an accumulation `Threshold` from `RiverShareAtDensityOne * density` (clamped to 0–0.5 share of land); returns if that threshold is ≤1. Walks every land sample in descending-accumulation order and, for each at or above threshold, carves a radius-1–3 disc (radius grows with `log(flow/threshold + 1)`, clamped 1–3) into `world.Land`/`world.Water` via the private `Carve` helper.

Everything else (`RiverShareAtDensityOne`, `Threshold`, `Carve`) is private to the `internal static class TerrainRiverStage`.

## Dependencies

- Reads and writes `TerrainWorld.Land`, `TerrainWorld.Water` (sets to `WaterBody.River`); reads `TerrainWorld.Count`, `TerrainWorld.Width`, `TerrainWorld.InBounds`, `TerrainWorld.Index` (all `TerrainWorld.cs`) — operates on the full sample grid, not the reduced per-tile grid.
- Reads `TerrainGenerationSettings.RiverDensity`, `.Seed` (indirectly, via the shared `TerrainFlow` call) (`TerrainGenerationSettings.cs`).
- Calls `TerrainFlow.Accumulate` (`TerrainFlow.cs`) to get the shared drainage network (`flowsTo`, `order`, `flow`) — the same network `TerrainErosionStage` computes independently for its own pass; both stages read `TerrainWorld.Elevation`/`CoastDistance` through that shared helper rather than duplicating the D8 walk.
- Reads the `WaterBody` enum (`WaterBody.River`, tested/compared elsewhere in the pipeline, e.g. `TerrainScaleConstraintStage`).
- Consumed by: `TerrainFieldBuilder.Build` (calls `Apply`); downstream, `TerrainScaleConstraintStage.ApplyTerrain`'s `ClearShortRivers` removes any river region on the *reduced* tile grid that falls below `TerrainScaleRules.MinRiverTiles` after tile reduction collapses this stage's sample-level carving into cells.

## Notes

- The class doc comment's "WHAT THIS REPLACES" section (describing a discarded scattered-source, independent-walk implementation) documents history rather than current behaviour — there is no trace of that old implementation left in the file; it is prose context, not a stale comment describing the code below it.
- `Threshold`'s `share <= 0.0f` guard (returns `float.MaxValue`) is dead code as currently called: `Apply` already returns early whenever `density <= 0.0f`, and by the time `share = Mathf.Clamp(RiverShareAtDensityOne * density, 0, 0.5)` is computed, `density > 0` so `share` is always positive. The guard would only matter if `Threshold` were called from elsewhere or `Apply`'s early return were changed.
- The tuned constant `RiverShareAtDensityOne = 0.0045f` is documented with a measured outcome ("2% of centres came out as 22% of the land under water") — a genuine tuning note, not a placeholder value.
- No dead code, stubs, or TODOs found in this file.
