# Terrain library rebuild

## Granite contact source candidate - 2026-09-13

`waterfall_contacts_v1` now contains a new six-piece green-backed source for
left/right wall edges, lip ends and bottom contacts. Runtime bounds, pivots and
protected water half-planes were declared before generation. Built-in generation
used a textual style description after local reference loading failed. The source
was preserved with its hash and prompt; it is not a validated atlas or approved
appearance. Crop/scale, alpha, water-boundary checks, Godot integration and separate
isometric artwork remain pending. Existing routes and artwork are unchanged.

## Wide isometric frame audit - 2026-09-13

All ten wide isometric routes pass 256 actual-playback captures each (2,560 total).
Independent projected CPU samples cover every waterfall and river section and
native river stem, excluding fixed-alpha occluded pixels. All 16 frames are
uniquely identified with matching phase and at most one color-byte difference.
Geometry, clipping, motion and save/reopen checks remain green. This completes
the sampled timing gate, not visual approval of natural cliff/bank contacts,
face repetition, large-map performance or the remaining terrain families.

## Isometric connected width routes - 2026-09-13

Ten isometric routes now cover both visible flow directions at widths 1/2/3/5/9.
Plateau footprints and native light/shade cliff faces expand with the channel;
water uses shared procedural motion, directed native lake mouths and depth paint.
GPU checks pass native inlet/outlet anchors, footprints, unclipped bounds, fixed
terrain/alpha and save/reopen for all ten cases. The five-cell north-south capture
was inspected, not approved. Full-route frame inference, natural bank/cliff
contacts, repeated face refinement and performance remain pending.

## Validated clock migration - 2026-09-13

The narrow square and both isometric depth routes now use the same validated
connected-water clock as the wide square candidates. Extended GPU audits pass
256 samples per route, including all 16 frames, depth editing and save/reopen.
Clock activation validates vertical frame bands, count, period and texture layout;
static backgrounds are allowed only when pixel-identical across all 16 bands.
Negative tests reject malformed layouts without enabling the material option.
Older standalone resources remain unchanged. Isometric wide-route integration,
natural terrain contacts, performance and full production coverage remain open.

## Wide-route clock boundary fix - 2026-09-13

An independent CPU-sample audit found an intermittent loop-boundary mismatch:
all five native river sections displayed frame 15 while waterfall sprites showed
frame 0. A short rerun missed it; 256 samples per width reproduced it. New wide
routes now select native water atlas bands from the same shader clock as sprites.
All 1,280 captures across widths 1/2/3/5/9 pass after the fix, covering all 16 frames
with zero sampled color error. Samples include inlet/outlet and internal section
edge pixels, but do not replace full visual contact review. Other connected
showcases still need this opt-in clock migration; standalone defaults are unchanged.

## Procedural shared waterfall sampling - 2026-09-13

An opt-in shader now samples continuous physical waterfall coordinates using a
64x8 deterministic noise texture, rather than requiring one exported animation
per offset. The field has a declared 1536px repeat period; negative and large
offsets pass model tests. All 36 projection/rise/width GPU comparisons match the
reference atlas within one color byte with unchanged alpha. Square connected
routes at widths 1/2/3/5/9 pass flow/depth and fixed-terrain motion checks without
new curtain frames. Full route join/phase, isometric wide routes, performance,
natural contacts and visual/production approval remain open.

## Shared waterfall GPU assemblies - 2026-09-13

All 36 projection/direction/rise/width assemblies pass all-frame GPU comparison
against CPU-composed source atlas regions. The audit covers 16 frames per case,
widths 1/2/3/5 and rises 16/32/64, with zero pixel mismatches and alpha changes.
The wide isometric capture was inspected, not approved. This verifies assembly
rendering, not natural bank/cliff contacts or full-route native clock behavior.
Arbitrary-width sampling and final artwork/production approval remain pending.

## Shared waterfall projection/rise resources - 2026-09-13

Shared-coordinate motion now has separately projected square and isometric
exports at 16/32/64px rise. Six named SpriteFrames resources load successfully;
their atlas dimensions, profile counts and loop configuration are packaged from
explicit metadata. Six model regressions pass, preserving old default behavior,
native endpoints, alpha and exact loops. Offsets 0..4 are review coverage only.
GPU review of the new projection/rise exports, arbitrary-width sampling, visual
approval and production integration remain pending.

## Shared curtain refinement - 2026-09-13

The development square width routes now use shared-coordinate curtain and foam
motion instead of restarting identical streaks each cell. Existing waterfall
sheets are unchanged. New model tests prove exact section contacts, original
river endpoints, a closed loop and fixed alpha; all four GPU width cases retain
stationary land/alpha. The revised width-5 capture was inspected, not approved.
The review export contains five offsets at 64px rise; arbitrary-width sampling,
other rises, isometric export, exact rendered join/phase audit and visual approval
remain required. This is not full waterfall-family coverage.

