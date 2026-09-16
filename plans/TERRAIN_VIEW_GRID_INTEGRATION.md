# Terrain Views And Grid Integration

Objective: review and enhance terrain views and how they are connected to and used
by the grid engine. Status: active, not complete.

Updated visual objective: retain the original painted map and add separate Pixel
Art and Cartoon profiles. All views share prop-size constraints and authoritative
terrain distribution. Roads are out of scope. Visual quality and grid correctness
must both be demonstrated.

## Current Checkpoint (2026-09-06)

- Latest boundary revision: lake banks now retain fine lake membership and an
  independent width through live-grid handoff/save/load. The painter reconstructs
  them from a lake distance field instead of cell-majority sand. Inland material
  coverage is aggregated by terrain across a radial 5x5 neighbourhood before
  sharpening, shared by Original, Pixel Art and Cartoon. Low-frequency edge
  variation replaces high-frequency cell-edge jitter. Texture/prop scale is unchanged.
- Current evidence: `tests/output/lab_styles/large_closeup_style_0.png` through
  `large_closeup_style_2.png`, Huge 128x80, seed 31415. This intentionally changes
  inland contours; the older approved image below is a historical reference,
  not a claim of byte equality after the latest boundary revision.
- Current verification: clean build; 62/64 integration probes passed. The lava
  GPU process crashed in native Windows/OpenGL code, then passed on direct rerun.
  The existing generated-coast centre mismatch below still fails. Lake tests
  validate 10,800 GPU radial samples plus independent CPU distance checks.
- Diagnostics are now opt-in in the lab. Large generated RGBAF coast fields
  retain smoothing at native resolution when doubling would exceed the budget.

- Superseded: the minimum whole-cell sand ring was rejected visually. Beaches
  now use fine Euclidean ocean distance; positive sub-cell widths are not rounded
  up to a gameplay cell. The painted grass and beach use the same smoothed coast.
- Historical approved baseline: `tests/output/shoreline_approved/cartoon.png`. After the earlier lab changes,
  a fresh Cartoon capture has the identical SHA-256:
  `3C09DF5856AA8A556D816621AE54E8A3B1295CBECF560DEB823291AB58A2B345`.
- Implemented: one top-of-panel view menu exposes Original, Pixel Art, Cartoon,
  Game tiles, Isometric, and Isometric tiles. Presentation selection never
  regenerates the grid. Dedicated scene tests exercise the actual menu.
- Implemented: the previously empty isometric-tiles view uses a complete native
  Kenney TileSet with explicit biome assignments, including sand and water.
  All 1,024 lab cells render with zero missing/unmapped assignments. This uses
  complete tiles, not invented transition peering bits on the grass-only atlas.
- Implemented: optional continuous luminance detail over Game Tiles, preserving
  the authored alpha and borders. This is not a replacement art pack; its coarse
  borders and the flat isometric view still need visual-quality review.
- Verification: clean build; lab-style GPU probe and art-style GPU probe pass.
  Integration checkpoint: 59/61 passed. The repaint probe's parse error was
  fixed and that probe passes separately. One production GPU failure remains:
  `terrain_water_surface_probe.gd`, seed 31415, cell (21,3), zoom 0.5. Smoothed
  generated coast disagrees with the live cell classification at that centre.
- Next: resolve the shared geometry/gameplay contract without reintroducing
  block-shaped centre patches or altering the approved appearance blindly.
  Keep the failing GPU assertion; rerun all probes after the fix. Do not claim
  complete terrain correctness, or author smooth isometric transitions by
  applying guessed labels to unrelated atlas frames.

## Painter Visual Work

Inspection of the actual addon lab's painted capture shows broad cloudy material
transitions and conspicuous tile-shaped coast geometry. Some rectangles in the capture
are intentional test edits, not generation defects; use a clean visual fixture before
judging the final appearance. Existing integration passes do not establish visual quality.

1. Add clean repeatable painted-view captures at map scale and close range, with no
   survey/placement debug overlays. Keep the same seed and texture assets for comparisons.
2. Improve material edge definition without holes, global alpha washes or disabling
   mipmaps. Separate transition sharpness from texture scale and shore smoothing.
3. Check the water mask against grid navigation/placement: improve coastline appearance
   without moving visible dry land so far that units appear to walk through water.
4. Review actual shipped texture detail, palette and scale together; add or replace art
   only after inspection, and preserve terrain/biome-appropriate feature placement.
5. Compare captures across all projections, zoom levels and live edits, and retain
   functional navigation, placement, height and save tests alongside visual acceptance.

## Current Scope

Primary work and acceptance belong to the original addon and its own demos/tests.
Do not edit the external Oilfield Days game for this goal. Implement reusable
terrain/grid behavior in Beep.Godot and demonstrate it in this repository's lab.
Game-specific well placement, drilling history and save-sidecar work are not required
to complete this addon review and must not displace terrain/grid integration work.
Older external-game entries below are historical integration evidence, not the active
work queue. Outstanding addon work includes cross-view correctness, authored-isometric
terrain art, terrain/grid editing and save lifecycle, and large-map repaint cost.

## Verified Progress

Ground cover, prop proportions and restored plain isometric art, 2026-09-06:
- User clarified that gray regions inside green land are a distribution bug,
  not a request for a better gray texture. Removed unconditional mountain-rock
  and climate-off hill-gravel substitutions. Coherence cannot absorb rainfall
  ground into peak material. Height/relief, coast and themed Rock/Lava remain.
- Wired the existing relief renderer into the lab with an authored RockObjects
  prefab. Four unchanged individual Rock1/Rock4 sprites come from Art/Rocks.
  Inspector texture arrays, density and shared dry-anchor scatter replace gray
  ground as the visual representation of these decorative rock props.
- Corrected the disproportionate flat-view art: trees 1.35 cells, small/large rocks
  0.4/0.6, reduced tree clumps to one/two and 12% size variation. Reeds retain
  their own smaller scale. Cached drawing bounds expose measurable proportions.
- At the user's request, restored plain Kenney isometric blocks in both authored
  demos, including the matching 8x7 atlas, 111x64 footprint, 32-pixel step and
  grass frame 54 without alternate grass frames. The earlier detailed-atlas
  switch documented below is superseded; it is not the current default.
- Clean rendered captures inspected for both painted and plain-block iso views.
  Lab seed 31415/Tiny has zero rock/gravel ground cells, 24 raised cells and nine
  decorative rock objects. Regression checks cover climate on/off, all relief
  levels, coherence, density zero, determinism, unchanged grid data and art loads.
- Still open: full sprite-footprint water clearance, rock gameplay interaction,
  equivalent isometric rock props, foliage palette and surf-band visual polish.
  This is not final acceptance of every terrain view or all supplied art styles.
- Final verification: clean build; 55/55 integration checks passed, zero skipped
  (38 headless, 17 GPU). Prop frame extents in the lab regression: tree minimum
  76.36 pixels, rock maximum 40.08 at 64-pixel cells. Live-cell and synthetic-cliff
  fixtures now specify their count/atlas explicitly instead of inheriting art defaults.

Absolute-grid material phase, 2026-09-06:
- Reproduced an actual full-map/crop mismatch: geometry and cell IDs used
  BoundsOrigin, while ground textures and shared water/noise restarted at zero.
  The initial negative-origin grass fixture had mean RGB error 0.191874.
- All three shader-backed addon renderers now bind map_origin. Painted material
  sampling/transition noise and shared water texture/noise/foam use absolute grid
  coordinates; ID, shade and coast-map UVs remain local. No new world or grid writes.
- New GPU probe compares two crops at positive/negative origins, 0.5x/1x/2x,
  transformed views, grass/rock boundaries, coastal sand and water, and open sea.
  Painted/flat-water errors are below 0.000002; isometric below 0.00009 in the
  tested interiors. Native water binding and unchanged live records are checked.
- Still open: crop-edge coast/shade neighbour halos. This fix anchors texture
  phase, not a complete streaming-chunk system or new terrain-generation method.
- Verification: clean build; full integration 54/54 passed, zero skipped (37
  headless, 17 GPU), plus fresh clean lab visual capture. The capture still shows
  overly bright foliage against olive ground and several parallel surf bands.
  Next visual review should compare those with the supplied reference art at
  fixed seed/time; do not change generation or grid classification to hide them.

Painted material tiling and bedrock, 2026-09-06:
- Full painted-renderer read and actual material inspection found a single repeat
  scale for unlike ground textures, and loose gravel used as bedrock. Added optional
  native TerrainMaterialTiling resource: nine texture-slot scales, aliases sharing
  their texture scale, and beach/submerged sand using the same resolved repeat.
- GPU checks cover each slot/alias at 0.5x/1x/2x, unaffected other materials and
  open sea, visible seabed, resource removal/reset, and unchanged map-resource
  identities/live grid bytes. No generator or gameplay-grid state changed.
- Added separate opaque mipmapped bedrock artwork based on the user's stone atlas.
  Four painted addon demos share Rock=6 tiling. Existing source art stays intact.
- Two generated versions failed raw seamlessness checks. Retained the first
  artwork with optional narrow renderer-side repeat-edge correction, not a false
  seamless-bitmap claim. GPU boundary jumps fell over 96% at three zooms with
  byte-identical interiors, full opacity and unchanged maps. Additional shader
  samples occur in the edge bands; this feature is not free or stochastic tiling.
- Exact prompts, rejected-edit evidence and renderer limits are recorded in
  docs/terrain-engine/BEDROCK_MATERIAL.md. Targeted editor import completed but
  retained existing shutdown RID leaks. No external game changes.
- Still open: overall palette/style, four-view art parity, cold large-map work,
  canopy footprints, and the unfinished authored-isometric terrain atlas.

Feature display integration, 2026-09-06:
- Read both complete feature renderers and inspected actual user-provided tree
  art. Flat clumps stacked 5-8 sprites in 18% of a cell; the full mixed sheet
  added orchard fruit/blossoms. View-local hashes also reseeded retained cells
  when BoundsOrigin changed.
- Shared bounded TerrainFeatureScatter now spreads dry anchors, keyed by absolute
  grid identity. Isometric props now use the owning surface's fine-water sampler,
  not just a cell's land flag. Terrain state, ownership and navigation are unchanged.
- Extracted the isometric frame-binding parser for use in both views. The lab
  and standalone painted demo use foliage-only woodland frames, trunk anchors
  and smaller requested clump counts from the existing user-provided sheet.
- Lab regression reproduced an independent isometric seed left untouched by the
  world coordinator. Draw now supplies the world seed to both feature views
  before surface events/visibility rebuilds, including inactive views after reseeding.
- Focused checks pass: crop/seed stability, 64-seed spacing, 32-seed cross-view
  dry-anchor agreement, actual GPU frame selection/rebinding at three zooms,
  unchanged live cell bytes and real lab captures. Details:
  docs/terrain-engine/FEATURE_SCATTER.md.
- Final regression after seed wiring: 51/51 passed, zero skipped (36 headless,
  15 GPU), zero build warnings/errors. Fresh painted and isometric captures were
  inspected. No external game was edited and no new terrain world was introduced.
- Remaining: canopy footprints can overlap water or each other; the retained art
  still has a different style from the ground. Rock material scale, large cold
  rebuilds and authored-isometric terrain art remain open, not disguised by props.

Cached shoreline reconstruction, 2026-09-06:
- Original addon only: painted, tiled and elevated-isometric water views share
  TerrainCoastField.RenderCache. TerrainWorldComponent remains the coordinator;
  GridCellDataComponent owns live state. No new world or legacy adapter was added.
- Preserve the raw distance field and reconstruct only its display texture at
  twice the resolution using native Image.Resize(Cubic). Replicated edge padding
  prevents border artifacts. Constant RGBA8 blocks use nearest expansion; varying
  blocks use shared halos, verified byte-for-byte against whole-image cubic output.
- Cache by immutable source texture and dimensions. Tiled/isometric live coast
  fields now also reuse LiveCache, so unrelated cell metadata edits do not pay
  reconstruction costs. Water edits still invalidate the field.
- Keep the existing single linear shader sample; no per-fragment cubic filter,
  albedo blur, global alpha wash or modification of the grid's water patches.
- Skip reconstruction at cell resolution or beyond a 4096-pixel dimension /
  4,194,304-output-pixel cap (16 MiB for RGBA8). This is a bounded display cache,
  not chunk streaming or a guarantee of hitch-free large water edits.
- Focused GPU/reference checks pass: analytic curved-shore mean error fell from
  0.533 to 0.269 pixels. Profiled first reconstruction on a curved-coast fixture
  at 32/144/240 cells: 6.8/40.7/106.3 ms; cached hits below 0.002 ms. These are
  local measurements, not performance promises or visual acceptance of the lab.
- Full addon regression: 51/51 passed, zero skipped (36 headless, 15 GPU), with
  a zero-warning/error build. The clean lab visual probe also passed at
  0.36x/1x/2x and preserved ID/shade/raw coast bytes. Inspected fresh captures
  still show mismatched rock/grass scale and prop style; no GPU speedup is
  claimed from clock-sensitive frame timings.
