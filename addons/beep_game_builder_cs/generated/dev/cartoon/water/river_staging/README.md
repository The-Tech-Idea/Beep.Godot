# River current candidate

Separate from lake water, sea surf and waterfall animation. Not approved production.

The 64px atlas has 12 directional modules: four straights and eight directed bends.
Each has 16 frames over 1.2 seconds. The stationary substrate uses the same material
as the lake candidate, but only river highlights travel along channel coordinates.
No bank or base-texture scrolling is used. Flow phase matches at module endpoints.

Use `godot/river_current.tres` with explicit directional placement. `flow_profile`
names the inlet then outlet, e.g. north_east. Do not rely on terrain auto-connect to
choose direction: two tiles with identical banks can carry opposite currents.
`godot/river_current_review.tscn` shows two connected bends with fixed banks.

The atlas layout is four columns, three rows per frame, with 16 frame blocks stacked
vertically. PNG and Godot metadata are generated together. The HTML review embeds
the same atlas for local-file playback.

Rebuild: `node tools/terrain-library/build-river.mjs`.
Tests: `node --test tests/river_motion.test.mjs`.
Godot: `node tools/terrain-library/verify-godot.mjs --river`.
Set TERRAIN_NODE_MODULES for sharp and TERRAIN_GODOT to the console executable.

Pending: wide rivers, junctions, obstacle wakes, inlet/outlet connections to waterfalls,
and visual approval. The lake files and existing waterfall art are not modified.
