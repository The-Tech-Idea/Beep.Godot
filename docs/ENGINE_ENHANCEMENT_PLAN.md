# Terrain + Grid + Resources — Enhancement and Fix Plan

This plan covers **all 108 files** under `addons/beep_game_builder_cs/ecs/terrain/` — the terrain engine (`Terrain*`), the gameplay grid system (`Grid*`), and the shared resource system — plus `ecs/ui/ResourceBadgeComponent.cs`. It is the result of a complete, file-by-file read of every one of those files on 2026-09-02, with every finding below verified against the actual source before inclusion.

It is the successor to `docs/terrain-engine/ENHANCEMENT_AND_FIX_PLAN.md`, which covered only the `Terrain*` half and is closed (all its findings fixed). That plan explicitly declared the `Grid*` half out of scope — "a separate system … would need reviewing elsewhere". This is that review, plus the cross-system findings only a whole-directory read could see.

## Status

**Phase 1 (correctness) is executed and verified** — every item below marked `Fixed` changed in this session, with `dotnet build` clean (0 warnings) and the full `addon_contract_scan.ps1` passing afterward. Phases 2–5 are specified and approved but not yet executed.

**Decisions already made** (so a future session does not re-litigate them):
- The directory **will be physically split** (Phase 4): `Grid*` → `ecs/grid/`, panels → `ecs/grid/ui/`; `Terrain*`/`Resource*` stay in `ecs/terrain/`.
- The dead crop/economy settings **will be wired up, not deleted** (Phase 5): crop regrowth, seed consumption on planting. The `shallow_water` build-blocking half was already done in Phase 1.

## Why there are 108 files in one directory

Not copy-paste bloat — every file is a distinct role — but two systems plus a bridge co-located: 49 `Terrain*` engine files, 51 `Grid*` gameplay files, 3 `Resource*` catalog files, 2 `Mountain*` + 1 `TextureElevation*` authoring tools, and 2 field/scatter helpers. All created together in commit `0763c30` (the folder held exactly one file before it); the `d4f3208` rename split them by *name* only. Consequences the split (Phase 4) addresses: the `Grid*` half had never been audited, ~14 `Control`-based panels sit outside `ecs/ui/`, and only 4 of 51 `Grid*` classes use the addon's component category bases.

---

## Phase 1 — Correctness. **Status: Fixed (this session).**

### 1.1 Shipped template scene referenced two deleted script files
`templates/scenes/grid_world_2d_iso.tscn` still pointed its `ext_resource` scripts at `GridSplatTerrainRendererComponent.cs` and `GridTerrainGeneratorComponent.cs` — files deleted in the `d4f3208` rename (which fixed the two demo scenes but missed the templates). The reference grid template could not load its terrain base or generator.
**Fixed:** both paths updated to `TerrainPaintedRendererComponent.cs` / `TerrainGeneratorComponent.cs`; node properties verified to match the current exports (`TerrainGeneratorPath`, `CellDataPath`, …). A repo-wide sweep for all 12 renamed class names plus the two deleted painterly classes found no other stale scene reference.

### 1.2 The whole static guard suite was dead
`tests/addon_contract_scan.ps1` failed on clean HEAD — `2D_ISO_TOOLKIT.md is missing TerrainDataLayersComponent` — and because the scan throws on first failure, **every check after line ~2121 had been silently unreachable**. The doc still described the pre-rename world (including the deleted `PainterlyTerrainComponent`/`GridPainterlyTerrainBridgeComponent`) while the scan demanded the new names.
**Fixed:** `docs/2D_ISO_TOOLKIT.md` rewritten as the accurate grid-system developer guide (all 50 scan-required class names, architecture diagrams, current template layout) and `docs/2d-iso-toolkit.html` refreshed to match. Scan passes end-to-end again.