- Remaining: expensive cold large-map edits, material/prop palette and scale,
  missing fourth-view terrain art, and complete feature-footprint water checks.
  The terrain/grid goal is still active; an external game is not its test harness.

Painted lava and themed-ground correctness, 2026-09-06:
- Added a distinct painted lava material (ID 13, LavaTexturePath), new opaque
  mipmapped basalt/fissure albedo, and bindings in the four painted addon demos.
  Water IDs remain 11/12; lava no longer aliases ordinary rock or gets skipped
  as water. Static albedo only, not lava-flow simulation or a new world recipe.
- The real volcanic capture reproduced a deeper generator defect: scale cleanup
  treated all flat rock as unwanted peak material. Seed 31415 at 32x32 generated
  591 lava/15 grass/zero rock. Themed palettes now survive relief flattening,
  using the biome classifier rather than another preset list. The same seed
  yields 378 lava/228 rock/no grass with unchanged water cell counts.
- Added 21 themed fixtures over seven presets and three relief tiers, preserving
  fine materials, water, elevation and shade. Existing climate peak cleanup and
  minimum-size relief/water tests still pass. Scale rules remain enabled.
- Reproduced starts on lava despite live-grid movement/build restrictions;
  start eligibility now excludes lava. All-lava and sole-habitable-cell fixtures
  verify the behavior without manufacturing habitable land.
- Real GPU checks distinguish lava/rock/water at three zoom levels. Navigation
  and placement update after rock edits; coast bytes stay unchanged. Captures
  under tests/output/lava_material use actual preset data and no debug grid.
- Full integration: 50/50 passed, zero skipped, clean build (36 headless, 14 GPU).
  Asset provenance, exact prompt, import and limitations: docs/terrain-engine/LAVA_MATERIAL.md.
- Remaining: refine ordinary-rock palette/repeats and shoreline stepping;
  supply volcanic art for other projections; validate shader sampler limits on
  lower-end targets. Review climate snow-versus-peak provenance and themed
  lake-bed fallback separately; those policies are not proved by this fix.
  The full visual/grid goal remains active, including the unfinished fourth view.

Authored isometric view contract, 2026-09-06:
- Read the full native autotile renderer and its TileSet. The lab's blank fourth
  view is real: 17 grass variants, zero authored peering assignments and missing
  biome transition coverage. Do not remove the incomplete diagnostic or treat
  this atlas as a complete multi-biome set by assigning unrelated terrain IDs.
- Reproduced two independent defects: conflicting grass bindings or a malformed
  extra entry could yield valid=true, and the first invalid-source rebuild left
  existing authored IsoTerrain cells visible because the cached layer was null.
- Parse/validate one kind-to-terrain mapping before painting; conflicts and
  malformed indices fail the configuration. Discover and clear the existing
  managed layer before source/art checks, including on the first rebuild.
- Read each live/generated cell once, group all aliases by terrain index, then
  retain native batched SetCellsTerrainConnect matching in first-binding order.
  No second world model, manual atlas-index guess or fallback terrain was added.
- Focused tests now pass. Bounds/unmapped-cell and detach/reattach checks also
  pass; those did not reproduce a defect and are not claimed as fixes.
  Full integration completed: 48/48 passed, zero skipped, clean build. The
  missing authored transition art remains incomplete despite the passing gate.
- Remaining authored-view work: obtain or author explicit transition art for
  the supported biome set, verify peer edges rather than only centre IDs, and
  check real GPU edge continuity and gameplay picking before lab acceptance.

Painted meadow artwork and runnable root demo, 2026-09-06:
- Added new green/dry meadow PNGs using built-in image generation. Quiet ground
  colour and small blade strokes replace the broad tangled mottling in the
  painted demo bindings. Original supplied textures remain untouched. Exact
  prompts/provenance and import instructions are in MEADOW_MATERIALS.md.
- Both returned assets are 1254-square, fully opaque and imported with mipmaps.
  Block-mean luminance variation is about 23% of the old textures; average
  opposite-edge mismatch is lower. This does not claim pixel-identical wrap.
- Fixed-seed GPU captures at 0.36x/1x/2x keep ID/shade/coast bytes unchanged.
  Separate green and dry art preserve biome distinction without recolouring
  cells or changing generator settings. Other projection atlases are unchanged.
- Fixed root run/main_scene: it referenced a removed demo. A real GPU run now
  starts the authored terrain generator lab and exits without runtime errors.
- Bulk editor import encountered a missing Blender path; targeted PNG reimport
  succeeded. Editor shutdown RID leaks remain a separate unresolved issue.
- Expanded integration suite to 48 checks: 48 passed, zero skipped, clean build.
  Added tests/output/.gdignore so diagnostic captures are not imported as project
  assets. They remain on disk and probes can continue writing captures there.
  Remaining art work includes consistent desert/stone detail and cross-view styling.

Texture-driven painted transitions, 2026-09-06:
- Reviewed the actual grass/dry-grass images and supplied grass/terrain atlases.
  Broad mottling is baked into the current seamless sources; the alternatives
  have gutters/borders and are not drop-in seamless material replacements.
  No supplied originals or external game files changed.
- Added MaterialEdgeDetail to the painted component/shader. Positive luminance
  weighting lets texture features influence material borders without extra
  texture fetches, terrain overlays or changes to the generated/live grid.
  Zero disables it. This is albedo-guided blending, not physical height mapping;
  it can favour brighter materials slightly at an existing material border.
- GPU fixtures pass at 0.5x/1x/2x zoom: bright detail advances, dark gaps recede,
  interior pixel difference is zero, and ID/shade/coast bytes stay identical.
  Coverage and sharpness fixtures still pass. Full-suite validation completed:
  47/47 passed, zero skipped, clean build. No native shutdown failure occurred
  in this run; the earlier intermittent AV remains unproven rather than fixed.
- Fixed-time lab captures compare off/on at overview, 1x and 2x. Improvement is
  subtle at the grass/gravel edge; this does not resolve the source palette,
  large texture mottling, tree-art mismatch or authored-isometric terrain art.

Painted inland water flecks, 2026-09-06:
- Reproduced a generation cleanup defect: five-cell lake/river fixtures were
  removed at cell resolution but each left five fringe samples in neighbouring
  majority-dry cells. This is source data, not an alpha or texture-blur effect.
- Added fine-component reconciliation after small-water cleanup. Lake/river
  sample components without a surviving matching water cell adopt the receiving
  cell data. Retained bodies keep their fringes; ocean samples are excluded and
  removed river mouths inside ocean cells become ocean rather than dry holes.
  Cell topology and the frozen landmass footprint remain unchanged.
- Focused tests pass for removed/retained lake and river bodies, scale-rule
  opt-out, ocean mouth preservation and four actual generated seeds. The full
  integration report records 47/47 passed, zero skipped. A subsequent clean
  build and focused rerun also pass the strengthened exact retained-body
  shape/material assertions and drained-sample land/material checks.
- Clean lab capture no longer shows the isolated water flecks among the trees.
  Seed 31415 remains two landmasses and two dry regions, with 48 features and
  six starts. Raw fine river coverage now reaches zero because those short
  watercourses were already removed from the gameplay cells; retained longer
  river fixtures still survive. Ocean and lake options have not been removed.
- This fixes stray-water consistency, not all coast roughness, material palette,
  authored-isometric art, arbitrary footprints or the earlier intermittent AV.

Painted refresh cost, 2026-09-06:
- Confirmed geometry was already preserved, but validation made three native
  tile-property queries per cell on every refresh. Fill now checks actual used
  bounds plus a native source/atlas/alternative-filtered cell count, falling back
  to individual repair for holes, outliers and substituted tiles. No stale
  size-only cache, new node or event subscription was added.
- Added grid TerrainRevision for terrain/elevation/patch/record changes. Painted
  views retain map textures keyed by source identity, revision, default terrain,
  bounds and coast settings; generator-only views use resolved field identity.
  Workflow signals remain unchanged; existing-cell flags/crops do not upload
  ground textures. Look uniforms and geometry still reconcile on explicit refresh.
- Same warmed 144/240-square fixture measured 44.999/100.477 ms before,
  25.652/57.300 ms with bulk validation, and 0.827/2.495 ms with map reuse.
  ID, slope-shade and coast hashes match. These are unchanged-refresh medians,
  not new generation or terrain-edit latency claims.
- Expanded the painted-origin probe for substituted source/atlas/alternative,
  flag/crop reuse, look updates, coast detail and a default-kind round trip with
  new cell creation. Corrected a test call's missing C# optional argument, then
  reran the complete suite: 47/47 passed, zero skipped, clean build. The final
  run measured 0.818/1.786 ms for warmed unchanged 144/240-square repaints.
- This does not complete delta-region repainting, generation latency, arbitrary
  collision footprints or visual art refinement. Terrain/elevation edits still
  rebuild their data; the improvement removes redundant unchanged/flag-only work.

Live fine-shoreline implementation, 2026-09-06:
- Completed the generation-to-grid handoff with immutable GridTerrainWaterPatch
  values in cell records. Uniform cells use one bit; mixed cells keep the
  generator resolution (up to 24). Snapshots persist compact Base64 bit payloads.
  TerrainWorldComponent coordinates generation, while GridCellData owns the live
  result; there is no additional world, generator fallback or renderer-owned save.
- Explicit SetTerrainKind/FillTerrain replaces a cell's patch, including same-kind
  painting. Flags/crops/metadata preserve it. Full/partial LoadCells restores it;
  malformed patch decoding occurs before mutating the live store. Coast caches
  observe patch identity as well as water flags, so same-kind edits invalidate.
- Defined the movement-centre contract: within the central half-cell square,
  gameplay water membership wins; outside it the original fine samples survive.
  This is not arbitrary-radius collision or full-building-footprint validation.
- TerrainFeatureRenderer checks final jittered anchors against the same fine
  sampler, falls back to a dry cell centre, or omits the stamp. Added separate
  anchor diagnostics so SpriteAnchor does not confuse image centres with roots.
- New headless/GPU probe covers four seeds, detail 4/8, 245760 non-core fine
  samples, binary snapshot round trips, same-kind edits, partial restore,
  malformed-input atomicity and maximum-jitter prop anchors. GPU checks inspect
  all 1024 cell centres per seed at 0.5x/1x/2x zoom.
- Full integration run passed 47/47, zero skipped (34 headless, 13 GPU), clean
  source build. Existing recipe/source-order, movement, placement and cross-view
  checks also pass. Native shutdown passed this run; earlier intermittent AV is
  still unproven fixed, not closed by a single green run.
- Clean same-world comparison now differs at 94/16384 coast signs rather than
  643/16384. The source-only field still has eight raw fine/cell centre mismatches;
  the new live-surface GPU test confirms the constrained rendered centres agree.
- Visual review: improved shoreline detail, but tiny residual water flecks near
  vegetation and narrow inlets still need art/shape review. The isometric block
  view remains intentionally cell-faced and shows repeated atlas art. This is
  not completion of painter quality, authored-isometric terrain art or all
  navigation/building footprint requirements.

Generated/live shoreline handoff, 2026-09-06:
- Added `--source-comparison` to the clean painted visual probe. It switches
  only the source binding for the same existing lab world, freezes wave time,
  disables extra shader beach compositing in both paths, and captures overview
  and close zoom. It does not regenerate or edit cells. This is a diagnostic,
  not a visual acceptance test or a production source switch.
- Seed 31415, 32x32 cells, CoastDetail 4: 643 of 16384 coast texture samples
  change land/water classification between generated and live sources.
  Generated/cell centre queries disagree at 8 cells. No feature centre samples
  fine water in this seed; small water patches visible near props are not proof
  that their centres are submerged.
- Source inspection confirms the loss: LoadGeneratedCells transfers cell-level
  terrain/elevation/water metadata, not the sub-tile mask. LiveCache reconstructs
  from a boolean per cell. Raising CoastDetail oversamples that reconstruction;
  it cannot recover the generator's lost shoreline. This contributes to the
  squared coastline independently of texture filtering/material blur.
- Do not simply clear CellDataPath in production: that bypasses player edits
  and restores a fine field whose cell-centre contract differs from navigation.

Implementation sequence for the shoreline handoff (status after the change above):
1. Establish a shared fine-surface representation owned by TerrainWorldComponent
   and consumed through the live grid, not a second generator/world or renderer
   fallback to stale generator settings. Define cell-centre/placement-footprint
   agreement explicitly without flattening all sub-tile coast detail. Centre
   agreement is implemented; larger footprint/clearance agreement remains open.
2. Preserve generated coast samples at the grid handoff; keep terrain painting,
   water edits, undo and source rebinding authoritative. Patch affected regions
   and invalidate coast caches on fine-mask changes, not only cell water flags.
   Handoff, whole-cell edits and invalidation implemented; general undo integration
   and bounded partial coast recomputation remain to be checked.
