# Terrain Integration Checks

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/run_terrain_integration.ps1
```

Use -GodotCommand with a Godot mono executable path if it is not on PATH.
The runner builds Beep.Godot.csproj, then runs 52 headless probes (50 registered in its
`$probes` table, plus `examples/iso_layers` and `examples/landmass`) and twenty-four real
OpenGL checks. -SkipRendering explicitly skips the latter; skipped is not passed.
Each process has a configurable wall-clock timeout, default 120 seconds.

## Probes that drive the lab wait for its build

`terrain_generator_lab.tscn` builds its world on a worker: `_Ready` defers `Generate()`, and the
build publishes over several frames before `GenerationFinished`. Every probe that loads the lab
extends `tests/terrain_lab_build.gd` and waits through its `await_lab_build(world)`, called
straight after the lab enters the tree or straight after a control starts a build. It returns
`{finished, success, message, count}` and asserts nothing, so a probe that expects a build to
fail or be cancelled reads it the same way as one that expects success.

- **Why not a frame count.** Two frames after `add_child` the world has not published: no
  `Splat/SplatSurface`, no `Iso/IsoSeabed`, `BuiltSize` still `(0, 0)`, and the lab has disabled
  its own controls. Eight probes read the lab that early and failed with messages that named none
  of this - "node not found", "lab controls overwrote configured map size", "styles must be
  reachable from every view", "expected sparse rock objects". Converted on 2026-09-16:
  `art_styles`, `lab_styles`, `style_beach`, `iso_water_origin`, `iso_art`, `terrain_lab_grid`
  (headless and `lab_capture`), `terrain_ground_cover` and `terrain_prop_sizing`. Waiting exposed
  one more fault behind `terrain_lab_grid`'s: its "a projection switch must not rebuild generated
  data" check compared the `CellData/TerrainData` TileSet, which exists only when the data layers
  are materialised - they read the published field directly by default - so the probe now sets
  `MaterializeTileLayers` on its own lab.
- **Why the signal, not `IsGenerating`.** `BuiltSize` is set partway through publication, before
  the painted snapshot and collision finish, so "not generating, `BuiltSize` set" is also what a
  build that failed late looks like. Six earlier copies of this wait polled `IsGenerating` alone
  and would have passed a failed first build; they now use the helper too (`stack_order`,
  `cell_data`, `terrain_view_parity_probe`, `terrain_lab_generation_probe`,
  `terrain_lab_tile_views_probe`, `terrain_water_look_capture`). **Mutations:** a helper that
  returns without waiting reproduces the original failures word for word; a lab whose
  `CollisionPath` names a missing node fails the build late, and the probe reports "Configured
  terrain collision is unavailable" instead of carrying on with a 32x32 `BuiltSize`.
- **Frame caps.** A rendered probe can run through `--quit-after 3600` frames before a Tiny world
  has published, and then quits with no marker and no error. The render table's last column is the
  frame cap; the probes that wait on the lab carry `0` and are bounded by 180 s of wall-clock time
  instead (the wait itself gives up at 120 s and says why). A failed assert does not end a probe,
  so a failing one of these runs to that bound - and the runner then kills the whole process
  tree with `taskkill /T`. It used to call `Process.Kill()` on what `Start-Process` returned,
  which is the launcher when `godot` on PATH is a `.cmd` shim: the engine survived the kill and
  kept running beside every probe after it. The frame cap had hidden that, because a capped
  probe quit on its own.
- **Not converted, deliberately:** `terrain_world_generation_probe`,
  `terrain_world_collision_publication_probe`, `terrain_world_iso_publication_probe` and
  `terrain_painted_publication_probe` test TerrainWorldComponent's generation itself - invariants
  on every frame of a build, restarts from inside its handlers, a world freed mid-build - so their
  loops are the thing under test, not a wait.

## The lake-bank finalizer crash, and how to reproduce it deliberately

`terrain_lake_bank_probe` and `art_styles` have both died with
`FATAL: Condition "gchandle.is_released()" is true` at `mono_object_disposed_baseref`, inside
`GC.RunFinalizers` — a Godot RefCounted disposed twice, reported far from wherever the second dispose
was set up. It was recorded as intermittent. On 2026-09-18 it became repeatable, and bisecting it
narrowed the recipe to three ingredients, none of which is the art drawn on screen:

- the lake fixture (`TerrainShorelineContourSmoke.PopulateLakeFixture`) run repeatedly, **and**
- a `TerrainMapArt` profile assigned to a painted renderer that is rebuilt after each fixture, **and**
- enough rounds for the collector to run — it dies on the third.

Measured with a 40-line driver doing exactly that and nothing else (no props, no GPU reads, no
assertions): the fixture alone survives 12 rounds, style switching alone survives 8 rounds, and the
two together die on round 3. It reproduces with `cartoon.tres` alone, which binds no props at all, so
the prop sheets are not involved. `art_styles` prints its own `[terrain-art-styles] OK` first and then
dies at exit, so its checks pass and only the shutdown is unsafe.

Two candidate fixes were tried and **did not** help, so neither is in the tree: dropping the `using`
on `TerrainPropSizing.VisibleRegion`'s `GetImage()` result, and dropping the `using` on the images
`TerrainMapArt.GroundTexture` hands to `ImageTexture.CreateFromImage`. The double dispose is
somewhere else in the profile-plus-rebuild path and still needs finding.

## A lake reads as water

`terrain_lake_colour_probe` (2026-09-18, GPU, `lake_colour`) frames the largest enclosed lake on the
painted demo and takes two frames of it: the normal render, and the shader's own `contour_debug`,
which paints a flat blue wherever it considers the fragment water. The second is therefore an exact
mask of what the shader thinks it is drawing, and the check is that the two agree — 95% of the pixels
the shader calls water must render blue over green.

It guards FIX-19. The painted view scores depth and how much seabed shows from distance to the
waterline in tiles, which is right for a sea and wrong for anything narrow: inside an enclosed body
on this map the coast field never exceeds **+0.085 tiles**, so on the sea's ramps every one of its
pixels was scored as touching the shore, took the palest water and kept a third of the sand bed
showing through. It came out greener than it was blue and all but matched the grass around it.

**Read the name with care.** The bodies it measures are mostly RIVERS: this map carries 59 river
cells against 8 lake cells, and six of its seven enclosed bodies are pure river. The rule guarded is
about water with no fetch, which covers both, but a pass here says nothing about lakes specifically.

**Mutation:** setting `LAKE_DEPTH` to 0 and `LAKE_BED` to 1 in `water_common.gdshaderinc` — their
no-op values — fails it with `only 0.0% of the lake reads as water`. Run and confirmed, not assumed.

## Every art style is a style of its own

`terrain_style_profiles_probe` (2026-09-18) walks the lab's view menu past the four projections and,
for each art style, checks that the menu names it, that selecting it puts that exact profile on the
world, that the profile carries its own trees, bushes and both rock sizes, and that props and relief
actually drew. No two styles may draw from one tree sheet — that is what a silent fallback to the
renderer's default sheet looks like. It also pins the menu's shape, which used to be owned twice:
six item names typed into `terrain_generator_lab.tscn` and a list built in code, with `Fill` adding
items only to an empty chooser, so styles added to `StyleProfiles` never appeared.

**Mutation:** pointing `low_poly.tres` at `cartoon_trees.png` fails it with
`Low Poly draws the same tree sheet as Cartoon`.

## Every view's texture filter is pinned

`terrain_texture_filter_probe` (2026-09-18) walks the lab through all four projections and pins how
each rendering type samples its art: nearest above the mip chain for the three prop renderers,
linear above it for tile, block and icon views, plain linear for the painted surface whose shader
declares a filter per `sampler2D`, and the library pack's own `PixelArt` answer in both views that
can draw a pack. `docs/terrain-engine/TEXTURE_FILTERS.md` explains why each is what it is.

A node is pinned only once it has **drawn** — the probe counts stamps, icons or used cells first —
because Godot's default is "inherit from the parent", so a renderer that never rebuilt would
otherwise sit at whatever the project defaults to and pass. **Mutation:** restoring
`LinearWithMipmaps` in `TerrainFeatureRendererComponent.Rebuild` fails it with
`flat props (trees, bushes): linear+mips, wanted nearest+mips`; restoring the mipless pair in
`TerrainLibraryPainter.Build` fails both pack pins. The second mutation earned its keep — it is what
showed that the painter, not either renderer, owns how a pack's tiles are sampled.

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

- One half-sample correction, checked where it lives (VIEW-07,
  `terrain_shoreline_contour_probe`, headless marker `[shoreline-contours] CPU OK`).
  The probe's C# half, `tests/TerrainShorelineContourSmoke.cs`, exercises
  `TerrainEuclideanDistance.ToTiles` on a straight boundary at 1, 4 and 12 samples
  per tile — the `n`-th sample out must read a whole number of tiles — and requires
  a sample inside the boundary to report zero rather than a negative distance.
  **Mutation:** dropping the `- 0.5` fails it with `ToTiles(1, 1) = 1, not 0.5 tiles`.
  It matters because the same rule decides which samples the shoreline stage calls
  sand and how far from the waterline the coast field says a point is, at two
  different sample resolutions; the contract scan pins that no other file under
  `ecs/terrain/` writes it and that the stage and the coast field both call it.
  The rest of that smoke is unchanged: a brute-force Euclidean oracle over 24
  random masks, empty/full masks, convex and concave analytic offsets, and a
  narrow land strip.

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
- Prop size against the art (FIX-18, `terrain_prop_sizing_probe`): a prop takes its category's size in
  cells, capped at its own art's resolution, in every view. On the shipped cartoon sheets no stamp may
  exceed the largest visible frame its own sheet holds — each view against its own sheet, since the
  isometric view binds 109x150 tree art where the flat binds 58x120 — and the largest stamp must reach
  it. On `forest_trees.png`, whose 314-pixel frames exceed any size asked of them, every stamp lands
  inside 1.75–2.25 cells, flat and isometric, and one edit of the shared resource still moves both.
  **Mutation:** the cap removed — "art smaller than the category was magnified to (107.6, 128.0)".
- Bushes among the trees (`terrain_understory_probe`, marker `[terrain-understory] OK`): live cells
  authored in columns of woods, forest, jungle, marsh, oasis and bare grass, crossed by a row of
  shallow water, drawn by the flat feature renderer with `cartoon_trees.png` and
  `cartoon_bushes.png` at `BushesPerWoodsTile` 2. Bushes stand on woods and forest tiles only, at
  most two per tile, each inside `TerrainPropSizing.Bushes`; every tree stamp is exactly where a
  renderer with no bush sheet puts it; the mean distance from a bush to the nearest trunk of its tile
  stays above 0.5 cells; `BushesPerWoodsTile` 0 draws none; a `TerrainMapArt.Bushes` sprite (10x40)
  is drawn instead of the sheet. The fixture is live cells rather than a generated map because no
  small generated map grew jungle or marsh - a temperate, a hot-wet and a full-latitude 48x48 world
  all came out woods and forest only - so "woods and forest only" could not fail there.
  **Mutations**, each failing its own check: bushes on every feature (a bush on a marsh tile);
  bushes on woods only (none on forest); bushes scattered as if the trees were not there (mean
  clearance 0.418 against 0.578 - the first threshold, 0.3, passed that mutation and was raised to
  0.5, between the two measured values). `terrain_prop_sizing_probe` measures the flat view's trees
  (`woods`, `forest`, `jungle`) and bushes against their own ranges through
  `GetStampBoundsOfKind`, since the view now draws two size categories.
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
- Player start areas (FEAT-09, `terrain_start_area_probe`, marker `[terrain-start-area] OK`): a
  48x48 Continents map, seed 31415, radius 10, a critical wheat entry on a one-cell distance band
  and a minimum of 120 cells (80 until FIX-15 greened the map and every start became usable). Every area cell is dry, not mountainous, not lava and 4-connected to
  its start; no two areas are 8-adjacent; each headquarters footprint is level and inside its area;
  report cell counts match the map; usable starts have at least two exits and both wheat; the
  fixture holds usable and unusable starts; relaxation was used; diagnostics match the reports.
  `TerrainDataLayersComponent` in runtime and materialised modes agrees with the generator's
  `StartAreaAt` on every cell and lists `StartCells` in start order, with the materialised start
  tiles re-inserted in reverse so only `start_index` can keep the order.
- Start areas in play (`terrain_start_area_play_probe`, marker `[terrain-start-area-play] OK`):
  `GridStartAreaComponent.OriginOf` matches every generator start and is `NoCell` past the last;
  `SpawnCellsFor` skips a blocked cell and orders by (distance², y, x) inside the start's own area;
  placement with `RestrictBuildToStartArea` refuses another start's origin as `outside_start_area`,
  allows its own, allows both with the restriction off, and reports `not_ready` for a missing
  start-area component or one without cells; `SpawnAtStartArea` puts three workers on the first
  three spawn cells; the overlay's `StartAreaSegmentCount` equals every area border side plus every
  headquarters perimeter, and 0 with `ShowStartAreas` off.
- Spawn markers and the playable cordon (FEAT-12, `terrain_spawn_markers_probe`, marker
  `[terrain-spawn-markers] OK`): a generated 48x48 world with `SpawnsPath` set writes one
  `Spawns/Start_<k>` marker per start, each carrying its `start_index` and the 3x3 `hq_footprint`,
  and a rebuild republishes exactly that many; `GridStartAreaComponent` with only `SpawnsRootPath`
  and `GridPath` reads every marker back as the generator's own start cell, answers `NoCell` past
  the last marker and reports `HasAreas` true. The `Spawns` node sits at a deliberate offset from
  the map root, so a marker written in the wrong space cannot land on the right cell. An authored
  map — two hand-built markers, no reservations — answers `OriginOf(1)`, reports `HasAreas` false,
  and leaves `RestrictBuildToStartArea` inert: three cells all placeable with
  `GridPlacementComponent.StartAreaWarnings` exactly 1 across the three queries. With
  `PlayableInset = 1` the outer ring and the far edge are out of `IsInBounds` while `(1,1)` is in,
  placement refuses the cordoned cell as `out_of_bounds` and allows the one inside, and an inset of
  40 is bounded to 23 with the middle of the map still playable.
- Faction catalog and start assignment (FEAT-10, `terrain_faction_assignment_probe`, marker
  `[terrain-faction-assignment] OK`): a 48x48 map, seed 31415, radius 10, at least three starts, and
  a four-faction catalog — `alpha` free, `bravo` locked to start 3, `charlie` unplayable, `delta`
  free. Seven sections. **`StartCount`** equals the generator's start count. **`Assign` refusals**,
  each with its own reason: `no_faction_catalog` before a catalog is wired, `unknown_faction`,
  `start_out_of_range` at both ends (−1 and `StartCount`), `start_taken:alpha` naming the holder,
  `start_locked:3` naming the lock, and the locked faction taking its own start; a refused faction
  holds nothing, and `FactionAtStart` answers the catalog index of the holder or 0. **`AutoAssign`**
  seats three playable factions, keeps `bravo` on its locked start 3 whatever catalog order says,
  gives `alpha` and `delta` the first two free starts, seats the unplayable `charlie` nowhere, and
  returns the same table on a second run. **`ActiveStartIndex`** is the local faction's assigned
  start with a catalog wired and `LocalStartIndex` with the catalog removed. **The save** captures,
  moves `alpha`, restores, and gets the original table back — then restores the *same* snapshot
  against a catalog whose order was reversed and still gives each faction its own start, because the
  table is keyed by faction id. **Two spawners** owned by `player_1` and `player_2`, with two
  registered players on `alpha` and `bravo`, each land on a cell whose `StartAreaAt` equals their own
  faction's `StartIndexOf + 1`, on different cells. **The overlay** draws every held start's border in
  that faction's authored colour (red for `alpha`, blue for `bravo`'s locked start) and, with
  `StartAreaPath` cleared and rebuilt, still colours a start from its own fixed palette rather than
  drawing nothing. **Mutations**, each failing its own check: the spawner reading `ActiveStartIndex`
  (both players land on one cell); the overlay ignoring the assignment; the save keyed by index (the
  reordered catalog hands factions each other's starts); `AutoAssign` not seating locked factions
  first; and `Assign` dropping the lock check. One guard could not fail at first — the fixture
  originally locked `bravo`, second in the catalog, to start 1, which is the start catalog order
  would have given it anyway, so removing the locked pass produced an identical table and the check
  passed against mutated code. It now locks `bravo` to start 3. The minimap half of FEAT-10 is not
  here: it is the `StartAreaTint` section of `tests/GridMinimapSmoke.cs`, run by
  `tests/grid_minimap_probe.ps1`.
- Start-area radius and start-distance scaling through the world recipe, storage modes and shifted
  origins: the recipe probe checks the radius and a 1.5 scaling reaching the generator (distances
  0..16), `start_area_radius` and `start_distance_scaling` at recipe version 5, a world with neither
  reserving and measuring nothing, the status suffix, and restore giving back the same reserved cells
  and the same distances; the exact-recipe probe expects version 5, a zero scaling saved, and 2.5,
  -0.5 and NaN refused without touching live cells; the data storage and origin probes compare
  `StartAreaAt` and `StartDistanceAt` across both storage modes, the start order, nine materialised
  layers and a shifted origin, and require the distance measured on every published cell and -1 off
  the map.
- Start distance, richness by distance and neutral sites (FEAT-14, `terrain_start_distance_probe`,
  marker `[terrain-start-distance] OK`). **Far country:** a 112x80 Continents world, seed 31415, two
  starts, abundant Oil And Gas resources, read in full at scaling 0 and at scaling 1. Scaling 0 reads
  -1 on every cell and reports no neutral sites; scaling 1 reads 0 on each start and, on every cell,
  the rounded Euclidean distance to the nearest start computed by brute force; every deposit keeps
  its kind and depth and carries exactly its scaling-0 richness times lerp(0.5, 1.5, d / farthest),
  clamped to [0.05, 1]; the surface resources do not move; and the near/far mean-richness ratio
  (within 15 cells against beyond 30; near was 10 until FIX-15 left only 7 deposits that close) falls
  relative to the same map unscaled. The plan's own guard -
  near mean below far mean - was dropped because it could not fail here: this map's deposits are
  richer far from its starts already, and it still passed with the curve inverted (0.169 near, 0.186
  far). **Crowded starts:** a 64x48 six-start map, radius 10, a kit of Neutral horses, iron and an id
  no catalog holds, Count 2. Every neutral placement lies where its two nearest starts are within 2
  cells, outside every area and its gap, dry, not mountainous, not lava and on the map; no resource
  exceeds Count x starts and any shortfall is reported; the unknown id is reported; the diagnostics
  count what the report lists; the fixture's own band crosses 50 area cells, so the area filter is
  exercised; a one-start map places nothing and reports `neutral_needs_two_starts`. **Mutations**,
  each failing: the richness curve inverted; the band filter dropped; the area and gap filter
  dropped; distances floored instead of rounded. The two FEAT-14 cases of the generation baseline
  (`*_start_distance`) pin the rest byte for byte.
- Construction approach selection at map edges/coasts and material retention when access is flooded.
- Native terrain collision and live edits.
- Authored lab projection switching and the playable gathering/movement sample.
- Actual rendered water alpha, lab screenshots and playground screenshots.
- Painted material coverage at zero/narrow/wide blend widths and extreme noise
  displacement, measured sharpness response, and unchanged terrain-ID/coast data.
- The ground's grain (FIX-17, `terrain_painted_blend_probe`'s `verify_ground_grain`): one material over
  the whole fixture, a 512-pixel one-pixel checker squeezed into a single tile so the base sample mips
  flat, and the grain sampling that same texture over eight tiles at one texel a pixel. The interior's
  variation must rise more than threefold (0.0000 flat against 0.1255) while its mean stays put (0.4980
  either way). **Mutation:** the grain multiplied by zero — 0.0000 against 0.0000. The probe's older
  coverage checks guard the other half: a grain centred on mid-grey instead of on the material's own
  average darkened solid sand and failed as 61,504 uncovered pixels.
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
- Seabed depth from the coast field (VIEW-05, the seabed section of
  `terrain_iso_river_probe`): a 3x3 inland sea beds every one of its cells, corners
  included, because every cell of it touches the shore; on a **diagonal** coast the
  cell two diagonal cells offshore beds in the same material band as the one inshore
  of it — the case where distance from the waterline and the deleted four-neighbour
  sweep disagree — four Manhattan steps against roughly two tiles of real distance,
  two material bands apart; and
  open water with no shore inside the field's range draws no bed at all, so the shelf
  ends instead of tiling the ocean. Restoring the four-neighbour sweep fails the
  diagonal-coast check with rock against gravel.
- Isometric cliff fitting: unchanged top texture/footprint at three zoom levels,
  shorter faces without adjacent-frame colour, and flat-source seabed rendering.
- Full standalone isometric stack guard: maximum stack height, distinct primary
  biome frames, land/river/bed coverage, layer and prop ordering, shader bindings.
  Its seabed check was rewritten for VIEW-05: it used to carry its own
  four-neighbour sweep and describe it as "the same breadth-first sweep out from
  the coast that the renderer uses" — a copy of the implementation, which compared
  two different metrics and failed on a correct change the moment the renderer
  stopped counting steps. It now asks the renderer (`SeabedDepthAt`) and checks
  what does not depend on the metric: the bed is painted exactly where the shelf
  reaches (`1 <= depth <= SeabedDepth`), every water cell against the shore has
  one, the shelf ends with open water left bare, and the layer's own reported cell
  count agrees with the map. The shore half of that is what caught the missing
  clamp — 21 shore cells with a negative sub-tile distance and so no bed, which a
  water cell draws as a transparent hole rather than a shallow.
- Rounded live coasts across all 16 corner patterns, isolated cells, one-cell
  channels, shifted origins and multiple detail levels. GPU checks preserve
  visible cell-centre land/water classification at two zoom levels.
- Identical coast/ocean data across the lab's painted, tiled and isometric views
  after a water edit; actual isometric polygon shader coordinates versus native
  grid positions under scale/translation.
- One world, one sea (VIEW-04, `terrain_view_parity_probe`): the per-projection sea row now
  covers all four views — `Splat/SplatSurface`, `TileRenderer/TileWater`, `Iso/IsoWater` and the
  new `IsoAutotile/TileWater` — and each must be drawn, carry a `coast_map` (without it the sea
  draws at open-sea opacity right up to the beach and nothing reports it), and agree with the
  first view that drew them on eight shared uniforms: `foam_strength`, `deep_tiles`,
  `shallow_tiles`, `wave_intensity`, `ground_texture_tiles`, `water_texture_tiles`,
  `foam_tiles_along`, `swell_directionality`. This replaced the plan's rendered
  same-cell-pixel comparison, which would have needed camera arithmetic to find one cell on
  screen in two projections; the uniform check measures the property that comparison stood in for.
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

## What the climate makes of the ground (FIX-15)

`tests/terrain_climate_share_probe.gd` measures generated maps through
`tests/TerrainClimateShareSmoke.cs`, which reads the field directly: a GDScript probe going through the
generator's per-cell accessors rebuilds the settings record twice a cell. The recipe is Oilfield Days'
shape (Continents, land 0.6, climate maps and scale rules on) with the game's span rule, kilometres /
10000 at 6 cells a kilometre. The two lab maps keep the scale rules' own span: a standard 64x64 (seed
2027) and a large 96x60 (seed 31415). Climate shares are of inland cells, which is land minus beach
sand; beach width belongs to the beach setting, not to the climate. Woodland shares are of all land.

- **Size does not make desert.** Temperate, normal rainfall, at 32, 64, 96 and 144 cells for seeds 31415
  and 2027: at most 10% desert, at least 70% grass and dry grass, and no more desert at 144 cells than at
  32. Measured 0% desert throughout. The mutation restoring the fixed 6.5-cell coastal reach fails it
  (31% and 16% desert at 144 cells).
- **Temperature reaches the ground, through the dry belt.** On both lab maps a hot, normal-rainfall
  world is at least 15% desert and at least 15 points more desert than a temperate one; on the basin,
  hot is at least 30 points more desert and dry grass than temperate. Measured 29% and 36% desert
  against 0%, and 85% against 2% on the basin. The mutation restoring the pre-FIX-15 belt (centre 31°,
  width 0.17, peak 0.20) fails all five: hot lab maps 0% desert, the hot basin 10% dry against 12%.
- **Rainfall reaches water, not the ground.** Temperate, on both lab maps and the basin: arid is at most
  5 points more desert than normal, arid has fewer lakes than normal, and rivers grow arid < normal <
  wet. Measured 0% desert at every rainfall; rivers 0.07–0.24%, 0.52–0.97% and 1.41–1.90%. Mutations,
  each failing its own checks: Rainfall shifting every tile's moisture again (−0.35 when arid) makes
  arid 100% and 98% desert on the lab maps and 33% on the basin; Rainfall no longer scaling water leaves
  lakes and rivers unmoved. Wet is deliberately not checked for more lakes: at wet the lake stage
  delivers fewer than at normal (0–2.11% against 5%), a defect FIX-15 found and left open.
- **A whole world keeps an interior.** At span one, land 12 or more cells from water is at least 15
  points drier than land within 3 cells of it. Measured 88–96% against 47–54%. The mutation to a
  60,000 km coastal reach fails it for seed 31415 (18% against 11%).
- **Woods follow their own field.** At most three times chance of woodland edges lie on a
  `TerrainFeatureStage` ranking block boundary, chance being one edge in `BlockTiles` (24, so 4%).
  Checked at normal rainfall on the game's basin (seeds 12345 and 2027) and the standard lab map.
  Measured 3–7% at `StandWavelengthTiles` 16. Mutations: the vegetation field scaled to the landmasses
  again (a hundred cells a wavelength on the basin) puts 16–25% there; a 40-cell wavelength 13–20%.
  This is the check on stands grown too coarse. The count of stands is not: every block ranks its own
  share of woodland, which holds the count up — both coarse fields still made 30–40 stands against 56–60.
- **Woodland is what shipped strategy maps grow.** Civilization VI's forest caps of 14, 18 and 22% of
  land for arid, normal and wet rainfall. On the basin, both seeds: normal 15–21%, arid 11–17%, wet
  19–25%, arid at least 2 points under normal and wet at least 2 over it. Measured 17.3–17.5%,
  13.3–13.6% and 21.2–21.4%. Mutations, each failing: Rainfall not scaling vegetation (17.2–17.5% at
  every rainfall); a reference share of 0.08 (normal 7.3–7.4%); a reference share of 0.26 (normal
  25.2–25.3%, arid 19.6%, wet 30.7–31.0%).
- **Woodland comes in forests.** On the basin at normal rainfall, both seeds: a median stand of at least
  18 cells and at least 70% of the woodland in stands of 25 cells or more. Measured medians of 22–23 and
  79–81%. A 6-cell wavelength gives medians of 12–13 and 37–47%, failing both. Stands also shrink with
  coverage — the 0.08 reference share failed them too — which the share check bounds.

## The generation baseline, and why the fixture moved on 2026-09-16

`tests/terrain_generation_baseline_probe.gd` hashes a generated world layer by layer against
`tests/fixtures/terrain_generation_baseline.json`: eighteen layers — terrain, `inland_terrain`,
relief, elevation, water source, continent, resources, features, start areas, `starts_and_shores`
and the sample-resolution fields among them — over ten cases (three seeds at two sizes, two
start-area cases at radius 8, and FEAT-14's two start-distance cases). It also holds a Huge build's managed allocation under a ceiling
written as a literal in the probe, so re-recording the hashes cannot quietly move it. The fixture
is rewritten only when the probe is run with `-- --record`, which prints as the deliberate act it
is. It is not one of the 45 registered headless probes; it has its own runner:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/terrain_generation_baseline_probe.ps1
```

