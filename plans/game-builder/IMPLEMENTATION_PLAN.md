# Beep Game Builder: scene and TileMapLayer delivery plan

Date: 2026-09-11. Planning complete; all BGB implementation packages below are **Proposed**. The user requested review, documents and tracker updates, not implementation in this turn. Canonical status lives in the [master tracker](../../docs/ENGINE_ENHANCEMENT_PLAN.md#game-builder-scenes-and-terrain-lifecycle-review-2026-09-11); update that table when work lands. This document owns specifications and acceptance criteria, not a second status ledger.

## Goal and fixed requirements

Terrain generation must produce real, populated, editable Godot **TileMapLayer** nodes. Developers must be able to paint/erase with native tools, save the layers as reusable scenes and use them without rerunning a generator. Support a Native Godot profile without Beep runtime scripts and an optional Beep gameplay profile with synchronized logical data. Add dynamic runtime generation and mutation using the same generator and tile bindings.

Keep existing terrain algorithms, grid ownership, GameApp and archive infrastructure. No new universal manager/autoload, wholesale renderer rewrite, automatic destructive scene regeneration, or promise of infinite worlds. Preserve current paths/APIs through migration adapters. Custom gameplay extensions must remain accessible through the project's existing C# and duck-typed/GDScript contract approach.

Design documents: [review](../../docs/game-builder/REVIEW.md), [scenes](../../docs/game-builder/SCENE_LIFECYCLE.md), [TileMapLayer output](../../docs/game-builder/TILEMAP_OUTPUT.md), [authoring](../../docs/game-builder/TERRAIN_AUTHORING.md), [runtime](../../docs/game-builder/RUNTIME_TERRAIN.md).

## Delivery sequence

| Milestone | Packages | Demonstrable outcome |
|---|---|---|
| M0: baseline and output contract | BGB-11 baseline, BGB-13 initial square output | A generated populated TileMapLayer is visibly editable; known test failures recorded |
| M1: reusable native maps | BGB-13 completion, BGB-03 minimal preview/apply, BGB-04 native profile | Generate → paint/erase → save → reopen → instance twice without generator or Beep scripts |
| M2: authored gameplay maps | BGB-02, BGB-03 gameplay sync, BGB-04 Beep profile, BGB-07 authored identities | Tiles and gameplay survive publish/fresh-process load with resources, navigation and objects intact |
| M3: scene/session integration | BGB-01, BGB-05, BGB-06, BGB-07 runtime reconstruction | Catalog-driven New Game/Continue/Retry; cancellable generation; placed entities restore |
| M4: large dynamic worlds | BGB-08 plus existing streaming dependencies | Edited resident and evicted chunks survive isolated save slots with bounded runtime work |
| M5: author experience and release | BGB-09, BGB-10 completion, BGB-11 regression suite, BGB-12 | Upgrade preview, integrated dock, examples and exported-game proof |

Native-only M1 deliberately does not wait for the complete recipe/save architecture. M2 cannot claim full gameplay-map persistence until the logical baseline and reverse mapping pass. Thin dock controls can accompany earlier milestones; the consolidated dock is not a prerequisite to proving file formats.

Estimates below are planning ranges in focused developer-days, including package-level tests, not commitments. The total is approximately 42–79 days before external content work or unresolved low-level streaming backlog. Re-estimate after M1; accept each milestone by evidence rather than elapsed time.

## BGB-01 — Transactional project and scene creation

Priority P2; estimate 3–5 days. Prerequisite: BGB-11 baseline. Surfaces: BeepGameBuilderDock, BeepGenreGenerator, BeepScreenGenerator, BeepFileUtils and project defaults.

- Introduce a UI-independent generation request and output plan; include GameInfo, settings, scripts and scenes in the plan.
- Validate path/name/schema and required inputs before writes; normalize/escape names and scene strings centrally.
- Stage new outputs and retain recoverable originals for explicit replacements. Preserve user edits by default. Treat scene/script pairs as one operation.
- Start with selected-genre generation and explicit opt-in sample content; preserve the existing API as an adapter.
- Test repeated Preserve, malformed names/quotes, missing templates and injected write failure. Gate: no partial published pair, lost edit or silent success.

## BGB-02 — Versioned map definition and complete recipe

Priority P1; estimate 3–6 days. Prerequisite: output contract from BGB-13; coordinate existing F09 and FEAT-06.

- Implement MapDefinition, effective TerrainRecipe and content identity contracts from the design docs.
- Capture origin, extent, normalized generation tuning and versioned catalog inputs; keep presentation independently replaceable.
- Add explicit Authored/Procedural/Hybrid mode. Import old scene exports/version-3 world recipes through an adapter; record missing legacy information rather than claiming exact reconstruction.
- Test custom origin/extent, nondefault noise/erosion/catalog settings, equivalent recipe hashes and unsupported versions. Gate: complete settings survive round trip and incompatible reconstruction is rejected or uses a full stored baseline.

## BGB-03 — Editor preview, apply and tile-edit synchronization

Priority P1; estimate 4–7 days. Prerequisite: BGB-13; BGB-02 for Beep profile. Related: existing FEAT-08 history.

- Wrap generation in an editor request with staging, progress and cancellation; keep preview outside save/export scope.
- Apply generated layer changes through EditorUndoRedoManager; preserve unrelated developer nodes and authored overrides.
- Support normal Godot tile edits. Start with explicit Sync gameplay from tiles; add reverse binding validation and dirty-direction tracking. Defer automatic incremental editor synchronization until the explicit path is correct.
- Test paint/erase/patterns, undo/redo, cancellation, scene switch during preview and incompatible regeneration bounds. Gate: undo restores exact tile tuples and gameplay snapshot; regeneration cannot silently erase pending edits.

## BGB-04 — Save reusable TileMapLayer scenes and gameplay baselines

Priority P1; estimate 4–7 days. Prerequisites: BGB-13 and BGB-03 minimal; BGB-02/BGB-07 authored records for Beep profile.

- Publish actual native layers, their complete owner chains and TileSet/resource dependencies to game-owned output.
- Native profile strips generator/editor-only dependencies. Beep profile adds synchronized logical chunks, objects and loader bindings; BuildOnReady is replaced by explicit authored loading policy.
- Stage output and validate a fresh read before committing the manifest. Keep the previous published revision on failure. Verify resource export inclusion and per-instance isolation.
- Convert the review's water→grass packing reproduction into a maintained regression, followed by fresh-process and exported-scene tests.
- Gate: native map works without Beep; Beep mode preserves logical state without regeneration; two instances do not share mutable data.

## BGB-05 — Scene catalog and transition coordinator

Priority P1; estimate 4–7 days. Prerequisites: BGB-02; integrate existing F04/session work.

- Add stable SceneId/MapId definitions and route adapters; preserve legacy GameInfo paths and level-index entry methods during migration.
- Centralize loading in a GameApp-owned service using the existing readiness barrier. Stage destination initialization with gameplay disabled.
- Add request identities, background resource loading, error/cancel recovery and explicit low-memory transition policy.
- Test menu/game/overlay/level routes, failed instantiation, superseding requests and nested terrain loads. Gate: no duplicate loaders/spawns, lost previous session on precommit failure or stale callback commit.

## BGB-06 — Dynamic terrain lifecycle and TileMapLayer publication

Priority P1; estimate 3–6 days. Prerequisites: BGB-02, BGB-05, BGB-13.

- Adapt BeginNewWorld to the common session contract without requiring TerrainLabComponent.
- Publish native tile cells on the main thread in bounded batches; wait for the chosen collision/navigation backend before gameplay readiness.
- Support local semantic edits with tile-neighbor refresh and chunk-aware invalidation. Preserve exact authored tile overrides where they are part of the save contract.
- Test cancellation before commit, postcommit failure, replacement of source/owner, repeated requests and editor/runtime recipe parity.
- Gate: no live-world replacement from stale work, no save/input during partial commit, and runtime output is populated TileMapLayer data.

## BGB-07 — Stable object identity and restoration

Priority P1; estimate 3–6 days. Prerequisite: BGB-02; integration with BGB-05. Reuse existing F05/F07 ownership.

- Add stable authored/runtime entity IDs and prefab catalog references, including destroyed-authored-object tombstones.
- Validate identities and references before mutation. Restore in passes so objects exist before relationships, jobs and occupancy are connected.
- Migrate path-based records only when unambiguous; report missing/duplicate entries. Preserve duck-typed component participation.
- Test an authored object, runtime-placed building, destroyed object, worker/job relation, rename and duplicate IDs.
- Gate: each entity restored exactly once; relationships bind to restored IDs; no missing building silently skipped.

## BGB-08 — Streamed-world save transaction

Priority P1; estimate 4–8 days excluding unfinished low-level streaming prerequisites. Prerequisites: BGB-02, BGB-05/06/07; coordinate ENH-04 and ENH-10.

- First resolve synchronous Save compatibility; add explicit pending/completion/error semantics.
- Capture a consistent save epoch, flush/archive required chunks, write slot-specific immutable files and publish the manifest last.
- Restore into a fresh live archive, validate corrupt/missing chunks and prevent slots sharing writable chunk files.
- Test eviction, resident and archived edits, slot isolation, cancellation, disk-full/write failure and corruption.
- Gate: original full-snapshot refusal remains until this path preserves all unavailable chunks; success means durable complete publication.

## BGB-09 — Template provenance and safe upgrades

Priority P2; estimate 2–4 days. Prerequisite: BGB-01.

- Record generated-base/template hashes and generator version; distinguish authored change from upstream change.
- Extend drift reports into an actionable upgrade preview with Preserve / Replace / Conflict decisions.
- Defer general semantic merging; offer side-by-side replacement where no safe automatic merge exists.
- Gate: addon upgrade never overwrites a changed scene implicitly; failed update preserves the last usable generated project.

## BGB-10 — Integrated scene and map dock

Priority P2; estimate 4–7 days. Depends on the implemented services above; deliver thin controls incrementally.

- Add project/scenes/maps views, scene-role creation and catalog navigation.
- Map controls: choose TileSet/bindings, Generate Preview, Apply, Open Tile Layers, Sync gameplay from tiles, Validate, Save Native Scene, Publish Beep Map, Play Published Map.
- Display operation state, dirty/unpublished changes, diagnostics and actionable errors. Select the actual output TileMapLayer for native Godot editing.
- Reuse the same services from dock, Inspector actions and MCP; do not implement separate generation behavior in each UI.
- Gate: a developer completes M1/M2 without writing code or moving through unrelated lab controls; cancellation/failure remains visible and recoverable.

## BGB-11 — Lifecycle regression and diagnostics

Priority P1; estimate 2–4 days for infrastructure plus coverage delivered with each package. Starts before other implementation.

- Record a current build/test baseline and classify existing failures. Do not copy older “all green” tracker statements as current evidence.
- Add resource/import and native tile-output validation; scene ownership/TileSet dependency diagnostics; stage timings and request IDs.
- Include fresh-process scene save/load, TileMapLayer edit persistence, repeated editor assembly reload with scene open, scene close/reopen, generation cancellation and two-world isolation.
- Reuse existing navigation, generation, resource, cell and session probes. Extend `editor_startup_smoke.ps1 -ScenePath` for actual scenes.
- Gate: mutation or deliberately broken fixtures demonstrate that each regression detects its target defect; no live error spam is accepted as a passing test.

## BGB-12 — Examples, migration and exported-game proof

Priority P1 release gate; estimate 3–5 days. Delivered across milestones, closed after the supported modes pass.

- Ship NativeTileMap, AuthoredTileMapGame, ProceduralTileMapGame and HybridTileMapGame examples with game-owned output paths.
- Provide generation/edit/save/reuse tutorials with screenshots and exact scene settings. Explain native versus Beep data, imports and compatibility.
- Test exporting without development caches. Check Native profile portability without addon scripts, authored gameplay loading without a generator, and runtime new/continue/save behavior.
- Gate: another developer follows the tutorial from a fresh checkout; all required resources are included; unsupported streaming/renderer combinations are stated explicitly.

## BGB-13 — Populated native TileMapLayer output

Priority P1 and first implementation target; estimate 3–7 days. Prerequisite: BGB-11 baseline. Full specification: [TileMapLayer output](../../docs/game-builder/TILEMAP_OUTPUT.md).

- Inventory populated display layers from TerrainTransitionLayerComponent and IsoTerrain from TerrainIsometricAutotileRendererComponent. Never substitute TerrainTileRendererComponent's empty LogicalGrid coordinate helper for the map.
- Define one explicit TileSet binding/emission adapter with validated sources, atlas cells, alternatives, terrain sets, layout/origin and layer roles.
- Reuse existing generator and renderer tile-writing paths where compatible. Provide simple square output first, then authored isometric output; keep dual-grid offsets explicit.
- Emit real native cells into a project-owned map root with correct owner chains, saveable TileSets, tile physics/navigation where configured and optional native markers/objects.
- Separate generator/preview nodes from exportable output so Native profile needs no Beep runtime drawing code. Preserve supported tile variants and shared resource isolation.
- Gate: populated cells can be painted/erased with stock Godot tools, saved/reopened/instanced twice; generation and subsequent play never erase developer edits automatically.

## Existing backlog integration

| Existing plan | Relationship to this plan |
|---|---|
| [Workflow F04](../GAMEAPP_GRID_TERRAIN_WORKFLOW_REVIEW.md#f04-p1-s-startup-and-scene-replacement-have-no-completion-barrier) | BGB-05 completes transition behavior around the session work already implemented |
| [Workflow review F05/F07/F09](../GAMEAPP_GRID_TERRAIN_WORKFLOW_REVIEW.md) | BGB-07 owns the integration work for identities; BGB-02 addresses complete recipe persistence |
| [FEAT-06 recipe](../terrain-grid/FEAT-06-huge-worlds-through-the-recipe.md) | BGB-02 must include its still-open recipe requirements; recheck status before implementation |
| [FEAT-08 history](../terrain-grid/FEAT-08-world-edit-history.md) | BGB-03 supplies editor transactions; share logical edit records rather than build a second history engine |
| [ENH-10 streamed saves](../terrain-grid/ENH-10-streamed-world-save.md) | BGB-08 is the product/session integration; keep one archive implementation and close both only with shared evidence |
| [ENH-07 tile streaming](../terrain-grid/ENH-07-tile-and-isometric-streaming.md) | Large-map BGB-06/08 needs its residency work; native authored-map output does not wait for it |
| [Terrain-grid index](../terrain-grid/README.md) | ENH-03/04/05/06, ENH-13-scatter and DUP-07 remain low-level dependency owners |

## Completion discipline

For every package, record changed surfaces, demonstrated user workflow, automated/visual/export evidence and remaining limitations in the master tracker. Mark Partial when only a profile/projection/milestone has landed. Do not mark an item implemented merely because the document exists or a native scene can be packed. No implementation starts by deleting existing authoring content or resetting the user's working tree.
