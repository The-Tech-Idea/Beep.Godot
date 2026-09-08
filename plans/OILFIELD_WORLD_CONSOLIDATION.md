# Oilfield world consolidation

## Status and scope

TerrainWorldComponent now drives both the actual Oilfield Days gameplay terrain and setup preview.
The game-side BasinWorld, TerrainMap, WorldMap and painter pipeline are removed, without aliases.
OilfieldPresentation only adapts oil-game entities and preparation requests to the shared scene.
The broader vehicle-routing/save/performance/appearance migration is not complete. Entries below
record progress chronologically newest first; older pending statements describe their earlier state.

## Gameplay Migration Verified 2026-09-06

- Painted ID/lighting rebuild now snapshots live terrain/elevation once per cell
  rather than repeatedly querying neighbour metadata. Shared shading reads this
  buffer, preserving clamped map edges and neutral water. Both images use managed
  RGBA8 buffers with bulk uploads rather than per-pixel native calls.
- Captured ID/shade hashes before the change for patterned elevations at 144x144
  and 240x240, including stale elevation on water. All four hashes remain identical;
  the profile now asserts them alongside the existing coast hashes. Source/game
  builds and actual game terrain probe pass, with the known game mountain warning.
- Timing was noisy under concurrent compilation, so no reliable speedup percentage
  is claimed for this step. A completed-build run measured 83.265/216.528 ms.
  This is still full-map CPU work and upload; dirty-region updates remain open.

- Added a bounded TerrainCoastField.LiveCache owned by each painted renderer.
  Reuse compares the exact current water mask, dimensions, detail and range;
  it does not trust event delivery or share mutable textures between worlds.
  Dry terrain/elevation/metadata edits can reuse ocean connectivity and distance
  pixels; floods, drains and changed source contents invalidate them.
- Painted-origin coverage now checks texture identity on reuse and invalidation
  for flooding, draining, source rebinding, detail and range changes. Warmed
  repaint medians measured 81.306/227.574 ms at 144/240 square cells, down from
  116.197/321.468 after bulk upload alone. Original coast hashes remain identical.
  This comparison excludes water-mask-changing edits and generation.
- Both addon copies match. Game terrain regression and builds pass (known game
  mountain warning remains). IDs and shade still rebuild; this is not yet an
  edit-local renderer or a solution to all large-map performance problems.

- Profiled warmed painted Rebuild calls independently of generation on 144x144
  and 240x240 live grids, CoastDetail=4. Replaced per-pixel native SetPixel calls
  in TerrainCoastField with managed RGBA8 encoding and one CreateFromData upload.
  Original/new coast hashes match byte-for-byte at both dimensions.
- Local five-run median repaint times: 146.296 -> 116.197 ms (144),
  391.672 -> 321.468 ms (240). These are headless CPU timings for the fixed test
  fixture, not general frame-time guarantees. The remaining cost is still too
  high for frequent edits; audit coast reuse/invalidation and other image work.
- Both addon copies updated. Added terrain_repaint_profile to the integration
  gate with exact encoding checks but no machine-specific speed threshold.
  Full suite: 29 passed, zero skipped. Game oilfield-terrain probe passes;
  builds pass with the known game-only mountain warning.

- TerrainShaderSurface.Fill no longer clears/refills the complete layer during
  unchanged shader refreshes. It reconciles actual tile state, leaving correct
  cells alone, repairing holes/incorrect cells and removing out-of-bounds cells.
  The single-quadrant shader coordinate contract is retained. Both addon copies
  match by SHA-256.
- Painted-origin regression verifies zero geometry Changed events on an unchanged
  repaint, hole repair, negative-cell removal and shrink/grow behavior. All 28
  terrain checks pass, including the four GPU checks. Actual game oilfield-terrain
  probe passes; source builds clean and game retains the known mountain warning.
- This removes geometry churn, not all rebuild work: Fill still scans cells, and
  coast/image uploads remain full-map. No wall-clock speedup claim was measured.
  Large-map profiling and remaining save/view correctness work stay open.

- Live painted hill lighting now derives from current cell elevation gradients,
  reusing TerrainShadingStage's light direction and bounded shading formula. It
  does not restore obsolete generated terrain_shade after leveling. Water is
  neutral and contributes zero elevation even if its previous land metadata remains.
  Standalone generator views retain their detailed generated shade field.