3. Persist that surface with world saves and restore it before renderer refresh.
   Verify offsets, partial regions, fresh generation and edited-world round trips.
   Implemented and tested through grid snapshots and existing recipe-order tests.
4. Extend actual generated-world tests across seeds: unedited detail retained,
   edited masks visible, navigation/placement footprints consistent, no props
   submerged. Four-seed sample/anchor/centre checks implemented; arbitrary-radius
   and full-building-footprint checks remain open.
5. Capture all three projections and representative zoom levels, then profile
   map generation/repaint cost. Material/art consistency remains a separate gate.

Open regression discovered during material-scale verification:
- The initial 44-probe run passed 43 checks, including all 12 rendered checks,
  but iso_layers crashed after passing assertions. Native 0xC0000005 occurs in
  godotsharp_internal_object_get_associated_gchandle / GodotObject.Finalize.
- Direct repeats and headless OpenGL reproduced it. Explicit scene unloading
  before quit did not resolve it. A minimal authored-scene probe passed without
  diagnostics and failed with diagnostics; disabling water or features passed
  individual trials, but do not treat that limited evidence as a proven cause.
- Retaining the river material wrapper passed an initial trial but failed on
  repetition. Kept explicit material ownership, not a claim of crash resolution.
  The attempted alternative shader-property read did not help and was removed.
- Added terrain_iso_shutdown_probe with default diagnostics and actual unload,
  plus no-diagnostics/no-water/no-features/no-build isolation switches. Its
  success marker is insufficient: the process must also exit zero. Next task:
  isolate managed/native object lifetime without disabling diagnostics, changing
  shader effects to hide the crash, or accepting intermittent shutdown failure.
- Latest expanded run: 44 of 45 checks passed; all 12 GPU checks passed.
  iso_layers exited cleanly this time, but terrain_iso_shutdown_probe crashed.
  The intermittent finalization defect remains unresolved. dotnet-dump and
  dotnet-stack are already installed for the next native/managed lifetime trace.
- External Windows debugger harness builds separately under tests/native_capture.
  Nine debugged runs exited cleanly without an access violation, so no dump was
  captured. A clean direct run and corrected no-features run also exited zero.
  These trials do not establish a fix; the preceding 44/45 suite result stands.
  Removed the in-process capture hook because it changed reproduction timing.
- Corrected --no-features isolation: remove IsoFeatures before entering the tree,
  rather than just clearing the builder path. Its independent SurfaceRebuilt
  subscription previously allowed feature construction even with that switch.

Painted material detail and independent water scale, 2026-09-06:
- Captured the same generated seed at repeat sizes 6 and 12, with/without the
  existing dry-grass tint, at overview and close zoom. Larger ground repeats
  show actual grass/stone detail more clearly. Retained existing colour tints;
  neutral dry-grass tint exposed a more yellow source, not a universal fix.
- Replaced combined TextureTiles with GroundTextureTiles (12) and
  WaterTextureTiles (6). Authored lab and painted demo use both. No legacy alias.
  Ground scale also drives submerged sand, preserving beach/bottom texture
  alignment; animated water retains its independent repeat size.
- Isometric water exposes the same two controls and copies them into river
  materials. Native atlas cells and height geometry are unchanged.
- New GPU probe checks actual affected/unaffected pixels at 0.5x/1x/2x zoom,
  full opacity and byte-identical ID, shade and coast maps across scale changes.
  Existing river probe checks non-default scale binding and sea/river parity.
- Mipmaps, explicit material gradients, blend settings, geometry and generation
  are unchanged. This is readable material scale, not a global sharpening filter.
  Coastal silhouettes and consistent art direction remain open visual work.

Relief material and fine-coast preservation, 2026-09-06:
- Confirmed that relief tiers and continuous elevation are separate contracts.
  Isometric surface levels use relief; painted lighting still uses elevation.
  Removing a tiny relief tier must not zero the natural elevation/shade field.
- Reproduced old peak samples after LevelSmallRelief, and water/sand samples
  overwritten by GroundPeakMaterial's whole-cell SetTile call. Added a focused
  ReplacePeakMaterial operation shared by both paths: change only dry peak
  material, synchronize removed relief tiers, preserve fine coast and biome detail.
- Expanded the final-topology regression with 18 relief fixtures covering
  rock/snow/gravel, flat/hill/mountain tiers and scale rules on/off. Every sample's
  water/land, material, elevation and shade is checked; cell/fine query agreement
  is checked. A six-cell range at the minimum size is retained unchanged.
- This addresses data consistency, not all perceived blur or natural coastline
  appearance. Keep visual acceptance and cross-view captures in the active queue.
- Final source build clean; all 43 integration checks passed, zero skipped,
  including all 11 rendered checks. Only original addon code/tests/docs changed.
- Material inspection: the clean lab's olive ground binds dry_grass.png, which
  already contains broad yellow/brown variation. Its shader additionally applies
  tint_dry_grass=(0.80,0.84,0.56). TextureTiles=6 and mip-filtered sampling are
  separate from BlendWidth=0.42/BlendSharpness=4. Next visual comparison should
  isolate authored texture colour/scale from edge blending, not apply a global
  saturation/sharpening effect or claim all apparent softness is filtering.

Final terrain topology and painter cleanup, 2026-09-06:
- Reproduced stale continent metadata in four generated seeds with scale rules
  enabled (67, 31, 9 and 11 mismatched cells). The pipeline labelled continents
  before cleanup drained lakes/rivers. Moved labelling after ApplyTerrain;
  resources, features and start placement now read the final dry-land regions.
- A controlled five-cell channel exposed 80 wet painter samples remaining after
  short-river cleanup made the gameplay tiles dry. Cleanup now uses the existing
  SetTile operation to update water, material and land at both resolutions.
- New source-stage fixtures cover river/lake draining. Generated-world regression
  independently flood-fills the final water grid with scale rules on/off over
  four seeds. All labels and reported counts now agree.
- Actual authored lab, 32x32, seed 31415: two footprints and two final dry-land
  regions (170 and 210 cells), not three. River/lake-disabled comparisons also
  yield two regions. The earlier extra continent was stale metadata, not proof
  of a third generated island. Captured overview and close painted views.
- Coastline still has a 12-tile straight run. This correctness fix does not prove
  final visual quality. Dry-land continent IDs are not navigation reachability:
  wading, bridges, relief and dynamic blockers are separate grid policies.
- The identified LevelSmallRelief follow-up is addressed by the later relief
  material correction above, preserving elevation rather than flattening it.
- Verification: clean build, 43 integration checks passed (32 headless and 11
  rendered), zero skipped. Original addon, tests and docs only; no game changes.

Seed placement and weighted coast growth, 2026-09-06:
- Instrumented the clean painted capture with actual generation diagnostics and
  a longest straight grid-coast run. The lab reaches 42% footprint exactly, so
  the initial saturation explanation did not hold for this seed.
- Reduced seed jitter from 80% to 20% of its lattice cell and retained each
  cell's aspect ratio for area-preserving growth distance. This reduces circular
  growth being clipped into a rectangle inside a wide, shallow allocation.
- The first candidate failed two small-archipelago compactness cases (30/36%).
  A no-lake/no-river diagnostic retained those failures, isolating growth rather
  than lake carving. Coast noise still used the average island radius despite
  unequal target weights; scaling amplitude/frequency per mass restored 43/46%
  fill and passed the original 40% guard unchanged.
- Latest clean lab capture is less rectangular and retains the 42% footprint;
  longest straight coast is 12 tiles versus 13 before, not a complete shape fix.
  At that checkpoint generation reported two footprints and three logical
  continents; the later final-topology investigation above resolved stale IDs.
- Final source build clean; all 42 terrain integration checks passed, zero skipped.
  Source addon only. No renderer distortion, hidden coverage change or legacy path.

Beach/landmass ownership correction, 2026-09-06:
- Traced the straight coast geometry through the landmass, biome and reduction
  stages. The separation gap still assumed beaches turn water into land, but
  current biome painting applies BeachWidth only to existing dry samples.
- A failing reproduction showed BeachWidth moving islands and reducing a 50%
  footprint request to 32.7% in a 32x32 archipelago. Removed the obsolete beach
  multiplier; foreign claims retain a fixed two-gameplay-tile separation.
- The new probe checks widths 0/1/3, two sizes, two seeds and three landforms:
  land/water masks stay identical, beach sand grows and the 50% target is met.
- Existing topology and landmass guards pass without count/compactness tolerance
  changes. Added these and the new beach probe to the main integration suite.
- Generation output intentionally changes; no legacy path. Replaced historical
  generated-coast hashes with an independent nearest-opposite-sample chamfer
  oracle, whole-field sign/ocean checks and exact repeated-build bytes.
- Final source build clean; 42 checks passed (31 headless, 11 rendered), no skips.
  No external game files, shaders or source texture images changed in this step.
- Clean painted capture still has long straight edges. Next: inspect eligible-area
  saturation and axis-aligned claim exclusion at dense coverage, then improve
  silhouettes with measured land/count/grid invariants. Do not hide the geometry
  using renderer-only distortion or claim this ownership fix solves the full shape.

Isometric cliff proportions and seabed, 2026-09-06:
- Retained the detailed atlas but fitted its vertical side faces from 54 to 28
  pixels through a native TileData shader. Top diamonds and their texture detail
  stay unchanged; source bitmap files were not edited. LevelHeight participates
  in the renderer's atlas/material cache key.
- A rendered synthetic atlas probe verifies top pixel/detail preservation, face
  reduction and no adjacent-frame colour at 0.5x, 1x and 2x. The initial edge alarm
  was traced to white RGB in the fixture's transparent pixels, not an atlas leak;
  corrected the fixture instead of adding unneeded sampling workarounds.
- Updated both addon demos to 28-pixel steps. The original standalone 2.5 maximum
  stack-height guard now measures 2.30 rather than 4.26, without relaxing the guard.
- Desert now uses frame 7, beach sand 6, gravel 120 and rock 34. Removed gravel's
  frame from sand variants. Primary terrain-frame distinction guard now passes.
- Flat seabed uses top-source tiles rather than individual cliff blocks. Clean
  captures show the underwater tile-side grid removed; GPU art probe checks source 1.
- Added the previously separate stack guard and new GPU cliff probe to the main
  runner. Final source build clean; 39 checks passed (28 headless, 11 rendered),
  zero skipped. Includes river/grid/placement/height/live-edit regression coverage.
- This remains stylized slab/block terrain, not natural continuous cliffs. The
  fitting shader assumes tightly framed diamond art. Banks, river-mouth drops,
  large-scale coast silhouettes and cross-view material consistency remain open.

Animated isometric rivers, 2026-09-06:
- Reproduced the static-atlas bypass with a failing rendered probe in the original
  addon. Replaced it with one indexed Polygon2D river batch using native grid
  coordinates, the shared water shader/textures and an isolated material.
- Kept narrow rivers at ground height and removed their dependence on seabed
  reach. Wide channels no longer drop to sea level because their centres lack a bed.
- River surface composites its sandy bottom rather than showing foreground block
  sides through alpha. Ocean breakers are disabled on that surface; actual shader
  animation changed all 493 sampled centre pixels over 1.5 seconds.
- Verified shifted-origin picking, visible native-diamond centres, configured
  navigation, source replacement/missing source, live edits and hidden-view catch-up.
  Default shallow-water wading policy was not changed.
- Removed obsolete ShallowWaterFrame/DeepWaterFrame exports and both demo bindings;
  no compatibility aliases or external game changes. Updated current renderer docs.
- Final source rerun after obsolete-export cleanup: clean build, 37 checks passed
  (27 headless, 10 rendered), zero skipped.
- Additional standalone tests/examples/iso_layers.gd now checks the animated river
  batch and passes those checks, but reports two outstanding art guards: a 4.26
  tile-height stack exceeds 2.5, and desert/sand plus gravel/rock share atlas frames.
  Do not raise the height limit or declare visual acceptance from the narrower suite.
  Next art work must address cliff proportions, biome distinction, banks and mouths.

Isometric demo atlas upgrade, 2026-09-06:
- Inspected the shipped detailed block/top sheets and the current Kenney demo
  bindings. Switched both original addon demos to the detailed 9x16 sheets with
  matching 92x53 footprint, 27-pixel block lift and 54-pixel elevation steps.
- Remapped terrain frames/variants to the new atlas, avoiding unrelated industrial
  frames. No bitmap originals were overwritten and no new renderer/world was added.
- Found disabled mipmaps on voxel_tops_seamless. Enabled them and used a targeted
  Godot reimport after the host's unrelated missing Blender setting interrupted
  broad import processing. Runtime inspection verifies the imported mip chain.
- Added an actual-authored-scene art probe, demo binding parity, mipmap checks,
  clean captures and gameplay-grid/surface coordinate checks.
- Clean source build and all 36 integration checks passed (27 headless, 9 GPU),
  with no skips. Editor-only import/shutdown warnings are separate from that gate.
