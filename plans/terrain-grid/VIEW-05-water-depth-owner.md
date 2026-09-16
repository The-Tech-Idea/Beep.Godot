# VIEW-05 — Water depth has one owner: the coast field

**Type:** fix (the block view measures its own depth by a different metric from the sea it draws over) · **Area:** `TerrainCoastField`, `TerrainIsometricRendererComponent`, `docs/terrain-engine/` · **Status:** Implemented 2026-09-16 · **Effort:** S (1 day) · **Risk:** low (seabed bands move by at most one step on diagonals; the guard records where)

## As landed (2026-09-16)

Built as designed. `TerrainCoastField.CellDistances(Pixels, size, rangeTiles)` decodes the R channel
at each cell's centre sample, and the block view's `MeasureWaterDepth` is gone - replaced by
`ReadWaterDepth`, which ceils those distances. `SeabedFrameFor`, its bands and the `SeabedDepth`
cut-off are untouched. Differences from the plan, each found by measuring:

- **The generated field had to be kept, not just uploaded.** Every `Build` overload returned an
  `ImageTexture` and dropped the pixels, so there was nothing to decode. `BuildPixels(generator, …)`
  is now the producer and `Build` uploads its result; `TerrainSeaSurface` keeps the `Pixels` it
  resolved (the live path takes `LiveCache.CoastField`) and answers `CellDistances`. The view
  therefore reads depth from the very field its own sea is drawn from, which is the point of the
  item - and it cost no second computation.
- **Saturation had to be handled explicitly.** The encoding clamps at the range the field was built
  with, so with the default range 5 and `SeabedDepth` 5 every deep-ocean cell would have read the
  deepest expressible step and drawn a bed. That is exactly the defect the old code's comment warns
  about: a bed under the whole ocean whose only visible effect is the straight line where it stops.
  A cell at or past the range is now treated as beyond the cut-off, so the bed ends at the shelf.
- **The plan's premise about the diagonal was wrong, and the guard had to be rebuilt around what
  the field actually measures.** The plan expected a pocket's corner cell to read ≈0.71 tiles. It
  does not: the field's distance transform runs on the fine sample grid, and a cell diagonally off a
  single land cell measures ≈1.13 tiles against ≈0.63 across the side - so both metrics band that
  cell the same, and a guard built on it passed against the old sweep. Measured, then rewritten:
  the metrics separate on a **diagonal coast**, where a cell two diagonal cells offshore is four
  four-neighbour steps from land but ≈2.5 tiles from the waterline - gravel against rock, two bands
  apart.

- **A water cell always beds at least the shore band.** The field measures the SUB-TILE coastline,
  so a cell the generator calls water can have its centre on the land side of its own water patch
  and read a negative distance. Ceiling that to 0 left 21 shore cells of the isometric demo with no
  bed - and a water cell draws no block, so that is a transparent hole at the shore rather than a
  shallow. Water clamps to at least step 1. `tests/examples/iso_layers.gd` found this, having been
  rewritten first (below); it is the defect the item would otherwise have shipped.
- **`tests/examples/iso_layers.gd`'s seabed check carried a copy of the implementation.** Its
  expectation was its own four-neighbour sweep, described in its comment as "the same breadth-first
  sweep out from the coast that the renderer uses" - true until the renderer stopped counting steps,
  after which it compared two different metrics and failed on a correct change. It now asks the
  renderer for the depth (`SeabedDepthAt`, added for it) and checks what does not depend on the
  metric: the bed is painted exactly where the shelf reaches, every water cell against the shore has
  one, and the shelf ends with open water left bare.

**Guards:**
- `tests/terrain_iso_river_probe.gd` gained a seabed section: a 3×3 inland sea beds every cell (all
  of it touches the shore); on a diagonal coast the cell offshore beds in the same band as the one
  inshore of it; and open water with no shore within the field's range draws no bed at all.
  **Mutation:** restoring the four-neighbour sweep failed the diagonal-coast check with rock against
  gravel. An earlier version of this guard passed against that same mutation and was replaced - the
  first test I wrote could not fail.
- Pin: `MeasureWaterDepth` absent from every file in `ecs/terrain/`, and the block view must read
  `_sea.CellDistances(size, CoastRangeTiles)`.
- Still green: runtime smoke; the headless integration run at its three recorded failures
  (`ground_cover`, `prop_sizing :40`, `lab_grid :14`), with `iso_layers` and `terrain_iso_river`
  passing.

