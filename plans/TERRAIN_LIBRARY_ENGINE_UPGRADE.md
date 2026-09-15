# Terrain Library and Engine Upgrade

## Contact overlap regression - 2026-09-13

`terrain_contact_motion_probe.gd` compares simultaneous source/review renders at
widths 1/2/3/5 across 16 controlled frames. All 64 cases preserve every sampled
water-corridor pixel while showing land contacts and distinct curtain frames.
Frame overrides isolate this gate from clock testing. Existing clock regressions
remain separate; no renderer defaults, resource promotion or navigation changes.

## Isolated contact integration - 2026-09-13

`package-contact-route-review.gd` creates development-only square width 1/2/3/5
review scenes from the tested routes and six standalone contact scenes. It checks
save/reopen, retained contact count and unchanged source hashes, then captures
Godot output. Four cases pass. No existing routes or renderer defaults change.
One static capture was visually inspected; animated joins and isometric contact
integration remain pending. Near-transparent RGB noise in raw previews is not
equivalent to visible compositing artifacts.

## Standalone contact candidates - 2026-09-13

`package-waterfall-contacts.gd` packages six development Sprite2D scenes with
explicit local pivots, visual-rise metadata and external textures. Save/reopen
checks pass; no collision, navigation, flow or existing route resource changes
are introduced. Seven export-contract tests also pass. Magnified left-wall
inspection found edge-color artifacts; runtime appearance, actual waterfall
contact fit and projection-specific isometric artwork remain unvalidated.

## Contact artwork preparation - 2026-09-13

Six square granite contact pieces have predeclared bounds and pivots plus a new
development source sheet. They are static land-side overlays with protected water
half-planes, not new navigation or flow rules. No scene consumes them before
calibration and validation. Source appearance review is pending; source generation
does not increase production coverage or replace the tested animation geometry.

## Wide isometric synchronization evidence - 2026-09-13

The full-route probe now uses independent projection-specific CPU samples and
256 captures per direction/width case. All ten cases identify all 16 frames
with synchronized native stems and sprite sections, within one color byte.
Visibility filtering excludes samples covered by later waterfall sections.
Fourteen model/coverage regressions also pass. This evidence applies to the
candidate routes, not all engine views, map shapes, performance or production.

## Native isometric width integration - 2026-09-13

`package-isometric-wide-routes.gd` assembles both flow directions through nine
cells using native projection transforms, unscaled face modules, shared-clock
water and validated mouth profiles. It resolves waterfall resources from their
manifest, not guessed generated filenames. Ten GPU cases pass geometry, motion
and save/reopen checks. Full route phase inference and production packaging are
not implied by the initial integration checks; renderer defaults stay unchanged.

## Guarded connected clock - 2026-09-13

`connected-water-clock.gd` validates all atlas sources before enabling the opt-in
shader clock. It rejects wrong count/period, horizontal or displaced frame bands,
missing shader support and changing static-band pixels. Route packagers stop
before replacing scene outputs on validation failure. Negative mutation tests
and extended square/isometric GPU route audits pass. This is a packaging guard,
not a new universal engine-pack contract or a renderer-default migration.

## Connected water clock precision - 2026-09-13

Native animation slices and sprite TIME selection were observed on opposite sides
of a loop boundary despite equal nominal periods. `terrain_lake_depth` now has a
disabled-by-default `connected_water_clock` mode that selects the matching atlas
band itself. New wide routes enable it; existing standalone materials do not.
This mode requires 16 equal vertical frame bands and a 1.2-second loop. Extended
CPU-reference GPU inference passes 256 samples per width (1,280 total), including
native river sections and every waterfall section. Other route migration, generic
pack-layout validation and performance remain pending.

## Continuous waterfall sampling - 2026-09-13

`terrain_native_clock_sprite` has a disabled-by-default shared waterfall option.
It retains atlas alpha while sampling physical coordinates through square or
isometric transforms. Existing materials keep their original behavior. Shared
noise data avoids per-section animation proliferation; repeat period is 1536px.
36 GPU reference cases pass within one color byte, and connected square routes
now exercise nine cells. No claim of performance equivalence or complete terrain
contact/route-clock acceptance is made by these checks.

## Shared waterfall assembly regression - 2026-09-13

`terrain_shared_waterfall_render_probe.gd` verifies 36 assemblies over all 16
frames using the native-clock shader with explicit frame overrides. GPU output
matches source-region composition with zero pixel/alpha errors. Actual automatic
native clock synchronization remains covered separately by route probes; this
assembly test does not imply new route/material/production coverage.