## Connected square width candidates - 2026-09-13

`package-wide-depth-routes.gd` assembles widths 1/2/3/5 from existing river,
waterfall and lake-mouth sections at unchanged 64px rise. Native flow/depth
bindings and initial GPU motion/fixed-land/alpha checks pass all four widths.
The width-5 capture was inspected: repeated curtain motifs remain conspicuous,
so this is connectivity work, not accepted water appearance. Exact join/phase
audits, visual repetition refinement, isometric widths and production remain open.

## Isometric route phase audit - 2026-09-13

Both native isometric depth routes now pass independent rendered-pixel phase
inference. Each direction samples 64 captures, covers all 16 frames, and uniquely
matches native river, river sprite and waterfall atlas frames with zero error.
The test uses fresh scene instances and opaque water pixels, not configured
playback values. Existing depth/paint/save and fixed-terrain checks also pass.
This closes the phase-audit gate only; wider routes, natural cliff contacts,
visual refinement, other materials and production approval remain unfinished.

## Native isometric depth routes - 2026-09-13

The new `depth_waterfall_route_v1/isometric` north-south and west-east scenes
replace baked lake sprites with native lake and depth layers, retaining authored
isometric cliff/waterfall art and a 64px rise. Each route has 25 lake cells and a
native river stem. Depth erase/repaint changes the displayed lake directly.
Both GPU cases pass depth contrast, fixed land/alpha, motion and save/reopen.
The north-south capture was inspected. Exact isometric frame-phase inference,
visual refinement/approval, wider routes and production packaging remain open.

## Connected depth waterfall route - 2026-09-13

`depth_waterfall_route_v1/square/route.tscn` connects retained upper-river,
granite waterfall and lower-river artwork to native river tiles and a depth-painted
lake. Original atlas pixels and the 64px rise are retained. Relative sprite
playback was out of phase with native tiles; the opt-in route now samples original
frames on renderer TIME. All 16 frames match across 64 GPU samples with zero pixel
inference error. Depth and motion leave land/alpha unchanged; save/reopen passes.
The square capture was inspected, not user-approved. Visual refinement, isometric
depth integration, wider routes, other banks and production remain pending.
No approval state, legacy location or renderer default changed.

## Modular lake mouths - 2026-09-13

`lake_mouth_sections_v1` provides 24 reusable bank/middle modules per projection
for all eight inlet/outlet profiles. Together with retained narrow contacts,
32 native patterns per projection demonstrate widths 1/2/3/5. Export comparisons
match 1,376,256 square and 344,064 isometric lake-facing pixels exactly; directed
river edges, internal section joins and full-loop static banks pass Node tests.

All 64 width/port/flow/projection cases pass native pattern repaint, invalid
section/flow/flip rejection, save/reopen and GPU checks. Visual inspection found
an artificial dark shallow-mouth strip; continuing only the visual depth mask
toward the shallow river removed it. Both shallow and deep mouth states are now
tested, with unchanged river pixels. Corrected square/isometric samples were
inspected, not user-approved. Other banks, large-map cost and production
integration remain open. The new pack supplements existing inlet/outlet evidence;
no canonical approval state, gameplay map, asset deletion or promotion changed.


## Modular sea-depth mouths - 2026-09-13

`sea_river_depth_v1` adds depth painting through the existing river-to-sea
mouths in both projections, using low-bank/middle/high-bank pieces and the
existing native patterns. No width-specific artwork was generated. All four
ports and widths 1/2/3/5 pass 32 native/GPU cases, including pattern repaint,
wrong-flow/flip rejection, save/reopen, fixed banks/alpha, camera anchoring and
animation. Upstream river pixels and river-facing mouth samples are unchanged.

Square/isometric width samples were inspected; appearance is not user-approved.
Other banks, large-map cost and production integration remain pending. These
are river-to-sea inlets, not authored reverse/tidal flow. Coverage adds evidence
for existing estuary requirements without overwriting canonical asset choices.


## Lake river-depth checkpoint - 2026-09-13

`lake_river_depth_v1` adds eight explicit one-cell inlet/outlet profiles per
projection and eight native placement patterns. Existing river and mouth frame
pixels are retained; depth shading blends from the unchanged river-facing edge
into the lake side. The pack declares flow, bank-neighborhood and depth-paint
constraints instead of inferring them from a generic 47-mask boundary.

