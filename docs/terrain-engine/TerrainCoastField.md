# TerrainCoastField

Support utility in the terrain pipeline, called by every renderer (flat tile, isometric, painted) that draws water — not itself a generation stage or a component.

`TerrainCoastField` builds a single-texture "distance to waterline + is-open-sea" field for a generated map, sampled below tile resolution so the coastline it encodes is curved rather than staircased. Renderers use this texture (not the raw water/land grid) to shade a beach a consistent width, shelve depth away from shore, and draw surf that runs out from the actual waterline instead of stopping at a tile edge. It exists as one shared static method precisely so the flat and isometric views, which each render the same generated sea independently, agree on where the coast is — two independently-derived coastlines would visibly disagree at the boundary between the two views.

## Public API

- `public static ImageTexture Build(TerrainGeneratorComponent generator, Vector2I size, int detail, float rangeTiles)` — for a `size`-tile map, builds an `Image.Format.Rgba8` texture at `size * detail` resolution. Red/Blue channel is signed chamfer distance to the waterline (0.5 = waterline, clamped/scaled by `rangeTiles`); Green channel is 1 where the water is open sea (grown one cell so linear filtering doesn't cut surf off at the land edge), 0 otherwise. `detail` is clamped to 1–8. Throws `ArgumentNullException` if `generator` is null.

Everything else in the file (`OceanCells`, `Distance`, the `ChamferStep` constant) is `private` implementation detail.

## Dependencies

- Calls `TerrainGeneratorComponent.IsWaterAtPosition(Vector2)` (from `TerrainGeneratorComponent.cs`) once per fine-grid sample to build the water mask.
- Calls `TerrainGeneratorComponent.ResolveField()` (from `TerrainGeneratorComponent.cs`), which returns a `GeneratedTerrainField` (from `GeneratedTerrainField.cs`), then reads `GeneratedTerrainField.WaterSourceAtCell(Vector2I)` to decide which water cells are `"ocean"` (as opposed to lake/river) for the open-sea mask.
- Writes nothing to shared state — it returns a new `ImageTexture` each call. Callers (`TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `TerrainTileRendererComponent` — outside this batch) hold the result themselves as `_coastMap`.

## Notes

- `Distance` is a two-sweep chamfer distance transform (3/4 weights), not a true Euclidean distance or a queue-based BFS — the doc comment explains this is deliberate (diagonal steps at weight 4 vs. orthogonal at 3 keeps contours close to circular for two sweeps and no queue), and the code matches the comment.
- The `OceanCells` growth-by-one-cell step is explained in its own doc comment (linear filtering at sub-tile resolution would otherwise cut surf off at the land-side edge of a coastal tile) and the code does implement an 8-neighbour dilation.
- No accepted-but-unread settings, no dead code, no silent failure path beyond the explicit null-generator throw.