## Shared waterfall resource packaging - 2026-09-13

Both projection variants now expose named SpriteFrames for all three rises.
Animation IDs include flow direction, bank/middle role and shared surface offset;
the package manifest records frame regions and native connector anchors. These
standalone resources do not alter engine defaults. Native dimension/reload checks
and six model regressions pass; new GPU assembly and production gates remain open.

## Shared-coordinate waterfall candidate - 2026-09-13

Optional waterfall surface offsets vary curtain lanes and impact foam across
section boundaries while preserving existing default output and connector pixels.
The square width review samples offsets 0..4 from a separate candidate atlas.
Five focused model regressions and four GPU motion cases pass. Arbitrary-width
runtime sampling, isometric equivalents and complete rendered seam/phase inference
remain pending; no production renderer behavior changed.

## Connected square widths - 2026-09-13

Four opt-in depth routes now assemble widths 1/2/3/5 with native directed lake
mouth patterns and unscaled sprite sections. Initial GPU checks pass stationary
terrain/alpha and water motion. Exact whole-route seam/phase inference remains
pending; narrow-route phase results do not prove these width variants. No renderer
defaults, navigation contracts or existing maps changed.

## Isometric rendered-phase validation - 2026-09-13

The isometric depth-route GPU probe now independently identifies displayed atlas
frames for native tiles, river sprites and waterfalls in both directions. All
16 frames are uniquely identified across 64 samples per route with zero error.
Fresh instances avoid freeze/resume clock perturbations. This verifies the new
opt-in route on the tested renderer; platform-wide pause/time-scale semantics
and production engine integration remain separate acceptance work.

## Native isometric depth routes - 2026-09-13

`package-isometric-depth-route.gd` builds both flow directions using native
LakeWater/LakeDepth layers instead of a disconnected baked lake preview.
River sprites retain native positions and draw order; the last overlapping
river sprite is replaced by its matching native stem. Original scenes remain
unchanged. Both GPU cases pass painting, depth, fixed terrain and save/reopen.
Exact phase inference is still pending for this projection; the square phase
audit must not be used as proof for isometric rendering.

## Connected depth waterfall route - 2026-09-13

The opt-in square `depth_waterfall_route_v1` scene combines existing waterfall
art with native river/lake layers and depth painting. Three sprites use original
atlas frames through `terrain_native_clock_sprite.gdshader`, fixing an observed
relative-playback mismatch without replacing existing scenes or defaults.
The GPU probe independently infers displayed native river, sprite river and
waterfall frames: all 16 match across 64 samples, including wrap, with zero pixel
error. Land/alpha stability and save/reopen pass. This does not establish visual
approval or cross-platform pause/time-scale behavior. Isometric depth routes,
wider showcases and production integration remain pending.

## Modular lake mouths - 2026-09-13

The lake-depth binding now supports opt-in wide inlet/outlet profiles, validating
directed river width sections, matching low/middle/high neighbors and native bank
neighborhoods. Existing narrow resources remain supported without enabling wide
profiles implicitly. There are 24 reusable modules and 32 native patterns per
projection, exercised at widths 1/2/3/5 across both flow roles and all ports.

A painted shallow mouth uses a visual mask continuation toward its existing
shallow river. Native Depth paint and logical river data stay unchanged; this
avoids interpreting the forbidden river-depth painting region as a deep border.
All 64 cases pass native/reopen and GPU checks, including separate shallow/deep
states, upstream-edge preservation, fixed banks/alpha, camera and animation.
Visual samples were inspected but not approved. Cartoon grass-bank coverage is
still not complete style/material coverage or production engine integration.


## Modular sea-depth mouths - 2026-09-13

Opt-in `TerrainSeaRiverDepthBinding` accepts the existing narrow and modular
wide inlet profiles for Depth painting. It validates profile identity, directed
upstream section, neighboring edge/middle sequence, inside sea support and bank
neighborhood. Unsupported flips and alternatives are rejected. The shader changes
only sea shading; its existing river blend preserves upstream frame pixels.

All 32 direction/width/projection cases pass native pattern repaint, save/reopen
and GPU depth, river-edge, fixed-bank/alpha, camera and motion checks. Both style
plans retain the complete terrain scope: these are cartoon grass-bank candidates,
not new pixel packs, other bank materials or production engine integration.
Visual approval and large-map performance remain gates; no maps/defaults or
gameplay rules changed and no asset deletion or promotion occurred.


## Lake river-depth checkpoint - 2026-09-13

