# River-to-sea inlet candidates

Development only. These resources are not approved artwork or production packs.

Open `square/river_sea_review.tscn` or `isometric/river_sea_review.tscn` in Godot.
The scenes and TileSets work without the C# terrain engine. Existing maps and
renderer defaults are unchanged.

## Native mapping

- Paint the sea using source 0 and its prepared corner-and-side terrain.
- Place an incoming narrow river using source 2.
- Place its mouth last using source 1, row 0: north/east/south/west inlet in
  columns 0/1/2/3 respectively. Names describe the external river port, not the
  direction in which water leaves the mouth.
- The corresponding source 2 river rows are 0/3/1/2; column 0 is the narrow
  channel. Only one-cell openings at zero rise are supported here.
- These mouth tiles deliberately have no automatic terrain assignment. Native
  terrain repainting over a mouth can replace it; reapply the selected connector
  after editing the coast. Flow inference and automatic connector placement are
  not implemented by these standalone resources.

Each animation has 16 frames over 1.2 seconds. Projection-specific atlas regions
are 64x64 or 64x32, with no runtime green backing. Shared grass shading is inherited
from the sea review material and its surface-plane node.

## Verification and limitations

`tests/terrain_river_sea.test.mjs` checks all four inlet profiles, river contacts,
sea contacts, fixed bank silhouettes and exact loop closure. Export checks compare
286,720 pixels against the existing sea atlas. The generic GPU probe accepts
`--river-sea`; it loads saved scenes and checks motion and stationary terrain for
the north inlet in both projections. Other orientations still need GPU review.

The finite review patch is not a complete coastline map. Repeated sea crests and
the short current-to-surf blend need visual refinement. Wide mouths, tides,
depth transitions, sand/rock banks and integrated waterfall routes are missing.
Do not treat numerical seam checks as visual approval.

## Rebuild

1. Set `TERRAIN_NODE_MODULES` to a Node dependency directory containing `sharp`.
2. Run `node tools/terrain-library/build-river-sea-connectors.mjs` at project root.
3. Run Godot with `--headless --path . --script res://tools/terrain-library/package-river-sea-connectors.gd`.

Rebuilding replaces these generated development candidates and the example
layouts. Keep user-authored maps elsewhere. Existing approved artwork is not
modified, relocated or deleted.