All 16 projection/profile scenes pass native loading, pattern presence, wrong-flow
and unauthored-flip rejection, missing-contact checks, save/reopen and GPU checks.
River pixels and mouth-edge samples remain unchanged; depth, animation, banks,
alpha and camera anchoring pass. Square/isometric samples were visually inspected,
not user-approved. Wider contacts, other banks, sea-depth contacts, large-map cost
and production integration remain pending. Existing lake inlet/outlet requirements
gain alternative evidence only; no canonical bindings are overwritten or promoted.


## Lake depth checkpoint - 2026-09-13

`lake_depth_v1/{square,isometric}/lake_depth_review.tscn` adds separate shallow
painting with native terrain masks, grass-bank contacts, deep interiors and
islands. Original local lake-ripple frame pixels are preserved; sea swells and
surf are not reused. Only connection-mask geometry is shared with the sea pack,
with packaged copies and source hashes recorded in the candidate manifest.

Both projections pass 650 compatible native bank/depth neighborhoods, invalid
paint recovery and save/reopen. Render checks cover fixed banks/alpha, camera
anchoring, erasure, repeat independence, animation and 72 deep-interior samples
per projection. Visual inspection caught atlas-sized shading blocks that motion
counts missed; native bank-cell lookup corrected them and a rendered regression
now covers the failure. Appearance remains unapproved. River/depth connectors,
other banks, large-map cost and production integration remain pending. This adds
alternative evidence for existing depth requirements, not new image-count credit.


## Coastal depth checkpoint - 2026-09-13

The newer `shared_sea_v1/{square,isometric}/coastal_depth_review.tscn`
candidates allow shallow/deep painting against existing grass coasts, headlands
and islands. They reuse the 47 depth masks through a one-texel-per-cell lookup;
they do not multiply the artwork into coast/depth combination sheets. Earlier
open-water-only depth scenes and evidence are retained unchanged.

Native tests pass 650 compatible coast/depth neighborhoods per projection,
invalid-paint retention/recovery, field-size limits and save/reopen. GPU tests
pass exact open-water overlay equivalence, camera anchoring, animated water,
fixed banks and alpha. These are technical checks, not visual approval.
River/depth contacts, lake-specific depth, other banks, large-map cost/chunking
and production engine-pack integration remain pending. Coverage remains 433
candidates of 14,864 requirements, with no promotion or deletion authorized.


The approved engine/view extension and milestone status are maintained in
[Terrain Library and Engine Upgrade](TERRAIN_LIBRARY_ENGINE_UPGRADE.md).
It adds optional prepared packs for Tiles and IsometricAutotile, preserves the
other views and legacy atlases, and specifies Explicit Apply authoring. The
original inventory, collection scope and cleanup safeguards below remain active.

## Agreed scope

Build cartoon and native pixel successors for all existing terrain art families.
Preserve approved older art at repository-root `legacy_art/terrain`, not inside
the addon. No immediate deletion or silent promotion is authorized.

The executable specification is `tools/terrain-library/specification.json`.
Runtime cells: square 64x64, isometric 64x32. Cartoon masters: 128x128/128x64;
pixel masters: 32x32/32x16, exported exactly 2x nearest. Author projections separately.
Front elevated orthographic 2.5D and separate diamond art, upper-left light, visible side walls, green-backed
masters, clean runtime alpha, no drawn grids, no new ramps. Heights are actual
rise: 64/32/16px, not the entire sprite bounding box.

## Deliverables and gates

1. Inventory existing files, SHA-256, dimensions, approval evidence, literal and
   dynamic reference sites. Establish dev/test/production, root legacy and quarantine.
2. Cartoon foundation: grass/dirt, dedicated water, one cliff material and an
   assembled river -> waterfall -> lower river. Complete 47-mask transitions,
   fixed-grid Godot resources and visual review before multiplying variants.
3. Cartoon completion: nine plain ground surfaces, shared seamless textures and
   independent decoration overlays; alternatives only for meaningful variation. All specified
   transitions, mountains 1/2, hills, quarter/half platforms, stairs, landforms,
   nature and the existing accessory families. Preserve eight material identities.
4. Native pixel equivalents: same logical coverage, independently authored style.
5. Production promotion after visual approval and Godot validation. Migrate legacy
   in reviewed batches only; keep previous working integrations until replacements pass.

## Complete modular coverage

Use repeatable middles with edges, corners and caps, not a finite small/medium/large
shape collection. The specification's family/material/direction/rise dimensions
are requirements, not instructions to duplicate artwork. Verified patterns can
satisfy equivalent logical cases; asymmetric lit art cannot be blindly rotated.

