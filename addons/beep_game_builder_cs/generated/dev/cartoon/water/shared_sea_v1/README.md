# Shared sea surface candidate

Development only. Open either projection's `sea_review.tscn` or
`river_sea_review.tscn` in Godot. The scenes use native TileMapLayers and do not
require the C# terrain engine. Existing candidates, maps and renderer defaults
are unchanged.

## What changed

The old sea atlas repeated the same two crests in every cell. This version keeps
coast shape, bank pixels and alpha in data atlases while shading wavelets in a
shared logical map plane. Wave positions, lengths and lifetime offsets vary
across the map. The shader advances only when the native atlas frame advances;
there is no separate TIME clock. The full loop remains 16 frames / 1.2 seconds.

These PNGs are **control data, not display artwork**. Retain all three together:

- `shared_sea.tres`: 47 native coast configurations, four explicit inlet profiles,
  twelve repeatable wide-mouth modules, sixteen native patterns and existing
  river sections. Sources 0/1/2 retain their previous IDs; width modules use 3.
- The Water layer's `terrain_shared_sea.gdshader` material, including its grass
  texture and source water color.
- `TerrainSurfacePlane.gd`, with the correct projection and cell dimensions,
  bound to the Water layer. Its repeat setting controls grass repetition, not
  the physical dimensions of the water motion.

Keep atlas sampling nearest. Do not compress, recolor or linearly filter control
data. The shader's marker values are reserved for this pack; do not apply it to
arbitrary unrelated artwork. The shader retains ordinary river pixels, authored
bank color and alpha. The exported control format is recorded in `manifest.json`.

Native mapping remains the same as `river_sea_connectors_v1`: use terrain
Connect painting for source 0, then explicitly place a source 1 inlet last.
Columns 0/1/2/3 select the external north/east/south/west river port. Source 2
river rows 0/3/1/2 match those incoming directions, using column 0 for the narrow
channel. Repainting terrain over an explicit inlet may replace it. These scenes
do not provide automatic flow inference or connector protection.

## Wider mouths

`mouth_widths_north.tscn`, `mouth_widths_east.tscn`, `mouth_widths_south.tscn`
and `mouth_widths_west.tscn` each demonstrate widths 1, 2, 3 and 5. Both
projection folders contain separately sampled resources.

Use the saved `mouth_{port}_{width}.tres` patterns for those widths, or assemble
larger mouths from source 3. Its columns 0/1/2 are the low-bank/middle/high-bank
modules; rows 0/1/2/3 select north/east/south/west river ports. Cross-channel order
is increasing logical x for north/south and increasing logical y for east/west.
Use the low bank once, repeat the middle as needed, then use the high bank once.
A two-cell mouth uses only the two banks; a one-cell mouth still uses source 1.
Repeatable middles do not contain additional internal bank lines.

Match the upstream source 2 sections to the same cross-channel order: columns
1/2/3 for low bank/middle/high bank, with river direction rows 0/3/1/2 matching
north/east/south/west inlet ports. Place explicit mouth patterns after painting
the sea. No automatic width or flow inference is provided.

## Evidence and limits

### Depth painting

Open `depth_review.tscn` in either projection folder. Keep the `Water`, `Depth`,
`SurfacePlane` and `DepthGuard` nodes together. Paint `shallow_sea` on the Depth
layer using native Connect/Path tools. Erasing depth reveals the underlying deep
sea; the explicit background region is also deep water. All 47 boundary masks
and the deep background use the same 16-frame clock. No extra bank lines are
drawn at depth changes. The showcase contains an interior shelf, a deep hole
and a separate shallow patch.

Depth painting currently belongs only on fully open sea cells. The guard rejects
coastline, land, river/mouth connectors, missing support and mismatched layer
transforms or projections. It suppresses only the depth shader output, reports
a configuration warning and keeps the painted cells intact. Fixing the placement
restores the overlay. It does not change the water map, layer visibility,
collision or navigation; depth is visual, not a gameplay elevation change.

