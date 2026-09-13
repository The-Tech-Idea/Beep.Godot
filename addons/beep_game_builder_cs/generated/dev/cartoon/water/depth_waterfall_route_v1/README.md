# Connected depth waterfall route

## Isometric width routes

`isometric/widths_manifest.json` lists both flow directions at widths 1/2/3/5/9.
These scenes expand native plateau and face cells, retain the 64px visual rise,
and connect shared-clock waterfall sections to native lake mouths and depth.
Build with `package-isometric-wide-routes.gd`; validate with
`terrain_isometric_wide_routes_probe.gd`. All ten cases pass anchors, footprint,
clipping, fixed terrain/alpha and save/reopen. Full-route phase inference also
passes 256 captures per case against independent projected CPU samples: all 16
frames match within one color byte. Generate those disposable references first
with `build-wide-route-reference.mjs`. Natural cliff/bank contact review remains
pending. Existing narrow sources are retained.

## Isometric candidates

`isometric/north_south.tscn` and `isometric/west_east.tscn` now use native
`LakeWater` and `LakeDepth`, replacing the old baked lake preview in these new
scenes only. Native depth painting updates the displayed route. Each scene has
25 lake cells plus its directed river stem; existing source artwork is retained.
Build with `package-isometric-depth-route.gd`; GPU-test with
`tests/terrain_isometric_depth_route_probe.gd`. Both directions pass native depth
editing, fixed terrain/alpha, motion and save/reopen. The rendered phase audit
also passes: 64 captures per direction uniquely match all 16 native river,
river sprite and waterfall frames with zero pixel error. Visual approval remains
pending. The root manifest describes the square route;
`isometric/manifest.json` describes these separate candidates.

## Square checkpoint

Development candidate, square cartoon projection only. Open `square/route.tscn`
in Godot: upper river, 64px granite waterfall, lower river, native river cells,
lake inlet and depth-painted lake. Paint shallow regions on `LakeDepth`;
unpainted lake remains deep. The binding keeps ordinary river outside depth paint.

Original terrain and animation atlas pixels are retained. Three sprite sections
use `terrain_native_clock_sprite.gdshader`, selecting 16 frames over 1.2 seconds
on renderer TIME to match native tiles. The packager validates top-left pivots
and uniform atlas frame strides. Existing scenes are not replaced.

Rebuild: `tools/terrain-library/package-depth-waterfall-route.gd`.
GPU test: `tests/terrain_depth_waterfall_route_probe.gd`.
64 rendered samples cover all 16 frames with matching native river, sprite river
and waterfall frames. Fixed terrain/alpha, depth, anchors and save/reopen pass.
Reports and captures belong to disposable `generated/test/library/output`.

Not production-ready or visually approved. Isometric depth integration, wider
routes, visual refinement, other banks and self-contained packaging remain open.