| Family | Required pieces |
|---|---|
| Ground | Nine fills; binary boundaries, inner/outer corners, isolated patches and holes |
| Paths | Straights, bends, four T orientations, crossings, end caps, narrow/wide connectors |
| Rivers | Both banks, inner/outer bends, interiors, narrowing/widening, sources, confluences, branches, crossings, islands, wakes, mouths |
| Lakes | Banks, coves, islands, shallow/deep water, river inlets/outlets |
| Sea | Coast, bays, headlands, islands, depth boundaries, estuaries, surf zones |
| Waterfalls | Inlets, lips, both edges, repeatable curtain width/height, impact foam, pools, outlets, stepped/split falls |
| Cliffs | Front/sides, convex/concave corners, caps, walls, height extensions, exposed ends, plateau rims/fills, bottom contacts |
| Hills/mountains | Extendable tops, rounded/square sloped hills, tier connections, separate Prefab 1/2 layers |
| Access | Separate 16/32px platforms; narrow stairs at supported 16/32/64px rises; no ramps |
| Landforms | Coastal cliffs, foothills, canyons, summits, craters, caves, arches, pillars, ledges, bridges with authored connectors |
| Accessories | Retain nature, rocks, wood, ore and containers as independent overlays; no new buildings/characters |

Land boundaries: grass to dirt/dry grass/sand/mud/rock, dirt to gravel, rock to
snow/lava. Water boundaries: grass/sand/rock banks and shallow/deep transitions.
Supply all 47 valid configurations for each compatible binary boundary. This
does not satisfy directional flow, height transitions or structure coverage.
Multi-material intersections use separate compatible overlay layers.

Connector profiles declare logical direction, material role, opening width and
elevation; placement uses native projection transforms. Missing/incompatible
connectors and ambiguous inflow/outflow assignments are reported, never guessed.
Review widths 1/2/3/5 cells, rises 16/32/64px and taller 80/128px assemblies.
Preserve eight geological constructions, not merely their colors.

## Coverage tracking and execution order

### Layered isometric plateau checkpoint

Open-sea depth candidates now provide 47 shallow/deep masks and an animated
deep background in both projections, sharing the existing map-wide sea motion.
Native tests pass all 94 configurations, erasure, save/reopen and invalid-paint
recovery. GPU checks pass depth contrast, matching deep holes, fixed banks and
alpha, and unchanged underlying sea when the guard suppresses invalid placement.
The separate Depth layer requires the supplied material, surface plane and
placement guard; it does not alter gameplay geometry. The guard compares native
map snapshots because cell-erasure change notifications were unreliable in the
tested runtime. Large-map comparison cost is still a production gate. These
94 mask configurations remain partial candidates: coastal/river contacts, lake depth,
visual approval and full engine-pack integration are not complete. Reconciliation
adds 47 missing isometric bindings and retains the earlier 47 square-depth
foundation selections, linking the new set as alternative evidence. The current
ledger has 433 candidate and 14,431 missing logical requirements; none are
validated or production-approved.

The shared-sea candidate now has twelve width modules (low bank, repeatable
middle, high bank in each inlet direction), reusing the four original narrow
mouths. Native patterns cover widths 1/2/3/5 in both projections. All 32
width/direction/projection combinations pass saved-resource loading, pattern
erase/repaint, save/reopen, explicit incoming flow, water motion, fixed banks
and alpha checks. Fractional-scale GPU checks caught frame-band texel rounding;
the control shader now samples stabilized texel centers. Eight sea/grass/estuary
requirements are bound as candidates, not approved assets. The ledger now has
386 candidate and 14,478 missing logical requirements at that checkpoint out of 14,864; these are
not image counts. Visual current/surf blending, other banks, depth transitions
and full family acceptance remain open.

An opt-in shared-sea candidate now separates the 47 coast masks and four inlet
profiles from map-wide wave shading. Compact, varied crests replace the prior
two-crests-per-cell pattern. Coast/grass alpha stays fixed and the shader reads
the native 16-frame tile phase rather than an independent timer. Square and
isometric GPU checks pass motion, fixed terrain, camera/map anchoring and
texture-repeat independence. Interior comparisons against a continuous surface
pass; the enlarged isometric reference has a separately reported outer-mask
rasterization difference. See shared_sea_v1/manifest.json and disposable
shared_sea_plane_render.json. The previous candidate is retained. Visual surf
approval and full engine-pack integration remain open; the width/direction
checkpoint above adds technical estuary coverage without completing the family.

Sea development candidates now provide 47 animated grass-coast configurations
in square and isometric projections, with distinct curved swell crests and
shoreward breaking surf. Native TileSets and open-coast/headland/island review
scenes are packaged. Tests cover fixed banks, 16-frame loop closure and every
realizable occupied-neighbor boundary pair; GPU motion/fixed-land checks pass.
An initial crosshatched wave field was replaced after rendered inspection.
Large-area repetition and surf quality remain unapproved; separate depth
transitions and sand/rock coasts are still missing. No promotion.

