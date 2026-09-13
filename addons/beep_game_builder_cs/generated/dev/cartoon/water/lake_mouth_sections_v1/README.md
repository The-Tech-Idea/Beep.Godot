# Modular lake mouths

Development candidate. Both `square/` and `isometric/` contain `widths_north_inlet`
and the corresponding outlet/east/south/west scenes. Each demonstrates widths
1, 2, 3 and 5 with visual depth painting.

## Pieces and native painting

The TileSet retains the existing lake, narrow mouths and river section sources
0/1/2. Source 3 adds 24 modules: low bank, middle and high bank for each of eight
explicit inlet/outlet profiles. Wider openings repeat the middle rather than
requiring another fixed-shape image. Existing narrow artwork is unchanged.

There are 32 native patterns per projection: eight earlier narrow river-contact
patterns and 24 wide-mouth patterns. Individual named pattern resources also
include width 1. Wide patterns place the mouth band only; provide the matching
directed river sections outside and lake water behind/beside it.

Use native terrain tools for ordinary lake boundaries and the separate Depth
layer. Depth painting is allowed on compatible mouth cells, not ordinary river
cells. The binding checks inlet/outlet role, direction, matching width section,
bank neighborhood and adjoining low/middle/high sequence. It rejects mismatches
and unauthored flips without deleting the edit. Invalid fields fall back to the
original water appearance until corrected.

Painted shallow mouths continue their visual mask toward the unchanged shallow
river. Native Depth paint and gameplay river data are not extended. This avoids
an artificial dark strip where a shallow mouth otherwise treats the unpaintable
river neighbor as deep water. Erasing mouth depth still allows deep lake shading.

## Animation and provenance

The 16-frame, 1.2-second local lake ripple texture is retained. River-facing
pixels match the appropriate original directed river section; lake-facing
regions are checked against the existing lake atlas during export. Both flow
roles are authored explicitly. No sea waves, extra TIME clock or whole-image
wobble is introduced. Depth changes shading, not navigation or gameplay height.

`manifest.json` records original source and lake-frame hashes, all modules and
their native regions, scene links and pending gates. The exported atlas is
calibrated from existing artwork and declared connection geometry; no separate
image is generated for each width. Dependencies still include development packs,
so this is not a self-contained production resource.

## Rebuild and review

With `TERRAIN_NODE_MODULES` pointing to a runtime containing `sharp`:

```text
node tools/terrain-library/build-lake-mouth-sections.mjs
godot --headless --path . --script res://tools/terrain-library/package-lake-mouth-sections.gd
node --test tests/terrain_lake_mouth_sections.test.mjs
godot --path . --rendering-method gl_compatibility --script res://tests/terrain_sea_river_depth_probe.gd -- --lake-wide
```

The exporter checks the lake-facing pixels in both projections. Node tests cover
profile validity, upstream pixels, exact internal joins, fixed banks and loop
closure. The Godot probe covers native pattern repaint, invalid section/flow/flip
rejection, save/reopen, depth at all widths, preserved river edges, camera anchoring
and animation. Reports/captures go to disposable test output.

All 64 width/port/flow/projection cases pass, including separate shallow and deep
states. Corrected square/isometric samples were visually inspected; these checks
are not user approval or production promotion.

User appearance approval, sand/rock banks, large-map cost/chunking and production
engine integration remain pending. Existing approved artwork is not replaced,
deleted or promoted by this candidate.
