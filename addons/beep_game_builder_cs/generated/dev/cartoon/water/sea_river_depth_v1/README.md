# Modular sea-mouth depth candidate

Open `square/mouth_depth_north.tscn` or its east/south/west equivalent. The
`isometric/` folder supplies the corresponding native diamond scenes. Each
scene contains 1-, 2-, 3- and 5-cell rivers entering a depth-painted sea.

## Native painting

Keep Water, Depth, SurfacePlane and CoastalDepthBinding together. Water uses the
existing prepared sea TileSet plus an opt-in depth-contact marker. Paint ordinary
sea with terrain Connect/Path tools; use its saved mouth patterns for explicit
inlets. A wide mouth consists of low-bank, repeatable middle and high-bank pieces,
not a fixed-width image. The representative widths are test cases, not new atlases.

Paint `shallow_sea` on Depth, including valid mouth cells. Erasing reveals the
underlying deeper sea. Ordinary river cells cannot receive sea-depth painting.
Each mouth requires the matching incoming river direction and width section,
ordinary sea behind it, and the correct adjoining bank/middle sequence. Missing
sections, mismatched flow, incompatible bank neighborhoods and unauthored tile
flips/alternatives are rejected rather than guessed or silently corrected.

Invalid edits remain available for correction. The derived depth field disables
itself, leaving original water visible. Depth changes visual shading only; it
does not change navigation, collision, elevation rules or existing logical maps.

The source control atlases and original river frames are retained. The native
16-frame, 1.2-second clock continues to drive river current and sea swell/surf.
The depth blend affects the sea-facing portion; upstream river pixels and the
river-facing mouth edge remain unchanged. No whole-image wobble is added.

## Evidence and remaining gates

`tests/terrain_sea_river_depth_probe.gd` tests 32 width/port/projection cases:
original frame data, native pattern repaint, invalid flow/flip/section rejection,
save/reopen, depth at every width, unchanged river and mouth-edge samples, fixed
banks/alpha, camera anchoring and animation. Captures and the report are in
disposable `generated/test/library/output/`.

The inspected samples are not user appearance approval. Other bank materials,
reverse/tidal flow, large-map cost/chunking and production engine-pack integration
remain incomplete. These development resources intentionally reuse dependencies
from `shared_sea_v1`; they are not a self-contained production pack. Provenance
is recorded in `manifest.json`. No original assets are replaced or deleted.

Rebuild after the shared sea and depth candidates:

```text
godot --headless --path . --script res://tools/terrain-library/package-sea-river-depth.gd
godot --path . --rendering-method gl_compatibility --script res://tests/terrain_sea_river_depth_probe.gd
```