One-cell grass-bank estuary candidates now provide explicit north/east/south/west
river inlets, separately sampled for square and isometric projections. All four
profiles pass fixed-bank, loop and river/sea edge checks; exporters compare
286,720 sea-facing pixels with zero mismatches. Native north-inlet showcase
captures pass animated-water/static-land checks in both projections. These are
technical candidates: the short current-to-surf blend and repeated ocean crests
remain visibly unfinished. Wide estuaries, tides, other bank materials and full
waterfall-to-sea integration are not covered. No production approval is implied.

The two isometric granite routes now extend through explicit north/west river
inlets into a 5x5 lake. Existing lake ripple and river-mouth artwork is reused;
the lake is not animated as a flowing river. Each composed route shares one
clock across 34 sprites. GPU checks pass inlet identity, native lake placement,
phase agreement, moving water roles, fixed terrain and bounds. Native lake
TileMap layouts are retained separately, but the synchronized preview is baked;
automatic propagation of layout edits is not implemented. Natural contacts,
foam approval, sea continuation and wide mouths remain open. No promotion.

Two narrow isometric river -> granite waterfall -> lower-river scenes now
connect the authored face TileSets to the procedural water modules at 64px rise.
A single native AnimationPlayer drives all nine connected sprite sheets on the
same 16-frame/1.2-second timeline. Saved-scene GPU checks pass inlet/outlet anchors,
phase agreement, moving upstream/curtain/impact/pool regions, fixed grass/rock and
stable bounds. The capture shows the geometric connection but remains a development
candidate: natural bank/lip/base contacts, foam approval, lake/sea continuation,
wide routes and engine pack integration remain unfinished.

Isometric water-only waterfall modules now have explicit upstream, vertical
curtain and downstream planes rather than a transformed square sprite. Two
visible flow directions, 16/32/64px rises and four bank/middle section roles
produce 24 width/rise/direction assemblies at widths 1/2/3/5. SpriteFrames use
16 frames per 1.2 seconds. Mathematical tests verify native adjacent-cell
connectors, river phase continuity, moving roles, fixed alpha and loop closure;
GPU checks pass all assemblies. These are technical candidates: painted bank
contacts, the complete isometric cliff route, foam visual approval, stepped/split
falls and production resources remain missing. Existing square waterfalls and
approved source artwork are unchanged.

New separately painted light/shade isometric granite spans replace the repeated
small-column look in a sibling 4x4 review scene. Each span has four contiguous
32x80 AtlasTexture modules referencing one 128x128 texture (64px rise), with
plain grass still independent. GPU assembly passes silhouette alpha and top
compositing. The source hash, guide geometry and uniform calibration are retained in staging.
This is appearance-candidate artwork, not a seamless cyclic atlas: outer ends,
mixed sequences, natural bottom/corner contacts and visual approval remain open.
Earlier approved/reference art and earlier development scenes are unchanged.

The long-face candidate also provides a native isometric TileSet, two saved
four-cell patterns and separate LightFace/ShadeFace TileMapLayers. Saved-resource
GPU comparison matches the sprite reference exactly; erasure and pattern repaint
restore the original image. A staging README documents sequence restrictions and
rebuild order. Face/material/sequence/visual-rise custom data is descriptive only,
not collision, navigation or engine elevation. This is standalone pattern support,
not unrestricted terrain autotiling or a promoted production pack.

Concave development layouts now cover a 5x5 notched footprint, 7x5 U shape and
7x7 ring with a 5x5 opening. Saved native cells preserve all 43 deliberately
empty top cells. GPU union-of-columns checks include internal seams and find
zero interior alpha gaps, unintended exterior geometry or top occlusion across
152,700 interior samples. The ring capture has visible inner walls and an open
center. This validates these layouts, not natural corner artwork, all possible
footprints or engine-integrated painting. Art variation and bottom contacts remain
unapproved; no new source artwork was copied to make these shape examples.

Two- and three-tier development scenes now compose those rectangular plateaus
at 64px increments (128/192px total height), retaining independent wall/top layers.
Saved-scene GPU tests pass native anchor, overlapping-tier draw order, shared
logical surface coordinates across elevation, and 450 sampled contact pixels.
The material-plane instance override is persisted explicitly so reopening does
not restart the grass texture. This is geometric composition evidence only:
natural bottom contacts, less repetitive walls and irregular/concave tiers remain
missing. The multi-tier family is not complete or approved.