- Remaining: the view is still stylized blocks, not natural continuous cliffs;
  rough-grass and painted dry-grass palettes differ. River cells still bypass
  the shared animated sea using a bright flat atlas tile; review that render path
  without hiding one-cell waterways behind the foreground ground blocks.

Painted shading/art isolation, 2026-09-06:
- Added clean no-shade and flat-material diagnostic captures. Full-strength
  cell-gradient hillshade caused most broad cloudy contrast in the lab capture;
  disabling it revealed intact texture detail. No replacement artwork was needed
  for this issue, and supplied assets were not overwritten.
- Lowered the painted renderer/shader and two authored demo defaults to 0.35
  shading gain. Neutralized default shader saturation/contrast to 1/1 instead
  of desaturating the entire land-and-water composite.
- The GPU shade probe measures about 0.102 brightness span at the new default
  versus 0.294 at full strength, with identical terrain-ID and shade data.
- The default 32x32 seed contains dry grassland (ID 1), not green grass (ID 0).
  Added optional same-footprint grass/desert/mud/snow material captures, hiding
  features during these artificial swaps. Do not treat them as biome generation
  or environment-aware scatter evidence.
- Visual work remains: stronger large-scale coast silhouettes, appropriate
  authored isometric ground, and final multi-biome/zoom/art consistency review.
- Source build clean; full suite passed 35 checks (27 headless, 8 rendered), with
  no skips. No external game files or supplied texture originals changed.

Live coast reconstruction and polygon water alignment, 2026-09-06:
- Replaced fine-pixel repetition of square live cells with centre-based bilinear
  water-mask reconstruction. Rounded corners retain cell centres, clamped map
  boundaries, ocean connectivity and one-cell channels; generator sampling is unchanged.
- The new shape probe failed the old square-corner implementation, then passed
  all 16 local patterns at detail 2/4/8 with positive/negative origins. GPU readback
  verifies visible land/water at native centres under two zoom levels.
- Clean lab captures show rounder local shore turns, not a new island silhouette.
  Large straight shores and the cloudy grass palette are still outstanding.
- Found the actual isometric Polygon2D sea still using a TileMapLayer batch
  vertex offset: cell (0,0) sampled as (1,0). A probe built from the authored lab
  water polygon reproduced this. The shader now distinguishes polygon-local
  vertices from tile-batch vertices; renderers explicitly supply their geometry.
- Added cross-view coast-byte equality to the lab projection/live-edit test.
- That equality check exposed tiled water's old detail-2 default against detail 4
  in the other views. All three now use TerrainCoastField's shared default detail
  and range (4 and 5 tiles); intentional per-view overrides remain explicit.
- Final source run: clean build, 34 checks passed (27 headless, 7 rendered), none
  skipped. Isometric ground still uses visibly block-shaped placeholder art;
  this run verifies its water alignment, not final isometric artwork quality.

Painted material coverage and definition, 2026-09-06:
- GPU reproduction: a uniform desert at BlendWidth=0 contained 11,232 incorrect
  grass pixels in a 248x248 interior. The circular weight kernel did not cover
  square cell corners; the shader substituted grass wherever its weight was zero.
- Replaced it with separable square coverage and moved the lookup neighbourhood
  with the noise displacement. Water-margin fallback uses the nearest sampled land
  material rather than inventing grass. No gameplay cell or coast-mask change.
- Added BlendSharpness (default 4) to concentrate transitions independently of
  material texture scale. Material lookups use explicit pre-branch UV gradients
  so mip selection remains defined; texture mipmaps are retained.
- Added a real GPU probe for full desert coverage, extreme edge displacement,
  live material edits, transition width, and unchanged ID/coast data.
- Clean lab close/overview captures show better-defined land-material boundaries.
  The broad grass colour patches and grid-shaped coast remain visible. This is
  partial visual progress, not artwork/coastline acceptance or goal completion.

Generated-view coast sampling, 2026-09-06:
- Standalone coast generation previously called a generator accessor per sub-cell,
  reconstructing/comparing settings repeatedly for one immutable field. It now resolves
  once per build and shares that field between water and ocean-mask sampling.
- Added an addon-only generator/painted-view probe for two seeds with captured baseline
  coast hashes, seed-change validation and warm repaint timing. No game files changed.

No-legacy external cleanup and synchronization audit, 2026-09-05:
- Removed unused game DualGridTerrain/EdgeMaskTerrain source and UID files; no wrappers retained.
- Current/game addon source comparison found 48 relocated-name candidates that a copy-only install
  would leave duplicated. Recorded counts/game-only filenames in OILFIELD_WORLD_CONSOLIDATION.md.
- Actual game build and terrain smoke pass after removal. BasinWorld/WorldMap/TerrainMap/painter
  caller migration is unfinished; no claim that removing two renderers completed world consolidation.

External game diagnostic-layer removal, 2026-09-05:
- Audited actual BasinWorld, WorldPreview and game PainterlyTerrainLayer; verified a clean game
  baseline build. Removed seven always-hidden atlas layers and their allocation/repaint helpers.
- Actual game build and new tests/basin_terrain_smoke.gd pass, with one populated terrain sprite
  and no removed diagnostic layers. This test is external to the addon's 25-check suite.
- Old game terrain models/painter remain pending replacement; no claim of completed migration.
  See OILFIELD_WORLD_CONSOLIDATION.md for the updated evidence and remaining boundary work.

Isometric prop lifecycle, 2026-09-05:
- Read the entire isometric feature renderer. Surface events now rebuild visible props immediately,
  defer hidden props, and catch up on show. Reattachment restores the surface subscription.
- World draw establishes prop visibility/bounds before the surface rebuild, retaining immediate
  visible updates during projection switches. Explicit hidden rebuilds remain supported.
- Expanded surface-height probe passes hidden flattening/show, reattachment/flooding, existing
  transformed anchors and failed-surface recovery. Clean build; full integration passed
  (25 passed, zero skipped, including rendered captures).
- Texture invalidation, saved private prop-layer reconstruction and external game consolidation
  remain open. These lifecycle checks do not establish complete addon correctness.

Resource-view lifecycle and live-only overlay, 2026-09-05:
- Read both complete resource views and their shared subtree binding. Added hidden-work deferral,
  show catch-up and reattachment rebinding; world draw keeps inactive resource/overlay bounds current.
- Live-resource-only overlays no longer require an unrelated generator. Generated markers, survey
  patches and start positions still require it. Drawing consumes baked batches independently.
- Focused probe passes hidden depletion/show, reattachment/restore, generator-free live markers,
  required-generator failure and mixed-view recovery. Clean build; full integration passed
  (25 passed, zero skipped, including rendered captures).
- Isometric prop lifecycle, texture invalidation and external game consolidation remain unfinished.

Feature/relief visibility and geometry, 2026-09-05:
- Read both complete renderers. Features previously rebuilt while hidden; relief deferred edits
  but never caught up on show and could still execute a previously queued rebuild after hiding.
  Both now defer hidden automatic work, refresh on show, and restore subscriptions on reattachment.
- World controller synchronizes inactive feature/relief bounds. Explicit hidden rebuild is retained.
- Relief no longer derives stamp axes from neighboring cell centers. Native cell corners avoid
  out-of-bounds neighbors and elevation-dependent stamp size/jitter.
- Focused probes pass hidden/show, reattached grid movement/live edits, and a hill on the last
  elevated map cell. Clean build; full integration passed (25 passed, zero skipped, including captures).
- Resource/overlay and isometric prop lifecycle, texture invalidation and external game consolidation
  still require work; these changes do not establish complete renderer coverage.

Painted view source/material lifecycle, 2026-09-05:
- Full TerrainPaintedRendererComponent read found shared saved ShaderMaterial mutation and stale
  visible terrain on missing source. Adopted materials are now duplicated before map-uniform writes;
  missing explicit cells clear the surface. Rebinding and rebuilding restore the map.
- Hidden automatic rebuilds pause; showing refreshes previously attempted views. Reattachment
  restores cell subscriptions and exit clears pending work. Explicit hidden Rebuild remains valid.
  World draw synchronizes painted bounds even when inactive.
- Expanded painted-origin probe verifies hidden/no-upload, show catch-up, reattachment live edits,
  two worlds with a shared input material retaining separate terrain IDs, and source failure/recovery.
  Clean build, focused probe and full integration passed: 25 passed, zero skipped, including captures.
- Feature/relief lifecycle, texture invalidation and external game consolidation remain unfinished.

Saved biome-display ownership, 2026-09-05:
- Runtime ownership lists do not survive PackedScene instantiation. Generated biome displays now
  persist an ownership marker so rebuild removes the saved generated displays before recreating
  their transition components. No name-based legacy detection or compatibility wrapper.
- Creating a biome display no longer reuses an arbitrary same-name authored layer. Godot assigns
  a unique readable name when necessary. Ownership/adoption still makes generated nodes packable.
- Expanded world-ownership probe passes three pack/instantiate/rebuild cycles, verifies exactly
  one generated display/transition, retained scene ownership, exact authored-name collision safety,
  and removal of the generated biome after reload. Clean build; focused probe and full integration
  suite pass (25 passed, zero skipped, including rendered captures).
- This covers biome displays. Reserved logical/water layer names and other renderer saved-state
  lifecycle remain separate review items, as does external game consolidation.

Tile display ownership, 2026-09-05:
- Full renderer read found atlas reconfiguration deleting arbitrary authored `*Tiles` children.
  Cleanup now uses tracked transition displays, checks their parent, and shares one implementation
  with missing-source cleanup. It no longer infers ownership from a name suffix.
- Regression covers authored RoadTiles through first build, source replacement/failure/recovery,
  and atlas removal, while verifying generated GrassTiles are actually removed.
- Clean build, focused live-cell probe, and full integration suite (25 passed, zero skipped) pass.
  Renderer reference
  corrected to document TerrainGeneratorPath and authoritative live CellDataPath.
- Saved-scene reconstruction/ownership and other view lifecycles still require review; this is not
  proof of complete world consolidation or full addon correctness.

Tile/transition hidden-work lifecycle, 2026-09-05:
- Read TerrainTransitionLayerComponent completely. Automatic full/incremental refresh now follows
  display-layer visibility. Standalone displays catch up when shown; renderer-owned displays let
  the parent refresh the full view once. Explicit refresh remains available while hidden.
- Tile coastline updates also defer while hidden. Rebuild callbacks coalesce, and full refresh
  clears stale pending dirty cells. World-managed inactive tile bounds/origin stay synchronized.
- Tree reparenting exposed a duplicate-disconnect bug in the first implementation. Exit now clears
  display binding/queued state and re-entry restores subscriptions for previously used components.
- Full suite: 25 passed, zero skipped, clean build. Expanded focused live-cell probe additionally
  passes standalone hide/show and remove/re-add subscription checks without engine/script errors.
- Painted/feature/relief view lifecycle, authored transition art, sample material delivery and
  external game consolidation remain unfinished. No claim of complete addon optimization.

Flat tile source lifecycle, 2026-09-05:
- Full TerrainTileRendererComponent read found stale displays on missing live sources, a generator
  fallback in deferred coastline updates despite explicit CellDataPath, and cached child transition
  bindings surviving same-path node replacement.
- Missing source clears owned biome displays and shader cells. Cache identity now includes the
  resolved source instance, so replacement creates correctly bound transition components. Generator
  paths resolve freshly. Disabling the water shader clears its old overlay, retaining biome tiles.
- Live-cell probe now verifies same-path source replacement, missing-source deferred coastline
  updates with a generator still available, recovery, and shader-overlay removal.
- Full integration: 25 passed, zero skipped, clean build. Docs/source comments updated to describe
  live cells as the edited map rather than the generated recipe.
- Hidden-work optimization of flat transition children and other flat views remains pending,
  as do missing authored transition art, sample deliveries and external game consolidation.

Inactive isometric view lifecycle, 2026-09-05:
- Resize warnings came from props receiving new bounds while their inactive block surface retained
  the previous size; the diagnostic called that surface the generator. World draw now synchronizes
  both isometric surface bounds/origin even when another projection is active.
- Block and authored-isometric automatic rebuild queues skip hidden views. Visibility changes
  refresh previously attempted views, while initial visibility still respects RefreshOnReady=false.
  Explicit Rebuild is unchanged for callers that intentionally prepare a hidden view.
- Lab regression verifies inactive startup, no hidden SurfaceRebuilt during resize, matching 48x48
  surface/prop bounds, show-triggered refresh and normal projection reactivation.
- Full suite: 25 passed, zero skipped, clean build. Headless and OpenGL lab logs contain no bounds
  mismatch warnings; only the expected missing authored peering-art diagnostic remains.
- Other renderers' hidden-work policies, authored transition artwork and external integration still
  require work. This is not a claim that every hidden view or the entire addon is optimized.

