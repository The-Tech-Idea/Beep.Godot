# TerrainShaderSurface

Renderer-support utility: builds the blank `TileMapLayer` surface that a shader (sea, painted ground) paints per-pixel.

The sea and the painted-ground blend used to be drawn on a `Sprite2D` stretched over the map — a shader-only surface with no tile data, invisible to collision/navigation and not saved with the scene. `TerrainShaderSurface` gives that same shader a home inside the tile system instead: it builds a one-tile `TileSet` whose single tile is blank (every pixel gets overwritten by the shader) and fills a `TileMapLayer` with that tile from the origin out to a given size. The tile shape matters — a diamond for isometric layers, a rectangle for square ones — because isometric cells overlap, and a full rectangle per cell would double-blend the transparent edges into a visible lattice.

## Public API

- `static TileSet BuildTileSet(Vector2I cellSize, bool isometric)` — builds a one-source, one-tile `TileSet` sized to `cellSize` (clamped to at least 2×2). For `isometric: true` it rasterizes a diamond (`|dx|/w + |dy|/h <= 1`) into an RGBA8 image and leaves the rest transparent; for `isometric: false` it fills the whole tile white. Wraps the tile in a `TileSetAtlasSource` and delegates the actual `TileSet` construction to `TerrainTileSets.Create(size, isometric)` before adding the source.
- `static void Fill(TileMapLayer layer, Vector2I size)` — clears `layer` and sets cell `(0,0)` at tile source 0 for every cell in the `size.X × size.Y` rectangle starting at the origin `(0,0)`. Also sets `layer.RenderingQuadrantSize = max(size.X, size.Y) + 1`, forcing the whole filled area into one rendering quadrant so the per-fragment `VERTEX` position the shader reads doesn't reset at a quadrant boundary. Filling must start at the origin (not an arbitrary rect) because a negative-index cell falls in a different quadrant regardless of quadrant size; callers wanting margin move the layer node itself and pass the shift to the shader.

## Dependencies

- Calls `TerrainTileSets.Create(Vector2I, bool)` to build the base `TileSet` (adds its own one atlas source on top).
- Consumed by (not read by this file, but its callers): `TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `TerrainTileRendererComponent` — each builds a water/painted-ground `TileMapLayer` via `BuildTileSet` + `Fill`. No other file in this batch reads or writes through `TerrainShaderSurface`.

## Notes

- `Fill`'s quadrant-size fix is load-bearing, not cosmetic: the doc comment records a real regression (a 64-tile map re-drawing the same 16-tile patch in a 4×4 grid) that this one line prevents from recurring. Any future caller that reuses this layer for a bigger map without going back through `Fill` (e.g. resizing without refilling) would reintroduce it.
- `BuildTileSet`'s minimum cell size is silently clamped to 2×2 (`Mathf.Max(2, ...)`) with no warning; a caller passing a degenerate size gets a valid but wrong-sized tile with no diagnostic.