The standalone lake depth binding now accepts explicitly authored inlet/outlet
contacts in an opt-in TileSet. It validates profile identity, external river flow,
inner lake support and the authored bank neighborhood. Unsupported tile flips and
alternatives are rejected because their pixels would no longer match metadata.
Depth painting is allowed on valid mouth cells, not ordinary river cells.

Eight profiles and native patterns per projection preserve the original 16-frame
current/ripple animation. A cell-role field keeps river pixels unchanged while
blending lake-depth shading over the mouth's lake-facing side. All 16 scene cases
pass native/save checks and GPU river-edge, bank, alpha, camera and motion checks.
These remain one-cell development contacts, not wide river coverage or production
engine-pack integration. Sea-depth connectors, other banks, large-map cost and
visual approval remain separate gates. No defaults, gameplay rules or assets were
deleted or migrated.


## Lake depth checkpoint - 2026-09-13

Standalone lake-depth scenes now use a lake-specific shader and resource roles
with the same calibrated cell-lookup contract as coastal sea. The original
16-frame local-ripple textures remain unchanged. Native Depth painting changes
visual depth only; grass banks, collision/navigation and existing maps are not
modified. Native cell-region lookup is used for shoreline masks because runtime
atlas UV packing is not a stable external-mask coordinate contract.

Native tests pass 650 compatible bank/depth pairs per projection, role mismatch
rejection, invalid-paint retention and save/reopen. GPU checks pass fixed shores,
camera/repeat anchoring, depth erasure, animation and deep-interior color checks;
coastal sea regressions also pass after sharing the mask lookup. Both packs stay
development candidates. Explicit river/depth connectors, other bank materials,
large-map performance/chunking, visual approval and engine-pack integration are
still separate gates. No promotion, default renderer change or deletion occurred.


## Coastal depth checkpoint - 2026-09-13

Opt-in `coastal_depth_review.tscn` scenes in both shared-sea projection folders
keep native Depth painting but apply its mask inside the sea shader only.
`TerrainSeaDepthBinding` derives cell origin from Godot `map_to_local()`, uses
stable diamond texel sampling, and excludes banks and explicit river connectors.
Invalid paint disables the derived field without deleting the paint or changing
the underlying sea. Collision/navigation and renderer defaults are untouched.

The native probe passes 650 compatible neighborhoods in each projection, quiet
steady-state uploads, invalid-paint recovery and save/reopen. GPU comparisons
match the existing open-water overlay exactly and preserve banks, bounds and
camera anchoring. A bounded lookup defaults to 1024 cells per side; chunking and
snapshot cost on large maps remain unverified. These standalone development
scenes are not production engine-pack integration or visual approval. Older
open-sea-only guard scenes remain available; coastal support supersedes only
their contact limitation, not their historical evidence.


## Approved scope

Support native Godot authoring and the engine in both `Tiles` and
`IsometricAutotile`. New compatible ground, shoreline and plateau packs use
47-configuration corner-and-side matching. Retain existing 15-piece dual-grid
atlases and existing direct isometric TileSets. `Painted` and block `Isometric`
retain their own rendering, water, features and art settings without a new-pack
dependency. Map shapes, climate, logical cells, generation and gameplay stay
independent of projection/artwork.

This extends `TERRAIN_LIBRARY_REBUILD.md`; its full material/collection inventory,
approval requirements, folder lifecycle and cleanup protections remain in force.

## Pack and renderer contracts

- Optional TerrainLibraryPack resources: stable ID/version, projection, prepared
  TileSets, normalized terrain bindings, connection mode, elevations, structures.
- Separate cartoon/pixel styles and separately authored front/isometric artwork.
  Runtime cells are 64x64 square or 64x32 diamond. Cartoon masters are 2x runtime;
  native pixel masters are half runtime and exported exactly 2x nearest-neighbor.
- Extend existing transition/native terrain APIs, retaining legacy half-cell
  offsets only on the dual-grid path. Use native map transforms for iso placement,
  bounds and picking; derive peering positions from the projection.
- Validate projection, IDs, required combinations, regions and animations before
  replacing working displays. Never reinterpret square art as diamond art.
- Batch changed cells and neighboring joins; handle erasure and map boundaries.
  Compare incremental results with full rebuilds. Keep cancellable time-sliced
  isometric rebuilds and benchmark both paths.

## Native mapping and engine editing

- Prepared TileSets, Connect/Path painting, alternatives, animated tiles, saved
  patterns and examples for both views. Use alternate matching modes only where
  explicitly authored; cross-layer connections require connector artwork.
- Standalone resources do not require C#. Named ground, water, elevated surface
  and overlay layers remain independently editable.
