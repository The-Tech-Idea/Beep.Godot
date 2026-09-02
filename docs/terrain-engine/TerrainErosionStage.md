# TerrainErosionStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` after elevation is first computed and before relief (hills/mountains) is classified.

`TerrainErosionStage` reshapes the raw noise-based height field so it reads as a carved landscape rather than a texture. It runs two coupled processes over several passes: stream-power incision, which lowers each land cell in proportion to how much water (drainage) passes through it and how steep its descent is, cutting deeper channels where flow concentrates; and hillslope diffusion, which relaxes each cell toward the average of its land neighbours, rounding ridges and filling hollows. The drainage network it incises along is computed once via `TerrainFlow.Accumulate` and reused for every pass (drainage changes far more slowly than height). The whole stage is a no-op if `settings.ErosionStrength <= 0`.

## Public API

- `internal static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — runs `Passes` (12) rounds of incision-then-diffusion directly on `world.Elevation` for every land cell, using a drainage network computed once at the start; returns early doing nothing if `settings.ErosionStrength <= 0` or there is no land. Diffusion strength and incision strength both scale with `settings.ErosionStrength` (clamped to 0–4).

That is the only public member; everything else (`Diffuse`, the tuning constants `DrainageExponent`, `Strength`, `MaxDrainageFactor`, `Diffusion`, `Passes`) is private to the class, and the class itself is `internal static`.

## Dependencies

- Reads and writes `TerrainWorld.Elevation`, `TerrainWorld.Land`, `TerrainWorld.Width`/`Height`, `TerrainWorld.Index`/`InBounds` (from `TerrainWorld.cs`).
- Reads `TerrainGenerationSettings.ErosionStrength` (from `TerrainGenerationSettings.cs`).
- Calls `TerrainFlow.Accumulate` (from `TerrainFlow.cs`) to get the shared D8 drainage network (`flowsTo`, `order`, `flow`) — the same network `TerrainRiverStage` reads, so carved valleys and drawn rivers agree.
- Called by `TerrainFieldBuilder.Build`, which runs it after `TerrainElevationStage.Apply` and before `TerrainElevationStage.Classify` — relief bands (hills/mountains) are cut as percentiles of the *eroded* height field, not the raw noise.

## Notes

- The incision/diffusion strengths in the code comments are backed by measured percentages (e.g. "a single pass changed 0.03% of the rendered map", "twelve passes changed 0.7%... against the median... 2.9% rather than 2.6%") — these read as genuine tuning notes, not stale claims, and match the constants as written (`Strength = 0.12f`, `Diffusion = 0.35f`, `Passes = 12`).
- Drainage is normalized against the *median* (`typical`) flow rather than the maximum, with a documented reason (dividing by the max makes ordinary cells erode near zero). This is a deliberate, explained design choice, not an oversight.
- `Diffuse` only ever touches land cells and reads only land neighbours, explicitly to avoid dragging sea elevation into the coastline.
- No accepted-but-unread settings or dead code found; the file is small and every constant is read somewhere in `Apply`/`Diffuse`.