Terrain lab viewport navigation, 2026-09-05:
- Read both lab partials. Pan/zoom previously treated screen coordinates as world/parent
  coordinates. Anchored zoom now uses the canvas transform, and panning converts pixel deltas
  through the preview parent's inverse transform. Fit measures transformed screen bounds.
- World-built events compare terrain extents: changing map size reframes, while same-extent
  regeneration retains user navigation. Projection changes still redraw without replacing cells.
- Lab probe covers rotated/non-uniform parent pan/zoom and changing 32x32 to 48x48. Its viewport
  is explicitly 1280x800 so a tiny headless viewport cannot clamp both maps at minimum zoom.
- Full suite: 25 passed, zero skipped, clean build. Isometric lab capture inspected: map is framed
  beside the settings panel. Docs now describe NewWorld/Redraw rather than removed Build API.
- Missing authored-isometric peering art remains unresolved. Resize testing also exposed deferred
  inactive-view bounds warnings; their generator/source ownership needs follow-up, not suppression.

Construction completion and rejection state, 2026-09-05:
- Required storage is resolved before construction visuals/metadata mutate, and passed into the
  material-wait transition. Missing storage no longer leaves an unregistered unfinished object.
- Sites record their chosen approach. Completion rechecks current navigation at that cell before
  publishing a finished building; blocked/missing access cancels the site through its existing
  removal/refund policy and emits build_approach_blocked. Playground displays the rejection.
- Extended approach probe verifies visibility/tint/Complete are unchanged on missing storage,
  and queue completion after flooding does not emit BuildSiteCompleted or retain the default site.
- Full integration: 25 passed, zero skipped, clean build. Existing grid_terrain_building_probe also
  passes, including delivered-material gating and real worker completion, with no engine errors.
- Queue completion and build success remain distinct signals. This does not relocate working crews,
  refund consumed physical materials, or validate every changed foundation cell. Construction-stage
  art, sample deliveries, broader lifecycle rebinding and external integration are still pending.

Timed construction in the terrain playground, 2026-09-05:
- Added authored BuildCatalog, shelter definition (one work turn), and BuildSites nodes wired to
  the existing placement, jobs and live navigation. The shared worker builds from a work face;
  queue-owned progress uses the existing KitMeter. No sample timer or second construction engine.
- Clear effects are dispatched only for clear jobs; BuildSites owns build completion. Status text
  distinguishes building from clearing. Rejected site registration removes the sample placement.
- Cancel Jobs emits per-job cancellation before clearing the ledger, ensuring incomplete buildings
  release footprints. Checkpoint capture rejects active sites. Restore disconnects build-site
  subscriptions while recreating completed shelters, then reconnects without new construction jobs.
- Real-scene probe observes intermediate build progress and completion, queued-site cancellation,
  navigation cleanup, completed-shelter checkpoint restore without jobs, and regeneration cleanup.
- Full suite: 25 passed, zero skipped, clean build and scoped whitespace check; OpenGL included.
- Material delivery in the sample, construction-stage art, building physics, projection chooser,
  active-job persistence, authored transition art and external game migration remain unfinished.

Construction access review, 2026-09-05:
- Full GridBuildSiteComponent read found a fixed front approach cell, even outside the map or
  in water. Added explicit live NavigationPath and deterministic cardinal perimeter selection.
  Both IsInBounds and IsBlocked are checked; IsBlocked alone does not include bounds.
- Registration rejects no_build_approach before changing construction metadata/visuals when no
  usable face exists. Explicit missing navigation does not use the previously cached map.
- Material delivery rechecks access before consuming stock. Inaccessible waiting sites retain
  materials; RefreshPendingBuilds lets terrain-edit orchestration retry without a polling loop.
- terrain_build_approach_probe passes edge/coast/sealed cases, missing explicit navigation and
  flooded delivery/recovery. Full integration run: 25 passed, zero skipped, clean build.
- This is local access selection, not a route search from each worker. Alternative approaches for
  already queued jobs, worker-driven construction in the playground, site-binding lifecycle and
  construction rejection rollback through PlacementPlaced remain outstanding.

Playable shelter placement and checkpoint restoration, 2026-09-05:
- Added scene-authored Place Shelter/Demolish KitButtons, Placement component and Buildings root.
  An authored shelter scene reuses the existing house icon and GridObjectComponent.
- Sample actions delegate to the shared placement implementation. Idle-only placement avoids
  trapping a moving/working truck; truck/live-deposit cells and occupied water edits are refused.
- Demolition releases routing/occupancy through GridObject teardown. Regeneration clears buildings.
  Checkpoint version 2 saves shelter cells and recreates them after shared world/grid restoration;
  no legacy demo checkpoint migration or separate terrain-placement rules were added.
- Real-scene probe verifies water/overlap rejection, navigation blocking, demolition cleanup,
  checkpoint reconstruction at projected cell coordinates and regeneration cleanup.
- Full suite: 24 passed, zero skipped, clean build. OpenGL building.png inspected at 1280x800:
  toolbar controls fit and shelter is visible at the restored cell. House icon is prototype art.
- This is immediate placement, not timed construction. Material delivery/build progress, physical
  building collision, a catalog UI, active-job persistence and external game migration remain open.

Placement against live terrain, 2026-09-05:
- Confirmation rechecks the complete footprint before charging or creating a scene. A previously
  valid preview cannot authorize construction after flooding, occupancy changes or broken bindings.
- Each footprint cell must project to a finite position and lie inside wired navigation bounds.
  RequireLevelFootprint defaults on and compares live relief levels; explicit opt-out supports
  slope-spanning designs without introducing a separate generated height source.
- Explicit placement collaborators resolve freshly. Programmatic preview control is no longer
  overwritten by mouse polling when UseMouseInput is disabled.
- Regression exposed leaked demolition occupancy when scene discovery found no placement owner.
  Placed GridObject components now bind to their creating grid/controller/navigation explicitly,
  releasing previous reservations before rebinding. Geometry subscriptions use that same grid.
- New terrain_placement_live_probe covers top-down/isometric, negative coordinates, flooding,
  relief opt-out, bounds, missing sources/root, confirmation recovery and demolition cleanup.
- Combined suite: 24 passed, zero skipped; clean build and scoped diff whitespace check.
- The playable terrain scene still needs authored building controls and a combined construction/
  save workflow. This test does not prove multi-world reservation teardown after source replacement,
  physical building collision, cliff/ramp barriers, or external game migration.

Terrain job effects and world rebinding, 2026-09-05:
- Clear completion checks the current tool's terrain eligibility before collecting a deposit.
  Flooded targets reject without consuming stock or crediting the wallet. Explicit broken tool
  paths reject instead of silently applying ungated clear/till/water/harvest mutations.
- Tools resolve explicit collaborators each operation. Queue effects resolve queue identity even
  when its path is unchanged; ConnectQueue detaches obsolete subscriptions and binds the current
  node. Stale completion callbacks cannot interpret an old job ID against a different queue.
- Extended terrain_job_preflight_probe covers flooded completion, restored valid clearing,
  changed cell sources, missing tool paths, same-path queue replacement and missing queue targets.
- Combined suite: 23 passed, zero skipped, clean build, including three OpenGL checks.
- Preflight is not a transaction across user signal callbacks. Building placement, active-job
  persistence, missing transition art and external game migration remain open.

Queued terrain work in the playable scene, 2026-09-05:
- Authored Jobs, Tools, JobEffects, Clear Job/Cancel Jobs buttons and a KitMeter in the playground.
  Existing queue/worker/follower/tool/effect components own validation, travel, work and mutation.
  Sample dispatch waits for direct movement to finish; direct move/gather cannot steal active jobs.
- Queue-owned progress drives the meter. Completion clears the live feature flag; the feature view
  responds through its normal live-cell subscription. No demo-side clear algorithm was added.
- Busy checkpoint capture is rejected. Loading/regeneration cancel and clear jobs before restoring
  state. Cancellation never applies an effect; invalid/unreachable jobs are cancelled instead of
  repeatedly reclaiming them forever.
- Extended real-scene probe verifies queued work is not an immediate edit, observes intermediate
  progress, waits for vegetation removal, checks cancellation and rejects water jobs.
- Full suite: 22 passed, zero skipped, clean build. OpenGL playground capture inspected; all
  authored toolbar controls fit the 1280x800 viewport.
- Building placement, multi-worker dispatch, active-job checkpoint persistence, sample projection
  chooser, authored transition art and external game integration remain incomplete.

Worker arrival correctness, 2026-09-05:
- Review before adding demo jobs found GridWorkerComponent treated all stopped movement as arrival,
  including routes cancelled by live flooding. That could complete clear/till/gather remotely.
- Follower now exposes HasReachedDestination, true only on completed routes and reset on new routes
  or cancellation. Worker records its accepted approach/job destination and requires both successful
  arrival and a matching destination before starting work; otherwise it releases the claim.
- Added terrain_worker_arrival_probe and included it in the combined runner. It uses the actual
  queue, worker, follower and terrain effect components: flooded/cancelled travel cannot apply clear
  effects, while successful travel still completes. Clean build and focused probe pass.
- Combined suite: 22 passed, zero skipped. Construction-worker effect probe also passes with no
  engine errors after fixing its temporary packed-scene source-node cleanup (previously leaked).
- The demo's queued-job control remains to be wired; this fixes its underlying execution contract
  first, rather than exposing remote terrain mutations as a working gameplay feature.

Playable checkpoint integration, 2026-09-05:
- Authored Save/Load KitButtons and a scoped GridWorldStateComponent in the playground scene.
  The sample combines existing recipe, live-grid, wallet and deposit snapshots with truck-cell
  coordinates through Godot's variant file format (object deserialization disabled).
- Loading bypasses new-world spawn initialization, so it cannot clear restored terrain or respawn
  a depleted deposit. Idle-only capture explicitly rejects active routes. Failed spawn selection
  disables map actions rather than leaving previous spawn coordinates active.
- Probe activates actual buttons: gather, save, flood/flatten, regenerate a different seed, change
  wallet, then load. Checks restored recipe/live kind, stock, depleted marker, truck position,
  navigation and physical collision. This is one checkpoint, not save slots or route persistence.
- Placement/job-dispatch workflow, projection switching in this sample, complete authored art and
  external Oilfield Days integration remain unfinished.

Combined integration runner, 2026-09-05:
- Added tests/run_terrain_integration.ps1: build plus 18 headless and three OpenGL checks,
  exact success markers, exit-code/error-pattern checks, per-process wall-clock timeouts,
  per-probe logs and JSON status/duration results. Rendering skips are recorded as skipped.
- Verified the Windows process exit-code handling after the first run exposed a null exit-code
  reporting issue in the runner. The corrected full run reports 21 passed, zero skipped.
- Real rendering checked water alpha and captured lab/playground views. Playground capture
  inspected: truck and deposit are in frame. Lab authored-isometric incompleteness remains an
  expected diagnostic, not a completed artwork claim.
- See docs/TERRAIN_INTEGRATION_CHECKS.md for invocation, output paths and coverage limits.
  The full addon gate, external migration and broader gameplay requirements remain outstanding.

Isometric atlas configuration cache, 2026-09-05:
- TileSet cache compares block/top paths, atlas rows/columns, native cell size, both texture lifts,
  all primary mappings and a copied variant list. Unchanged settings reuse the resource; changed
  settings rebuild it. Empty frames and missing explicitly configured sheets fail the build.
- Flat cells and rivers no longer reference nonexistent source 1 when no top sheet is configured;
  they use the block source. A single-frame variant override now selects that frame instead of
  silently falling back to the primary mapping.
- Surface-height probe verifies reuse, changed native size/lift/frame mapping, single-choice
  variants, sheet removal, degenerate frame rejection and recovery. Clean build; surface-height,
  collision, world-live-source and view-grid probes pass.
- This validates configuration changes, not hot replacement of image contents at unchanged paths
  or completeness of authored isometric terrain-connect transition art.

Measured isometric bounds and failed-build cleanup, 2026-09-05:
- Block renderer caches SurfaceExtent during rebuild from native base-plane bounds and all
  elevated top-face corners. World preview/camera bounds now consume it instead of assuming
  two height levels. Ocean overscan and decorative art overhang are deliberately excluded.
- Failed source/TileSet builds clear native tiles, hide water, reset extent/availability and emit
  SurfaceRebuilt. Grid/collision consumers see no surface; feature renderer clears its stamps.
- Extended surface-height probe checks every generated summit corner against the built bounds,
  failed-source clearing and recovery. Clean build; surface-height, collision, view-grid and
  world-live-source probes pass.
- Art-dependent sprite overhang framing and TileSet authoring-cache invalidation remain separate
  work; the measured extent is logical terrain geometry, not a complete artwork bounding box.

Live projected collision bridge, 2026-09-05:
- Added TerrainCollisionComponent: native StaticBody2D category bodies with per-cell convex
  shapes, sourced only from live cells and GridProjectionComponent.CellCorners. No second map
  or navigation model. Zero category masks omit shapes; land defaults off, water/steep use 4/8.
