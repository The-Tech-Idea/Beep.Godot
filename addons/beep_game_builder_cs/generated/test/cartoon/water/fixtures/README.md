# Foundation fixture

Sources and candidate atlas resources: generated/dev/cartoon/water/staging.
Run from the repository root:

```text
node tools/terrain-library/build-foundation.mjs
node tools/terrain-library/check-foundation.mjs
node tools/terrain-library/build-preview.mjs
node tools/terrain-library/check-preview.mjs
node tools/terrain-library/verify-godot.mjs
```

Use a Node runtime with sharp and Playwright, or point TERRAIN_NODE_MODULES at its
node_modules directory. Set TERRAIN_BROWSER to the installed browser executable
and TERRAIN_GODOT to the installed Godot console executable.

Godot verification automatically runs tools/terrain-library/package-foundation.gd
in an isolated project under ignored output. It imports staging/runtime at the same
res:// paths, validates resources, then copies them to staging after a successful report.
Never run this packaging script against production or promote its output automatically.

Current checks: 47 unique legal masks, 1,024 horizontal and 1,024 vertical adjacent
neighbourhoods, atlas dimensions, opaque coverage, absence of chroma background,
water frame differences and fixed non-water banks. Browser tests cover all four
transitions, playback and desktop/mobile framing. Godot checks resource loading,
48 regions per atlas, animation frame counts and terrain-connect examples.

Pending before production: visual approval, richer fill alternatives, cliff/waterfall
connectors and integrated elevation example. No foam animation is claimed in this fixture.