### 1.3 TerrainTileRendererComponent destroyed all children — then resurrected dying layers
`EnsureLayers()` freed **every** child on a configuration change — taking the shader sea layer (`TileWater`) and any user-authored nodes with the biome layers. Worse, `QueueFree` leaves nodes in the tree until frame end, so `TerrainAuthoring.EnsureLayer` found the dying layers by name, handed them back, and the freshly configured layers died at end of frame — one blank/broken build after every atlas change, self-healing on the next rebuild.
**Fixed:** teardown now targets only `TileMapLayer` children whose names end in `Tiles`, and does `RemoveChild` before `QueueFree` (the `MountainPrefabGeneratorComponent.ClearGeneratedParts` precedent) so dying nodes can never be found and reused.

### 1.4 Isometric water layer double-parented
`TerrainIsometricRendererComponent.EnsureWaterSurface` called `AddChild(_water)` on a layer `TerrainAuthoring.EnsureLayer` had already created, parented, and adopted — firing Godot's "already has a parent" error on every first build.
**Fixed:** the redundant `AddChild`/`Adopt` removed; `EnsureLayer` is the single acquisition path.

### 1.5 Bulk cell loads emitted ten thousand per-cell signals
`GridCellDataComponent.LoadCells` emitted `CellChanged` per record. `GridTileMapLayerBridgeComponent` answers each with `RefreshCell` → `TileMapLayer.UpdateInternals()` — the exact per-cell-internal-update cost its own `Rebuild` comment warns against — so loading a generated 128×80 map ran ~10,240 internals updates and then the full `CellsChanged` rebuild repainted the same map again.
**Fixed:** `LoadCells` now emits one `CellsChanged` for the whole batch (doc comment states the contract); per-cell granularity remains on the editing API (`Till`, `Water`, `SetFlags`, …), which is where single edits happen.

### 1.6 Map overlay warned every frame instead of once
`TerrainMapOverlayComponent._Draw` pushed the "no generator" warning — and `_Draw` runs on every canvas redraw (window resize, any sibling invalidating the frame) — while `Rebuild`, the correct reporting point, silently returned.
**Fixed:** warning moved into `Rebuild`; `_Draw` just returns.

### 1.7 Editor-generated props vanished on reload
`SeededTerrainPropScatterComponent` supports `GenerateInEditor` but never adopted its stamps — the exact silent "map vanishes on reload" failure `TerrainAuthoring` exists to prevent. Cleared stamps also stayed in-tree until frame end, so same-named replacements got auto-renamed (`@GeneratedTerrainStamp_000@2`).
**Fixed:** stamps are adopted after `AddChild`; `RemoveGeneratedStamps` removes before queueing free.

### 1.8 Scatter exports silently overwritten by the catalog one frame later
`GridResourceScatterComponent.CreateResourceNode` wrote its randomized `Min/MaxAmount` and gather exports onto each node — then the node's `_Ready → ApplyCatalogDefinition` overwrote all of them from the catalog whenever the id was defined there. Accepted, stored, silently discarded: the exact pattern the previous plan hunted.
**Fixed:** single-owner contract made explicit — the scatter writes amount/gather rules only for ids the catalog does **not** define; catalog-defined ids take everything from their definition. Also hardened the depletion callback to check `IsInstanceValid` on the captured placement component.

### 1.9 TerrainWorldComponent held stale renderer references
`Resolve()` re-validated only the generator; the ten renderer references were cached with `??=` forever, so a freed or replaced renderer stayed a dead reference — the staleness bug each renderer's own `ResolveGenerator` was already fixed for, present in the orchestrator itself.
**Fixed:** one `Refresh<T>` helper applies the `IsInstanceValid` re-check to all thirteen cached references.

### 1.10 The `lava` kind existed for the generator and nobody else
`TerrainPreset.Lava` produces `"lava"` tiles (both `ThemedKind` and `PlainKind`), but the kind was missing from `TerrainTileSets.Kinds`, from the painted renderer's shader id map (fell through to id 0 → **a lava field painted as grass**), and from the isometric frame table (**skipped → holes in the map**). `Describe` also hand-wrote passability as `kind != "rock"`, so lava was walkable while nothing said it should be.
**Fixed:** `"lava"` appended to `Kinds` (append keeps existing tile indices stable), mapped to the rock material/block as the honest stand-in until lava art exists, `GroundOf` returns `Steep` for it, and `Cell.Passable` now derives from `GroundOf(kind) == Land` so the flag and the physics body can never disagree.

