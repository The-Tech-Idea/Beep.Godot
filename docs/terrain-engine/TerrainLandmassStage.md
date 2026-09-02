# TerrainLandmassStage

Pipeline position: **generation stage** — an internal step in world generation that decides WHERE land exists (as a fixed number of grown, separated landmasses), run before any terrain-kind classification, relief, or rendering happens.

`TerrainLandmassStage` is an `internal static class` with a single entry point, `Apply`, that fills `TerrainWorld.Land` (a boolean-per-cell buffer) by placing N seed points on a jittered lattice and growing each one cell at a time (via per-mass `PriorityQueue<int, float>` frontiers ordered by noise-perturbed distance from its seed) until the map's target land coverage is reached. The class header explains at length why this replaced a single thresholded-noise field: that approach could only ever produce one blob (one radial falloff, one center) and, at playable coverage, percolated into a connected web rather than compact landmasses. Separation between masses is enforced by a coarse per-tile ownership grid (`ForeignClaimNear`) checked at claim time, sized to survive beach erosion inward from both banks of a channel — not by post-hoc buffering, which the comments document as having failed three different ways during development.

## Public API

- `static float FeatureTiles(TerrainGenerationSettings settings)` — characteristic landmass size in tiles (`min map span / sqrt(landmass count)`), floored at 4; consumed elsewhere as the frequency basis for shape/terrain noise (this stage does not use it itself).
- `static int LandmassCount(TerrainGenerationSettings settings)` — thin forward to `settings.RequestedLandmassCount`, kept as the single place this rule is read from.
- `static void Apply(TerrainWorld world, TerrainGenerationSettings settings)` — the stage entry point: clears `world.Land` (the buffer is reused between generations, so this clear is required, not cosmetic), returns early with no land for `TerrainPreset.Sea`, otherwise computes an eligible-cell mask, places seeds, and grows every landmass together via `Grow` until `TargetLandCoverage` worth of cells are claimed.

## Dependencies

- Reads `TerrainWorld.Land` (writes it), `.Count`, `.Width`, `.Height`, `.Index(x,y)`, `.InBounds(x,y)`, `.TileCentre(x,y)`, `.SamplesPerCell` (defined in `TerrainWorld.cs`).
- Reads `TerrainGenerationSettings.Seed`, `.Preset`, `.Size`, `.TargetLandCoverage`, `.RequestedLandmassCount`, `.OceanMarginTiles`, `.BeachWidth`, `.CoastlineRaggedness` (defined in `TerrainGenerationSettings.cs`), including `TerrainPreset.Sea` from the same file's enum.
- Uses Godot's `FastNoiseLite` and `PriorityQueue<int,float>` directly; does not read from or write to any other file in this directory (no renderer, no `TerrainGeneratorComponent`, no `TerrainLayers` dependency — this stage runs purely on the `TerrainWorld` data model before classification).

## Notes

- `Squared(TerrainWorld world, int left, int right)` is a private static helper computing squared cell distance from two flat indices — it is never called anywhere in this file (or, per the class being `internal`, likely anywhere else). Dead code: an unused private method left in place; per the "accepted-but-ignored" concern this is worth flagging for removal or for wiring in wherever a distance check was meant to use it instead of the inline `dx*dx+dy*dy` already duplicated in `Grow`'s per-neighbour loop.
- The per-neighbour distance-to-seed calculation inside `Grow` (`dx*dx + dy*dy`, `Mathf.Sqrt`) duplicates exactly what `Squared` computes, just inlined and using `Mathf.Sqrt` directly rather than calling the helper — i.e. `Squared` looks like it was extracted for this call site and then the call site was written inline anyway, leaving both to drift independently if the distance metric ever changes.
- The class carries extensive "measured" commentary (percolation ratios, isthmus tile counts, spindly-island bounding-box ratios) documenting *why* the current algorithm exists and what three earlier separation strategies failed at. This is unusually thorough design history embedded as comments rather than in an external doc — useful context, not a defect.
- `gapTiles = (beachTiles * 2) + 2` bakes in the assumption that beach erodes inward from both banks by `BeachWidth` tiles; if `TerrainGenerationSettings.BeachWidth` semantics change (e.g. becomes asymmetric or a different unit), this formula would silently under- or over-separate landmasses with no assertion catching it.
