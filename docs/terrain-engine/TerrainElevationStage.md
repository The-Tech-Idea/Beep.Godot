# TerrainElevationStage

Generation stage in the terrain pipeline, run by `TerrainFieldBuilder` in two separate calls: `Apply` early (building the raw height field, before erosion), and `Classify` later (cutting relief bands, after erosion has reshaped the height field).

`TerrainElevationStage.Apply` builds `TerrainWorld.Elevation` for every land sample as a weighted blend of three terms: an inland term (distance from the coast, normalized against the widest landmass on the map so a small island and a big continent read the same), a ridged-fractal noise term (weighted heaviest, so highlands form connected mountain ranges rather than round blobs), and a roughness term. `TerrainElevationStage.Classify` is a separate, later step that cuts the (by then eroded) elevation field into flat/hills/mountains bands by percentile of land elevation — Civilization-style, so "a fifth of the land is hills" holds regardless of how the raw noise happened to come out, rather than by a fixed elevation threshold.

## Public API

- `public static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)` — computes `world.CoastDistance` via a distance transform from land, then writes `world.Elevation` for every sample as `sqrt(inland)*0.28 + ridge²*0.57 + rough*0.15` (clamped to 0–1) for land, or 0 with `TerrainRelief.Flat` for water. `settings` is accepted as a parameter but not read anywhere in the method body (see Notes).
- `public static void Classify(TerrainWorld world, TerrainGenerationSettings settings)` — writes `world.Relief` per land sample to `Mountains`/`Hills`/`Flat` by comparing `world.Elevation` against percentile cutoffs derived from `settings.HillsFraction`/`MountainsFraction`. Returns immediately, leaving every sample at its previous `Relief` value, when both fractions are `<= 0`.

Both methods are on an `internal static class`; there is no other public surface, and `Negate` is a private helper.

## Dependencies

- Reads and writes `TerrainWorld.Elevation`, `TerrainWorld.Relief`, `TerrainWorld.CoastDistance` (from `TerrainWorld.cs`).
- Reads `TerrainWorld.Land`, `Width`, `Height`, `Count`, `Index`, `TileCentre` (from `TerrainWorld.cs`).
- Calls `TerrainGeometry.DistanceTo(bool[], int, int)`, `TerrainGeometry.Ridged(float)`, `TerrainGeometry.Normalized(float)`, `TerrainGeometry.Percentile(float[], bool[], float)` (from `TerrainGeometry.cs`).
- Reads `noise.Ridge` and `noise.Roughness` (`FastNoiseLite` instances) off a `TerrainNoiseSet` (from `TerrainNoiseSet.cs`).
- Reads `TerrainGenerationSettings.HillsFraction`, `MountainsFraction` (from `TerrainGenerationSettings.cs`) — only in `Classify`, not in `Apply` (see Notes).
- Called by `TerrainFieldBuilder.Build` (outside this batch): `Apply` runs before `TerrainErosionStage.Apply` (also outside this batch, per that stage's own doc), and `Classify` runs after erosion has reshaped the height field — the file's own doc comment explains this split is deliberate so relief bands are cut from post-erosion elevation, not the raw noise shape.

## Notes

- `Apply`'s `settings` parameter is accepted but never read in the method body — every other value the method needs (`world`, `noise`) is used, but `settings` is dead weight in that particular signature. `Classify` does use its own `settings` parameter. This isn't a functional bug (nothing is silently ignored that should have taken effect — `Apply` just doesn't need per-run settings), but the parameter is worth flagging as unused per the "accepted-then-ignored" pattern the project's own standards call out; a reader skimming the signature would reasonably expect `Apply` to be settings-driven the way `Classify` is.
- The doc comment's weighting rationale ("The ridged term carries most of the weight... The inland term is only a gentle lift") matches the code's literal coefficients (0.57 for ridge², 0.28 for sqrt(inland), 0.15 for roughness).
- `Classify`'s early-return when both fractions are ≤ 0 leaves `world.Relief` at whatever it already was (set to `Flat` for every land sample by `Apply`, since `Apply` never writes anything but `Flat` to land — relief is entirely `Classify`'s job) — consistent, not a bug, but means calling `Classify` a second time with zero fractions after some other stage has modified `Relief` would leave that stage's values untouched rather than resetting them to flat.
- No dead code beyond the unused `Apply(settings)` parameter noted above.
