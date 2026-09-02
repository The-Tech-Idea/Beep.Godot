# TerrainGeometry

Support utility: a stateless, `internal static` grid-algorithms class used by the generation stages, not itself a stage or a component.

`TerrainGeometry` holds the small set of array-based grid algorithms the terrain pipeline's generation stages need repeatedly — four-way neighbour enumeration, flood-fill connected-component labelling, multi-source BFS distance fields, percentile thresholds over a masked sample set, and a few noise-shaping helpers. It is written for reuse of caller-supplied buffers rather than allocation, because (per its own doc comments) some of these operations run many times over a full field during one generation pass — the doc for `LabelComponents` says landmass bisection alone calls it "seventeen times over," and an earlier per-call-allocating version was the single largest cost in generation.

## Public API

- `static IEnumerable<int> Neighbours(int x, int y, int width, int height)` — yields the flat array indices of the up-to-4 orthogonal (4-way) neighbours of `(x, y)` that lie inside the `width`×`height` bounds; doc notes 4-connectivity is also the definition used for what counts as one landmass.
- `static int LabelComponents(bool[] mask, int width, int height, int[] labels, int[] stack, List<int> sizes)` — flood-fills `mask` into 4-connected components using an iterative stack-based DFS (all buffers supplied by the caller, sized to the field, and reused): writes each true cell's component index into `labels` (-1 for false cells), appends each component's cell count to `sizes`, and returns the component count.
- `static int CountComponents(bool[] mask, int width, int height)` — convenience wrapper that calls `LabelComponents` with fresh, non-reused buffers; documented as being for callers that just want the count and are not on a hot path (so its allocation is deliberate, not an oversight).
- `static int[] DistanceTo(bool[] source, int width, int height)` — multi-source BFS: returns, per cell, the integer step count to the nearest cell where `source` is true (0 for source cells themselves); used for coast-distance, which in turn drives elevation and the beach band.
- `static float Percentile(float[] values, bool[] mask, float percentile)` — sorts the subset of `values` where `mask` is true and returns the value at the given percentile (clamped 0-1, nearest-rank via rounding); returns 0 if no cell is masked in. Used to turn a relative target ("20% of land is hills") into a concrete noise-value cutoff regardless of the noise field's actual distribution.
- `static float Normalized(float signedNoise)` — remaps a `[-1, 1]` signed noise sample to `[0, 1]`.
- `static float Smooth(float value)` — smoothstep-style cubic ease (`3v² - 2v³`).
- `static float Ridged(float signedNoise)` — ridged-noise transform (`1 - |signedNoise|`), turning smooth fbm into sharp crests so mountain ranges read as ranges instead of round blobs.

## Dependencies

- Has no dependency on any other file in this directory — it operates purely on caller-supplied `bool[]`/`float[]` grid buffers and Godot's `Mathf`.
- Is read by seven other files in this directory: `TerrainFeatureStage.cs`, `TerrainFieldBuilder.cs`, `TerrainBiomeStage.cs`, `TerrainElevationStage.cs`, `TerrainClimateStage.cs`, `TerrainContinentStage.cs`, and `TerrainWaterStage.cs`. Confirmed one concrete call site: `TerrainFieldBuilder.cs` line 189 calls `TerrainGeometry.CountComponents(world.Footprint, world.Width, world.Height)` to report continent/landmass counts into diagnostics.

## Notes

- Purely a support/algorithms module — no state, no Godot node, no `[Export]`s, nothing game-facing. It is the one file in this batch with zero coupling to the rest of the terrain pipeline's data model.
- `CountComponents` intentionally re-allocates fresh buffers every call (`new int[mask.Length]`, `new int[mask.Length]`, `new List<int>()`) where `LabelComponents` is written specifically to avoid that cost — this is a deliberate, documented trade-off (simplicity for the cold path) rather than an oversight, per its own doc comment.
- `LabelComponents`'s DFS uses a local `Push` closure captured inside the `while` loop; this allocates a closure once per outer call (not per cell), which is consistent with the file's stated hot-path-conscious design.
