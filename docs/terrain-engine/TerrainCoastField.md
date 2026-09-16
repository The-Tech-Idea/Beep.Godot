# TerrainCoastField

Support utility in the terrain pipeline, called by every renderer (flat tile, isometric, painted) that draws water — not itself a generation stage or a component.

`TerrainCoastField` builds a single-texture "distance to waterline + is-open-sea" field for a generated map, sampled below tile resolution so the coastline it encodes is curved rather than staircased. Renderers use this texture (not the raw water/land grid) to shade a beach a consistent width, shelve depth away from shore, and draw surf that runs out from the actual waterline instead of stopping at a tile edge. It exists as one shared static method precisely so the flat and isometric views, which each render the same generated sea independently, agree on where the coast is — two independently-derived coastlines would visibly disagree at the boundary between the two views.

## Public API

`DefaultDetail = 12` and `DefaultRangeTiles = 5` are the common renderer defaults.
For matching coast fields across views, use the same detail/range overrides as well
as the same cell source, bounds and origin. Different sampling resolutions can
produce different rounded corners even when they read the same terrain grid.

- `Build(generator, size, detail, rangeTiles)` returns an RGBAF distance texture.
  Detail is clamped to 1-16 and reduced to respect the raw sample budget. R is
  signed distance to any water; B is distance to ocean; G is the grown ocean
  mask for surf. A distinguishes generated fine geometry from manually painted
  whole-cell geometry. Distance is normalized around 0.5 and clamped by range.

- `BuildPixels(generator, size, detail, rangeTiles)` (internal) is the producer
  `Build` now uploads: it returns the same RGBAF field as managed `Pixels` before
  it becomes an `ImageTexture`, and `Build` is `BuildPixels(...).Upload()`. Every
  `Build` overload used to return the texture and drop the pixels, so a caller
  that also reads the field on the CPU had nothing to read and would have had to
  build a second coastline. `BuildLivePixels` and `BuildLakePixels` are the live
  and lake equivalents, and `LiveCache.CoastField` hands back the live path's
  pixels the same way.
- `CellDistances(Pixels field, Vector2I size, float rangeTiles)` (internal)
  decodes one distance per **cell** from the R channel: the sample nearest the
  cell's centre (samples sit at `(x + 0.5) / detail` tiles, so cell `y`'s centre
  is sample `y * detail + detail / 2`), read back as `(encoded - 0.5) * 2 * range`
  tiles. Positive is water, negative is land, and the value saturates at the range
  the field was built with — a caller wanting bands wider than that must build the
  field with a wider range. `field.Size / size` gives the detail, so a field built
  at any sampling resolution decodes against the same cell grid.

- `Build(GridCellDataComponent cells, Vector2I origin, Vector2I size, int detail, float rangeTiles)` builds from live water membership. Boundary-connected water is ocean; enclosed water is inland.
- `LiveCache.Resolve(...)` offers bounded, per-view reuse for the live overload.
  It compares cell water flags, immutable fine-patch identities, dimensions,
  detail and range. Flag/crop changes need not regenerate coast pixels. Explicit
  terrain painting can change the coast even if the cell remains dry.

Distances use a separable squared Euclidean transform, then a half-sample edge
correction. Floating-point pixels avoid the old 8-bit distance quantization.

Both of those come from [`TerrainEuclideanDistance`](TerrainEuclideanDistance.md),
and since VIEW-07 (2026-09-16) the correction is not written here at all. The R
channel is `TerrainEuclideanDistance.Signed`, which applies it internally; the B
channel — distance to open ocean for any sample that is not itself open ocean —
calls `TerrainEuclideanDistance.ToTiles` directly, on both write paths: the live
cache's windowed rewrite (`FieldCache.WriteWindow`) and the whole-field build
(`BuildPixels`).
Sharing that rule with the shoreline stage is what keeps the beach the generator
cut and the beach the painted view draws the same beach, even though the two work
at different sample resolutions.

## Water depth is this file's fact (VIEW-05, 2026-09-16)

How far a cell is from the waterline has one owner, and it is the R channel here.
Every sea already draws from it: the shared include shades `shallow_tiles`/
`deep_tiles` against `coast_map`, and the painted view's stylised path reads the
same texture. The isometric block view used to measure its own depth instead — a
four-neighbour sweep out from land, which counts Manhattan steps — while the water
drawn over that bed shaded by this field, which is Euclidean. Along a diagonal
coast the two disagreed by up to a step, so the seabed's material band changed
where the water's tint did not. `CellDistances` closed that: the view now reads
depth from the very field its own sea is drawn from, at no second computation,
because `BuildPixels` keeps what `Build` used to throw away.