Its job is to say whether a change touched generated maps, and it was used both ways on 2026-09-16:

- **VIEW-07** folded five copies of the half-sample correction into
  `TerrainEuclideanDistance.ToTiles`. Every hash was **identical** afterwards — that identity is the
  evidence the refactor was behaviour-preserving, and it is why no other guard had to prove it.
- **FIX-14** removed the `TerrainRelief.Flat` gate from lake shores, so a lake is banked whatever
  the land behind it does. That **changes generated maps** — cells that used to be grass are now
  sand — so the fixture was **re-recorded** the same day. Re-recording a baseline is only honest
  when the change was meant to move it: here the intended difference is the lake bank, and
  `terrain_beach_footprint_probe` and `terrain_lake_bank_probe` (which check band widths against
  independent analytic offsets rather than against stored hashes) pass on the new fixture and would
  have caught a band that moved for any other reason.

- **FIX-15** (2026-09-17) measures the coastal moisture reach in kilometres, moves the dry belt to
  15–35° latitude, and sets woodland stands in tiles and woodland cover as a share of land. That
  **changes generated maps**, so the fixture was **re-recorded** after structurally comparing each
  case's layers, old against new. Against the fixture from before FIX-15, these changed in every case:
  terrain (cell, sample and inland), features, resources, underground resource and richness, and starts
  and shores. Underground depth changed in six of the ten cases. Start areas, reports, distance and
  neutral sites changed where the case has them. Continent, elevation, relief, sample water, water
  source, liquid resources and shade did not change in any case: the land, the water and the relief
  did not move. It was recorded in three steps as the design settled: the reach with 6-cell stands, then
  the 16-cell stands and cover (features only, in all ten cases), then the dry belt with Rainfall taken
  off moisture. None of the steps moved the land, the water or the relief. The intended differences are checked independently by
  `terrain_climate_share_probe`.

