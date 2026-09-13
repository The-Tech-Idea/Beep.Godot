# Source review: scene and map lifecycle

Date: 2026-09-11. Scope: the main Game Builder dock and generators; scene routing and level loading; terrain authoring, generation/publication and persistence boundaries. This is a targeted review, not a file-by-file audit of every component or every genre.

## Existing foundations to retain

| Area | Observed implementation | Evidence |
|---|---|---|
| Project creation | Standard folders, GameInfo, autoload/settings configuration, template copies for shared and genre scenes | [BeepProjectGenerator](../../addons/beep_game_builder_cs/core/BeepProjectGenerator.cs), [BeepGenreGenerator](../../addons/beep_game_builder_cs/core/BeepGenreGenerator.cs): `StampProject` |
| Dock | Genre/theme settings, generation, template validation, scene drift, GameInfo save, screen creation | [BeepGameBuilderDock](../../addons/beep_game_builder_cs/ui/BeepGameBuilderDock.cs) |
| Generated scene protection | Scene copy uses `SafeWriteText` and overwrites only for `OverwriteAll`; this is stronger than the stale comment saying it always overwrites | [BeepGenreGenerator](../../addons/beep_game_builder_cs/core/BeepGenreGenerator.cs): `CopyUiSceneFromPath` |
| New UI screens | Creates a C# script and a composed `.tscn`, with an overwrite argument | [BeepScreenGenerator](../../addons/beep_game_builder_cs/core/BeepScreenGenerator.cs): `CreateScreen` |
| Drift detection | Compares copied scene text with current template text | [BeepSceneDrift](../../addons/beep_game_builder_cs/core/BeepSceneDrift.cs): `Compare` |
| Runtime session | GameApp owns clock/settings/locale/saves; save manager coordinates loading participants and readiness | [GameApp](../../addons/beep_game_builder_cs/ecs/GameApp.cs), [GameStateManagerComponent](../../addons/beep_game_builder_cs/ecs/GameStateManagerComponent.cs) |
| Design-time terrain | `[Tool]` world exposes **Generate map**; generated layers/nodes can receive scene ownership | [TerrainWorldComponent](../../addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs), [TerrainAuthoring](../../addons/beep_game_builder_cs/ecs/terrain/TerrainAuthoring.cs) |
| Runtime generation | Detached worker settings, cancellation, stale-result rejection, budgeted cell publication and renderer/collision completion handling | [TerrainWorldComponent.Generation](../../addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.Generation.cs), [TerrainGenerationJob](../../addons/beep_game_builder_cs/ecs/terrain/TerrainGenerationJob.cs) |
| World ownership | Recipe reconstruction and mutable grid restoration are separate; `Redraw` does not call new-world generation | [TerrainWorldComponent](../../addons/beep_game_builder_cs/ecs/terrain/TerrainWorldComponent.cs): `RestoreWorld`, `Redraw`; [GridWorldStateComponent](../../addons/beep_game_builder_cs/ecs/grid/GridWorldStateComponent.cs) |
| Persistence primitives | Chunk encoding/validation, live archive infrastructure, fresh save snapshots and scoped save discovery | [Cell snapshots](../../addons/beep_game_builder_cs/ecs/grid/GridCellDataComponent.Snapshots.cs), [archive](../../addons/beep_game_builder_cs/ecs/grid/GridCellArchiveComponent.cs), [save ownership](../SAVE_SNAPSHOTS.md) |

## Findings and recommended priorities

Priority here is delivery priority: P1 prevents lost work or inconsistent playable worlds; P2 improves authoring and management. It is not a claim that every proposed capability is a current bug.