- Both addon copies updated after verifying they matched. Extended the painted
  origin probe with shifted-coordinate lighting, stale shade rejection, opposite
  slope lighting and automatic flood repaint. All 28 integration checks pass;
  actual Gameplay OpenGL startup/capture also passes. Source build clean; game
  retains its existing mountain TextureFilter warning.
- This closes the neutral-live-lighting defect below, not the entire terrain
  review. Cell-resolution lighting differs from generated sub-cell shading.
  Whole-map climate appearance, paint/grid coastline alignment, large-map rebuild
  cost, game save fidelity and remaining route recovery still require work.

- Fixed missing SnowTexturePath in the shared game Terrain scene. Its unbound
  shader sampler rendered snow/ice as solid white. Bound the existing addon
  snow_ice.png asset and enabled mipmaps in the game's import settings; the source
  addon already had mipmaps enabled, so no duplicate asset or source change needed.
- Actual scene terrain_materials_probe passes in OpenGL: all ten sampler textures
  bound, expected snow resource and mipmaps present, snow-region pixel variation
  above 0.08. Inspected the captured texture and reran full Gameplay OpenGL startup
  successfully. This verifies material rendering, not whether each snow location
  is climatically appropriate. No generation rules changed in this fix.
- Headless import completed but emitted a GodotTools.HotReloadAssemblyWatcher timer
  error at editor shutdown; runtime probes were clean. This editor-tool issue is
  separate from the missing terrain texture.
- New code finding: BuildIdMap ignores terrain_shade metadata for live cells and
  supplies neutral lighting, although LoadGeneratedCells stores the shade. Audit
  how edits invalidate/recompute lighting before restoring this data connection;
  merely reading stale shade after leveling could introduce incorrect shadows.

- Regional climate scale corrected: the game has six tiles per kilometre, but
  automatic climate rules interpreted 240 tiles as a planet. Added the explicit
  UseCustomClimateSpan/ClimateLatitudeSpan recipe override (version 3), retaining
  automatic biome-region sizing and the existing climate/elevation/moisture model.
  Game recipe uses kilometres/10000; preview and gameplay share this recipe.
- Both addon copies updated. Exact-recipe test covers forwarding, save/restore,
  live-cell preservation and returning to automatic scale. All 28 integration
  checks pass; actual Gameplay OpenGL startup/capture passes with regional settings.
  Source build clean; game has the existing mountain TextureFilter warning.
- Visual review is NOT complete: the gameplay capture frames mostly the prepared
  base, still shows white terrain behind the buildings, and cannot establish whole
  map climate quality. Altitude cooling, missing snow art, initial camera framing,
  region-wide biome distributions and generation cost remain to inspect.

- Fixed direct GridPathFollowerComponent movement discarding distance after one
  waypoint per update. Node2D workers/trucks now consume the distance budget across
  corners, validating every edge and respecting cancellation/replacement callbacks.
  Arrival updates grid identity in the same step; exact-budget floating-point arrival
  has a small positional tolerance. Both addon copies match by SHA-256.
- Extended terrain_follower_live_probe to compare one long update with 100 short
  updates, verify corner traversal and immediate arrival, and flood the next edge
  from a waypoint callback. The probe and actual unit_grid_travel_probe pass.
  Source builds cleanly; game builds with its existing mountain TextureFilter warning.
- CharacterBody2D still uses its separate MoveAndSlide physics branch. Do not claim
  simulation-time parity for that branch. Remaining routing work includes blocked
  route recovery; remaining terrain work includes regional appearance/performance
  and game-specific save identity/validation noted below.

- Actual terrain save integration wired: EngineHost.Save captures the live gameplay
  snapshot, SaveSlots writes a Base64 Godot Variant payload in the JSON sidecar, and
  Load/Gameplay restore it after recipe build but before units are restored or resumed.
  Gameplay teardown clears the save callback. NewGame clears restored terrain AND yard
  state (yard was previously retained). Saves without available gameplay terrain refuse
  rather than silently writing seed-only state. Old sidecars without terrain are not loaded.
- Save slot naming now considers both JSON and engine payload files, avoiding overwrite
  of an orphan payload after a failed sidecar write.