### 1.11 Shallow water was buildable by default
Generated maps mark rivers, lakes, and the continental shelf as `shallow_water`, but every build-side `BlockedTerrainKinds` default list omitted it — so the sea floor accepted buildings, roads, spawns, and deposits out of the box.
**Fixed:** `shallow_water` added to the defaults of `GridPlacementComponent`, `GridRoadComponent`, `GridToolActionComponent` (list and hardcoded fallback), `GridSelectionJobCommandComponent`, `GridWorkerSpawnerComponent`, `GridResourceScatterComponent`. **Deliberately not** added to `GridNavigationComponent`, where shallow water is wadeable at 2.5× cost — that asymmetry is now documented on its list. Serialized scenes keep their authored lists; defaults affect new nodes.

### 1.12 Hygiene: dead code and orphaned documentation
Verified-dead and removed: `TextureElevationTileSetGeneratorComponent.ScenarioTopTint` and `.Wrap` (zero callers), two unused locals in `SampleDirectTexture`, an unused `moisture` local in `TerrainFeatureStage.Choose`. Six orphaned/misattached `<summary>` blocks re-homed or deleted (`TerrainWorld.CellShade`, `TerrainBiomeStage.WaterKind` quota-era text, `TerrainCoherenceStage.AbsorbTargets`, `TerrainScaleConstraintStage.NeighbourLand`, `TerrainAuthoring.Adopt`, two dead quota-era blocks in `TerrainGeneratorComponent`). The stale "world buffer is REUSED between generations" comment in `TerrainLandmassStage` corrected (the builder constructs a fresh `TerrainWorld` per build; the defensive clear stays, now honestly explained).

**Phase-1 verification** (all against `Godot_v4.7.1-stable_mono_win64`, after a one-time full `--import`):
- `dotnet build` clean — 0 warnings, 0 errors.
- `tests/addon_contract_scan.ps1` — passes end-to-end (it did not on HEAD; see 1.2).
- `tests/terrain_guards.ps1` — **all 15 guards pass**, including `tile_layers` and `iso_layers`, the two that exercise the renderers changed in 1.3/1.4.
- `tests/renderer_reporting_probe.ps1` — OK (all 9 renderers report).
- `tests/grid_terrain_{topology,feature,transition}_probe.ps1` — OK.
- `tests/runtime_smoke.ps1` — OK (covers the grid placement/builds/resources/scatter/jobs chain the 1.5/1.8/1.11 changes touch).
- `tests/grid_terrain_lake_scatter_probe.ps1` — **fails identically on clean HEAD** (`prop_count == 0`): the probe hardcodes an absolute sprite path outside the repository (`C:/.../The-Tech-Idea/Art/Plants/...`), making it machine-dependent. Pre-existing, environmental, and filed as its own fix: bundle the sprite (or drop the external path) so the probe is self-contained like every other guard. **Fixed during Phase 4 verification** — the probe now uses the bundled `textures/plants/weed01.png` and passes (`lake_cells=1458 props=128 water_props=0`).
- Headless-guard prerequisite worth knowing: on a machine that has never imported the project, run `<godot> --headless --path <root> --import` once, or guards fail on missing `.godot/imported/*.ctex` artifacts and can time out.

---

## Phase 2 — Performance. **Status: Fixed (this session).**