**Not done, and why:** the plan asked for the pre-existing `iso_water_origin` and `iso_art` rendered
failures to be made green before the bed change was judged. They fail because the lab's block view
has not built its `IsoWater`/`IsoSeabed` nodes when those probes look at them - a lab timing defect
with nothing to do with depth, recorded in the known-failing list since 2026-09-15. The bed change
is judged instead by `iso_layers` (which builds the isometric DEMO, not the lab, and passes) and by
the river probe's own seabed section. The two rendered probes remain as they were.

## Gap

The block view decides how far below the surface to draw its seabed with its own breadth-first search from land, `MeasureWaterDepth` (`TerrainIsometricRendererComponent.cs:669-709`), into `_depth` (`:343-344`), run right after it has built the coast field (`:402-404`, measures at `:412-413`), and consumed per water cell at `:460-467` to pick a seabed frame (`SeabedFrameFor :717-724`, cut-off `SeabedDepth :125`). Four-neighbour steps are Manhattan distance. The sea the bed shows through shades its depth from the coast field — `shallow_tiles`/`deep_tiles` against `coast_map` (`shaders/water_common.gdshaderinc:77-83`) — which is Euclidean (`TerrainEuclideanDistance.cs`, Felzenszwalb–Huttenlocher). Along a diagonal coast the two disagree by up to a step, so the bed band steps where the water tint does not.

Elsewhere the fact already has one owner. The painted and tile seas take depth from the field (`terrain_splat.gdshader:358` for the painted stylised path; the shared include for both). The field itself encodes signed distance in tiles per fine sample (`TerrainCoastField.cs:23-27`; R written at `:538`, the ocean-only B at `:541-543`, and the same in the static builder `:795-797`). What the generator calls `shallow_water`/`deep_water` (`TerrainBiomeStage.cs:67-77`: lake and river always shallow, sea shallow only where it touches land) is a *wading classification* for gameplay, not a depth — and the block view correctly does not read it for depth (`:435-437` reads land/water only). Three notions of depth; one of them (the field) is the one every sea already draws.

## Design

1. **`TerrainCoastField.CellDistances(Pixels field, Vector2I size, float rangeTiles) → float[]`**: per cell, the signed distance decoded from the R channel at the cell-centre fine sample (`Pixels` at `:34`; the encoding at `:538`). Positive is water.
2. **The block view reads it.** `_depth[i] = Mathf.CeilToInt(distance)` for water cells, 0 for land; `MeasureWaterDepth` is deleted; `SeabedFrameFor` and its bands are untouched, `SeabedDepth` still cuts the bed off. R counts every water body (lakes and rivers included, as the BFS measured from any land), so a lake still shelves as today.
3. **Documentation:** `shallow_water`/`deep_water` documented on the generator's page as the wading classification; depth is "distance from the waterline, owned by the coast field" on the coast field's and the block view's pages.
4. **VIEW-11** (windowed block edits) reuses `CellDistances` over the coast `LiveCache` window instead of re-measuring the map.

## Guards (fail first)

- Extend `tests/terrain_iso_river_probe.gd` (it already builds a 9×9 block view over authored cells, `:24-61`): author a 3×3 sea pocket → each water cell's seabed frame equals `SeabedFrameFor(ceil(CellDistances) − 1)`, no bed beyond `SeabedDepth`, and the pocket's corner cell — diagonal to its nearest land — reads step 1 (Euclidean ≈ 0.71 tiles from its centre to the corner boundary). **Mutation:** restore the BFS → the diagonal cell reads 2 (two four-neighbour steps), the check fails.
- Pin: `MeasureWaterDepth` absent from `ecs/terrain/` (forbid; the method is deleted, not renamed).
- Rendered: `iso_water_origin` and `iso_art` re-run (both pre-existing failures on 2026-09-15 for an unrelated timing reason; they are made green before the bed change is judged), `terrain_world_iso_publication_probe` green.

## Dependencies / collisions

VIEW-04 lands first (the sea surface owns the coast resolve; `CellDistances` reads the same `Pixels`). DUP-15 (landed) reconstructs live water into the field the same way. VIEW-11 is the second consumer. No collision: the block renderer is this session's file.

## Out of scope

A generated bathymetry field (a different fact, not produced today); lake depth authoring; the seabed art bands; the transparent-surface opacity dials.