- Edit -> Apply / Discard through the existing plugin. Persistent working layers
  are painted with native tools. Pending edits block destructive redraw,
  regeneration and projection switching until Apply or Discard.
- Apply validates edits and auto-adjusted neighbors; commits explicit terrain and
  elevation mappings as one undoable operation; preserves unrelated gameplay data.
- Persist working layers and baseline. Reject conflicts without overwriting either
  version. Logical edits update the shared cell store; cosmetic choices remain
  projection-specific. Reuse ownership, notifications and save contracts.
- Navigation, collision and jumping are not inferred from sprite height. Caves
  and bridges do not implicitly create multi-level navigation.

## Artwork and structures

### Surface workflow update

Use plain terrain shapes/masks with shared seamless material textures and separate
grass/stone decoration overlays, following the user's repeated-texture reference.
Do not generate redundant decorated fill sheets. Keep all required matching
configurations: shared texturing reduces duplicated art, not connection coverage.
An opt-in RGB-mask shader and square/isometric basis presets now exist, with
resource/coordinate checks. Existing renderers remain unchanged. Rendered mask
atlases, transform integration, smooth cartoon filtering and visual seam/alpha
review are still pending; this is not a completed or approved production pack.
Details: `docs/game-builder/TERRAIN_MASK_SURFACES.md`.

Retain the full original ground, water, mountain, hill, landform and accessory
scope, plain bases with meaningful optional alternatives and eight geological identities. Sources have
green backgrounds without gridlines; runtime alpha is clean. Lighting is upper
left. Front-facing sidewalls and isometric faces are authored independently.

Specify footprints, pivots, sorting, connection points and rise before production.
Deliver plateau fills, cliff faces/corners/caps/extensions, tiered Prefab 1, broad
Prefab 2, rounded hills and square hills with sloped edges. Standard rise is 64px;
helpers are exactly 32px and 16px independent of projected footprint. Platforms
and narrow stairs stay separate; no ramps. Caves, arches, bridges and ledges use
appropriate scenes/patterns instead of forced single-tile implementations.

Keep lake, river, waterfall and sea motion distinct. Preserve approved compatible
waterfall work; author connectors per projection. Use synchronized 16-frame,
1.2-second loops with fixed terrain. Complete banks, islands, depth changes,
channels, junctions and mouths. Flow helpers reject ambiguity or missing coverage.

## Milestones and current status

### Layered isometric plateau checkpoint

Explicit one-cell river-to-sea inlet candidates now package four selectable flow
profiles. A separate native Depth layer now supplies 47 shallow/deep configurations
per projection plus animated deep fill. It uses the same surface coordinates and
native frame clock as Water. The opt-in placement guard rejects non-open-sea
support or projection/transform mismatch without deleting paint or changing
navigation. Native and GPU depth tests pass, including erasure, save/reopen and
restoration after invalid placement. Runtime map-snapshot polling is a temporary
guard fallback whose large-map cost needs benchmarking before production.
Coastal/river depth connectors, lake depth, visual approval and engine bindings
remain separate work; do not treat this helper as completed engine integration.

Explicit one-cell river-to-sea inlet candidates now package four selectable flow
profiles. The shared-surface extension adds source 3 with twelve reusable width
modules and sixteen saved native mouth patterns per projection (four ports by
widths 1/2/3/5). Source 1 keeps the existing narrow mouths; source 2 keeps the
incoming river sections. Matching remains explicit, not inferred by auto-terrain.
All 32 width/direction/projection cases pass native pattern placement, erasure,
save/reopen and GPU motion/fixed-bank checks. See disposable
generated/test/library/output/sea_mouth_widths_render.json. The source 3 control
atlas uses four-row animation bands; this is verified against the shader clock.
These are standalone candidate scenes, not integrated production engine packs.

Earlier one-cell river-to-sea inlet candidates package four selectable flow
profiles alongside native sea terrain in both projections. An opt-in
shared_sea_v1 variant replaces baked per-cell crests with map-coordinate shading
and control atlases; it retains the same native terrain and inlet selections.
The shader obtains its animation phase from native atlas UV frame bands (not
TIME). TerrainSurfacePlane supplies an optional surface_repeat_cells uniform
so water dimensions remain independent of surface texture repetition. GPU
checks verify fixed phase, camera/map anchoring and continuous interior joins.
The data TileSet requires its supplied layer material and surface plane; it is
not a standalone color atlas and is not yet bound into production engine packs.