### 2.1 A* pathfinding pays node-resolution and allocation costs per step
`GridNavigationComponent.ResolveReferences()` runs inside `StepCost → TraversalCost`, inside `IsBlocked` (twice), and inside `TerrainCostMultiplier` — and when a path export is set, the first branch (`if (!GridPath.IsEmpty) _grid = GetNodeOrNull<…>`) re-resolves **unconditionally on every call**, unlike the cached else-branches. A 10,000-cell search performs tens of thousands of native `GetNodeOrNull` walks. On top of that: `Heuristic` calls `_roads.MinimumCostMultiplier` — O(all roads) — per visited node; `IsBlockedTerrainKind` re-normalizes every entry of `BlockedTerrainKinds` (3 string allocations each) per neighbour; `TerrainCostMultipliers` is a Variant dictionary probed per step.
**Fix:** resolve references **once** at the top of `FindCellPath`; snapshot per-search state into plain C# structures (pre-normalized blocked set, `Dictionary<string,float>` costs, cached road minimum — invalidated by `RoadsChanged`); make the path-set branches respect the cached-and-valid fast path like the search branches do.

### 2.2 HUD panels rebuild their UI every frame
With `AutoRefresh` on (the default), `GridWorkerStatusPanelComponent`, `GridProductionPanelComponent`, and `GridObjectivePanelComponent` run `RefreshPanel()` from `_Process` — which `QueueFree`s every row Label, allocates new ones, recursively scans the units/production root, and (workers) marshals `GetJobs()` — at 60 Hz. `GridMinimapComponent` redraws every frame and rebuilds `GetRoadCells()`/`GetJobs()`/`GetSelectedCells()` Godot collections per draw. `GridInteractionCursorComponent._Process` calls `QueueRedraw()` unconditionally. `GridCalendarHudComponent` refreshes per frame for the progress bar alone.
**Fix:** event-driven refresh — the signals all exist (`QueueChanged`, `WorkerStateChanged`, `ProductionStateChanged`, `ObjectiveProgressChanged`, `RoadsChanged`, `SelectionChanged`, `HoverCellChanged`, `ModeChanged`) — plus in-place row reuse (update `Text`/color on existing labels, add/remove only on set changes). Keep a low-rate timer (2–4 Hz) only for the genuinely continuous readouts (work-remaining seconds, day progress, minimap camera rectangle).

### 2.3 The cross-component per-cell hot path the ResolveField pass missed
`TerrainIsometricFeatureRendererComponent`'s loop calls `_iso.IsLandCell(cell)` and `_iso.SurfacePosition(cell)` per feature cell — and each of those, inside `TerrainIsometricRendererComponent`, goes through the generator's public per-cell wrappers, paying the ~40-property settings rebuild-and-compare the previous plan eliminated everywhere else.
**Fix:** internal field-taking overloads (`IsLandCell(field, cell)`, `SurfacePosition(field, cell)`) or a resolved-field cache on the isometric renderer refreshed per rebuild.

### 2.4 Cell overlay never updates at runtime — and would be slow if it did
`GridCellOverlayComponent` only `QueueRedraw`s from `_Process` **in the editor**; at runtime nothing connects `CellChanged/CellsChanged`, so tilled/watered state drawn at startup goes stale (latent correctness bug filed here because the fix is the same event-driven redraw). Its `_Draw` also iterates `GetCells()` — a full marshalled dictionary per cell per draw.
**Fix:** subscribe to the cell signals for redraw; add a lean typed enumerator on `GridCellDataComponent` (`IEnumerable<(Vector2I, CellFlags)>`) for C# consumers so drawing does not marshal dictionaries.

### 2.5 Generation-to-cells handoff marshals ten thousand dictionaries
`TerrainGeneratorComponent.GenerateTerrain` builds a `Godot.Collections.Array` of per-cell `Dictionary`s to call `LoadCells`. One-shot, but it is the single biggest allocation in a world build.
**Fix:** an internal typed fast path (`LoadCells(ReadOnlySpan<(Vector2I, string)>)` or direct record writes) used by the generator; the Variant API stays for GDScript callers.

### 2.6 Minor
`TerrainTransitionLayerComponent.IsTransitionTerrain` re-normalizes `TransitionTerrainKind` per cell (×4 per display cell × layers) — cache the normalized kind and alias set per refresh. `GridRoadComponent`/`GridCellOverlayComponent` `_Process`-redraw every editor frame — gate on actual change.

---