- Individual edits update affected shape owners; bulk edits and geometry notifications rebuild
  the bounds. Deferred coalescing avoids physics callback mutation. Rebinding/missing surfaces
  clear stale shapes. World CollisionPath wires the source and projection after view selection.
- Authored the component in terrain_grid_playground and enabled the truck's water/steep mask.
- Clean build. Physics point queries and CharacterBody motion tests pass for transformed square
  and isometric grids, live flooding, category changes, source removal, raised terrain, flattening
  and invalid surface removal. Playground, world-live-source and surface-height probes pass.
- This completes top-surface category collision, not cliff-side/ramp barriers, building occupancy
  physics, automatic independent-parent-transform tracking or large-map chunk/performance work.
  Missing authored transition art and external game migration are still outstanding.

Underground map identity, 2026-09-05:
- Published underground metadata now has a stable fingerprint over absolute bounds and all
  deposit IDs/richness/depth bands. Subsurface snapshots carry that identity; no legacy fallback.
- Cached depletion cannot supply stock for missing sources or a different deposit map. Same-map
  rebuilds preserve stock. Reads against an interim map during restore do not erase pending
  state; first mutation on a different map starts a new ledger. Capture excludes mismatched data.
- Extended data-origin probe covers missing wiring, same-map rebuild, restore before rebuild,
  changed-seed metadata and interim queries before the matching recipe returns.
- Physical geometry, transition art and external game integration remain pending.

Isometric feature geometry, 2026-09-05:
- Feature stamps now derive scatter axes and fit width from the block renderer's native surface
  corners transformed into feature-local space. Removed the duplicate untransformed diamond
  offset arithmetic. Added a field-taking surface-corners overload for per-cell rebuild loops.
- Sheet cache invalidates when configured paths change; removing a sheet clears its stamps and
  restoring it reloads them. Degenerate atlas frames do not produce stamps. Generator lookup
  resolves the current path instead of retaining an old source.
- GetStampAnchors reports actual cached trunk positions. Extended surface-height probe checks
  deterministic anchors and hill containment under separate nonuniform scales/rotations, sheet
  removal/restoration, live flattening and flooding at a negative/nonzero map origin.
- Clean build; surface-height, world-live-source and view-grid probes pass.
- Transform changes still require Rebuild/geometry notification; this does not implement an
  automatic parent-transform watcher, physical collision bridge or missing transition art.

Recipe data has no physical ownership, 2026-09-05:
- Removed TerrainDataLayersComponent's body-generation exports and unused TerrainTileSets
  DefineBody/ShapeCell helpers. All eight hidden layers are metadata-only, with collision and
  navigation disabled and no physics/navigation layers created in their TileSets.
- The removed path built square bodies from immutable recipe terrain regardless of the visible
  projection or live flooding/flattening. It was a second, stale physical world, not a valid
  implementation of the gameplay grid. No compatibility properties remain.
- Updated callers and component documentation. Data-origin probe now asserts physical inertness
  by default and checks live flood/unflood navigation independently of unchanged recipe metadata.
- Build: zero warnings/errors. Data-origin, survey-overlay, world-recipe, world-live-source,
  navigation-height and the actual grid-playground probes pass.
- Native CharacterBody collision integration remains required: implement live projected geometry
  with explicit ownership and tests for edits, view switches and elevations. Removing the stale
  recipe bodies is not completion of that bridge or external game migration.

Authored isometric origin, 2026-09-05:
- Added BoundsOrigin to the authored autotile renderer. Live reads, native tile writes,
  coverage checks and edit filtering use absolute cells; generated samples stay local.
- Native preview extents include the shifted perimeter. World supplies the generator origin
  and translates local start cells before camera targeting. Generator paths resolve fresh.
- Extended live-cell and view-grid probes cover shifted painting, live edits, stale-cell
  clearing, native layouts and camera starts under transforms.
- Build passes with zero warnings/errors. terrain_live_cells_probe, terrain_view_grid_probe,
  terrain_world_live_source_probe and terrain_lab_grid_probe all report OK.
- This completes origin wiring for this view, not its missing authored transition art,
  physical collision/navigation alignment or external game migration. No legacy shim added.

Isometric block origin, 2026-09-05:
- Added BoundsOrigin to the current block renderer. Internal generation passes remain local;
  OffsetTerrainSurfaceData translates those reads into the absolute live-cell store once at the
  source boundary. Native block/seabed tiles and public surface APIs use absolute cells.
- Coast fields read the shifted live map. The continuous water polygon moves by the native grid's
  origin displacement without changing shader-local coordinates. World supplies the origin and
  includes its displacement in camera bounds/start targeting.
- Extended surface-height probe uses a negative/nonzero live origin and verifies native tile
  bounds, water displacement, hills, outlines, picking, props, stationary objects, flattening and
  flooding under parent transforms. Generated zero-origin summit checks remain in the same probe.
- Clean build; surface-height, world-live-source and view-grid probes pass.
- No legacy adapters or compatibility shims were added. Authored isometric autotiles, physical
  collision/navigation bodies and the broader game integration remain incomplete.

Generated data origin, 2026-09-05:
- TerrainDataLayersComponent samples recipe fields locally and writes all eight native data layers
  at BoundsOrigin + local cell. StartCells and all twelve convenience queries use absolute cells.
- Generator paths resolve on every rebuild; a missing configured generator clears stale layer data.
- World binds the data layers to its own generator, supplies bounds origin, and invalidates the
  redraw cache when origin, generator identity or generator wiring changes.
- New terrain_data_origin_probe compares every query at every cell before/after a disjoint negative
  origin shift, checks native used bounds and starts, and surveys/extracts a shifted deposit through
  the actual grid components. Missing-source clearing and world rebind recovery are covered.
- Clean build; data-origin, survey-overlay, world-recipe and world-live-source probes pass.
- This verifies logical data addressing, not physical collision/nav polygon alignment in isometric
  or separately transformed views. Those bodies and isometric-origin integration remain pending.

Flat feature alignment, 2026-09-05:
- Added BoundsOrigin and optional GridPath to TerrainFeatureRendererComponent. Live feature queries
  use absolute cells; generated field queries remain local. The shared LiveTerrainSurfaceData owns
  water/lava/cleared-cell suppression instead of a second copy in the feature renderer.
- Native cell centers/corners determine stamp placement, jitter axes and scale. World rebuilds
  features after binding the selected grid, supplying origin and flat tile size. GeometryChanged
  refreshes baked positions; subscriptions detach on exit or rebind.
- Sheet-path changes invalidate cached art; unchanged sheets stay cached. Degenerate atlas frames
  do not create stamps. Generator paths resolve fresh, and out-of-bounds cell edits do not repaint.
- Feature grid probe covers negative origin, rectangular cells, separate parent transforms,
  grid motion, water/clearing suppression and removing/restoring sheets. World binding and actual
  playground tests pass. Data-layer and isometric-origin work remains open.

Flat origin alignment, 2026-09-05:
- Painted terrain IDs and coast fields now read absolute live cells through BoundsOrigin. Shader
  tiles remain zero-local in a single quadrant; an empty native LogicalGrid maps absolute cells.
- World passes origins to painted/tiled views; flat camera bounds and start targets include origin.
- Origin probe verifies positive/negative bounds, identical coast data, live edits and native
  picking under translated/rotated/nonuniformly scaled parents. World probe verifies origin binding
  and camera targets in both flat views. Build, grid-view and playground probes pass.
- OpenGL playground capture exposed focus-before-zoom clamping; the sample now sets zoom first.
  Rendered truck and deposit are visible. Capture mode asserts the truck remains within the frame.
- Nonzero origins for features, data layers and isometric views remain incomplete. Do not treat
  this as full-world origin support or use the narrow checks to claim that.

Navigation/view binding, 2026-09-05:
- Reproduced navigation retaining an old placement node after its path changed. Explicit placement,
  road and live-cell paths now resolve current nodes once per query, including node replacement at
  an unchanged path. Automatic discovery also refreshes per query, not per A* visited cell.
- World grid rebinding no longer leaves navigation on a previous grid when the new explicit path
  is missing. Restoring the path restores the connection.
- World path queries reject non-finite input and invalid projected cells. Returned points are
  checked after PathFound callbacks so removal of a surface cannot return a partial/NaN route.
- New terrain_navigation_binding_probe covers occupancy, road costs, replacement live cells and
  invalid surfaces. World live-source, terrain-height and live-follower probes pass; build is clean.
- Playground probe now waits for actual physics movement instead of repeatedly calling AdvancePath
  with a delta that CharacterBody2D.MoveAndSlide does not use. Three consecutive headless runs pass.
- These checks do not finish the remaining origin, authored-art, placement, save and game migration work.

Runnable integration sample, 2026-09-05:
- Added tests/examples/terrain_grid_playground.tscn, inheriting the painted demo and existing truck
  scene. Controls are authored UI-kit buttons; no runtime HUD construction or replacement engine.
- Wires world/grid/navigation, live cells, truck follower, resource nodes, wallet and live icons.
  Move/gather/flatten/flood/regenerate actions exercise the actual addon APIs.
- Real-scene probe verifies gathered wallet amount, depleted icon removal, blocked flooded return
  route and relief edits. Headless and OpenGL runs pass; rendered frame inspected. Camera bounded
  to terrain and spawn selection requires an inland traversable neighbourhood.
- This sample does not yet cover placement, job dispatch, save UI or projection switching.
  Broader terrain-view integration and authored-isometric artwork remain incomplete.

Resource icon geometry, 2026-09-05:
- Added GridPath and BoundsOrigin to resource icons; native centers/corners determine anchors
  and size. Generated field queries remain local while live node filtering uses logical bounds.
- GeometryChanged refreshes baked positions; world binds the selected grid before rebuilding icons.
- Build has zero warnings/errors. Extended live-resource probe verifies actual cached icon centers
  for rectangular/isometric layers, parent transforms, negative origins and geometry notifications.
  World live-source and actual lab probes pass. Sample gameplay wiring remains pending.

Live resource markers, 2026-09-05:
- Traced generated surface/liquid resources through GridResourceScatterComponent into
  GridResourceNodeComponent, the existing owner of remaining amounts. No second balance store added.
- Optional ResourceRootPath on both overlay and icon views reads only live nondepleted nodes
  in that subtree. Shared TerrainResourceViewBinding observes gather/restore and node lifecycle.
  Missing configured roots show nothing; empty roots retain generation-preview behavior.
  (2026-09-15, VIEW-13: the overlay's resource drawer and its ResourceRootPath were removed; the
  icon view is the one resource drawer and keeps this binding.)
- ResourceChanged is emitted after gather/restore. Views coalesce event rebuilds and detach
  subscriptions on exit or root change. Direct property mutation requires explicit Rebuild.
- Fixed resource icon sheet/mapping invalidation, bounded frame indices to sheet capacity,
  refreshed generator paths and included liquid resources in generated icon previews.
- Build and live-resource, survey, lab and broad runtime smoke probes pass. Resource icon
  geometry still needs native-grid/nonzero-origin support; sample gameplay wiring and full
  rendered verification of live resource markers remain open.

Overlay geometry integration, 2026-09-05:
- Replaced overlay square-coordinate assumptions with optional gameplay GridPath.
  Markers use native centers; underground patches use transformed cell polygons.
- Added BoundsOrigin: generated-field samples remain local, while discovery/store/geometry
  queries use absolute logical coordinates. Missing configured grids clear stale batches.
- World binds the selected grid before rebuilding overlay and relief; geometry notifications
  refresh cached overlay positions. No terrain regeneration is required for view switches.
- Survey geometry probe covers rectangular/isometric native cells, transformed parents and
  negative origins. Actual lab dropdown verifies overlay/gameplay agreement in flat views.
- Build, survey, lab, live relief and recipe probes pass. OpenGL lab capture passed and flat
  tiled view was inspected. Authored isometric art remains incomplete as previously diagnosed.
- Surface-resource depletion ownership and origin support across all other views remain open.

Survey overlay integration, 2026-09-05:
- Read the overlay, prospecting, subsurface store and gatherable resource-node classes.
- Overlay now responds to discovery changes, depletion and restored drawdown/discovery state.
  Added DiscoveryChanged and StateRestored signals at the owning components. Event rebuilds coalesce.
- Configured-but-missing prospecting or subsurface nodes hide underground patches instead of
  leaking deposits. Source changes rebind on Rebuild; old subscriptions detach on exit/replacement.
- terrain_survey_overlay_probe covers survey, depletion, restore, RevealAll and missing sources.
  Build has zero warnings/errors; recipe and actual lab scene probes also pass.
- Surface/liquid markers still describe generated deposits, not gatherable node stock. Native
  grid geometry and nonzero-origin support for this overlay are still pending. No external-game
  migration or full terrain integration completion is claimed.

Live flat relief integration, 2026-09-05:
- Read the complete relief and resource renderers and the shared live surface adapter.
- Relief now consumes the same live cell metadata as elevated geometry and navigation.
  Flattening removes stamps; flooding suppresses stale relief; restored cells rebuild it.