The earlier direct-color inlet candidate packages four selectable flow
profiles alongside native sea terrain in both projections. They are not assigned
automatic terrain peering rules: authors choose inflow direction deliberately.
Each has 16 frames over 1.2 seconds; river and sea contact sampling is tested.
Native north-inlet GPU review passes in both projections. See disposable
generated/test/library/output/river_sea_render.json. Other inlet orientations
have atlas/math coverage, not GPU route approval. Visual blending, wider mouths,
other bank materials, engine pack binding and production promotion remain open.

Native sea terrain candidates now support corner-and-side painting with 47
grass-coast masks per projection and 16-frame/1.2-second swell/surf loops.
Sea physical boundary sampling was corrected independently of existing river
and lake code. All realizable occupied-neighbor edge pairs pass animation seam
tests; native GPU checks show moving water and unchanged banks. See disposable
generated/test/library/output/sea_render.json. This does not establish visual
approval, estuary connectors, depth transitions or production pack integration.

The isometric waterfall routes now include explicit north/west inlet connectors
and a lake with its own ripple frames on the shared AnimationPlayer timeline.
Native lake TileMap layouts are retained alongside baked synchronized previews.
GPU validation checks connector IDs, source mappings, native cell placement,
34 synchronized sprites, moving lake/river/fall regions and fixed terrain. See
generated/test/library/output/isometric_granite_lake_render.json. Automatic native
layout edit propagation, full engine integration, sea continuation and visual
approval are not established by this checkpoint.

Connected isometric granite routes now cover one-cell north-to-south and
west-to-east falls at 64px rise. Inlet/outlet placement uses native map_to_local
anchors, with separate upper/lower shared-surface planes. One AnimationPlayer
drives nine sprite frame tracks, avoiding independent playback drift. GPU checks
pass phase agreement, water-role motion, static terrain and unchanged alpha;
see generated/test/library/output/isometric_granite_route_render.json. These are
opt-in standalone development scenes, not default renderer replacements, lake/sea
completion, all-width coverage or production-approved art.

Projection-specific water-only SpriteFrames now encode explicit inlet/outlet
anchors and across-cell steps for north-to-south and west-to-east visible falls.
The physical upper/lower planes use native isometric coordinates; the curtain
keeps actual vertical rise at 16/32/64 game pixels. No navigation is inferred.
Twenty-four assembled width/height/direction cases pass GPU framing, alpha and
visible animation checks. River edge phase and loop closure have unit coverage.
Full terrain route integration and visual motion approval are still pending;
see generated/test/library/output/isometric_waterfall_render.json.

A sibling long-granite-face review scene now uses eight saved AtlasTexture
modules from separately authored light/shade faces, with no duplicate bitmap
payloads. Their original four-piece order per face is explicit; arbitrary
shuffle, cyclic repetition and terrain-engine pack bindings are not validated.
GPU checks pass the assembled silhouette and separate grass surface. See
generated/test/library/output/isometric_long_faces_render.json. This improves
candidate wall variation without replacing existing renderer defaults or packs.

The same art now has native TileMapLayer face bindings, eight atlas sources and
two saved structural patterns. Texture origins are verified against the sprite
reference with zero pixel mismatches. Saved-scene picking round-trips, custom
metadata, erasure and pattern repaint pass. Standalone use requires no C# engine;
matching remains explicit structural patterns, not forced 47-mask terrain. See
generated/test/library/output/isometric_native_faces_render.json and the staging
README. Gameplay elevation and navigation remain independently authored.

Standalone native TileMapLayer examples now include notch, U and hollow-ring
footprints. Saved/reloaded cell counts, occupied and empty surface centers,
projected column-union alpha (including internal seams), exterior emptiness and
top/wall compositing pass GPU checks. See disposable
generated/test/library/output/isometric_shapes_render.json. These three cases
do not establish general terrain-engine authoring or natural concave art coverage.

Saved standalone examples now include two and three tiers, each with 64px rise.
GPU checks compare isolated tier renders against the combined scene and compare
grass at equivalent logical points after elevation compensation. Both pass,
including sampled contact alpha checks. Native instance overrides preserve the
surface plane on save/reopen. These nested-rectangle cases do not establish
general concave/overlapping-structure sorting or navigation. See disposable
generated/test/library/output/isometric_tiers_render.json.

Native TileMapLayer development scenes cover 1x1, 2x2, 3x2 and 5x3 rectangles.
Cliff walls use native isometric placement and Y sorting; the separate plateau
surface is raised 64px and shares the material plane. GPU comparison against
the isolated top finds zero occluded surface samples, and assembled silhouettes
have zero interior alpha gaps. This does not validate concave joins, holes,
multi-tier sorting, alternate rises, isometric waterfall connectors or engine
pack publication. Repeated wall artwork still requires visual improvement.
The report is generated/test/library/output/isometric_plateaus_render.json.