## Phase 3 — Consolidation. **Status: Fixed (this session).** Shared helpers landed as `GridTerrainRules` (blocked-kind defaults + normalization), `GridDefinitionReader` (dual pascal/snake definition parsing), extended `GridVariantReader` (`TryReadCell`/`TryReadWorldPoint`), `TerrainGeometry.HashInt`; the `MountainPrefabGeneratorComponent.TextureFilter` export was renamed `PartTextureFilter` (it shadowed `CanvasItem.TextureFilter`).

The grid half never had the dedup pass the terrain half got. Verified duplications:

- **3.1 Blocked/allowed-terrain logic ×7.** The `BlockedTerrainKinds` default list + `NormalizeTerrainKind` + allowed/blocked check loops are pasted into Navigation, Placement, Road, ToolAction, SelectionJobCommand, WorkerSpawner, ResourceScatter. → one `GridTerrainRules` static helper (normalize once, `IsBlocked(kind, blocked, allowed)`, shared default list), same consolidation `TerrainTileSets.IsWaterKind` was for the terrain side.
- **3.2 Variant cell/number readers ×3.** `TryReadCell`/`TryReadInt`/`ReadVariant` (~70 identical lines each) in GridPathFollower, GridToolAction, GridSelectionJobCommand. → fold into `GridVariantReader`.
- **3.3 Pascal/snake definition readers ×5 (~450 lines).** GridBuildDefinition, GridCropDefinition, GridProductionRecipe, GridObjectiveDefinition, GridResourceAmount each carry near-identical dual-key `ReadString/ReadInt/ReadFloat/ReadBool/ReadArray/ReadObject` boilerplate. → one `GridDefinitionReader`.
- **3.4 Residual hash copy.** `MountainTileMapLayerGeneratorComponent.HashInt` re-implements the `Hash01` mixing constants. → expose `TerrainGeometry.HashInt`, delete the copy.
- **3.5 Panel scaffold ×8 (optional).** The bind-or-generate Title/Summary/Rows pattern (~150 lines per panel) → a `GridPanelScaffold` helper. Worth doing together with 2.2 since both rewrite the same methods.
- **3.6 `MountainPrefabGeneratorComponent.TextureFilter`** shadows `CanvasItem.TextureFilter` (CS0108-class wart, duplicate Inspector row). → rename the export (e.g. `PartTextureFilter`) and sweep scenes for the property name.

---

## Phase 4 — Structure (directory split — **approved**). **Status: Fixed (this session).** All 53 `Grid*` files (51 original + `GridTerrainRules`/`GridDefinitionReader` from Phase 3) moved to `ecs/grid/`, with the 13 `Control`-based panels under `ecs/grid/ui/` (the count below said 14; the source has 13). Every referencing `.tscn`, probe preload, and scan path literal was rewritten; `GridObjectComponent` now inherits `GameplayComponent` (a pure marker over `EntityComponent` — no serialized behavior change); the category policy is documented in `SKILL.md`; `ADDON_GUIDE`/`ADDON_REFERENCE`/`ADDON_FULL_SCAN` were regenerated with a dedicated grid section. Bonus fix while verifying: `grid_terrain_lake_scatter_probe.gd` pointed at an absolute art path outside the repo (failed on any other machine, documented in Phase 1's verification) — it now uses the bundled `weed01.png` and passes.

1. Move the 51 `Grid*` files to `ecs/grid/`, with the 14 `Control`-based panels under `ecs/grid/ui/`. Class names and `[GlobalClass]` registrations do not change; `.uid` files move with their scripts.
2. Fix script paths in every referencing `.tscn` (`templates/scenes/grid_*.tscn`, `templates/scenes/terrain/grid_world_kit_hud_example.tscn`) and in path-literal guards (`addon_contract_scan.ps1`, `tools/Generate-AddonGuide.ps1`).
3. `GridObjectComponent : EntityComponent` violates the addon contract ("always inherit a category base"). → `GameplayComponent`, after verifying `GameplayComponent` adds no behavior that changes serialized scenes.
4. Document the category policy honestly in `SKILL.md`/`ARCHITECTURE.md`: grid *system* components (models, services) are plain `Node`s by design; only agent-like components (`GridWorkerComponent`, `GridPathFollowerComponent`, `GridCameraControllerComponent`) use category bases. The alternative — migrating 47 classes onto category bases — is churn without benefit.
5. Regenerate `docs/ADDON_GUIDE.md` / `ADDON_REFERENCE.md` / `ADDON_FULL_SCAN.md` via `tools/Generate-*.ps1` afterward.
6. Full guard suite + template-scene load smoke before and after.

