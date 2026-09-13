# River contacts for depth-painted lakes

Development candidate, not production-approved. Open any of the eight named
scenes in `square/` or `isometric/`: north/east/south/west inlet and outlet.

## Native mapping

The supplied TileSet has these sources:

- 0: lake bank/fill terrain. Use native terrain Connect/Path painting.
- 1: eight explicit mouth profiles, columns 0..7 in manifest order.
- 2: preserved river sections. This contact candidate supports narrow profiles
  only, column 0, rows north-south, south-north, west-east, east-west.

Eight saved native patterns contain a mouth plus three matching river cells.
Place the selected pattern at the corresponding edge of a lake. The mouth
requires lake water behind and beside it and the matching river flow on its
external side. Select inlet/outlet explicitly; the binding does not guess flow.
Patterns provide the contact/river pieces, not the surrounding lake footprint.

Paint `shallow_lake` on the separate Depth layer, including a valid mouth cell.
Erasing depth reveals deep lake water with the existing shallow shoreline band.
Depth painting on ordinary river cells is rejected, not silently interpreted.
Keep Water, Depth, SurfacePlane and CoastalDepthBinding together.

Original river and mouth frame pixels are preserved. The lake-depth shader
leaves ordinary river tiles unchanged, applies zero tint at the river-facing
mouth edge, and fades into lake depth between 0.5 and 8 logical pixels from
that edge. Lake ripples and river current keep their original synchronized
16-frame, 1.2-second animation. No independent TIME clock or whole-image wobble
is added.

Wrong flow, missing lake contact, incompatible bank neighborhoods and unauthored
tile flips/alternatives are rejected. Invalid edits stay available for correction;
the derived depth field is disabled and original water remains visible. This
does not change collision, navigation or logical gameplay elevation.

## Verification and limits

`tests/terrain_lake_river_depth_probe.gd` exercises all 16 projection/profile
cases. It checks retained frame pixels, native patterns, wrong-flow/flip rejection,
missing contacts, invalid river-depth paint, save/reopen, fixed banks/alpha,
unchanged river pixels and mouth edge, camera anchoring and animation.
Reports and captures are disposable under `generated/test/library/output/`.

Visual samples were inspected; user appearance approval remains pending. Wider
mouths, sand/rock banks, sea-depth contacts, chunking/large-map performance and
production engine-pack integration remain incomplete. Sources and the original
manifest hash are recorded in `manifest.json`. Existing assets are not replaced.

Rebuild after the original river/lake and lake-depth candidates:

```text
godot --headless --path . --script res://tools/terrain-library/package-lake-river-depth.gd
godot --path . --rendering-method gl_compatibility --script res://tests/terrain_lake_river_depth_probe.gd
```