### Execution priority (supersedes the prior working order)

1. Reconcile requirements against existing source/candidate evidence using the v2
   specification and explicit coverage bindings. Unknown assets remain retained.
2. Finish native grass/dirt masks and shared-surface examples in square AND
   isometric projections, including terrain painting, seams, alpha and transforms.
3. Finish one grass/rock water/elevation family: river -> waterfall -> lower river
   -> lake and river mouth -> sea, plus a multi-tier cliff/plateau composition.
4. Review holes, islands, corners, branches, widths 1/2/3/5, rises 16/32/64 and
   taller composed walls, sorting and cross-layer connectors before expansion.
5. Expand remaining cartoon families, then independently authored pixel equivalents.

The rebuild plan's Complete modular coverage table and specification.json define
the mandatory family inventory. Do not substitute 47 generic masks for river
flow variants, cliff faces, height transitions or complex structures. Declare
connector direction/material role/opening width/elevation; native transforms place
them. Report missing/incompatible artwork and ambiguous flow instead of guessing.

Masks preserve authored borders/alpha; seamless surfaces use a shared material
plane, not per-tile or camera coordinates. Use smooth cartoon and nearest pixel
filtering. Decoration is separate. Lake, river, waterfall and sea retain distinct
motion, synchronized 16-frame/1.2-second loops at their connectors and fixed terrain.

No new structure-editor feature takes priority over this sequence. All original
compatibility, production-isolation, preservation and cleanup gates remain active.

### Existing implementation, not milestone completion

1. Renderer foundation: implemented initial pack validation and opt-in publication
   for both projections. Real front grass/dirt binding supplied. Local edit probes
   compare with rebuilds. Legacy atlas switch-back, view-grid and isometric
   staleness probes pass. Full all-view regression and broad benchmarks remain pending.
2. Dedicated isometric grass/dirt runtime pack: pending. Two appearance-approved
   source references are retained; calibrated masks, regions and joins are not yet
   validated. Diagnostic test atlas is not art.
3. Explicit Apply: terrain-kind working copies and Inspector Edit/Apply/Discard
   implemented for both projections. Native brush, erasure, cosmetic alternatives,
   atomic conflicts, undo/redo and pending scene save/reload probes pass. Pending
   sessions guard redraw/regeneration/view changes; gameplay metadata is preserved.
   Explicit elevation profiles and region staging now map authored alternatives
   to normalized logical values and independent pixel rises in both renderers.
   Profile coverage, undo, cross-projection persistence, incremental rendering and
   fractional-elevation scene roundtrips are tested. The maintained headless editor
   harness now exercises actual Inspector buttons, scene undo/redo, pre-save
   notifications and pending reload with editor-only seed restoration in both
   projections. Rejected Edit setup preserves existing session state and cosmetics.
   Pointer-driven brush/layout review, height-aware cliff composition and structure
   editing remain pending. This is not full visual milestone acceptance.
4. Cartoon water/elevation showcases in both projections: pending, building on
   retained lake/river/waterfall candidates; no production approval implied.
5. Remaining cartoon collections, then native pixel equivalents: pending.

Acceptance includes all connections, erasing/repainting, boundaries, islands,
junctions, native picking, height/pivots, sorting, transparency, animation and
cross-pack joins. Verify save restoration, metadata preservation, invalid packs,
view switches, all four renderers and all existing map-shape presets. Maintain
one showcase per style with separate front/isometric scenes. Routine screenshots
and reports belong in ignored test output. Visual approval gates promotion.

## Preservation and cleanup

Structure placement foundation now provides opt-in authored scene placement for
both projections, pack layout metadata, native grid anchors, pivots, footprint
bounds and saved ownership. Headless diagnostic scene tests cover both projections.
Inspector staging and Apply/Discard for additions now use one undoable reparent
transaction, retaining authored instances and pending scene ownership. Diagnostic
tests cover undo/redo, reopening, conflicting IDs and changed layout contracts.
This is not structure authoring milestone completion: existing-instance removal,
height-aware composition, connector checks and
real structure artwork review remain pending. No existing prefabs are replaced.

World-scoped structure registration now participates in existing pending-edit
guards and projection-specific visibility. Tests cover blocked switching,
generation entry, orphaned pending children, all four visibility selections and
independent unregistered worlds. Structure edits/history reject an owning world
that is generating or showing another projection. Combined terrain/structure
transactions and full visual authoring acceptance remain pending.