- New TerrainSaveProbe exercises real EngineHost Save/Load and actual Gameplay scenes,
  not only dictionary restoration: live mud, metadata, road, navigation block, preparation
  deduplication, callback teardown and new-game reset all pass. It creates an unused slot
  and removes only that test-created slot. Uses the simulation's valid default 24-cell
  world (a 4-cell test was rejected by its template). Game build succeeds with existing
  mountain warning. The engine still advances one month on load by its existing contract.
- Still incomplete: persist well-site/drilling presentation identity, comprehensive
  malformed sidecar validation before changing the active run, transactional file writes,
  and avoid generating/repainting a complete fresh terrain before restoring live cells.
  Terrain state is now connected to the real save button; do not claim complete game save
  fidelity until those remaining identity/lifecycle checks are handled.

- Added an authored State node using existing GridWorldStateComponent in Terrain.tscn,
  with explicit local Cells/Roads/Navigation paths and unrelated captures disabled.
  It does not auto-register with addon saves; the game's host remains the save owner.
- OilfieldPresentation.CaptureTerrainState/RestoreTerrainState wrap that shared grid
  snapshot with seed/dimensions/version and applied preparation/clearance/pad identities.
  Restore checks identity and required grid sections, stages preparation records before
  mutation, and redraws without re-preparing sites. It is not a duplicate grid serializer.
- Game build passes with existing mountain warning. Extended oilfield_terrain_probe passes
  binary Variant serialization into an independent world: all live cells/metadata equal,
  road and explicit navigation block restored, repeated preparation preserves saved mud.
- This is a tested snapshot boundary, NOT completed Save/Load integration. Next wire the
  host sidecar/EngineHost/Gameplay lifecycle, persist well-site/drilling presentation
  identities, validate file input thoroughly, and test an actual slot round trip. Current
  Save button still saves only the old draft/yard sidecar until that wiring is complete.

- Corrected the ServiceTruck assessment after reading its actual caller: Gameplay
  unconditionally disabled ControlsEnabled and never referenced the truck again. It
  was a parked leftover from an abandoned player-driving mode, not an active driving
  feature. Deleted ServiceTruck.cs/UID, its scene node/resource and Gameplay field/setup.
  No compatibility alias or replacement controller added. Kept directional vehicle
  assets used by RoadTruck and DriveVector used by CameraRig.
- Verification: game builds with existing mountain warning; full gameplay probe passes
  absence of retired files/node, nonempty dispatched fleet and a Travel follower on
  every fleet unit, plus existing startup/teardown checks. Unit/ambient route probe
  passes. Active game/scene/data search finds no ServiceTruck or retired UID references.
- Supersedes the earlier player-truck collision work item: there is no supported manual
  driving mode to wire. Shared native collision remains available in the addon; do not
  add a new player mode or thousands of colliders merely to preserve removed dead code.
  Remaining goal work includes actual-game terrain saves, regional appearance/performance,
  facility reconciliation and route recovery rather than another movement implementation.

- Ambient RoadTruck migrated to the existing shared GridPathFollowerComponent with
  explicit Grid/Navigation paths supplied by OilfieldPresentation. Removed its L-shaped
  interpolation, straight-distance progress calculation and independent real-time pace.
  It now obeys simulation pause/speed and shuttles on validated routes. Unchanged
  snapshots do not restart the route; failed travel remains stopped rather than jumping
  across water. Stopping activity cancels the route and hides this cosmetic truck.
- Drive now takes a concrete Vector2 destination: its previous nullable Vector2 signature
  was not callable through Godot. OilfieldPresentation explicitly handles the no-site case.
- Game build passes with existing mountain warning. Extended unit_grid_travel_probe passes
  ambient detours, pause, repeated-snapshot continuity, two completed legs, flooded-route
  stop and cancellation. Full headless gameplay_terrain_probe passes.
- ServiceTruck is active, not legacy: Gameplay.tscn instantiates the player-controlled
  CharacterBody2D. Its independent MoveAndSlide/map clamp still lacks terrain collision
  integration. That requires shared collision boundaries, not an AI path follower.
  Automatic ambient route recovery after terrain repairs is not implemented yet.

