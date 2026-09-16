# TerrainSeaSurface

Renderer-support utility (`internal sealed class`, one instance owned by each view that draws a
sea): the coast field a sea is measured from, the material it is painted with, and the geometry
it covers, as one object (VIEW-04, implemented 2026-09-16).

Two views had written this twice and a half. The tile view and the block view each resolved the
same coast field through their own caches, each adopted-or-created the same material, each set
the same block of surface uniforms, and then differed only in the SHAPE they put it on — a
batched tile layer top-down, an overscanned polygon in the block view. The painted view has no
surface at all: it mixes the sea into its own opaque ground pass, so it keeps only the shared
dials and is not a caller here.

[`TerrainWaterLook`](TerrainWaterLook.md) owns how the sea *looks*, for every view at once. This
owns how one view *draws* it. What each view still owns is genuinely its own: which coast window
it reads (`CoastRangeTiles`, `CoastDetail`), the transparent-sheet uniforms, and its geometry's
parameters.

## Public API

- `readonly record struct Sheet(float MaxOpacity, float ClarityTiles, float LakeOpacity, float ShoreOpacity)`
  — the uniforms `iso_water.gdshader` declares for a transparent sheet floating over seabed
  geometry. They describe a surface, not a look, so they stay per view: the painted composite has
  no equivalent, and the block view's water is read through more of it than the flat view's.
- `ShaderMaterial? Material` — the material the sea is painted with, or null before one is built.
- `bool HasCoast` — whether a coast field has been resolved; without one the sea has no shallows.
- `void ResolveCoast(GridCellDataComponent?, TerrainGeneratorComponent?, Vector2I origin, Vector2I size, int detail, float rangeTiles)`
  — reads the coastline this sea is measured from: `TerrainCoastField.LiveCache` when a map has
  live cells, otherwise `TerrainCoastField.BuildPixels` from the generator's field, otherwise null.
  The live cache keeps its own revision, which is what lets the render cache skip re-uploading an
  unchanged field. Both paths keep the resolved `TerrainCoastField.Pixels` beside the texture — the
  live path takes `LiveCache.CoastField`, the generated path uploads the pixels it just built — so
  a view that also reads depth on the CPU reads the same field it draws (VIEW-05, 2026-09-16).
- `float[] CellDistances(Vector2I size, float rangeTiles)` — how far each cell is from the
  waterline, in tiles, positive out to sea: the retained `Pixels` decoded through
  `TerrainCoastField.CellDistances`. Empty when no coast has been resolved, which is a caller's cue
  that it has no depth to shelve by. The block view's seabed is the one consumer today; VIEW-11
  (windowed block edits) is planned to be the second, over the coast `LiveCache` window.
- `void ForgetCoast()` — drops the coast field, texture and pixels together, so the next build
  resolves it again.
- `ShaderMaterial? BuildMaterial(Node owner, string shaderPath, TerrainWaterLook look, in Sheet sheet, Vector2I origin, Vector2I size, Vector2I cellSize, float coastRange, bool flatProjection, bool tileBatch, ShaderMaterial? authored = null)`
  — builds or refreshes the material in one order: the shader, the coast field, the sheet's own
  uniforms (`cell_size`, `tile_offset`, `tile_batch`, `flat_projection`, `max_opacity`,
  `clarity_tiles`, `lake_opacity`, `shore_opacity`), then the shared look through
  `TerrainWaterMaterial.Apply`/`ApplyTextures`. Returns null and warns when the shader will not
  load; warns — rather than failing silently — when the coast field is missing, because a null
  `coast_map` loses the shallows with nothing to show for it.
- `TileMapLayer TileBatched(Node2D owner, string layerName, Vector2I cellSize, Vector2I size, Vector2 position, bool isometric)`
  — the flat and isometric-tile geometry, through `TerrainAuthoring.EnsureLayer` and
  `TerrainShaderSurface.BuildTileSet`/`Fill`: one blank tile per cell, batched into a single
  rendering quadrant, placed at `TerrainLayers.ZFor(TerrainLayers.Sea)` with `ZAsRelative` off.
  The TileSet is rebuilt when the cell size or the square/diamond shape no longer matches.
- `static Vector2[] Polygon(Vector2I cellSize, Vector2I size, int margin)` — the block view's
  geometry: four corners of an overscanned quad in that view's own isometric projection.

## The field is kept, not just uploaded