Development scenes now assemble 1x1, 2x2, 3x2 and 5x3 cell plateaus from the
guided granite corner and a separate shared-grass top, without new source copies.
GPU checks pass with zero interior alpha gaps and zero top-surface mismatches.
Near-opaque alpha ringing from downsampling was corrected in the runtime export;
the original source and transparent outline are retained. These are rectangular
64px-rise geometry candidates, not complete cliff artwork. Repeated wall motifs
remain visible; concave shapes, holes, varied wall sections, exposed ends, bottom
contacts, multiple tiers and visual approval remain open. No production promotion.

`specification.json` v2 replaces the single projection and mandatory four-fill
fields. No current generator consumes those removed fields. Requirements track
style, projection, family, material, piece/configuration, elevation and water role.
Explicit bindings record source, runtime region, Godot resource, validation and
approval evidence. An unbound requirement is missing, even when similar art exists.
Source appearance approval is not atlas or production approval.

Maintain `tools/terrain-library/coverage-bindings.json` as the reconciliation ledger.
Generate disposable requirements-to-assets reports with `node tools/terrain-library/coverage.mjs`.
Existing square transition and river candidates remain evidence awaiting explicit
mapping; unknown assets are retained. This report is not a cleanup/deletion list.

Delivery order is reconciliation; grass/dirt masks/shared surfaces in BOTH
projections; one grass/rock river -> waterfall -> lower river -> lake/sea family
and multi-tier cliff showcase; modular edge-case review; remaining cartoon;
native pixel equivalents; separately gated promotion/legacy work.
No new structure-editor features take priority over these terrain milestones.

## Work areas

`generated/dev/{style}/{pack}` holds tracked specifications and staging; experiments
are ignored. `generated/test/{style}/{pack}` holds maintained fixtures and ignored
output. `generated/production/{style}/{pack}` owns sources/runtime/godot and an
approved manifest. Production cannot depend on dev, test or legacy. The addon
installer excludes dev/test. Existing unsorted art remains until reviewed migration;
the installer exclusion is not a claim that every old generated folder is ready.

The approved narrow waterfall foam review is a reference, not permission to call
all eight other animations approved. Existing source sheets have missing corners
and duplicate junctions. Preserve them; do not stretch them into apparent compatibility.

## Acceptance

Check 47 legal mask cases and illegal diagonal normalization, exact grid dimensions,
edge continuity, island/channel/junction cases, height ratios, pivots and alpha.
Validate actual Godot TileSets/SpriteFrames and composed scenes. Pixel output uses
nearest filtering and stable clusters; cartoon stays smooth but not blurred.
Animation must visibly change foam and water while keeping banks and rock fixed;
pixel-change counts cannot replace watching motion. Runtime navigation stays separate
from artistic elevation; reuse the existing TerrainTileSets custom-data contract.

Also test shared texture alignment across layers and transformed maps, authored
borders, holes, coves, river branches/mouths, mixed widths, height changes and
cross-layer contacts. Use nearest pixel filtering and smooth cartoon filtering.
Maintain one showcase per style with separate projection scenes, saved patterns,
native matching and save/reopen checks. Routine captures remain disposable output.

Water roles are separate, not one interchangeable animation: lake ripples, directional
river current, waterfall curtain/impact foam, and sea swells/breaking surf. The current
staging candidate implements lake water only. Its calm local ripples must not be
advertised as river flow or ocean surf. The existing waterfall reference is unchanged.
Detailed statuses are recorded in dev/cartoon/water/motion_profiles.json. A separate
river_staging candidate now provides four straight currents and eight directed bends,
16-frame PNG/Godot resources and a two-bend review scene. Wide channels, junctions,
wakes, waterfall connections and visual approval remain pending.

## Cleanup contract

No direct-reference match is not proof of disuse. Resolve directory scans, dynamic
paths, manifests, embedded HTML/JS assets and external consumers before removal.
Classify as approved production, legacy, active dev, maintained test, waste candidate
or unresolved. Unknown means retain. Exact duplicate hashes are review groups only.

For an approved removal list, first copy bytes to a dated `.art_quarantine` batch,
verify hashes and restoreability, then request explicit removal confirmation.
Quarantine is outside the addon, ignored in Git and Godot; keep indefinitely until
separate permanent disposal approval. No deletion command is part of the initial tools.
Legacy relocation must update references and import settings, validate resource loads
and preserve original identities before old paths can be removed.

## Progress

- A separately authored isometric granite corner candidate now uses an exact
  geometry guide, uniform 2x calibration, a 64x32 diamond footprint and nominal
  64px rise. Green-backed source/master, alpha runtime, plateau mask and native
  TileSet are retained in grass_granite_iso_corner_v1. GPU comparison reports
  zero native-anchor mismatches and no plateau alpha holes. Two freeform attempts
  with incompatible proportions remain experiments. Adjacent plateau/wall joins,
  concave corners, bottom overlays and appearance approval are still pending.

