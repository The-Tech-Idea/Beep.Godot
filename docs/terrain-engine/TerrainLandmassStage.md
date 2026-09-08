# TerrainLandmassStage

Pipeline position: **generation stage** — an internal step in world generation that decides WHERE land exists (as a fixed number of grown, separated landmasses), run before any terrain-kind classification, relief, or rendering happens.

`TerrainLandmassStage` fills the per-sample `TerrainGenerationBuffer.Land` mask.
It places seeds on a jittered lattice, then grows their connected regions using
per-mass priority queues ordered by noise-perturbed distance. Weighted growth
produces different mass sizes. An eligibility mask reserves the ocean margin;
a coarse gameplay-tile ownership grid prevents foreign masses from touching.
Growth stops at the requested coverage or when all frontiers are exhausted.

## Growth Geometry

Seeds are jittered within the central 20% of their lattice cells. The previous
80% range often put a seed close to the hard ocean margin before growth started.
Each seed carries an area-preserving stretch derived from its lattice cell's
aspect ratio: distance divides x by sqrt(width/height) and multiplies y by it.
Wide, shallow regions can therefore grow broad landmasses without first pressing
circular growth against their top and bottom limits. This changes seeded output;
there is no legacy generation branch.

Coast roughness amplitude and sampling scale use each mass's weighted target
radius, not the average radius of all islands. The average over-distorted smaller
masses after the growth-placement change: two small-archipelago fixtures fell to
30% and 36% bounding-box fill. Per-mass scaling restored them to 43% and 46%,
above the existing 40% guard without changing its tolerance.

The clean 32x32 lab fixture still reaches its exact 42% footprint. Its longest
axis-aligned shoreline run changed from 13 to 12 tiles; captures show a less
rectangular outline, not elimination of every straight coast. The diagnostic
is a grid-edge measurement, not a perceptual quality score. The earlier report of
two footprints but three continents came from labelling before scale cleanup.
After moving continent labelling to the final water grid, this fixture has two
dry regions of 170 and 210 cells, verified by independent flood-fill. Long straight
shorelines remain a visual review item.

## Beach Ownership

The separation check reserves a fixed two-tile gap. It does not read BeachWidth.
The biome stage paints sand only on existing land; it does not grow land into water.
The former `(ceil(BeachWidth) * 2) + 2` gap incorrectly coupled these decisions,
moving islands and reducing achievable coverage when the beach was widened.
That coupling also crowded land toward the rectangular outer eligibility boundary.

`terrain_beach_footprint_probe.gd` reproduced this and now verifies unchanged
land/water masks across widths 0, 1 and 3, two sizes, two seeds and all landforms.
Rivers/lakes are disabled to isolate shoreline ownership. Beach sand still expands
on the existing land, and the tested 50% footprint target is met.

## Public API

- `static float FeatureTiles(TerrainGenerationSettings settings)` — characteristic landmass size in tiles (`min map span / sqrt(landmass count)`), floored at 4; consumed elsewhere as the frequency basis for shape/terrain noise (this stage does not use it itself).
- `static int LandmassCount(TerrainGenerationSettings settings)` — thin forward to `settings.RequestedLandmassCount`, kept as the single place this rule is read from.
- `static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)` — clears Land, returns without land for the Sea preset, then builds eligibility, places seeds and grows land. The field builder currently allocates a fresh buffer for each build.

## Dependencies

- Reads `TerrainGenerationBuffer.Land` (writes it), `.Count`, `.Width`, `.Height`, `.Index(x,y)`, `.InBounds(x,y)`, `.TileCentre(x,y)`, `.SamplesPerCell` (defined in `TerrainGenerationBuffer.cs`).
- Reads `TerrainGenerationSettings.Seed`, `.Preset`, `.Size`, `.TargetLandCoverage`, `.RequestedLandmassCount`, `.OceanMarginTiles`, `.CoastlineRaggedness` (defined in `TerrainGenerationSettings.cs`), including `TerrainPreset.Sea` from the same file's enum.
- Uses Godot's `FastNoiseLite` and `PriorityQueue<int,float>` directly; does not read from or write to any other file in this directory (no renderer, no `TerrainGeneratorComponent`, no `TerrainLayers` dependency — this stage runs purely on the `TerrainGenerationBuffer` data model before classification).

## Notes

- `Squared(TerrainGenerationBuffer world, int left, int right)` is a private static helper computing squared cell distance from two flat indices — it is never called anywhere in this file (or, per the class being `internal`, likely anywhere else). Dead code: an unused private method left in place; per the "accepted-but-ignored" concern this is worth flagging for removal or for wiring in wherever a distance check was meant to use it instead of the inline `dx*dx+dy*dy` already duplicated in `Grow`'s per-neighbour loop.
- The per-neighbour distance-to-seed calculation inside `Grow` (`dx*dx + dy*dy`, `Mathf.Sqrt`) duplicates exactly what `Squared` computes, just inlined and using `Mathf.Sqrt` directly rather than calling the helper — i.e. `Squared` looks like it was extracted for this call site and then the call site was written inline anyway, leaving both to drift independently if the distance metric ever changes.
- Historical measurements in comments are not current contracts; the beach-growth
  assumption was contradicted by the actual biome and tile-reduction code.
- High coverage can still press land against the outer margin or the square
  foreign-claim exclusion area. The clean painted capture still has long straight
  coast segments. Removing beach coupling does not establish natural silhouettes.
- Existing landmass and topology probes check count/compactness, target reporting,
  monotonic coverage, edge water, lake enclosure and deterministic reconstruction.
  They do not constitute visual acceptance of every seed or coverage setting.