Structure editor verification now passes in both projections using the actual
Inspector handlers, scene undo manager and editor disk-save path. Reopening
preserves pending additions and validates Apply; published additions survive
disk saves after Discard. The opt-in harness is maintained under tests, while
fixture scenes and logs stay in disposable output. Pointer-driven visual review
and real structure artwork acceptance remain pending.

Existing-instance moves now stage one saved placement record without duplicating
the live scene. Apply/Undo preserve instance children and gameplay metadata;
changed placements or transforms reject conflicting commits. Runtime disk-reopen
tests and actual Inspector move/Undo/Redo pass in both projections. Multi-move
batches, deletion and visual acceptance remain pending.

Staged moves now have an editor-only destination-cell outline and direction marker,
using native square/diamond geometry and both layer transforms. No preview scene
instances or runtime drawings are created. Tests cover geometry, Apply clearing
and Undo restoration; rendered visual acceptance remains pending.

The structure Inspector now has a live-instance selector that loads ID/anchor,
refreshing catalog and instance lists, and basic setup feedback. The actual
Inspector selector/staging/Apply/Undo test passes in both projections. These
authoring improvements do not imply completed artwork or visual approval.

Continue generated/dev, test and production separation. Approved legacy belongs
at repository-root legacy_art outside the addon. Production cannot depend on dev,
test or legacy; packaged reuse needs provenance. Unknown assets are retained.

Review literal/dynamic references and duplicates. Present exact cleanup paths,
hashes, reasons, retained replacements and byte totals. Relocate in verified
batches, update references and preserve identities, then request source-removal
approval. Quarantine first with verified restore metadata; stop on changed files
or new references. Permanent disposal needs separate approval. No immediate
deletion is authorized or performed.

### Shared-Surface Fixture Progress

The isometric granite corner now has a native 64x32 TileSet with a 64x96 sprite,
ground anchor (32,80), and Godot texture origin (0,32). The offset sign was verified
against an explicit Sprite2D reference in a GPU test. Its separate plateau mask
uses an elevated shared-surface plane. Alpha tests protect grass from chroma-key
holes. This is one corner candidate, not complete isometric elevation coverage;
neighbor joins, height extensions and waterfall integration remain open.

Central reconciliation now includes 143 additional water/elevation/fill candidate
bindings, with exact runtime regions, resources and limited coverage descriptions.
Existing reviewer decisions are preserved; reconciliation cannot promote assets.
River direction labels denote outflow heading, left/right banks are relative to
travel, and lake inlet/outlet directions denote the external river port. Technical
waterfall layers remain evidence without implying complete geological structures.

River/lake connectors are packaged as explicit flow-profile tiles alongside the
native lake terrain set, not inferred by automatic terrain painting. Eight
profiles per projection preserve lake-facing atlas pixels and river-port phase.
The square painted waterfall route now reaches a lake using a shared material
plane across sprites and TileMapLayer. Both projection connector reviews and
the square route pass GPU checks. Wide mouths, sea adapters and the isometric
waterfall route are still pending; defaults and production packs are unchanged.

The square painted river/waterfall route now includes a separately layered
bottom-contact sprite. Its footprint is 320x40, its ground anchor is y=20, and
its visual-rise contribution is zero. It remains behind animated water and
does not alter logical elevation or navigation. Contact artwork is a candidate;
native repeated/corner contacts and the isometric counterpart are not complete.

TerrainSurfacePlane now also accepts explicitly bound Sprite2D/AnimatedSprite2D
surfaces, preserving TileMapLayer projection checks and atomic validation. A
separate grayscale mask lets the painted cliff plateau share grass coordinates
without replacing the source art or rock face. GPU tests verify the rim against
underlying ground, unmasked rock preservation and scene-translation stability.
This resolves the prototype rim texture seam, not the missing bottom connector.

The square grass/granite source now has calibrated 64x80 front regions and a
development scene combining a static cliff with independent animated river,
waterfall and lower river. GPU checks confirm terrain stability and water motion.
Source-contiguous regions are not an arbitrary-repeat or native terrain set:
grass-rim blending, bottom contacts, side/corner profiles and isometric resources
remain unvalidated or missing. No working renderer resources were replaced.

Front-facing water-only waterfall SpriteFrames now support four repeatable width
sections and 16/32/64px visual drops. Upstream/downstream interfaces retain the
river_sections_v1 phase. Native playback checks all four motion roles and stable
alpha across widths 1/2/3/5. These are standalone technical candidates, not terrain
engine defaults; cliff art, isometric projection and route integration remain open.

