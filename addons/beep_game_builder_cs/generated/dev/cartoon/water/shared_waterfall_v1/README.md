# Shared waterfall candidates

The narrow square and both isometric depth routes have also been migrated to
the connected clock and pass 256-sample GPU audits. Packagers use
`tools/terrain-library/connected-water-clock.gd` before activation: animated tiles
must match all 16 vertical bands and the 1.2-second period; static tiles must have
identical pixels in every band. Incompatible layouts fail before scene replacement.
The validation probe tests rejection without mutating the material or originals.

## Connected native clock

The new square width routes enable `connected_water_clock` on their lake/river
material. This selects from 16 equal vertical atlas bands using the sprite clock,
avoiding an observed frame-15/frame-0 mismatch at native animation wrap. Do not
enable this on differently arranged atlases. It is off by default elsewhere.
Generate independent test samples with `build-wide-route-reference.mjs`, then
run `terrain_wide_depth_routes_probe.gd`: 256 captures per width verify every
section against native river tiles and CPU color references. Current widths
1/2/3/5/9 pass with zero sampled error; other showcases still need migration.

## Continuous sampling

Development width routes now enable `shared_waterfall_enabled` on the native
clock shader. Set `waterfall_noise` to `motion_noise.res`, base bytes from
`motion_parameters.json`, rise 16/32/64, kind 0 narrow / 1 low bank / 2 middle /
3 high bank, projection 0 square / 1 north-south iso / 2 west-east iso, and offset
to the section's physical cross-coordinate (cell index times 64). Retain the
matching bank-role atlas region for alpha. Offset sampling no longer requires
extra exported frames; its declared multi-cell period is 1536px.

36 GPU reference assemblies pass all frames within one color byte, with fixed
alpha. Square connected widths through nine cells also pass motion checks.
Exact wide-route clock/seam acceptance, performance and terrain contacts remain
pending. The five-offset atlas exports below are references, not a shader limit.

Six SpriteFrames resources cover square and isometric projections at rises
16, 32 and 64 game pixels. See `projections_manifest.json` for frame dimensions,
profile IDs, atlas columns and connector anchors. These are water-only modules;
terrain and navigation remain separate.

Animation names use `direction.kind.offset_N`. Select narrow for width 1;
low_bank, repeated middle and high_bank for wider compositions. Offset N is the
section index across the waterfall, not an animation phase. This review export
provides offsets 0..4 only; do not wrap them to claim arbitrary-width support.
Isometric north_south sections step by (32,16); west_east by (-32,16).

All animations have 16 frames over 1.2 seconds. Connected native tiles require
the shared renderer-clock integration used by the route candidates, not separately
started AnimatedSprite playback. Model endpoint/loop/alpha tests and native
resource loading pass. GPU assembly checks cover all 36 projection/direction/
rise/width cases and all 16 frames: zero source-composition mismatches or alpha
changes. These use explicit frame overrides; actual clock synchronization is
tested separately in route probes. Terrain contacts, arbitrary-width support and
visual approval remain pending. Old sheets are unchanged; no production approval.