- Dispatched Unit travel now uses the existing shared GridPathFollowerComponent.
  Gameplay supplies authored Grid/Navigation to Dispatcher.Raise and Station; no
  fallback navigation or direct-vector outbound/return travel remains in Unit.
  Simulation pacing manually advances the follower with its own physics processing
  disabled, so pause cannot be bypassed. Send rejects unreachable sites before marking
  a unit busy. Live route failure cancels the unsubmitted job, enters Stranded, and
  reports failure; CommandBar offers recall after access is restored. Arrival checks
  destination blocking and is emitted from follower completion, not proximity alone.
- Restore cancels installed routes/old pending job identity; returning units install
  a fresh route. Preparation completion emits only once even without a state-changing
  subscriber. These changes do not add persistence for live terrain state.
- Verification: actual game builds with existing mountain warning only. New
  unit_grid_travel_probe passes water-goal rejection, detours, pause including a real
  physics frame, water avoidance, single arrival, return, flooded-route failure without
  arrival, stranded recall, single preparation and cancelled restore. Full headless
  gameplay_terrain_probe passes startup/recipe agreement/teardown.
- Remaining travel: cosmetic RoadTruck and ServiceTruck still have independent movement;
  route replanning/recovery from a flooded current cell, dispatch command outcomes in
  the full UI, and road costs versus animation pacing need further integration review.

- Isometric water alignment: GPU probe now extracts both the production vertex function
  and water projection inversion. Native diamond cells exposed an X-minus-one sampling
  error. Both water projections require restoration of the batch's first-cell-center
  offset; fixed source/game iso_water shaders accordingly. Checks pass for square cells,
  64x64 diamonds and 64x32 diamonds, with samples on both sides of their centers.
- Full integration revalidated: current source build clean, 28 tests passed, zero skips,
  including GPU alignment, water alpha, lab capture and playground capture. The prior
  UI-kit compile failure is resolved in the current worktree (no UI edits in this turn).
  This verifies this suite, not final art quality or full external-game routing/save work.

- Separate top-down water alignment: extended the GPU cell-position probe to extract
  iso_water's production vertex function too. It reproduced the same half-cell error
  in flat_projection mode. Source and game shader copies now restore the square-batch
  offset only for that mode. GPU alignment and water-alpha/mask probes pass.
  Isometric coordinate inversion is unchanged and still needs equivalent GPU coverage.
- Source build rechecked: three current UI-kit errors remain (KitKnob/KitLevelPath
  reference missing KitChrome.DirectionFromKey/IsConfirmKey). No terrain compiler errors
  reported, but full-suite verification remains unavailable until this build succeeds.

- Painted shoreline alignment fixed in both addon/game terrain_splat.gdshader copies:
  square TileMapLayer batch VERTEX is relative to the first cell center. The shader
  now adds half cell_size before sampling IDs, shade and coast fields. Previously
  visual material/coast boundaries lagged native cell geometry by half a cell.
- New GPU terrain_shader_alignment_probe reproduces the old mismatch and passes with
  the correction. It extracts the production vertex function and samples both halves
  of all 16 native cells in a translated 4x4 surface, checking GPU-encoded cell IDs.
  Registered it in the full runner's rendering checks. Actual-game OpenGL terrain
  probe passes; inspected oilfield_features.png confirms tree foot on its grass patch.
- Full integration rerun did NOT pass: stopped at source build with unrelated current
  UI-kit errors (missing KitChrome keyboard helpers and KitRadarChart.AdjustByArrow).
  No UI-kit files changed for this terrain fix. The standalone GPU test used existing
  C# assemblies and the current shader; do not treat it as a successful full build.

- Feature renderer migration: removed OilfieldPresentation's Scenery node, tree loop,
  per-cell texture lookup and private placement hash. Authored Terrain.tscn now includes
  TerrainFeatureRendererComponent, connected to World.FeaturesPath, live Cells and Grid.
  Both preview and gameplay therefore use the same batched feature rendering path.
- Source/game addon copies both add SpriteAnchor and OasisScaleMultiplier, with existing
  default behavior unchanged. Game config uses bottom anchoring, one sprite per cell,
  2.4 global scale and 0.375 oasis multiplier with single-frame tree/scrub assets.