A re-recorded fixture is a new baseline, not a passing test: hashes taken after a change can only
prove that nothing *else* moves afterwards.

## One-sea checks outside this suite

`tests/terrain_water_material_probe.gd` is not one of the 46 registered headless probes; it has
its own runner and requires the `[terrain-water-material] OK` marker as well as a zero exit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/terrain_water_material_probe.ps1
```

Rewritten for VIEW-04, it authors **one** `TerrainWaterLook` with thirteen deliberately
non-default values and pushes it to all four views — the painted composite included, and the
autotile view on the lab's authored isometric TileSet — then checks that every water material
carries all thirteen, that the four agree, that an empty foam path leaves `use_foam_sheet` false,
that the shared writer still clamps a scripted out-of-range value to the shader's own floor and
ceiling, and that no renderer exports a water dial of its own. A value equal to the shader's
default could not distinguish "the view passed my value" from "the view passed nothing", which is
why none of the thirteen is a default. Seven other probes — `terrain_material_scale`,
`terrain_lava_material`, `terrain_water_surface`, `terrain_bedrock_texture`,
`terrain_live_coast_shape`, `terrain_painted_blend` and `terrain_iso_river` — author a look for
the same reason: they used to set water dials on a renderer, and Godot's `set()` on a missing
property does nothing, so they would have become silent no-ops.

`tests/terrain_water_look_capture.gd` is a capture tool, not a check, and is deliberately
registered in no runner. It renders the lab in all four views plus the tile view before and after
the shared defaults, into `tests/output/water_look/`, and prints each view's sea uniforms:

```powershell
godot --path . --script res://tests/terrain_water_look_capture.gd --quit-after 3600 `
  --rendering-method gl_compatibility --resolution 1280x800
```

The owner reviewed the change live in the lab on 2026-09-16 and approved it; these captures are
the same evidence for anyone revisiting it.