Directed narrow river junctions provide 32 explicitly routed candidate presets
per projection in river_junctions_v1, with standalone TileSets and review scenes.
They share grass surfaces and the narrow-channel motion contract. Named flow
custom data is not inferred by terrain matching. Open-edge checks cover all 16
frames; GPU probes check motion, fixed land and missing cells. Wide junctions,
two-to-two routes and cross-role connectors remain pending. No defaults changed.

Directional adapter route assembly now selects native authored pattern variants
and straight-section flow profiles for north/south/east/west travel. Eight review
scenes are available across the two projections; GPU visibility/static-land
checks pass in all four directions. Renderer defaults remain unchanged.

The maintained adapter-pattern GPU probe now exercises all saved patterns in
both projections, checking valid alternatives, exact footprint area, opaque
coverage and replaced surface markers. All 32 cases pass. Per-direction route
integration and appearance approval remain separate requirements.

Width adapters now have saved native TileMap patterns and a composed route in
both projections. Explicit alternative IDs prevent blank pattern cells. GPU
checks cover placed-cell visibility and fixed banks during animation; complete
directional review and flow/structure adapters remain pending.

Width-adapter geometry now shares the straight-river phase and bank contract.
Adjacent width transitions pass endpoint compatibility tests; unsupported jumps
are explicit errors. This does not yet supply native adapter resources or complete
the river-to-waterfall showcase.

The requirements ledger now links current square/isometric grass/dirt and lake
boundary resources, retaining earlier candidates as alternatives. No validated
or approved status is inferred from the existence of resources or passing probes.
Flow, depth and structure requirements remain separate from these binary masks.

Native square/isometric river section TileSets now support repeatable straight
channel widths through separate banks and middles. Four flow directions are
explicit profile metadata, not terrain-matching guesses. Width scenes and GPU
static-land checks pass; transition/junction artwork and route integration remain
incomplete. No existing renderer defaults or approved resources are replaced.

Standalone connector profiles and route validation now reject mismatched widths,
elevations, flow directions, projections, motion roles, phases and missing atlas
regions. Existing square river port resources and a bent-route regression probe
are implemented. No automatic flow inference or missing connector substitution
is introduced; renderer defaults remain unchanged.

Dedicated lake grass-bank TileSets now use native 16-frame animation while the
shared grass surface and bank contours remain fixed. Separate square/isometric
scenes load without C# and pass sampled GPU motion/static-land checks. The full
river-to-waterfall-to-lower-river route and its connectors remain incomplete.

`terrain_mask_all_render_probe.gd` now renders all 94 square/isometric cases and
checks exact projected footprint coverage and mask-color replacement. It passes
on the GPU and writes both review overviews to test output. Complex composed
joins, elevation connectors and final appearance approval are still separate gates.

`TerrainSurfacePlane.gd` now binds explicit layers to a common world material
plane without requiring C#. GPU split-layer and compensated map-transform
comparisons pass in both projections. Missing layers, incompatible projections
and singular transforms reject group updates; bound materials are isolated.
This is opt-in and does not alter existing renderer configuration.

All 47 native configurations now pass per projection, together with representative
widths and erasure/neighbor preservation. Surface candidates use shared calibrated
256px runtime resources from 512px masters (four-cell repeats). GPU stability
still passes after export. Full rendered coverage and production approval remain open.

An opt-in cartoon shader now provides linear primary/secondary surface sampling
without changing the existing nearest-filtered shader. Red/blue mask interiors
select grass/dirt; authored border colors and source alpha are retained. Separate
surface candidate scenes in both projections pass the GPU camera-motion check.
Production promotion and calibrated exports remain pending.

The GPU probe `tests/terrain_mask_render_probe.gd` now captures native/2x layouts,
checks nonblank textured output and camera-translation stability. Both layouts
pass; isometric development fixtures now explicitly use diamond-down layout.
These checks do not establish complete 47-case rendered seam acceptance.

`tools/terrain-library/build-mask-fixtures.gd` builds standalone square and
isometric grass/dirt TileSets and review scenes under development staging.
Each has 47 legal masks plus background, projection-specific named Godot
neighbors, native Connect painting and a shared-surface material. Headless
neighbor, painting, coordinate round-trip and save/reload checks pass.
The solid-color surface is a technical placeholder: rendered texture alignment,
seams, smooth cartoon filtering and visual approval remain pending. Existing
renderer defaults and production resources are unchanged.