Do this phase **after** Phase 2/3 diffs land, or before them — but not interleaved; every open diff at split time must be rebased across the moves.

---

## Phase 5 — Enhancements. **Status: Fixed (this session).** Crop regrowth re-arms on harvest and survives saves (`crop_regrow_days`; `RemoveCrop` uproots); planting charges `SeedItemId` from the wallet (`ConsumeSeedsFromWallet`, `missing_seeds`, `TrySpendAmount`); the minimap bakes a terrain background (`ShowTerrain`); `TerrainDataLayersComponent` publishes continent and start-position layers (`ContinentAt`, `IsStartPositionAt`, `StartCells`); navigation/placement/tools take an explicit `DataLayersPath` to read terrain kinds from the generated map; the occupancy-ownership matrix is in `2D_ISO_TOOLKIT.md` and guarded by the contract scan.

- **5.1 Crop regrowth (approved wire-up).** `GridCropDefinition.RegrowDays` is exported and read by nothing. Implement in `GridCellDataComponent.HarvestCrop`/`AdvanceDay`: a harvested crop with `RegrowDays >= 0` keeps its `CropId`, resets age to `DaysToMature - RegrowDays` equivalent, and ripens again; guard with an example script.
- **5.2 Seed consumption (approved wire-up).** `GridCropDefinition.SeedItemId` is exported and read by nothing — planting is free. `GridToolActionComponent.ApplyPlant` (and the plant job effect) should spend one `SeedItemId` from the wallet when a wallet is wired and the definition names a seed; reject with `missing_seeds` otherwise.
- **5.3 Minimap terrain background.** `GridMinimapComponent` draws roads/jobs/units on a flat color — no terrain. Bake a small biome-color texture once per build (from `TerrainDataLayersComponent` or the generator) and blit it as the background.
- **5.4 Complete the published map.** `TerrainDataLayersComponent` publishes terrain/resource/feature/relief, but continent ids and start positions remain generator-only — a saved map re-opened without regenerating loses `ContinentAt` and starts. Add a continent data layer and start-position markers so the "no generator needed at runtime" promise covers everything.
- **5.5 Let the grid read the published map directly.** Navigation/placement/tools read only `GridCellDataComponent` (the build-time copy). Give them the same optional `DataLayersPath` the scatter already has, so passability and terrain rules can come straight from the durable map — closing the gameplay-side twin of the render-side "two sources" defect the previous plan fixed.
- **5.6 One documented owner for "is this cell blocked".** Four stores exist by design (placement occupancy, navigation blocked set, cell-data Blocked flag, tile-data passability) and are synchronized by convention. Document the ownership matrix in `2D_ISO_TOOLKIT.md` and add a guard asserting placement writes navigation blocks iff `MarkPlacedCellsBlockedInNavigation`.

## Known and deliberately not planned

- **Isometric-autotile peering bits** remain an art task (see the closed terrain plan) — not code-fixable.
- **Replacing grid A\* with Godot navigation** — rejected; cell-exact paths, road costs, and per-cell blocking are the point of a cell-based mover.
- **Migrating all 51 grid classes onto category bases** — rejected in favor of 4.4's documented policy.
- **A lava material slot in the splat shader** — follow-up art/shader work; Phase 1 registered the kind with rock as the stand-in.

## Verification strategy