- Checks: source build clean; game build succeeds with existing mountain warning;
  source terrain_feature_grid_probe passes (intentional no-sheet warning case);
  game oilfield_terrain_probe passes headless and OpenGL, testing no duplicate Scenery,
  no per-tree children, live water/lava suppression, anchor and scrub scale, and automatic
  vegetation removal after flooding. Full OpenGL gameplay_terrain_probe passes too.
- Inspected tests/output/oilfield_features.png and gameplay_terrain.png. Tree transparency
  renders correctly, but shoreline foot placement looks offset relative to the painted
  grass cell. Investigate grid/surface cell sampling and jitter before accepting alignment.
  Regional snow/material appearance remains unresolved. Renderer still rebuilds its stamp
  list on live changes; this migration removes scene-node churn, not all rebuild cost.

- Command/view separation follow-up: removed PaintGround's replay of all known sites
  and access roads from RefreshTerrain. Build prepares the initial base explicitly;
  PrepareSite surfaces its footprint and attempts validated access immediately;
  newly resolved wells/dry holes and changed plant plots apply their own preparation.
  Repeated smaller jobs do not shrink existing prepared footprints. RefreshTerrain
  now redraws the world/scenery only, without issuing terrain or road writes.
- Verification: game builds (existing mountain warning only), oilfield_terrain_probe
  passes immediate surfacing, connected access, removed-road preservation, and exact
  serialized cell/road equality across repeated refreshes. Full headless
  gameplay_terrain_probe also passes startup, host/terrain recipe agreement and teardown.
  Initial-base cells are intentionally prepared during Build; recipe equality is
  checked outside that footprint, not against unmodified base cells.
- Remaining: scenery and facility reconciliation, routing dispatched vehicles,
  actual-game save/load of preparation/cells/roads, regional appearance/performance.
  Existing roads invalidated by terrain edits and explicit road-repair interactions
  still need review; view refresh no longer silently constructs replacement routes.

- Follow-up: site refresh previously flattened and resurfaced every known footprint,
  overwriting live cell terrain/relief edits. OilfieldPresentation now records applied
  clearance/pad operations per world build and applies only new footprints. Expanded
  clearances preserve existing gravel. These records are operation identities, not a
  second terrain store. Rebuild resets them; actual-game save persistence remains pending.
- Game build passes with the existing mountain TextureFilter warning. Extended
  oilfield_terrain_probe passes in Godot: edited base water/relief survive refresh,
  edited prepared-site material survives repeated preparation, and a fresh world
  prepares its base again. Also asserts obsolete source/scene/UID files remain absent.
- Remaining refresh work: separate site commands entirely from view refresh, stop
  retrying road construction on every redraw, and reconcile scenery incrementally.
  This change does not establish routed vehicle travel or saved preparation history.

- Deleted BasinWorld, TerrainMap, PainterlyTerrainLayer and the game's addon PainterlyTerrainComponent
  (plus UID files), and removed the separate WorkedGround scene. Retained domain presentation as
  OilfieldPresentation, with no independent terrain arrays or terrain generation implementation.
- Gameplay and preview both use authored scenes/world/Terrain.tscn. OilfieldTerrainRecipe supplies
  their shared size, land, folded seed and climate mapping. NewGameDraft/SaveSlots carry an explicit
  TerrainClimate choice independently of simulation severity. Full save round-trip remains untested.
- Site preparation edits shared GridCellDataComponent; requested job pad dimensions survive redraw.
  Access roads use GridNavigationComponent paths plus GridRoadComponent build validation instead
  of painting ground over water. Roads refuse disconnected shores; dispatcher vehicles still need
  migration to the same navigation/follower contract. No claim of correct routed travel yet.
- Repeat Build clears only owned presentation layers/identity collections, reuses authored terrain
  cells, regenerates the new recipe and clears roads. Game scenery now reads generated feature
  metadata instead of deciding which grass cells contain trees independently.
- Both minimaps read shared cell kinds through one palette helper. Updated the last old-painter
  reference in the game addon grid-world template. Active-code/scene search finds no removed names.
- Fixed direct gameplay startup order: load/create EngineHost run before building terrain, then
  subscribe/bind presentation. Teardown disconnects host/simulation callbacks and clears PackYard.