Before VIEW-05 every `TerrainCoastField.Build` overload returned an `ImageTexture` and dropped the
pixels it was made from, so the only way to ask the field a question on the CPU was to build a
second one — or, as the block view did, to measure something else entirely. `BuildPixels` is now
the producer and `Build` uploads its result; this class holds both. Keeping the pixels costs no
extra computation on either path (the live cache already retained them, and the generated path now
uploads pixels it was building anyway) and it is what makes depth answerable from the same field
the sea is drawn from.

## The material is adopted, not replaced

A `ShaderMaterial` saved with the scene is duplicated and adopted rather than replaced, because
replacing it wiped every uniform hand-tuned in the Inspector on each reload. Everything
`BuildMaterial` writes still wins; only uniforms nothing writes survive by it. The block view
passes its existing `IsoWater` material as `authored` for exactly this.

## Every cell is filled, not just the wet ones

`TileBatched` fills the whole map rectangle. The shader draws the shore fade and the foam
slightly inland of the waterline, so filling only water cells would clip both at a cell boundary
and put a straight edge along every beach. The shader decides what is water; the geometry only
has to be there to rasterise.

## A lake ends in a line, the sea does not (FIX-14, 2026-09-16)

`iso_water.gdshader` masks the surface off at the shore with `on_water`, a `smoothstep` across the
signed distance, and it used one softness for every body of water. The open sea's edge is soft on
purpose — a beach, a wash and surf carry that transition, and a hard line there reads as a cut-out
— but a lake has none of those, so the same softness read as the lake's water smearing into the
ground around it. Both ends of that ramp are now mixed on `sea.open_sea`, the coast field's own
flag that already keeps surf off a lake: the sea transitions over roughly half a tile (`-0.35` to
`0.15`), a lake over about a tenth of one (`-0.08` to `0.02`). The sea's edge is unchanged.

`on_water` is itself scaled by `flat_projection`, so the crisper lake edge appears in the
**top-down** sea (the Tiles view). Drawn isometrically the mask is deliberately 1.0 — there the sea
runs on under the opaque land above it to fill the strip a coastal block's overhang would otherwise
leave bare — and a lake's edge in those views is carried by `lake_opacity` and depth instead, which
this change did not touch. The painted composite makes the same lake/sea distinction in its own
pass; see [TerrainPaintedRendererComponent](TerrainPaintedRendererComponent.md).

## Callers

| View | Geometry | `flatProjection` | `tileBatch` |
| --- | --- | --- | --- |
| Tiles (`TerrainTileRendererComponent`) | `TileBatched("TileWater", isometric: false)` at `BoundsOrigin * AtlasTileSize` | true | true |
| IsometricAutotile (`TerrainIsometricAutotileRendererComponent`) | `TileBatched("TileWater", isometric: true)`, then moved so its cell (0,0) lands where the terrain layer draws `BoundsOrigin` | false | true |
| Isometric block (`TerrainIsometricRendererComponent`) | `Polygon(...)` on the authored `IsoWater` `Polygon2D` | false | false |

The block view keeps its own `BuildWaterMaterial` as a three-line call into this class: its quad
is neither flat nor batched, and `RebuildRivers` duplicates the sea material for its opaque river
sheet, so the ordering (sea material first, rivers after) had to stay where it was.

## Dependencies

- `TerrainCoastField.LiveCache`/`RenderCache`/`BuildPixels`/`CellDistances` — the shared coast
  distance field, its bounded cubic display reconstruction, and the per-cell decode of its R
  channel. See [TerrainCoastField](TerrainCoastField.md).
- `TerrainWaterMaterial.Apply`/`ApplyTextures` — the one writer of the shared water uniforms.
- `TerrainShaderSurface.BuildTileSet`/`Fill` and `TerrainAuthoring.EnsureLayer` — the blank
  shader surface and the saved-with-the-scene layer it lives on.
- `TerrainLayers.ZFor(TerrainLayers.Sea)` — the shared stack position, not a per-view dial.

## Tests

`tests/terrain_water_material_probe.gd` reads the material this class builds in all four views;
`tests/terrain_view_parity_probe.gd` checks that each of the lab's four seas carries a
`coast_map` and the same shared uniforms. Dropping the autotile view's coast resolve fails
"the sea has no coast map, so it draws without shallows".

`CellDistances` is covered through its consumer: the seabed section of
`tests/terrain_iso_river_probe.gd` (an inland sea, a diagonal coast, open water past the field's
range) and the seabed check in `tests/examples/iso_layers.gd`, which asks the block view for
`SeabedDepthAt` rather than carrying its own metric. A contract-scan pin requires the block view to
read `_sea.CellDistances(size, CoastRangeTiles)`.