Every phase lands with: `dotnet build` clean → `addon_contract_scan.ps1` → `terrain_guards.ps1` + `renderer_reporting_probe.ps1` + the four `grid_terrain_*_probe.ps1` + `runtime_smoke.ps1` against Godot 4.7 mono. New behavior gets a falsifiable guard first (make it fail once, then fix): batch-load signal-count probe (2.x), a nav-search timing smoke, a scatter-catalog contract example, and a template-scene load check for Phase 4's moves.

## Grid gameplay review (second pass, 2026-09-02). **Status: Fixed.**

A fresh correctness pass over the gameplay loop after the five phases landed — jobs, workers, builds, production, economy, calendar, saves. Five defects fixed, three noted:

1. **Cancelled build jobs stranded paid ghost buildings.** `GridBuildSiteComponent` only forgot the site; the placed node stayed tinted, incomplete, and footprint-blocking forever, with the wallet already charged. Cancelling now tears the site down (`CancelBuildSite`): removes the placed node and refunds what placement recorded as charged (`grid_build_cost_charged` meta), behind `RemovePlacedOnJobCancelled`/`RefundOnJobCancelled` exports and a `BuildSiteCancelled` signal.
2. **Occupancy was conflated with navigation blocking.** `BlocksNavigation=false` on a build definition disabled *occupancy* too (placement line copied one into the other; `GridObjectComponent.ReserveFootprint` gated both halves on it), so every walkable build could be stacked without limit on one cell. `GridBuildDefinition.OccupiesCells` (default true) now owns the occupancy half; placement marks the two separately, and the object component's gate covers only navigation. Bonus from the same fix: placement now stamps the object to *reserve* the marked cells, so demolishing a placed building finally releases its occupancy and navigation blocks (they used to leak forever).
3. **Jobs saved as Claimed loaded as permanently stuck.** Worker ids embed instance ids no reload reproduces, and workers do not persist their current job — a loaded claim always belonged to a ghost. `GridJobQueueComponent.RequeueClaimedJobsOnLoad` (default true) requeues them.
4. **The calendar HUD showed a stale date after a load.** The HUD refreshes labels only on `DayAdvanced`, and `RestoreState`/`SetDate` emitted nothing. Both now emit.
5. **A worker walked to a cancelled job and worked it anyway** (visible when the queue keeps cancelled jobs), then failed with a misleading `complete_rejected`. `StartWorkOrFail` now verifies the job is still Claimed by this worker and fails fast with `job_no_longer_claimed`.

Noted, not changed: `GridObjectiveTrackerComponent.RestoreState` emits no signals (the objective panel self-heals by polling; event binders would stale); footprint release is boolean, so two objects sharing a cell free it when the first leaves (no refcount — known limitation); a gather with no wallet anywhere in the scene silently drops the yield (consistent with seed spending's wallet-optional convention).

New smoke coverage: walkable-build-occupies, claimed-job requeue on load, and build-site cancel teardown (refund + node removal). Verified: build 0/0, contract scan, runtime smoke, 15/15 terrain guards.

## Final verification (all phases landed)

Run after Phase 4, against `Godot_v4.7.1-stable_mono_win64`:

- `dotnet build` — 0 warnings, 0 errors.
- `tests/addon_contract_scan.ps1` — OK (guards modernized where refactors moved literals, and new guards added for: `GridDefinitionReader`, the regrow/seed wiring, `TrySpendAmount`, `DataLayersPath` on navigation/tools, the data-layer accessor surface, and the `2D_ISO_TOOLKIT.md` ownership matrix).
- `tests/terrain_guards.ps1` — 15/15 OK on the split tree.
- `tests/renderer_reporting_probe.ps1` — OK (9 renderers).
- All four `grid_terrain_*` probes — OK, **including `lake_scatter`**, which this session made self-contained.
- `tests/runtime_smoke.ps1` — OK, including the template scenes with their rewritten script paths and four new smoke assertions for seed spending and crop regrowth. The one behavior change surfaced by the smoke: `GridCropDefinition.SeedItemId` now defaults to `""` (empty = free planting) — the old `"turnip_seed"` default would have priced every authored crop in turnip seeds the moment 5.2 wired seed spending to the tool.