- World binds relief to the shared cells and gameplay grid after projection binding.
  Native rectangular cell anchors respect parent transforms and nonzero logical origins.
- Relief now uses the shared relief enum instead of duplicate numeric constants; sheet-path
  changes invalidate cached textures. Source/grid events coalesce and disconnect on exit.
- terrain_live_relief_probe and extended terrain_world_live_source_probe pass. Actual OpenGL
  hill/flattened captures were inspected and pixel changes verified. Lab, recipe, navigation,
  follower and headless runtime smoke tests pass; build has zero warnings/errors.
- Still incomplete: resource icons read generated resources, overlays need ownership review,
  relief rebuilds scan the full bounded region, and other renderers need origin validation.
  This is not whole-map visual completion or migration of the external Oilfield Days checkout.

World ownership review, 2026-09-05:
- Fully read the eight world-named addon classes (including core WorldStateData and both TerrainWorldComponent partials),
  TerrainFieldBuilder, GeneratedTerrainField, both lab partials, external BasinWorld and
  its WorldMap; searched their references. See docs/ARCHITECTURE.md for the role inventory.
- Renamed internal TerrainWorld to TerrainGenerationBuffer, including stage signatures,
  references, file/UID and documentation. One temporary generation workspace, not a scene engine.
- Fixed duplicate lab/world deferred startup. Opening an already-built world in the lab
  reuses it, and reports are driven by WorldBuilt rather than duplicate post-build updates.
- Fixed camera/status late attachment and world-path rebinding; detached old event sources.
- Scoped snapshot discovery and object restoration to the configured root; missing explicit
  roots fail closed. Capture/restore now resolves current collaborator paths, not stale caches.
- Verified: build has zero warnings/errors; terrain_world_ownership_probe, terrain_view_grid_probe,
  terrain_world_recipe_probe, terrain_world_live_source_probe, terrain_lab_grid_probe,
  terrain_surface_height_probe and headless_runtime_smoke pass.
- The separate static addon_contract_scan still fails at its pre-existing unbraced BuildOnReady
  regex: current startup uses a braced block for the save-load barrier. Runtime startup/save
  tests pass; the full static gate is not claimed green.
- Not a complete addon-wide audit or game migration. The separate Oilfield Days checkout still
  has the old BasinWorld terrain pipeline; no old painter was restored or changed here.

2026-09-05:
- Read TerrainWorldComponent and its Drawing partial, GridProjectionComponent,
  GridTileMapLayerBridgeComponent and TerrainIsometricAutotileRendererComponent.
  Inspected placement consumers and block-renderer surface positioning.
- GridProjectionComponent now accepts TileMapLayerPath. Cell centers and picking
  delegate to Godot MapToLocal/LocalToMap through the layer's transform. Square and
  isometric outlines use the same native centers and transform into grid-local space.
  Manual geometry remains available when no native layer is selected.
- The authored isometric renderer exposes GetTerrainLayer, CellPosition and GridExtent.
  TerrainWorldComponent uses these instead of treating that projection as a flat painted map.
- Tiles-view preview/start sizing uses both atlas dimensions rather than painted size.
- terrain_view_grid_probe passes: square/isometric shapes, six TileSet layouts,
  negative coordinates, transformed parents, different grid/layer parents, native
  center round-trip, outline alignment, native extent and non-square flat tile sizing.
- dotnet build passes with zero warnings/errors. Headless verification is not visual
  proof of art alignment, sprite overhang, rendered coastlines or raised-cell picking.

## Remaining Requirements

Superseded game-copy detour, 2026-09-05:
- User clarified that TerrainWorldComponent is the current engine and the old
  PainterlyTerrainLayer pipeline is removed from the intended architecture.
  The diagnostic-toggle edit below was reverted; do not develop that old pipeline
  or use its existence in an outdated checkout to redefine the integration goal.
- Inspected the actual Oilfield Days checkout and built OilfieldDays.csproj
  successfully. It uses game/World/BasinWorld.cs -> PainterlyTerrainLayer.cs ->
  the older addon PainterlyTerrainComponent, not TerrainWorldComponent. The latter
  is absent from the game copy; grid classes also live under the older terrain
  folder. The checkout contains extensive local changes, including addon files.
- Read the game painter and BasinWorld's build/repaint ownership. The logical
  sources are WorldMap/TerrainMap; seven hidden atlas layers are display-only.
  A temporary diagnostic-toggle optimization was undone following the user's correction;
  there is no retained game-side performance change from that detour.
- Required migration: adapt WorldMap/TerrainMap to the shared
  GridCellData source, replace the old painter host, integrate placement/traffic
  against shared projection/navigation, then import the reviewed dependency set
  without overwriting unrelated local changes. Build and runtime equivalence must
  be verified separately; do not claim the lab fixes already run in Oilfield Days.

Live elevated surface, 2026-09-05:
- Introduced ITerrainSurfaceData for generated and live terrain queries used by the
  block renderer. Live source reads cell kind, feature, relief, elevation and water-source
  metadata without generating another world. Generator bulk handoff retains the new
  terrain_elevation and terrain_water_source metadata in existing cell persistence.
- Isometric block renderer accepts CellDataPath, rebuilds after edits, and uses live coast
  data. Water aliases map to its water art. SurfaceRebuilt lets props refresh after height
  classification is ready rather than independently guessing completion order.
- Isometric features now query the renderer's surface source and follow its surface-level
  decisions. Tests verify live hill height, flattening with prop level movement, water
  classification and removal of props after flooding. Generated summit tests still pass.
- This does not implement height-aware navigation/ramp rules, biome suitability beyond
  water/lava, incremental block/coast rendering, scene-wide wiring or visual verification.

Elevated geometry follow-up, 2026-09-05:
- Found summit promotion existed only in drawing. SurfacePosition and prop-level selection
  stayed at peak level. SurfaceLevel now centralizes summit and ground-level river-top rules
  for rendering, exposed-side checks, surface queries and isometric prop levels.
- SurfacePosition uses the selected native layer transform; isometric features transform
  renderer-local surface positions into their own coordinates through global space.
- terrain_surface_height_probe generates actual summit tiles and compares every drawn
  cell's highest native layer with SurfaceLevel/SurfacePosition. It initially exposed a
  second mismatch for river tops, now corrected. Build, height, live and geometry tests pass.
- This is generated elevated geometry correctness, not live elevation conversion or
  navigation over ramps. Those remain open together with visual proof and scene wiring.

Live feature handoff, 2026-09-05:
- Generator bulk handoff now includes terrain_feature, terrain_relief and terrain_shade
  metadata per cell, alongside its kind. This metadata uses the existing cell save format.
- Flat TerrainFeatureRenderer accepts CellDataPath, observes edits/bulk changes, and
  draws explicit live feature metadata. Cleared cells, water and lava suppress land props.
  Removing those conditions exposes retained metadata; permanent removal can clear the
  feature metadata. Live biome-specific suitability beyond water/lava remains to be reviewed.
- Tests verify sprite counts, clearing, restored cleared-cell data, water suppression,
  and generated feature/relief/shade metadata handoff. Live and recipe tests pass; build
  passes without warnings/errors. Generated metadata memory/performance costs are unprofiled.
- Isometric feature placement and elevated terrain still require live-data conversion;
  do not infer all feature views are wired from the flat-view test.

Painted live-map follow-up, 2026-09-05:
- TerrainPaintedRendererComponent accepts CellDataPath and observes single/bulk live
  changes through a coalesced repaint. Generated-only previews keep their field path.
- Water/sea/ocean explicitly map to the deep-water material slot, not grass fallback.
- Live terrain IDs and coast distances use the same cell rules/coast builder as the tile
  renderer. Live mode uses neutral hillshade and no synthetic generated beach; sand must
  be present in cell data. Live elevation-derived shading is not yet implemented.
- The runtime probe compares painted/tile coastline image bytes for the same map and
  verifies the water ID and removal of stale IDs/coast after a bulk reset.
- TerrainWorld currently wires GeneratorPath/renderers independently. It needs a reviewed
  shared live-source contract before scenes are switched en masse, especially for elevated
  and feature renderers that still read generated fields. Do not infer those are converted.

Composed Tiles/live-water follow-up, 2026-09-05:
- TerrainTileRenderer now accepts CellDataPath and passes it to its transition layers.
  Live mode retains configured biome layers so edits can introduce a new kind without
  depending on the original generated-kind list. Water/sea/ocean atlas bindings are included.
- Parent-owned layers match exact kinds to avoid overlapping grass/dry-grass and water
  aliases; standalone transition layers retain configurable alias matching.
- TerrainCoastField can build its existing signed-distance encoding from live cells at
  BoundsOrigin. Boundary-connected water is ocean; enclosed water is inland. Generated
  previews retain their original subcell shoreline sampling. This is a declared live-map
  policy, not persistence of original generator water-source metadata.
- Live edits coalesce shader coast rebuilding; transition edits remain incremental.
  Coast distance/flood rebuilding is still whole-map work and needs large-map profiling.
- Expanded native runtime test with supplied atlases verifies dry mask, new lake tiles,
  inland/ocean classification after connecting to the boundary, and clearing tiles/mask.
  Build and terrain_live_cells_probe pass. Visual art review and game-scene wiring remain open.

Dual-grid follow-up, 2026-09-05:
- TerrainTransitionLayerComponent now supports authoritative CellDataPath independently
  of a generator. Runtime cell edits update the four affected dual-grid tiles in a
  deferred batch; bulk changes request a full bounded refresh. Native peering mode
  still uses a full refresh. Live bounds prevent the default kind bleeding outside
  the map, and subscriptions disconnect on exit.
- Expanded terrain_live_cells_probe verifies four tiles for one live cell, removal
  after changing its kind, and preservation of an unrelated display tile.
- Parent TerrainTileRenderer wiring, biome presence filtering and the shader coast
  field are not yet converted; do not claim the composed Tiles view is finished.

Live-map follow-up, 2026-09-05:
- TerrainIsometricAutotileRendererComponent accepts CellDataPath as its authoritative
  source. It uses GridCellRules.TerrainKindAt, the same normalized rule as navigation;
  no generated-kind fallback can overwrite edits in this mode. Generator-only previews
  still work without a live map.
- Live single-cell and bulk changes queue a coalesced deferred repaint. Rebuild rebinds
  source subscriptions; exit disconnects them. Current implementation repaints the entire
  bounded layer, not an incremental neighborhood; large-map optimization is still open.
- Navigation invalidates its cell-source cache when CellDataPath changes.
- terrain_live_cells_probe exercises native peering tiles, grass/water edits, navigation
  blockage, bulk reset, replacement-source binding and edits after replacement. Its
  TileSet includes terrain and empty-edge combinations; a test with incomplete peering
  coverage correctly exposed missing tiles and was corrected rather than suppressing it.
- Build, live-cell and geometry probes pass. This is not completion of live rendering
  across painted, dual-grid or elevated block views, nor visual validation of production art.

Coordinate follow-up, 2026-09-05:
- Authored isometric CellPosition/GridExtent include the child TileMapLayer transform.
- PreviewExtent/StartPositionView include the active renderer transform and remain in its
  parent's coordinates. New WorldExtent/StartPositionGlobal include the parent canvas transform.
- TerrainWorldCamera now uses global queries for bounds and focus rather than passing local
  preview coordinates into FocusWorld.
- The geometry probe now verifies renderer offsets, child-layer offsets, transformed parent
  extents, native start-cell global position, and actual camera whole-map/start focus and bounds.
  Build and geometry probe pass. Bounds are conservative axis-aligned rectangles, not exact
  sprite silhouettes. Nonzero map-cell origins and block-height picking remain to be reviewed.

- Trace and unify renderer-local, parent-local and global view coordinates, including
  renderer offsets and nonzero map origins. Current extent queries describe logical
  tiles, not sprite overhang. Recheck transformed preview roots and cameras.
- Connect view switching to the gameplay grid explicitly. GetTerrainLayer enables
  native binding, but existing scenes have not all been wired to it automatically.
- Audit live cell edits, restore ordering and layer updates: visible terrain must agree
  with placement and navigation, without regenerating away player edits.
- Verify grid/navigation bounds propagation after generation, load and projection changes.
- Audit elevated block surfaces, access ramps, unit placement and picking; a flat native
  map conversion does not itself resolve which raised surface a click intersects.
- Run an authored and generated interactive fixture with placement, selection, pathfinding,
  edits and view switching. Inspect rendered output; retain native Godot scene controls.
- Review the game-side addon copy before synchronizing changes; no synchronization yet claimed.

## Native Layer Binding

Redraw work follow-up, 2026-09-05:
- Redraw reuses generated data layers when their component, bounds and requested
  flat tile size are unchanged. NewWorld/RestoreWorld still rebuild recipe data.
  This avoids eight full data-layer fills on a normal projection switch.