- Water/elevation reconciliation now adds 143 explicitly scoped candidate
  bindings: four shared fills, straight river banks/middles, narrow width adapters,
  directed junctions, river/lake ports and three south-facing granite cliff roles.
  The ledger totals 378 candidates and 14,486 missing logical cases out of 14,864;
  these are not image counts. No entries are validated or production-approved.
  Re-running adds zero duplicates and preserves existing decisions. Water-only
  waterfall modules remain evidence, not eight geology-specific structure claims.

- Explicit river/lake inlet and outlet candidates now cover four directions in
  both projections (eight profiles each). All river-edge samples match across
  16 frames; 573,440 lake-facing pixel samples match the existing lake atlas.
  Native resources and both projection inlet reviews pass GPU motion/fixed-bank
  checks. The painted square showcase now continues upper river -> waterfall ->
  lower river -> lake, with a continuous bank outline in visual inspection.
  This is a development candidate, not full water-family or visual acceptance;
  wide mouths, other bank materials, sea and isometric waterfall routes remain.

- The painted route now includes an independent granite-foot contact candidate
  in grass_granite_contact_v1. Original RGBA artwork, a green-backed 2x master and
  a 320x40 alpha runtime are preserved with prompt/hash provenance. Uniform
  scaling retains the contour; the overlay straddles the nominal ground line
  without adding rise and draws beneath water. GPU checks retain fixed terrain,
  matching grass and all water motion roles. The contact softens the previous
  hard cutoff, but visual approval and repeat/corner/side variants remain pending.
  The unused straighter source is retained as an experiment, not deleted.

- The first painted cliff route now uses a separate plateau mask and the shared
  grass surface. GPU comparison finds zero mismatches across 3,072 rim samples,
  zero changes to unmasked rock, and zero sliding pixels after moving the scene.
  Original source art remains unchanged. This resolves the previously observed
  grass-rim texture seam; authored bottom contacts and the other missing cliff
  connectors remain open. Source approval and production promotion are pending.

- A new green-backed grass/granite front-wall source is retained with generation
  prompt and hash in grass_granite_front_v1. Controlled 2x master/runtime exports
  provide five contiguous front regions; only their original adjacency is valid.
  The first painted river -> waterfall -> lower-river scene passes fixed-terrain
  and four-role animation checks at a nominal 64px rise. Visual inspection still
  shows a grass-rim texture seam and an abrupt bottom contact. These need authored
  masks/contact pieces before approval. Outer repetition, sides/corners, other
  heights, isometric artwork and lake/sea continuation remain incomplete.

- Water-only front-facing waterfall sections are now technical candidates in
  waterfall_sections_v1: narrow, low bank edge, repeatable middle and high bank
  edge at exact 16/32/64px drops. Native SpriteFrames demonstrate widths 1/2/3/5.
  River ports retain current phase; lip, vertical curtain streaks, impact foam
  and downstream pool move separately with stable alpha over 16 frames/1.2s.
  Analytic repeat/port tests and all 12 GPU assemblies pass. No cliff terrain is
  included, no approved original was changed, and visual approval is pending.
  Isometric modules, painted cliff contacts and the complete connected showcase
  remain required; this is not a production waterfall pack.

- Directed narrow river junction candidates now include 24 T branch/confluence
  profiles and eight one-to-three/three-to-one crossings per projection. Named
  routes, 16-frame loops, native TileSets and separated review examples are
  packaged in river_junctions_v1. Every open edge matches narrow channels across
  all frames; GPU checks find moving water and fixed land. Appearance approval,
  wide junctions, two-to-two crossings and lake/waterfall/sea connectors remain
  pending. This does not complete river coverage or authorize promotion.

- Widening/narrowing routes now have saved scenes for all four flow directions
  in both projections. The three additional directions pass GPU visibility and
  sampled static-bank checks, alongside the earlier north-to-south route.
  This does not complete wide bends, junctions or cross-role connections, and
  does not substitute for visual animation approval.

- All 16 saved adapter patterns per projection now pass GPU footprint coverage
  and mask-replacement checks (32 rendered cases). Square directional overview
  inspected for contour/orientation consistency. This does not approve animation
  quality or every directional joined route; wide bends and junctions remain open.

- Widening/narrowing adapters are now native animated TileSets with 16 saved
  directional patterns per projection. Separate narrow and reusable outer-bank
  variants assemble a 1->2->3->2->1-cell route. Rendered review caught and fixed
  invalid pattern alternatives; cell visibility and sampled static-bank checks
  now pass. Every-direction visual review, wide bends and junctions remain open.