Depth is not something the generator records. `shallow_water`/`deep_water` are a
*wading classification* for gameplay — see [TerrainBiomeStage](TerrainBiomeStage.md)
— not a distance, and nothing should read them as one.

## Display Reconstruction

The painted, tiled and elevated-isometric renderers pass the raw field through
an internal per-view `RenderCache` before binding `coast_map`. The raw `Build`
output is unchanged. Generated RGBAF contours first receive a separable five-tap
binomial filter with a roughly one-third-cell radius. Manual-cell geometry is
exempt. Native cubic image resizing then produces a twice-resolution
display texture, sampled with the existing linear GPU sampler. This does not
blur ground artwork or change cell IDs, navigation, placement or saved patches.

Two replicated border pixels give consistent clamp behavior. RGBA8 processing
expands constant blocks with nearest sampling and reconstructs varying 64-pixel
blocks with shared two-pixel halos. A regression compares all output bytes to a
whole-image native cubic resize, including partial blocks and block boundaries.

The cache reuses immutable source identity and cell dimensions. It returns the
raw texture for one-sample-per-cell input. When doubling exceeds 4,194,304 pixels
or source dimensions exceed 2048, generated RGBAF input within the 1,250,000
raw-pixel budget is still smoothed at native resolution. Large maps must not
silently skip contour smoothing. Thus display dimensions can be twice `CoastDetail`'s raw
dimensions, up to 4096 per axis and 64 MiB for RGBAF. Unchanged live water/patch
data also reuse `LiveCache` in all three views. No shared global cache exists.

`tests/terrain_coast_filter_probe.gd` checks constant edges, allocation bounds,
source isolation, block equivalence, coarse-input passthrough, a CPU cubic
reference and GPU analytic-curve accuracy. `--profile` reports first-build and
cached timing. Large cold rebuilds can still stall; caching does not solve that.

`BuildLake` uses explicit fine lake membership, not boundary flood-fill or the
ocean field. The painter caches it with its terrain snapshot and applies the
same bounded reconstruction. Its R channel carries lake distance; ocean bands
continue to use the original coast texture's B channel.

## Source Dependencies

- Resolves `TerrainGeneratorComponent.ResolveField()` once per generated build. All fine-grid water samples and ocean-cell queries read that same `GeneratedTerrainField`, avoiding repeated settings construction/comparison. A subsequent build resolves current settings again, so changing the seed still changes the coastline.
- Writes nothing to shared state — it returns a new `ImageTexture` each call. Callers hold the result themselves as `_coastMap`: the painted view directly, and the tile, autotile and block views through the one [`TerrainSeaSurface`](TerrainSeaSurface.md) each of them owns, which since VIEW-05 keeps the resolved `Pixels` beside the texture so it can answer `CellDistances` without a GPU readback or a second build.

## Notes

- Generated live cells retain bit-packed sub-tile water patches. These retain
  their fine borders, with bilinear sampling and compact radial centre correction
  toward gameplay classification. Patches are saved with the grid, not recovered from whichever
  generator settings happen to be in the Inspector.
- Cells without patches use water membership interpolated bilinearly between cell centres before
  thresholding the fine-grid mask at 0.5. This rounds outer/inner corners instead
  of repeating each square cell at higher resolution. Every cell centre retains
  its live classification, and straight one-cell channels keep their width.
  Boundary samples clamp to the nearest cell; no outside ocean is invented.
- The reconstruction changes only the visible coast field. Navigation, placement,
  terrain IDs, elevation and ocean flood connectivity still read the live grid.
  Fine patches recover generated coastline detail that cell-only reconstruction
  previously discarded. They do not generate new landforms or move navigation cells.
- `CreateLiveWaterSampler` provides the same pre-shader membership snapshot to
  vegetation placement. It does not include animated surf or shader shoreline noise.
- `tests/terrain_live_coast_shape_probe.gd` checks all 16 local patterns at detail
  2/4/8 and shifted origins, isolated cells, and one-cell water/land channels. Its
  GPU path checks the production painted waterline against native cell centres
  at two zoom levels. The lab probe compares identical live coast bytes across
  painted, tiled and isometric views after a live water edit.

- Raw distances are Euclidean; filtered display distances are approximate.
  Smoothing can move a boundary relative to a gameplay cell centre. The GPU
  generated-centre regression at seed 31415, cell (21,3), remains open; the
  approved visual baseline alone does not certify gameplay alignment.
- The `OceanCells` growth-by-one-cell step is explained in its own doc comment (linear filtering at sub-tile resolution would otherwise cut surf off at the land-side edge of a coastal tile) and the code does implement an 8-neighbour dilation.
- `tests/terrain_generated_coast_probe.gd` verifies byte-identical coast output for two seeds through the standalone generator/view path, without relying on an external game.
