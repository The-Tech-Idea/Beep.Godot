# Terrain Integration Checks

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/run_terrain_integration.ps1
```

Use -GodotCommand with a Godot mono executable path if it is not on PATH.
The runner builds Beep.Godot.csproj, then runs 42 headless probes and twenty-two real
OpenGL checks. -SkipRendering explicitly skips the latter; skipped is not passed.
Each process has a configurable wall-clock timeout, default 120 seconds.

## Latest Run

2026-09-06 shoreline/inland update: clean build, 62/64 passed, zero skipped.
`lava_material` crashed in native Windows/OpenGL code during the suite but
passed on a direct rerun. `generated_coast_centres` still fails at seed 31415,
cell (21,3), zoom 0.5; the assertion remains intact. The new lake-bank tests
cover widths 0/0.25/1/2, ocean/river separation, save/load, edits, and radial GPU
samples across all three painted styles. Inland corner/texture tests also pass.
Large lab captures include close-ups of the reported 128x80, seed 31415 map.

### Earlier Checkpoint

2026-09-06 checkpoint before registering the new lab-style probe: clean build;
59/61 passed, zero skipped. A repaint-test parse error
was then fixed and `terrain_repaint_profile.gd` passed separately. The remaining
failure is generated-coast GPU classification at seed 31415, cell (21,3), zoom
0.5. This is an open rendering/gameplay alignment issue, not an accepted result.

Separately, `terrain_lab_styles_probe.gd` passes on the GPU: the combined menu
selects all three art profiles, preserves the grid, and the native isometric
TileSet paints all 1,024 cells. `terrain_art_styles_probe.gd` also passes, and the
Cartoon capture is byte-identical to the user-approved baseline. The lab's fourth
view now renders complete Kenney terrain tiles. It does not have smooth authored
transitions or elevated props. Captures: `tests/output/lab_styles/`.

## What It Checks

- Coastal grass separation at all eight neighbouring cell positions over three
  seeds and three landforms. Ocean beaches survive majority reduction and final
  topology cleanup. The GPU beach probe checks sand centres in Original, Pixel
  Art and Cartoon, captures all four projections, and verifies view changes do
  not rewrite live cells. Centre checks alone do not prove every shoreline pixel.

- Absolute-grid material/noise phase in full versus cropped painted, native tiled
  water and native isometric water views. Covers positive/negative origins, three
  zooms, rotations, nonuniform painted scale, material edges, coastal sand/water,
  unchanged grid records and opaque coverage. This is an interior comparison,
  not a guarantee of chunk-edge continuity without neighbouring field data.
- Per-material repeat sizes for nine texture slots and terrain aliases, actual
  affected/unaffected GPU pixels at three zooms, beach/seabed scale agreement,
  profile removal/reset, unchanged map-resource identities and live grid bytes.
- Bedrock opacity/mips and bindings in four demos; actual GPU repeat correction
  at three zooms with unchanged interiors and full opacity. The source bitmap
  itself is not asserted seamless; the renderer corrects its repeat-edge jump.
- Decorative feature scatter: absolute-cell seed/crop stability, bounded clump
  spacing, dry-centre rejection behavior, shared flat/isometric fine-water
  acceptance over 32 seeds, and actual terrain-selected sprite pixels at three
  zooms, including binding replacement and woods fallback without grid writes.
- Cached display coast reconstruction: immutable raw input, constant borders,
  block-halo equivalence to whole-image cubic resize, allocation cap, coarse-input
  passthrough, independent CPU reference and GPU curved-shore accuracy. Live
  metadata-only edits reuse coast textures across painted/tiled/isometric views.
- Dedicated painted lava ID/material, imported opacity/mips, live navigation and
  placement, coast-map stability after lava-to-rock edits, and GPU pixels at
  three zoom levels. The real volcanic preset retains rock/lava without grass.
- Themed ground preservation during relief cleanup across seven presets and
  three tiers, and start selection excluding blocked lava while retaining a
  habitable-cell candidate.

- Meadow material opacity, imported mipmaps, reduced large-scale brightness
  variation and average opposite-edge mismatch against the retained originals.
- Fine shoreline handoff to live cells over four generated seeds: sample
  preservation outside movement cores, same-kind painting, cache invalidation,
  binary grid snapshot/partial restore, atomic malformed-input rejection,
  maximum-jitter vegetation anchors and GPU cell centres at 0.5x/1x/2x zoom.
- Rectangular terrain fills: negative origins, preserved cell state and bulk notification counts.
- Painted refresh reuse: unchanged and workflow-only map texture identity;
  look-setting updates with cached data; coast detail/default-kind invalidation;
  external wrong source/atlas/alternative repair and normal bounds/hole repair.
- World ownership, recipe restore and shared live-cell sources.
- Authored isometric tile binding validation: conflicting/malformed bindings,
  first-rebuild stale-layer clearing, recovery and reattachment with live edits.
- Beach width paints existing land without moving the footprint: widths 0/1/3,
  two sizes, two seeds, all three landforms and an achievable 50% land target.
- Generated topology and landmass guards: coverage, compactness/count, edge water,
  lake enclosure, continuous/grid cell centres and deterministic reconstruction.
- Final topology after scale cleanup: river/lake drain fixtures update painter
  samples, and independent cardinal flood-fill matches every continent ID and
  diagnostic count over four seeds with scale rules enabled and disabled.
- Orphan water-fringe cleanup: removed lakes/rivers leave no samples in adjacent
  dry cells; retained bodies keep their fine shape/material; ocean/mouth water
  and landmass footprints survive. Independent fine-water flood-fill over four
  generated seeds finds no unanchored water bodies with scale rules enabled.
- Relief cleanup across rock/snow/gravel, flat/hill/mountain tiers and scale
  rules on/off: cell/fine material agreement without filling shoreline water,
  erasing sand detail or flattening continuous height/shade. Minimum-size ranges survive.
- Generated coast encoding against an independent nearest-opposite-sample 3/4
  distance oracle, every texel's wet/dry sign and ocean flag, byte-identical
  repeated rebuilds and seed invalidation. Fixed pre-fix island hashes are not
  used as a coast-encoding contract after the beach-spacing correction.
- Exact rectangular recipe dimensions and land footprint, override persistence and invalid-input rejection.
- Native projection geometry, bounds origins, surface heights and atlas changes.
- Painted, feature, relief, resource and underground overlay alignment.
- Large-grid repaint timing and byte-exact coast encoding at 144x144 and 240x240.
  Timing is reported, not used as a machine-dependent pass/fail threshold.
- Byte-exact ID and shade maps for a varied live elevation fixture, including water
  with obsolete land elevation, before/after batched sampling and image upload.
- Navigation source binding, height transitions and live follower invalidation.
- Worker arrival validation: failed travel cannot complete terrain jobs remotely.
- Clear-effect preflight, explicit tool source rebinding and replacement queue isolation.
- Placement revalidation after flooding, footprint bounds/relief, explicit ownership and demolition.
- Construction approach selection at map edges/coasts and material retention when access is flooded.
- Native terrain collision and live edits.
- Authored lab projection switching and the playable gathering/movement sample.
- Actual rendered water alpha, lab screenshots and playground screenshots.
- Painted material coverage at zero/narrow/wide blend widths and extreme noise
  displacement, measured sharpness response, and unchanged terrain-ID/coast data.
- Texture-driven material transitions: bright/dark detail moves the edge in
  opposite directions, with unchanged interiors and ID/shade/coast data at
  0.5x, 1x and 2x zoom. This is not proof of overall artwork quality.
- Painted shading gain measured through GPU pixels, with unchanged ID/shade maps
  and full-strength shading still available as an explicit setting.
- Independent ground/water texture repeat sizes at three zoom levels: actual
  affected/unaffected GPU pixels, unchanged ID/shade/coast data and full opacity.
- Actual isometric demo art bindings, imported mip chains, clean overview/close
  captures, standalone-demo parity and grid positions with the detailed atlas.
- Isometric rivers: shared animated shader, native grid coverage/picking under
  negative origin and zoom, navigation policy, disconnected/narrow/wide channels,
  authored material isolation, live drain/refill and source/visibility lifecycle.
- Isometric cliff fitting: unchanged top texture/footprint at three zoom levels,
  shorter faces without adjacent-frame colour, and flat-source seabed rendering.
- Full standalone isometric stack guard: maximum stack height, distinct primary
  biome frames, land/river/bed coverage, layer and prop ordering, shader bindings.
- Rounded live coasts across all 16 corner patterns, isolated cells, one-cell
  channels, shifted origins and multiple detail levels. GPU checks preserve
  visible cell-centre land/water classification at two zoom levels.
- Identical coast/ocean data across the lab's painted, tiled and isometric views
  after a water edit; actual isometric polygon shader coordinates versus native
  grid positions under scale/translation.
- GPU cell-ID sampling against native square-cell positions using the painted shader's
  and water shader's production vertex functions, plus the water shader's production
  projection inversion for 64x64 and 64x32 isometric diamonds.

Latest ground-cover/proportion/plain-iso run (2026-09-06): clean source build,
55/55 passed, zero skipped (38 headless, 17 GPU). Actual lab seed 31415/Tiny has
24 raised cells, zero rock/gravel ground cells and nine decorative rock props.
Smallest tree-frame extent is 76.36 pixels; largest rock-frame extent is 40.08
pixels at 64-pixel cells. These are frame bounds, not opaque pixel silhouettes.
The tests also check zero density, deterministic rebuilds, unchanged cell records,
mipmapped sprite imports and zoom-independent prop sizes. Isometric art regression
now protects the restored plain Kenney atlas, frame 54 and matching grid geometry.
Two test fixtures were made explicit: the live-cell test requests four stamps,
and the synthetic cliff test owns its 92x107 atlas geometry independently of demo art.
Both retain their original behavior/pixel assertions. Fresh painted and iso captures
were inspected. Full prop footprints and cross-view rock objects remain open.

Previous material-origin run (2026-09-06): clean source build, 54/54 passed, zero
skipped (37 headless, 17 GPU). The new crop regression failed before the shader
origin fix and passes across the three shader-backed renderer paths afterwards.
The clean painter capture also passed and was visually inspected. Current images:
tests/output/painter_visual/overview.png and close.png. Bright foliage versus
olive ground and parallel surf bands remain visual follow-up work. The fourth
view's missing atlas, large cold rebuild cost and existing editor shutdown leaks
are not resolved by this run. Passing tests are not full visual acceptance.

Earlier feature-scatter/world-seed run (2026-09-06): clean source build,
51/51 passed, zero skipped (36 headless, 15 GPU). The lab seed assertion
reproduced the old independent-isometric-seed failure before its fix. New GPU
frame-selection tests run at 0.5x/1x/2x; the clean painter probe also passed.
Latest woodland captures: tests/output/painter_visual/overview.png and close.png;
full projection captures: tests/output/terrain_lab/view_*.png. Material scale,
overall art consistency, large cold rebuild cost and fourth-view art remain
incomplete; the passing suite is not full visual acceptance.

Earlier authored-autotile contract run (2026-09-06): clean source build, 48/48
passed, zero skipped. Expanded live-cell fixtures reproduce and verify fixes
for conflicting/malformed binding acceptance and first-rebuild stale tiles.
The fourth lab view remains explicitly incomplete because its art is unauthored.

Earlier meadow-art run (2026-09-06): clean source build, 48/48 passed, zero
skipped (35 headless and 13 rendered). New material imports retain mipmaps and
opacity. The root project also starts the terrain lab in a real GPU run.
`tests/output/.gdignore` excludes diagnostic captures from Godot's asset scan,
without deleting them or preventing probe output writes.

Earlier material-edge run (2026-09-06): clean source build, 47/47 passed,
zero skipped. The texture-driven edge fixture measures opposite bright/dark
boundary shifts at three zooms and zero interior pixel difference. This run
also includes the fine water-fringe cleanup and exact retained-body tests.

Earlier painted-refresh run (2026-09-06): clean source build, 47/47 passed,
zero skipped (34 headless and 13 rendered). The previous intermittent shutdown
failure did not recur in this run; this is not proof that its cause is resolved.
The expanded painted-origin probe includes native geometry repair and map-cache
invalidation/reuse. Final warmed repaint medians were 0.818 and 1.786 ms for
144/240-square maps; texture hashes remained unchanged.

Earlier material-scale run (2026-09-06): clean source build and the new rendered
scale check passed, but the isometric stack process crashed during finalization
after its assertions passed. Direct reproduction also failed. An explicit
scene-unload check did not solve it; retaining the river material's managed
reference passed once but failed during repetition. That run was not green.
The new `terrain_iso_shutdown_probe` exercises diagnostics and scene unloading;
the runner requires successful process exit, not only the printed success marker.
The expanded rerun passed 44 of 45 checks, including all 12 GPU checks; the new
shutdown reproduction failed while iso_layers passed. The intermittent failure
remains open in the terrain integration plan.

Success requires exit code zero, the exact probe success line, no timeout and no
Godot/script failure patterns. Logs are retained separately for stdout and stderr.
Deliberate invalid-configuration warnings remain visible in those logs. The runtime
MCP bridge is disabled for the test process and its previous environment value is
restored afterwards.

## Outputs

- tests/output/terrain_integration/results.json: each probe's status, duration,
  process exit code, timeout flag, rendering flag and log locations.
- tests/output/terrain_integration/*.log: full per-probe output.
- tests/output/terrain_lab/view_0.png through view_3.png: rendered lab views.
- tests/output/terrain_playground/start.png: starting truck and terrain.
- tests/output/iso_river/animated_a.png and animated_b.png: controlled river fixture
  rendered 1.5 seconds apart; actual river-centre pixels must change.

## Limits

Latest warmed repaint measurements with revision-keyed map textures and native
bulk surface validation: 0.827 ms at 144x144 and 2.495 ms at 240x240. Immediately
before these changes, the same fixture measured 44.999 and 100.477 ms; the bulk
geometry check alone measured 25.652 and 57.300 ms. ID/shade/coast hashes match.
These local medians cover unchanged explicit `Rebuild` calls, not generation,
terrain-changing edits, frame rate or arbitrary-map performance guarantees.

The repaint profile excludes generation and initial population. It measures five
explicit warmed Rebuild calls with CoastDetail=4 and a fixed water/grass boundary,
reporting their median. Before bulk coast-image upload, local medians were 146.296
and 391.672 ms; after, 116.197 and 321.468 ms. Coast SHA-256 hashes were identical.
These are headless CPU timings, not GPU frame times or a guarantee for arbitrary maps.

With per-painted-view coast reuse, the same warmed fixture measured 81.306 and
227.574 ms; the original coast hashes still match. This optimization applies when
the water mask is unchanged, not to a repaint that modifies coast topology. The
painted-origin probe verifies reuse and invalidation independently of timing.

The lab's authored-isometric terrain-connect art is incomplete. Its probe expects
an explicit incomplete-view diagnostic and verifies that it does not claim a valid
paint. A passing lab probe is not evidence that the missing art has been authored.

This suite is not the full addon gate. It does not establish completion of the
external Oilfield Days migration, cliff/ramp collision barriers, large-map performance,
all artwork quality, or the combined placement/job/save gameplay workflow.

The former standalone tests/examples/iso_layers.gd is now part of this suite.
Its original 2.5-cell stack-height limit and distinct-primary-frame checks are
unchanged. The authored stack now measures 2.30 rather than 4.26 cell heights.

Earlier integration run: 42 passed, zero skipped, clean C# build on 2026-09-06.

For clean painter appearance captures, run `godot --path . --rendering-method
gl_compatibility --script tests/terrain_painter_visual_probe.gd`. It captures the
addon lab at overview and 1:1 scale with a fixed seed, without HUD, survey markers
or intentional test edits. Outputs: tests/output/painter_visual/overview.png and
close.png. A successful capture is not visual acceptance of the current artwork.
It also prints actual generation coverage/count diagnostics and the longest
axis-aligned coast run in gameplay tiles. The latest fixture retains its 42%
footprint and measures 12 tiles versus 13 before growth placement/scaling changes;
post-carving logical region count differs from footprint count and remains under review.

Add `-- --diagnose` for no-shade/flat-material captures and material ID counts.
Add `-- --materials` for same-footprint grass/desert/mud/snow material comparisons.
Add `-- --edge-detail` for fixed-time texture-driven edge comparisons at three zooms.
Add `-- --meadow-art` for original/new grass artwork on the same generated map.
Those swaps hide features and are texture diagnostics, not generated biome scenarios.