- Verification: game build succeeds (one pre-existing mountain TextureFilter hiding warning).
  oilfield_terrain_probe passes: shared recipe equality, pad data, water barrier retained, road
  rejection, repeat-build node count and authored cell owner reuse. Full Gameplay headless launch
  completes without errors. gameplay_terrain_probe passes with real OpenGL: host seed/dimension
  agreement, real surface node, screenshot and snapshot-after-teardown without callbacks to freed UI.
  Visually inspected tests/output/gameplay_terrain.png. It shows rendered terrain and yard, but
  regional snow/dryness, texture appearance and startup time are not accepted as finished.

Remaining: route dispatcher/service traffic through grid navigation; preserve live terrain and
preparation/roads through actual game saves; incremental facility reconciliation; regional climate
scale and generation performance; detailed rendered-game interaction checks and template runtime audit.

## External Cleanup Verified 2026-09-05

- Setup preview now instantiates Terrain.tscn at design time in NewGame.tscn. WorldPreview only
  configures TerrainWorldComponent and frames the viewport. No BasinWorld, TerrainMap, old painter,
  yard, traffic, or separate generated arrays in the preview. Reuses its authored instance and
  skips generation for unchanged Bind inputs; pre-ready Bind is applied once on Ready.
- Setup statistics now count GridCellDataComponent kinds and actual BuiltSize. The land control
  explicitly caps at 92%, with the Desert profile also 92%; footprint wording distinguishes it
  from measured dry area after lakes/rivers. Legend replaces obsolete Yard with Snow/ice.
- Named climate choices map to temperature/rainfall: Temperate=temperate/normal,
  Coastal=temperate/wet, Arid and Desert=hot/arid, Sub-arctic=cold/normal. Simulation severity
  remains separate. High seed bits are folded into the terrain engine's signed integer seed.
  Gameplay has NOT adopted this recipe yet; preview and gameplay can differ during migration.
- New world_preview_probe passes, exercising pre-ready bind, exact size, both seed halves,
  instance reuse, unchanged bind, hot/arid and cold profiles. Full NewGame.tscn runs headless
  and OpenGL without errors. Captured tests/output/new_game_terrain.png and visually inspected.
  Build has no errors, existing mountain-prefab warning remains.
- Full setup startup measured 7.2-7.7 seconds at 144x144: performance remains inadequate.
  Capture shows temperate regional terrain is overly dry with excessive snow; current scale rules
  interpret large logical bounds as a broad climate span, not a 24km regional area. Profile that
  pipeline and resolve physical-region climate scale before claiming acceptable map quality.

- Both TerrainWorldComponent copies now support exact positive CustomBounds and explicit
  LandCoverage overrides (0.05..0.92 land footprint before inland waters), persisted in recipe v2.
  They are opt-in and validated before generation. Tests cover 24x36, 70% footprint, save/restore,
  preserved live edits, rejected invalid values and return to presets. Game shared-scene probe
  now exercises the non-preset size too. Full integration: 27 pass, zero skips. Game builds with
  its existing mountain-prefab warning; shared-scene headless probe passes.
- Remaining setup mismatch found in NewGameSetup: land slider permits 0.95, exceeding generator
  maximum 0.92. Resolve the exposed range/meaning explicitly before wiring preview/gameplay.
  Climate severity mapping and actual caller replacement remain pending; no claim that the new
  scene has replaced BasinWorld yet.

- Synchronized terrain/grid modules, including their grid UI subfolder, required EntityComponent,
  GameStateManagerComponent and GameStateData dependencies, and terrain shaders into the game.
  Moved old grid paths to their current module paths and rewrote explicit resource references.
  Tools/sync-game-terrain.ps1 preserves replaced files outside the project under
  C:/Users/f_ald/.codex/migration-backups. No whole UI-kit copy was performed.
- Game-only dependency migration: GameApp owns a Saves child; save callers now use that owner,
  not the removed GameStateManagerComponent.Instance. No compatibility alias added. The actual
  Oilfield game uses EngineHost rather than GameApp; full game save integration remains pending.
- Added scenes/world/Terrain.tscn in the actual game: TerrainWorldComponent, generator, cells,
  painted TileMapLayer view, native projection and navigation with explicit paths. Uses existing
  game texture assets. Does not instantiate BasinWorld or PainterlyTerrainLayer. It is not yet
  connected to Gameplay/WorldPreview: their kilometre, land-fraction and climate inputs must be
  mapped explicitly into the world recipe without silently rounding or ignoring player values.
