# TerrainEuclideanDistance

Support utility: a stateless, `internal static` distance-transform class shared by the generation
stages and by the renderers' coast field, not itself a stage or a component.

`TerrainEuclideanDistance` computes an exact Euclidean distance transform over a sampled binary
mask in linear time, using the separable lower-envelope algorithm of Felzenszwalb and Huttenlocher
(*Distance Transforms of Sampled Functions*, Theory of Computing 8, 2012), which the file cites. It
also owns the one conversion from a squared distance in samples to a distance in gameplay tiles.

## Public API

- `static double[] Squared(bool[] mask, Vector2I size, bool seedValue, CancellationToken token = default)`
  — squared distance, in samples, from every cell of `mask` to the nearest cell equal to
  `seedValue`; allocates the result.
- `static double[] Squared(bool[] mask, Vector2I size, bool seedValue, double[] result, CancellationToken token = default)`
  — the same transform written into a caller-owned `result` at least the mask's length, for a
  caller that reuses one buffer across passes. Mismatched dimensions or a short result throw
  `ArgumentException`.
- `static void Squared(bool[] mask, Vector2I size, bool seedValue, float[] result, CancellationToken token = default)`
  — the same transform, computed in `double` per row and column but stored in a `float` field. Every
  intermediate and final value is a whole number (a squared step count), so the stored field is
  exact wherever the squared distance is below 2^24 — a straight run of 4096 samples — and rounded
  but still monotone beyond it. That is enough for a band test against a threshold far below that
  bound, which is what the shoreline stage asks, through the shared float scratch rather than a
  private double field the size of the map. The coast field keeps the `double` form above, because
  it draws the value itself.
- `static float ToTiles(double squared, int samplesPerTile)` — `max(0, sqrt(squared) - 0.5) / samplesPerTile`:
  the half-sample correction, then the sample resolution divided out. Fewer than one sample per tile
  throws `ArgumentOutOfRangeException`.
- `static float ToTiles(float squared, int samplesPerTile)` — the same rule for the float scratch the
  generation stages share.
- `static float[] Signed(bool[] water, Vector2I size, int samplesPerCell, CancellationToken token = default)`
  — distance in tiles from the waterline, positive in water and negative on land. Curves retain
  sampling error. An entirely uniform mask has no boundary at all, so it saturates at the map
  diagonal rather than returning infinity or NaN.

## The half-sample correction has one owner (VIEW-07, 2026-09-16)

`ToTiles` is that owner. The half sample is what puts a straight boundary *between* two sample
centres rather than on one of them: a sample touching the boundary is half a sample from it, not a
whole one. Drop it and every band is half a sample too wide, on both sides.

It has one owner because the number decides two things that must agree — which samples
[`TerrainShorelineStage`](TerrainShorelineStage.md) calls sand, and how far from the waterline
[`TerrainCoastField`](TerrainCoastField.md) says a point is. The stage evaluates it at the
generator's sample resolution (`SamplesPerCell`) and the field at its own (`detail`, 12 by default),
so the two only line up while they apply the same rule. A beach the generator makes and a beach the
painter draws are the same beach, or they are a bug.

The formula was written out five times in three files before this — once in `Signed`, twice in the
stage and twice in the coast field. The call sites now are:

- `Signed`, in this file, for every sample of a signed field.
- `TerrainShorelineStage`, for the ocean band and again for the lake band.
- `TerrainCoastField`, for the live window's ocean-distance write and for the generated build's —
  the B channel in both.

`Signed` keeps one case of its own, and it is not a boundary distance: the uniform-mask saturation
above.

## Guards

- A contract-scan pin, both directions: no file under `ecs/terrain/` except
  `TerrainEuclideanDistance.cs` may write `Math.Sqrt(…) - 0.5`, and `TerrainShorelineStage.cs` and
  `TerrainCoastField.cs` must both call `TerrainEuclideanDistance.ToTiles(`.
- `tests/TerrainShorelineContourSmoke.cs` (headless, run by the registered
  `terrain_shoreline_contour` probe) checks `ToTiles` on a straight boundary at 1, 4 and 12 samples
  per tile, and that a sample inside the boundary never reports a negative distance. **Mutation:**
  dropping the `- 0.5` fails it — `ToTiles(1, 1) = 1, not 0.5 tiles`.
- The same smoke checks `Squared` against a brute-force nearest-opposite-sample oracle over 24
  random masks including the empty and full cases, and that `Signed` stays finite with the right
  sign on a uniform mask.
- `terrain_generation_baseline_probe`: every recorded layer hash identical across the VIEW-07
  refactor — the proof that folding five copies into one changed no generated map. (The VIEW-07
  plan calls this "96 hashes"; the fixture as it stands holds eighteen layers over eight cases.)

## Dependencies

- Depends on no other file in this directory: it operates purely on caller-supplied
  `bool[]`/`double[]`/`float[]` buffers plus Godot's `Vector2I`.
- Read by `TerrainShorelineStage` (band tests), `TerrainCoastField` (signed field and ocean
  distance) and `TerrainShorelineField` (the presentation snapshot, through `Signed`).