- Width-transition geometry now defines two-cell-long widening/narrowing banks
  between adjacent channel widths. Endpoint pixels match the repeatable straight
  sections for 1/2, 2/3 and 4/5-cell transitions; monotonic banks and loop closure
  tests pass. This is geometry/motion implementation only, not delivered atlas
  artwork. Native packaging, all flow directions and rendered review remain open.

- Current candidate reconciliation now maps 235 logical requirements. New ground
  and lake boundaries replace older candidate links only; older records remain
  explicit alternatives and their files are untouched. A repeated reconciliation
  is idempotent. All entries remain candidates, not validated/approved assets;
  binary mask bindings do not imply completed flow or structural coverage.

- Repeatable straight river sections are now packaged for both projections:
  narrow, low bank, middle and high bank in four directed flows, each with 16
  frames. Native width-review scenes assemble 1/2/3/5 cells using repeated
  middles. Loop, fixed-bank and analytic join tests pass; GPU captures show
  changing water with unchanged sampled land. Wide bends, width adapters,
  junctions, cross-role connectors and animation approval remain pending.

- Existing 12 square river modules now have 24 source-backed candidate port
  resources with explicit direction, width, elevation, material and animation
  contracts. Route validation passes the existing bent river with declared
  external endpoints and rejects gaps/unknown endpoints. Raster opening review,
  wide channels, junctions and cross-role adapters remain required.

- Dedicated cartoon lake-bank candidates now provide native 16-frame, 1.2-second
  animated TileSets in both projections, with 47 grass-bank configurations plus
  background and an island review scene. Existing local-ripple motion is reused
  with provenance. GPU samples show changing water and unchanged grass/banks.
  This is not a river/sea/waterfall substitute. Visual animation approval, other
  bank materials, depth transitions and flow connectors remain pending.

- All 47 configurations per projection now have GPU-rendered review captures.
  Exact central 3x3 footprint checks report no alpha gaps or exposed mask colors
  across all 94 cases. Both overview sheets were inspected; appearance approval
  is requested separately. These checks do not certify every possible mixed-width
  arrangement or elevated connector. Captures remain disposable test output.

- Opt-in standalone surface-plane binding is implemented for both projections.
  GPU split-layer and translated/rotated/nonuniformly-scaled map comparisons pass
  with zero changed samples. Invalid groups preserve their prior coordinates;
  unrelated materials are not modified. Exhaustive rendered joins and elevation
  connectors still require review before completing the grass/dirt milestone.

- Surface exports are calibrated at 512px master and 256px runtime per four-cell
  repeat. Both projection scenes reference shared runtime textures and separate
  TileSets rather than embedding source image payloads. Native terrain painting
  now exercises all 47 cases per projection, widths 1/2/3/5, and erasure with
  neighbor preservation. These checks pass; exhaustive rendered seams and
  cross-layer/transformed-map review remain pending.

- Plain grass and dirt surface artwork v1 is now in development staging with
  source hashes and candidate metadata. Separate square/isometric mask scenes
  render both surfaces using linear cartoon sampling and nearest mask sampling.
  GPU camera stability passes; no large source-edge discontinuity is flagged.
  Runtime calibration, all-case rendered coverage and user approval remain open.

- GPU diagnostic captures now pass for both mask projections with zero changed
  samples at corresponding terrain points after camera translation. Rendered
  inspection exposed and corrected the isometric fixture's default staggered
  layout to diamond-down. Native and 2x captures remain disposable test output.
  This is diagnostic surface evidence, not artwork approval or full seam coverage.

- Foundation reconciliation now records 141 exact existing square atlas regions
  across grass/dirt, grass/shallow water and shallow/deep water as candidates.
  Their baked surfaces require conversion; none are validated or promoted.
- Square and isometric grass/dirt mask fixtures each provide 47 configurations
  plus a background tile, shared-surface material and native painting scene.
  Native neighbor checks, painting, picking round-trips and save/reload pass.
  These plain-color technical fixtures do not satisfy artwork approval; rendered
  seams, nonuniform shared-texture alignment and cartoon filtering remain pending.

- Complete modular coverage plan/specification v2 and generated requirements report:
  implemented. Five existing candidate/source evidence records are retained, with
  no automatic production approval. Full asset-to-requirement
  reconciliation remains the next artwork milestone, not a completed inventory.

- Folder lifecycle, specification, read-only audit, quarantine-copy safety and tests: implemented.
- Source migration/deletion: not performed; exact reviewed batches required.
- Cartoon foundation: four candidate transition sets, each with 47 masks and a
  background region; three sets have 16-frame water animation. Godot resources,
  autoterrain scenes and desktop/mobile preview pass the initial checks. Cliff and
  waterfall connectors, shared-surface mask packaging and visual approval are still pending.
- Remaining artwork, pixel successors, full Godot integration and promotion: pending.