- Added tests/shared_terrain_probe.gd in game. Headless and real OpenGL runs pass: deterministic
  same-seed generation, isolated instance state, live water blocking, renderer reads live edits,
  redraw does not regenerate, grid coordinate round trip. Existing basin smoke still passes.
  Build succeeds with one existing source-module TextureFilter member-hiding warning (mountain
  prefab), no errors. These tests do not establish full game runtime or save/load correctness.

- Deleted WorldMap.cs, its UID and TerrainKind enum. Road/pad materials now use the existing
  GridCellDataComponent, instantiated from scenes/world/WorkedGround.tscn. No map wrapper remains.
  The old painter reads those grid records directly; natural TerrainMap generation is still pending
  replacement, so this does NOT establish one authoritative store for all terrain yet.
- Added FillTerrain to the source and game addon cell component. It paints a clipped-by-caller
  rectangle, preserves flags/crops/metadata and emits one bulk notification only when changed.
  Added game-addon bulk clear notification without copying unrelated addon changes.
- Access paint clips to map bounds and covers both endpoints, including coincident endpoints.
  It remains a host-selected L-shaped access route, NOT navigation-validated pathfinding; migration
  to GridRoadComponent/GridNavigationComponent is still required before it controls truck movement.
- Actual-game smoke now verifies yard/prepared pads, access road records, bounds and idempotent
  repaint. Both projects build cleanly; this smoke and the new grid fill probe pass in Godot 4.7.
  Full terrain integration runner also passes all 26 checks, including three OpenGL runs, with
  zero skips. This does not verify full external gameplay migration or navigation of access roads.

- Extracted domain scale and flat-world coordinate conversion into OilfieldCoordinates, which
  owns no terrain or generation. Removed BasinWorld's public scale/ToWorld API rather than keeping
  forwarding aliases. Updated all static callers; reference search finds none using the removed API.
- Minimap and placement-map marker conversion now scales both axes independently; the placement
  grid also uses panel height for its horizontal lines. Added an actual-game C# coordinate probe
  covering domain scale, negative-cell flooring, cell-centre round trips and rectangular maps.
  Game build: zero warnings/errors. Godot 4.7 coordinate and basin terrain probes both pass,
  with explicit success markers and no engine/script errors. These are not full gameplay tests.
- Fully read the coordinate callers BlockOverlay, PlantYard, RoadTruck, ServiceTruck,
  StandingOrders, Dispatcher, Gameplay, Minimap and LeaseMap. NewGameSetup/SidePanels full
  caller migration is still pending. This extraction does not remove BasinWorld's active ownership.

- User explicitly requires removal of legacy worlds, not a BasinWorld compatibility wrapper.
  Removed unused DualGridTerrain/EdgeMaskTerrain classes and their UID files after checking all
  game scripts/scenes/resources for references. Game build and basin smoke remain clean.
- Addon C# inventory: current 510 sources; game 415. Same-path comparison finds 120 exact matches,
  240 changed files and 150 current paths absent from game; 48 absent paths have same filenames
  elsewhere in the game addon. This is inventory, not a semantic equivalence audit.
- Game-only C# filenames: BeepTextureBaker, TurnManager, PainterlyTerrainComponent, UISkin,
  KitArt, KitGrain, KitGrainTable. Their callers must be migrated before removing active dependencies.
  Ordinary recursive copying would retain relocated old classes and cause duplicate compilation.
- BasinWorld remains active, not removed or renamed. Its caller migration is still required.

- Re-read BasinWorld, WorldPreview and PainterlyTerrainLayer in the actual game checkout. The game
  builds cleanly before and after this change (zero warnings/errors, --no-restore).
- Removed BasinWorld's seven always-hidden diagnostic terrain layers, their atlas allocation
  helpers and all seven repaint calls. They were not inputs to the visible surface; the painter
  reads WorldMap/TerrainMap directly. No compatibility layer was added.
- Added game-project tests/basin_terrain_smoke.gd. Actual Godot headless execution passes with
  no engine/script errors: one populated terrain sprite, no removed diagnostic layers recreated.