| Finding | Evidence and consequence | Plan |
|---|---|---|
| R01 / P1: owned scene output is not a complete map save | `TerrainAuthoring.Adopt` sets node ownership, but live cell records are held in `GridCellDataComponent`'s private store. Packing an owned Cells node does not serialize those records. See reproduction below. Visible terrain can survive while gameplay data is absent. | BGB-02, BGB-04 |
| R02 / P1: editor generation has no transaction at its entry point | `GenerateMap` invokes `NewWorld`, which generates cells and draws directly. That path does not create an editor undo action or a staged commit. `BeginNewWorld` explicitly rejects editor execution. Existing runtime cancellation is not an editor preview transaction. | BGB-03 |
| R03 / P1: there is no explicit authored-map load mode on TerrainWorld | `BuildOnReady` defaults true at runtime, and `NewWorld` writes cells. Turning it off prevents regeneration but does not restore an authored cell baseline. A boolean is insufficient to express authored/procedural/hybrid initialization. | BGB-02, BGB-04, BGB-06 |
| R04 / P1: the persisted recipe is narrower than effective generator settings | `CaptureRecipe` stores world axes, seed and selected custom controls. `TerrainGenerationSettings` also has origin, frequencies, erosion and other tuning, plus catalog inputs. Capture/restore must preserve effective values and versioned content identity, not depend on later defaults. | BGB-02; existing F09 and FEAT-06 |
| R05 / P1: saved grid objects require an existing node path | `CaptureGridObjectStates` records a path and state; `RestoreGridObjectStates` looks up that path and skips absent nodes. It does not recreate a runtime-placed building from a prefab identity. | BGB-07; existing F05/F07 |
| R06 / P1: ordinary grid snapshots cannot save an evicted world | `GridWorldStateComponent.CaptureState` calls `CaptureChunkState`, which throws when `_unavailableChunks` is nonempty. Keep that refusal until a complete archive save transaction exists. | BGB-08; existing ENH-10 |
| R07 / P1: scene replacement is not a rollback-safe transaction | `SceneNav.ChangeScene` suspends the session before changing scenes and reports failure without a rollback path. `LevelLoaderComponent.TryLoadLevel` removes the old instance before instantiating the new one. `MainGameComponent` has another level-loading path. | BGB-05; existing F04 |
| R08 / P2: project generation lacks a single preview/commit result | `StampProject` writes GameInfo, folders, translations, scenes and project settings in stages. Scene skip protection exists, but it does not establish one transaction across every output. `CreateScreen` writes script and scene independently. | BGB-01 |
| R09 / P2: drift is detected without generated-base provenance | Comparing current template text to a user's scene identifies a difference, but cannot alone distinguish user edits from a template upgrade. Default preservation is right; upgrades need an explicit preview and recorded base version/hash. | BGB-09 |
| R10 / P1: ready has different meanings across entry points | Synchronous `NewWorld` emits `WorldBuilt` after `Draw`; async publication waits for additional work. Async generation has safeguards, but its entry point does not itself register a GameStateManager load. A reusable session adapter must coordinate saves, input, simulation and owner lifetime. | BGB-05, BGB-06 |
| R11 / P2: editor workflow is distributed across the dock, Inspector buttons and lab | Existing entry points are useful, but no reviewed dock flow ties recipe selection, preview, authored overrides, bake validation, scene registration and launch testing together. | BGB-10 |
| R12 / P1: startup-only evidence is too weak for this lifecycle | This session exposed live stale delegates, non-tool resource casts and incomplete texture imports that a build or short plugin startup did not detect. Scene startup now passes after targeted repairs, but a full repeated C# hot-reload campaign was not performed. | BGB-11, BGB-12 |
| R13 / P1: native tile output needs a documented publication contract | Real tile implementations already exist: `TerrainTransitionLayerComponent` writes display cells and `TerrainIsometricAutotileRendererComponent` fills `IsoTerrain`. But `TerrainTileRendererComponent.GetTerrainLayer` returns a `LogicalGrid` coordinate layer; returning that alone would not deliver populated terrain. The owner requires reusable, editable TileMapLayer maps, including a profile without generator/runtime-script dependencies. | BGB-13, BGB-04 |

R13 evidence: [TerrainTileRendererComponent](../../addons/beep_game_builder_cs/ecs/terrain/TerrainTileRendererComponent.cs): `GetTerrainLayer`; [TerrainTransitionLayerComponent](../../addons/beep_game_builder_cs/ecs/terrain/TerrainTransitionLayerComponent.cs): display-layer `SetCell` / `SetCellsTerrainConnect`; [TerrainIsometricAutotileRendererComponent](../../addons/beep_game_builder_cs/ecs/terrain/TerrainIsometricAutotileRendererComponent.cs): `EnsureLayer` / tile publication. Extend these implementations where compatible; do not start another terrain-generation algorithm.

## Focused reproduction: logical cells versus owned nodes

Executed on Godot 4.7.2 .NET during this review, without changing addon code:

1. Create `AuthoredMap` with an owned `GridCellDataComponent` and owned `Marker2D` at `(64,96)`.
2. Set cell `(2,3)` to `water`.
3. `PackedScene.Pack` the root and instantiate the packed scene as a second tree.
4. Read the new marker and cell; then restore a separately captured chunk snapshot.

Observed output:

```text
before: terrain=water
after_pack: terrain=grass
owned marker: (64,96)
after_explicit_restore: terrain=water
```

This isolates the missing persistence boundary; it is not a test of every renderer or an exported-game test. The temporary probe is under ignored `tmp/`; BGB-04 must add a maintained fresh-process regression. Saving a `.tscn` must not be advertised as a full playable-map bake until the gameplay snapshot is included and restored.

## Reconciliation with earlier plans

The [2026-09-05 workflow review](../../plans/GAMEAPP_GRID_TERRAIN_WORKFLOW_REVIEW.md) contains historical findings and later implementation notes. Do not read its original ownership table as current status: session barriers and fresh snapshots have since landed. F04, F05, F07 and F09 still supply relevant acceptance requirements.

The [terrain-grid plans](../../plans/terrain-grid/README.md) remain owners of low-level navigation, residency, archive and renderer work. In particular, ENH-10 contains an assumption that saving is already asynchronous-capable; the current `GameStateManagerComponent.Save(int)` is synchronous. BGB-08 must introduce an explicit pending/completion contract before integrating async archive flushes. Do not implement an `async void` save behind a boolean result.

## Verification limits

This turn performed source inspection, reviewed existing tests/docs and ran the focused packing reproduction. It did not rerun the full build/contract suite, manipulate the user's open scene, test every genre, export a game or measure large-world performance. Earlier in this conversation the build and terrain editor startup passed; the broad contract scan reported 16 unrelated failures. Treat that as historical evidence, not a new clean baseline. BGB-11 establishes a fresh baseline before implementation work starts.

## Godot references checked

Owned nodes are packed with their scene; arbitrary managed state still needs a serialization contract. See [PackedScene](https://docs.godotengine.org/en/stable/classes/class_packedscene.html). Editor edits should participate in [EditorUndoRedoManager](https://docs.godotengine.org/en/stable/classes/class_editorundoredomanager.html). Runtime loading can request resources in the background and must check completion before retrieval to avoid blocking; see [background loading](https://docs.godotengine.org/en/stable/tutorials/io/background_loading.html). Published project assets and writable saves have different paths; see [Godot data paths](https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html).