- World no longer explicitly rebuilds isometric props a second time when they
  already subscribe to that renderer's SurfaceRebuilt. Editor/unconnected cases
  retain an explicit rebuild. Feature source path changes now rebind a still-live
  previous renderer instead of retaining it indefinitely.
- Recipe and actual-lab probes assert TileSet resource identity is retained across
  redraws/view switches and replaced by NewWorld. These prove skipped rebuilds,
  not a wall-clock speedup claim. Large-map profiling remains outstanding.

Live elevation notification follow-up, 2026-09-05:
- GridProjection subscribes to the selected elevated renderer's SurfaceRebuilt.
  A completed live repaint now emits GeometryChanged and redraws the grid, allowing
  explicitly bound GridObjects to follow changed surface heights immediately.
- Path assignment rebinds the subscription; clearing/replacing the path and tree
  exit disconnect the previous source. NotifyGeometryChanged also refreshes the
  subscription for explicit scene rebinding.
- The height probe now places a bound stationary object on the hill, checks its
  global position after live flattening/flooding and verifies detached renderer
  rebuilds produce no grid notifications. Height, world-source, follower and native
  geometry probes pass; build is clean.
- Arbitrary parent transform changes still require a geometry notification from
  their owner. This completes live elevation notifications, not the outstanding
  art, origin, performance, ramp generation and full interactive-scene work.

Stationary object binding follow-up, 2026-09-05:
- GridProjection emits GeometryChanged via NotifyGeometryChanged. TerrainWorld
  calls it after binding the selected view. GridObject's explicit GridPath binds
  its parent to the saved Cell center; child art owns visual offsets.
- Bound objects reposition on geometry notification, cell changes and restored
  metadata. Moving path followers retain position ownership; waypoint arrival
  updates GridObject.Cell so subsequent idle reprojection uses the arrived cell.
- Tests cover an offset tile renderer switching a stationary building, projection
  changes during movement without snapping, arrived cell identity and idle-unit
  reprojection. No screen-coordinate inverse guess is used for stationary objects.
- Standalone callers changing grid settings should call NotifyGeometryChanged.
  Automatic notifications for live elevated rebuilds/transform edits, occupancy
  ownership across moving units, and rendered object alignment remain to be audited.

Custom route entry follow-up, 2026-09-05:
- Removed the unrelated diagnostic-toggle edit from the outdated game copy after
  the user's architecture correction. TerrainWorldComponent remains the engine.
- Cell routes with configured navigation now include the unit's current cell when
  omitted, then validate all adjacent edges before installation. This closes the
  unchecked approach-to-first-waypoint cliff shortcut. Invalid routes do not replace
  the existing route; raw world-point routes remain a separate explicit API.
- Navigation.CanTraversePath resolves one working set for the supplied route and
  does not apply AllowBlockedGoal to intermediate cells.
- Follower tests cover omitted-start cliff rejection, valid ramp access and rejection
  of nonadjacent supplied waypoints. Automatic replanning and idle-unit reprojection
  remain open, together with the other full-goal requirements.

Continuous elevated water follow-up, 2026-09-05:
- Replaced elevated water's raster-mask TileMapLayer with an owned Polygon2D
  using four projected map-boundary vertices. Logical map cells and coast data
  remain unchanged. Open-water geometry is now constant-size instead of a filled
  overscan tile grid, and there are no internal tile edges to minify.
- The shared sea shader extends signed coast distance offshore from the nearest
  map boundary. Previously it switched immediately to deep water outside the map,
  exposing the map's diamond boundary. Distance now grows offshore and saturates,
  rather than clamping indefinitely or stepping at the boundary.
- Recaptured and inspected the actual 1280x800 elevated lab after both fixes:
  the fine tile seams and hard diamond coast-distance cutoff are absent in this
  fixture. This does not establish all-seed/all-zoom water correctness.
- Height regression now checks Polygon2D geometry and zero tile offset. Water
  mask/modulation regression remains applicable to the shader's tile consumers.
- Remaining: authoring existing saved scenes that contain the former IsoWater
  node type, broad zoom/viewport coverage, art quality and full gameplay fixtures.

Water compositing follow-up, 2026-09-05:
- iso_water.gdshader replaced COLOR alpha without preserving the texture mask.
  TerrainShaderSurface supplies diamond cutouts for isometric tiles, so this
  painted overlapping tile rectangles despite the intended mask. The shader now
  multiplies water opacity by incoming COLOR.a, preserving cutouts and modulation.
- Recaptured the real lab. Mask preservation changes the composite as expected,
  but minified raster diamond edges now expose fine inter-tile seams. A continuous
  tessellated surface (or exact non-overlapping tile geometry) still needs review;
  alpha-mask correction alone is not a seamless-water solution.
- terrain_water_alpha_probe is a rendered SubViewport regression checking a
  transparent corner and combined material/CanvasItem opacity. Run with a graphics
  renderer, not headless dummy rendering.

Rendered lab follow-up, 2026-09-05:
- Ran the actual lab through Godot's OpenGL compatibility renderer on the RTX
  3060 Ti at 1280x800. The lab probe supports --capture and writes four viewport
  images under tests/output/terrain_lab. Inspected all four original captures and
  re-inspected authored/elevated output after the fixes below.
- Visual inspection exposed startup controls replacing the world's authored axes
  with hardcoded defaults: a requested Tiny map became Standard. All controls now
  initialize from the world, including seed and projection; the lab probe asserts
  the requested 32x32 extent survives startup.
- The status label clipped the incomplete-view reason offscreen. It now wraps in
  a reserved bottom band, and settings stop above it. Rendered evidence confirms
  the complete reason is visible at 1280x800.
- Remaining visible defects: authored isometric view blank from missing peering
  authoring; block relief uses abrupt stacked geometry; water surface boundaries
  are visible in the elevated view. Flat captures also show diagnostic resource/
  underground overlays which need clearer separation from normal terrain viewing.
- This is real rendered evidence, but not interactive placement/unit validation,
  complete biome-art verification, or a declaration that the terrain views are done.

Active cell-route follow-up, 2026-09-05:
- GridPathFollower retains logical cells for cell routes. It checks the active
  edge through Navigation.CanTraverse before moving, stops and emits MoveFailed
  when an edit blocks the edge, and rejects missing configured geometry/navigation.
- Only the active segment endpoints are reprojected per advance. Changes in view
  geometry preserve normalized segment progress instead of steering toward stale
  screen coordinates. Raw world-point routes remain explicitly world-space.
- Cell routes begin at their first supplied waypoint rather than skipping to a
  globally nearest later waypoint. Waypoint callbacks may cancel/replace a route
  without the old advance continuing through the replacement's arrays.
- terrain_follower_live_probe covers mid-edge projection change, flooding stop,
  one failure notification, waypoint callback cancellation and successful arrival.
- Remaining: automatic replanning, approach-to-first-waypoint validation for custom
  routes omitting the current cell, idle-unit reprojection, collision-body testing,
  generated/visible ramps, and crowd-performance profiling of per-edge checks.

Relief traversal follow-up, 2026-09-05:
- Navigation now checks edges using live terrain_relief (0/1/2), independently
  of projection and visual summit ornamentation. RespectTerrainHeight defaults
  on; MaximumStepHeight defaults zero. Maps without relief remain flat.
- A cardinal Vector2I terrain_ramp_direction on the lower cell points uphill.
  It allows bidirectional traversal of one relief step only; blocked water,
  occupancy and other cell restrictions still apply. No automatic ramps are
  generated by this change.
- Diagonal edges cannot bypass cliff corners, including Diagonals.Always.
  CanTraverse exposes the same adjacent-edge rules used by A*. TraversalCost
  remains a cost query, not a reachability query, as required by existing users.
- terrain_navigation_height_probe covers cliff separation, routing through ramps,
  reversed/wrong directions, flooded exits, save/restore, two-level rejection,
  explicit step allowance and diagonal bypass prevention.
- Inspected GridPathFollowerComponent: it stores world points and does not yet
  revalidate live edges or reproject active routes. This remains required work,
  along with generator-authored access ramps and visible ramp geometry. The new
  edge rule alone does not establish complete moving-unit terrain integration.

Authored-view validation follow-up, 2026-09-05:
- Inspected grassland.png and its entire TileSet resource. The resource has 17
  atlas regions and two terrain names but no tile terrain assignments or peering
  bits. The image contains grass variants, not coverage for the full biome set.
  No unverified pixel-to-terrain mapping was substituted for missing authoring.
- Isometric authored rendering now validates tile shape, terrain-set existence
  and assigned peering tiles before painting. Invalid rebuilds clear previous
  tiles rather than leaving an old map visible behind a new generation report.
- GetPaintDiagnostics reports unmatched requested cells separately from unbound
  cells; nonempty output alone no longer implies complete rendering. The lab
  status includes the incomplete-view reason/counts after projection switches.
- Regression tests cover valid native coverage, eight intentionally unbound water
  cells, invalid resource diagnostics, stale-tile clearing and the real lab status.
- The lab's grassland TileSet still needs verified authoring and biome assets.
  This change diagnoses its rendering failure; it does not repair the artwork
  or establish visual completion of the authored isometric view.

Gameplay wiring follow-up, 2026-09-05:
- World GridPath and NavigationPath configure the gameplay grid after drawing.
  Painted and authored isometric views use native layers; elevated views use
  ElevatedTerrainPath. Dual-grid views expose a separate empty LogicalGrid layer
  because their display tiles are offset from gameplay cells. No per-cell data
  is duplicated in this geometry-only layer.
- Navigation receives the shared live cell source, grid path and generated bounds.
  Path changes replace its cached grid reference. The lab scene now contains
  authored Grid/Navigation nodes connected through the world controller.
- World live-source regression covers navigation water blocking, bounds, source
  replacement and logical dual-grid picking. The real lab dropdown probe switches
  all four projections, preserves an edited cell, verifies navigation blocking,
  and checks native coordinates after preview reframing.
- Lab testing exposed an IsoFeatures bounds ordering warning (fixed by assigning
  bounds before SurfaceRebuilt), and missing terrain peering coverage in the lab's
  authored isometric TileSet. The latter remains an open visual/content defect;
  coordinate tests do not establish a correctly rendered authored-isometric map.
- Nonzero origins across all views, live ramp traversal, interactive placement/
  movement validation, game-copy integration and visual review remain open.

Elevated picking follow-up, 2026-09-05:
- GridProjectionComponent.ElevatedTerrainPath binds elevated block geometry and
  takes precedence over TileMapLayerPath/manual projection. Cell centers and
  outlines use the renderer's own top-surface level and native layer transforms.
- TerrainIsometricRendererComponent.SurfaceCellAt resolves one native candidate
  per elevation from highest to lowest, rejects cells outside the bounded map,
  and accepts only the candidate's actual top level. Cost is bounded by the
  number of elevation layers, not map area.
- SnapWorld preserves the original position when no surface exists, including a
  removed/missing configured source, rather than assigning NaN to the target.
- The height probe covers transformed renderer/grid parents, raised top picking,
  outline alignment, live flattening/flooding, out-of-map points and missing sources.
- Cliff side/overhang occlusion, ramp traversal, debug-grid invalidation on terrain
  edits and automatic world-to-grid projection wiring remain pending. This binding
  does not make every elevation transition navigable or complete scene integration.

World-source follow-up, 2026-09-05:
- TerrainWorldComponent now binds painted, dual-grid, authored isometric, elevated
  isometric and flat feature views to one live cell store. CellDataPath overrides
  the generator's source; when empty the generator's configured source is used.
  Missing configured sources cancel the build instead of falling back to recipe data.
- Redraw refreshes/switches views without generating or replacing cells. The lab's
  projection dropdown now calls Redraw rather than NewWorld.
- World reference resolution and generator cell resolution honor path changes even
  while the previous target remains alive. Explicit source replacement redirects
  both generation and rendering to the new map.
- terrain_world_live_source_probe verifies nested relative paths, source ownership
  across five renderers, painted live water, projection-switch edit preservation,
  source replacement and restore preservation. All six terrain/runtime probes
  passed. After the lab dropdown change, the final build passed with zero warnings
  and errors and the world live-source probe passed again. The dropdown itself
  has not yet been exercised interactively.
- Still pending: generated-only relief/resource/overlay views, height-aware gameplay
  picking/navigation, scene-wide grid wiring, visual review and game-copy integration.
  This step is source wiring, not completion of the terrain integration goal.

Set GridProjectionComponent.TileMapLayerPath to the actual ground TileMapLayer.
For the authored isometric renderer, GetTerrainLayer returns its IsoTerrain child.
Set the path relative to the grid using grid.GetPathTo(layer). Existing placement,
selection and mouse consumers using CellToWorld/WorldToCell then share native geometry.
TileSet layout, tile size and layer transforms are authoritative in this mode;
the manual Projection/TileSize/Origin settings are ignored for these conversions.
Square/isometric outlines are supported; other shapes report a configuration warning.