- Preview still constructs yard/traffic and recreates the full world on every Bind. Both gameplay
  and preview still use old terrain ownership. Addon synchronization, authored shared world scene,
  live grid migration and full caller review remain pending. No broad addon-copy overwrite performed.

Reviewed world-role classes: the seven current world-named addon classes (both terrain controller
partials), TerrainGenerationBuffer, and the game's BasinWorld and WorldMap. Caller search includes
Gameplay, WorldPreview, Minimap, SidePanels, LeaseMap, BlockOverlay, Dispatcher, StandingOrders,
PlantYard, RoadTruck and ServiceTruck. Those callers need full review during migration; a reference
search is not a full audit of them or of every addon component.

## Confirmed findings

- BasinWorld.Build still constructs TerrainMap plus WorldMap and the old painter. Its seven hidden
  atlas layers and repaint passes have now been removed; replacement with the current terrain
  engine remains outstanding.
- BasinWorld.Build appends children without resetting the previous build's layers or site collections.
  Calling it twice on the same instance can retain old scene nodes and gameplay presentation state.
- PrepareSite levels a job-specific rectangle, but PaintGround clears all leveling and recreates
  prepared sites using ProspectPadTiles. The requested preparation footprint is lost on repaint.
- SyncChain queues every plant child for deletion and recreates the complete chain on each Bind,
  including unchanged structures. Stable simulation identity should drive incremental updates.
- WorldMap.IsDrivable accepts every in-bounds cell; it does not consult water, relief or occupancy.
  It must not become a second movement rule beside GridNavigationComponent.

## Completed addon cleanup

- TerrainWorld was renamed TerrainGenerationBuffer to distinguish scratch arrays from a live world.
- World helpers and grid snapshots re-resolve configured ownership paths; snapshot discovery is scoped.
- Duplicate painted/overlay Node2D references removed; their typed renderers own visibility directly.
- Flat view grid-path binding consolidated instead of three copies.
- Explicit generation before deferred startup no longer generates twice. BuiltSize updates after draw.

Verification: dotnet build reports zero warnings/errors. Godot headless ownership, recipe,
live-source and view-grid probes pass. The ownership probe covers both deferred suppression and
later explicit regeneration. These checks do not validate the external game's migration.

## Implementation sequence

1. Inventory the game checkout's addon differences before synchronizing. Port dependencies to the
   current addon contracts rather than adding compatibility shims. Compile the game before and after.
2. Author one reusable terrain scene with TerrainWorldComponent, generator, live cells, renderers,
   grid and navigation. Gameplay and setup preview instantiate that same scene implementation.
   Keep viewport/camera presentation separate; no second terrain-generation algorithm for previews.
3. Replace BasinWorld's terrain ownership with references to this scene. Retain only the oil-game
   read-model adapter, stable entity-to-node mapping and domain coordinate conversion in the host.
   Game-specific OGSim types must not enter the reusable addon.
4. Migrate roads, pads and preparation footprints into the shared cell/road state. Track the actual
   preparation rectangle per site. Navigation, minimap and terrain views must read that state.
5. Reconcile wells and facilities incrementally by stable entity IDs. Remove vanished entities,
   update changed properties, and preserve unchanged nodes. Define reset behavior for a new game.
6. Migrate Gameplay, WorldPreview, minimaps and dispatch callers; replace duplicated pixel/grid
   conversion with GridProjectionComponent plus one domain metres-to-cell conversion.
7. Remove obsolete TerrainMap, WorldMap and PainterlyTerrainLayer only after all consumers have
   been ported. Remove hidden diagnostic atlas creation and its repaint passes, not just visibility.
8. Run the actual gameplay and preview scenes, not only the editor. Verify generation count, startup
   timing, map agreement, shoreline movement rejection, preparation footprints, dispatch arrival,
   save/load, repeated new games, and preview teardown without retained nodes.

## Acceptance

- One terrain implementation shared by gameplay and preview; separate instances are allowed.
- One authoritative live cell store per world; no independent game-side terrain material array.
- One movement rule shared by pathfinding, dispatch and interaction.
- No references to the removed painter pipeline in active game code or scenes.
- Authored scene composition; no runtime construction of HUD controls.
- No claim of zero duplication until the game migration and its runtime checks are complete.