Validation is deferred and batched. Native cell-change notifications were not
reliable for erasure in the tested runtime, so this opt-in guard also compares
native map-data snapshots. Peering-bit validation runs only after changes, but
snapshot comparison cost on large maps remains a production gate. Bulk loaders
can call `DepthGuard.refresh()` after loading; save/reopen and ordinary erasure
are covered by the native test. See the [Godot TileMapLayer reference](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html)
for native change notifications and deferred layer updates.

The depth control PNG is data, not a standalone image tile sheet. Keep its
scene-local shared sea material and surface plane. Its marker is blue value 249;
red encodes the shallow-water blend. The older guard scene remains open-water
only. For grass-coast contacts use the separate coastal scene below; river/depth
connectors and lake-depth artwork are still missing.

### Coastal depth painting

Open `coastal_depth_review.tscn` in the desired projection folder. Keep Water,
Depth, SurfacePlane and CoastalDepthBinding together. Paint `shallow_sea` on
Depth with native terrain tools. Its overlay is suppressed; a compact cell
lookup applies the same mask only inside the existing sea water, leaving banks,
grass, alpha and surf boundaries intact. This supports grass coastlines, coves,
headlands and islands without a separate atlas for every coast/depth combination.

The binding rejects land, missing sea, river connectors, mismatched transforms
and fields larger than its configured limit (default 1024 cells per side).
Invalid edits remain available for correction; the original sea stays visible.
No navigation, collision or gameplay elevation is inferred. Save/reopen rebuilds
the derived field. Chunking and large-map snapshot cost are not yet validated.

`terrain_coastal_depth_native_probe.gd` checks 650 valid neighborhoods per
projection, invalid-paint recovery, size limits and save/reopen.
`terrain_coastal_depth_render_probe.gd` checks exact overlay equivalence,
camera anchoring, water movement, fixed banks and alpha. These do not establish
visual approval or production readiness.

- `tests/terrain_shared_sea.test.mjs`: loop closure, non-per-cell repetition,
  wavelet partition continuity, marker encoding and river/sea control contacts.
- `tests/terrain_lake_render_probe.gd -- --sea --shared-sea`: native coastline
  animation and stationary grass/banks in both projections.
- The same probe with `--river-sea --shared-sea`: north-inlet integration.
- `tests/terrain_sea_mouth_widths.test.mjs` checks actual neighborhood masks,
  upstream/downstream contacts, section joins, static banks and full loops.
- `tests/terrain_sea_mouth_widths_probe.gd` exercises all 32 width/direction/
  projection cases, saved patterns, erasure/repaint and save/reopen. Its report
  is `sea_mouth_widths_render.json` in disposable output.
- `tests/terrain_shared_sea_plane_probe.gd`: pinned-frame stability, camera and
  map movement, repeat-setting independence, phase advancement and tiled versus
  continuous interior shading. The scaled isometric reference's outer alpha
  staircase differs from native cell rasterization; its full comparison is
  reported separately, and only that outer silhouette is excluded from the
  interior field check.
- `tests/terrain_sea_depth.test.mjs`: all realizable shallow/deep edge pairs.
- `tests/terrain_sea_depth_native_probe.gd`: 47 native masks per projection,
  erasure, blocked-placement recovery, retained paint and save/reopen.
- `tests/terrain_sea_depth_render_probe.gd`: depth contrast, matching deep holes,
  animated water, stationary banks and unchanged sea beneath invalid depth paint.

Routine captures and reports go to `generated/test/library/output/`. These
checks are not visual approval. Surf/current/depth appearance, other banks,
river depth contacts, lake depth, engine-pack integration, performance
on target devices and production promotion remain pending.

## Rebuild

At the project root, with `TERRAIN_NODE_MODULES` pointing to dependencies that
include `sharp`, run:

```text
node tools/terrain-library/build-shared-sea.mjs
godot --headless --path . --script res://tools/terrain-library/package-shared-sea.gd
godot --headless --path . --script res://tools/terrain-library/package-sea-depth.gd
godot --headless --path . --script res://tools/terrain-library/package-coastal-depth.gd
```

The editable masters for this technical candidate are the control generator and
shader, not a new painted source sheet. The original source image is retained and
hashed for its water color. Rebuilding replaces generated candidates and sample
layouts here; keep authored maps elsewhere. No production artwork is changed.
