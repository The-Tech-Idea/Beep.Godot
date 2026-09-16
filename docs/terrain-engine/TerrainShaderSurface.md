# TerrainShaderSurface

Renderer-support utility: builds the blank `TileMapLayer` surface that a shader (sea, painted ground) paints per-pixel.

The sea and the painted-ground blend used to be drawn on a `Sprite2D` stretched over the map — a shader-only surface with no tile data, invisible to collision/navigation and not saved with the scene. `TerrainShaderSurface` gives that same shader a home inside the tile system instead: it builds a one-tile `TileSet` whose single tile is blank (every pixel gets overwritten by the shader) and fills a `TileMapLayer` with that tile from the origin out to a given size. The tile shape matters — a diamond for isometric layers, a rectangle for square ones — because isometric cells overlap, and a full rectangle per cell would double-blend the transparent edges into a visible lattice.

## Shader contract: replaced in colour, never in modulate

A surface shader replaces the blank tile's colour, not the modulate it is drawn with. Godot hands a
`canvas_item` fragment the texture sample × vertex colour × the item's `Modulate`, `SelfModulate`
and every parent's modulate in `COLOR`. So every surface shader captures that input first
(`vec4 modulate = COLOR;`) and multiplies its result by it (`terrain_splat.gdshader`,
`iso_water.gdshader`). For the isometric diamond tile that input also carries the cutout. A shader
that writes `COLOR` without it drops a tint or fade set on the renderer node.

The scene's `CanvasModulate` (`AmbientController`: day/night, weather, seasons) never travels through
`COLOR`. Godot multiplies it in after the fragment function, and skips it for an item whose shader
declares `render_mode unshaded`. No terrain shader is unshaded; the tile view's ground detail and the
natural-terrain art were, and stayed daylight at night. Verified on Compatibility and Forward+
(VIEW-14, 2026-09-15). Guarded by `tests/terrain_item_modulate_probe.gd` (rendered) and the
terrain-shader pin in `tests/addon_contract_scan.ps1`.

## Public API

- `static TileSet BuildTileSet(Vector2I cellSize, bool isometric)` — builds a one-source, one-tile `TileSet` sized to `cellSize` (clamped to at least 2×2). For `isometric: true` it rasterizes a diamond (`|dx|/w + |dy|/h <= 1`) into an RGBA8 image and leaves the rest transparent; for `isometric: false` it fills the whole tile white. Wraps the tile in a `TileSetAtlasSource` and delegates the actual `TileSet` construction to `TerrainTileSets.Create(size, isometric)` before adding the source.
- `static void Fill(TileMapLayer layer, Vector2I size)` reconciles the origin-based rectangle with source 0, atlas coordinate `(0,0)`, alternative 0. Correct cells remain untouched; holes and changed tiles are repaired, and cells outside the requested extent are erased. It updates `RenderingQuadrantSize` only when necessary to keep the surface in one quadrant. Sizes clamp to at least one cell per axis. Callers wanting a nonzero world origin move the layer rather than filling negative rendering cells.

Repeated shader-data refreshes therefore do not clear and recreate tile geometry.
The normal path checks `GetUsedRect()` and the count returned by
`GetUsedCellsById(0, Vector2I.Zero, 0)`. Matching bounds and the complete matching
tile count prove the rectangle is valid without three managed/native queries per
cell. This still performs a native bulk scan, not constant-time cached validation.
If either check fails, the helper repairs individual tiles and removes outliers.

The [Godot TileMapLayer API](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html#class-tilemaplayer-method-get-used-cells-by-id)
supports simultaneous source, atlas and alternative filtering. Checking only
bounds/count without those filters would incorrectly accept a substituted tile.
The painted-origin probe covers wrong sources, atlas coordinates, alternatives,
holes, outliers and bounds changes, as well as unchanged geometry notifications.

## Dependencies

- Calls `TerrainTileSets.Create(Vector2I, bool)` to build the base `TileSet` (adds its own one atlas source on top).
- `TerrainPaintedRendererComponent` calls `BuildTileSet`/`Fill` directly for its `SplatSurface`.
- Every sea reaches them through [`TerrainSeaSurface.TileBatched`](TerrainSeaSurface.md) since VIEW-04 (2026-09-16): the Tiles view's square `TileWater`, and the IsometricAutotile view's diamond one. The block-isometric view's sea is an overscanned `Polygon2D` and uses neither.

## Notes

- `Fill`'s quadrant-size fix is load-bearing, not cosmetic: the doc comment records a real regression (a 64-tile map re-drawing the same 16-tile patch in a 4×4 grid) that this one line prevents from recurring. Any future caller that reuses this layer for a bigger map without going back through `Fill` (e.g. resizing without refilling) would reintroduce it.
- `BuildTileSet`'s minimum cell size is silently clamped to 2×2 (`Mathf.Max(2, ...)`) with no warning; a caller passing a degenerate size gets a valid but wrong-sized tile with no diagnostic.
