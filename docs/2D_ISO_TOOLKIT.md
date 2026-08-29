# Beep 2D And Isometric Toolkit

Generated for the current addon source tree on 2026-08-25. This guide focuses on the reusable grid, top-down, isometric, painterly terrain, and HUD components used by builder, farming, settlement, and tactical scenes.

## Design Rules

1. Author the visible scene in Godot first. Use `Node2D`, `TileMapLayer`, `CanvasLayer`, `Control`, `Button`, and `Label` nodes that are visible at design time.
2. Attach Beep components as behavior nodes. Set exported `NodePath` fields in the inspector instead of creating whole HUD panels at runtime.
3. Keep generated UI as an explicit fallback only. Components such as `GridToolPaletteComponent` and `GridWorkerSpawnerPanelComponent` bind authored controls by default.
4. Use `PainterlyTerrainComponent` as the broad visual base, then use TileMap/overlays only for collision, roads, crops, placement, and tactical feedback.
5. Keep grid state cell-based. Isometric projection changes how cells are drawn, not how jobs, resources, workers, or saves are modeled.

## Starter Scene Layout

Use this structure for a small settlement/builder scene:

```text
GridWorld2DIso : Node2D
  Terrain : Node                     (PainterlyTerrainComponent)
  TerrainBridge : Node               (GridPainterlyTerrainBridgeComponent)
  VisualTileLayer : TileMapLayer      (optional detail/collision sync)
  Grid : Node2D                       (GridProjectionComponent)
  Cells : Node                        (GridCellDataComponent)
  TerrainGenerator : Node             (GridTerrainGeneratorComponent)
  Roads : Node2D                      (GridRoadComponent)
  Selection : Node2D                  (GridSelectionComponent)
  Placement : Node2D                  (GridPlacementComponent)
  Navigation : Node                   (GridNavigationComponent)
  Jobs : Node                         (GridJobQueueComponent)
  Units : Node2D
  Base : Node2D
    WorkerSpawner : Node              (GridWorkerSpawnerComponent)
  HUD : CanvasLayer
    ResourceBar : Control             (GridResourceBarComponent)
    ToolPalette : Control             (GridToolPaletteComponent)
    BasePanel : Control               (GridWorkerSpawnerPanelComponent)
```

The addon template `addons/beep_game_builder_cs/templates/scenes/grid_world_2d_iso.tscn` follows this pattern.

## Terrain And Projection

`PainterlyTerrainComponent` renders `PainterlyTerrainSprite` as a saturated plain biome base and renders `PainterlyTerrainDetailSprite` above it for overlays. Biome detail is off by default; when enabled, `BiomeDetailCoverage`, `BiomeDetailPatchScale`, and `BiomeDetailPatchSoftness` restrict it to seeded local patches instead of painting the whole scene. Desert/sand patches add dune ridges, dust, and pebbles; grass/forest patches add clumps and sparse flower flecks; earth/swamp adds damp patches; rock adds cracks and stone marks; snow/ice adds scratches and soft drifts. Bundled material textures are opt-in through `UseBundledMaterialTextures`; leave them off when the base should stay clean instead of noisy or pale.

Use the painter behind grid overlays to avoid thousands of visible terrain tiles. `RenderOffset` positions the generated child `Sprite2D` when the painter component itself is authored as a plain `Node`. The default painter resolution matches a 64px tile before any safety cap is applied, which avoids the old low-resolution upscale blur on normal maps. Check `LastGenerationWasCapped`, `LastGeneratedPixelsPerTile`, and `LastAppliedTerrainScale` when a very large map still looks soft; that means `MaxGeneratedPixels` reduced the output resolution.

`GridPainterlyTerrainBridgeComponent` connects the grid model to that painterly base. Point it at `PainterlyTerrainComponent`, `GridCellDataComponent`, and optionally `GridRoadComponent`; it samples terrain kinds, crop/land flags, water effects, and road cells into one generated terrain image. The bridge passes each cell's terrain kind through to the painter, so a desert cell receives desert detail while grass, rock, snow, and swamp cells receive their own layer treatment. This is the recommended broad-map visual path when you want a non-tiled terrain look.

`GridTerrainGeneratorComponent` fills `GridCellDataComponent` with seeded terrain kinds. It can copy size, seed, noise, and preset settings from `PainterlyTerrainComponent`, then write grass, dirt, sand, water, deep water, rock, swamp, mud, snow, ice, lava, and related terrain kinds into the grid model. A generation pass reuses its noise samplers across all cells, so startup cost scales with map cells rather than repeated noise setup. Use it before the painterly bridge rebuilds when the map should be procedural but still pathable and saveable as cells.

`GridPainterlyTerrainBridgeComponent` can own the first procedural pass by setting `TerrainGeneratorPath` and `GenerateBeforeFirstRebuild`. In that setup, keep `PainterlyTerrainComponent.GenerateOnReady` and `GridTerrainGeneratorComponent.GenerateOnReady` disabled; the bridge generates cell data once, suppresses the duplicate change-triggered rebuild, then renders the shared grid state.

`GridProjectionComponent` owns the conversion between world positions and cells. Set `Projection` to top-down or isometric, set `TileSize`, and let placement, selection, cursor, pathing, minimap, and workers share the same projection.

`GridNavigationComponent` should point to `GridCellDataComponent` through `CellDataPath` when terrain affects movement. It can treat `CellFlags.Blocked` and exported `BlockedTerrainKinds` such as water, ocean, deep water, and lava as impassable, and it applies `TerrainCostMultipliers` for terrain such as sand, mud, snow, ice, rock, and shallow water. Road cost from `GridRoadComponent` is multiplied with terrain cost, so a dirt path over slow terrain still improves movement without replacing the terrain model.

`GridTileMapLayerBridgeComponent` mirrors cell state and road state into a Godot `TileMapLayer` when a project still needs TileMap collision/detail data. Keep this as a bridge, not the primary world model.

`GridCellOverlayComponent` draws visual feedback for cleared, watered, selected, blocked, and special-purpose cell states without changing the terrain base.

`GridMinimapComponent` renders a compact overview of cells, roads, jobs, workers, and placed objects for HUD use.

`GridCameraControllerComponent` provides map panning, drag movement, zoom-at-cursor, keyboard movement, bounds clamping, and focus helpers for large top-down/isometric maps.

## Cells, Crops, Roads, And Calendar

`GridCellDataComponent` stores terrain kind, flags, crops, watered state, cleared state, and occupancy data.

`GridCropDefinition` describes a crop's growth timing, allowed seasons, regrow behavior, and yield data. `GridCropCatalogComponent` looks those up for tools and planting.

`GridCalendarComponent` advances days/seasons and can tick crop growth. `GridCalendarHudComponent` displays date/progress and can optionally expose a next-day button.

`GridRoadComponent` stores road cells and traversal cost multipliers. Point `CellDataPath` at the same `GridCellDataComponent` to reject roads on blocked cells or impassable terrain such as water, ocean, deep water, and lava. `GridNavigationComponent` reads roads, occupied cells, cell-data blocked flags, blocked terrain kinds, and terrain movement costs when finding paths.

## Selection, Interaction, And Placement

`GridSelectionComponent` handles hover, click, drag rectangle selection, and selected-cell state.

`GridInteractionModeComponent` coordinates player clicks across select, inspect, tool, build, and disabled modes.

`GridInteractionModeBarComponent` exposes authored mode buttons for the current interaction mode.

`GridInteractionStatusComponent` displays the active mode, hovered cell, selected tool/build, and recent feedback.

`GridInteractionCursorComponent` draws valid/invalid hover outlines for top-down and isometric cells.

`GridToolActionComponent` applies the selected land/crop/road/resource tool to one cell or the current selection. It can use `CellDataPath` and `NavigationPath` to reject direct clear/hoe/plant/queue-job actions on water, lava, disallowed terrain, or out-of-bounds cells before mutating the grid.

`GridPlacementComponent` previews and confirms placeable builds. It reserves footprints, can spend resources, configures placed `GridObjectComponent` metadata, can read `GridCellDataComponent` so blocked flags, water/lava terrain, or an explicit `AllowedTerrainKinds` list control where buildings may be placed, and can write blocking build footprints into `GridNavigationComponent` through `NavigationPath`.

## Objects, Builds, And Production

`GridObjectComponent` marks placed buildings, resources, props, or units as selectable grid objects with id, display name, kind/category, description, cell, footprint, completion state, and metadata. Authored scene objects can opt into `ReserveFootprintOnReady` and wire `PlacementPath`/`NavigationPath` so bases, depots, rocks, and buildings reserve their footprint without project-specific code.

`GridObjectInspectorComponent` binds selection to an authored inspector panel.

`GridBuildDefinition` is the data resource for a placeable object: id, display name, category, footprint, cost, preview texture, and optional scene.

`GridBuildCatalogComponent` stores available builds and starts placement.

`GridBuildToolbarComponent` presents build categories and build choices.

`GridBuildSiteComponent` creates build-site jobs and completes placed builds.

`GridProductionRecipe` describes production inputs, outputs, and duration.

`GridProductionComponent` runs production on a building and spends/refunds resources through the wallet.

`GridProductionPanelComponent` displays machines and production state.

## Resources And Jobs

`GridResourceAmount` is the reusable resource id plus quantity data resource for build costs and production recipes.

`GridResourceWalletComponent` stores settlement resources. Startup balances should be authored with primitive dictionary data through `StartingResourceAmounts`, not C# resource subresources.

`GridResourceBarComponent` binds resource ids to authored labels.

`GridResourceNodeComponent` represents gatherable resources on the map. Point `PlacementPath` at `GridPlacementComponent` and enable `MarkCellOccupiedOnReady` when authored trees, rocks, crates, or props should block building placement until they are gathered, depleted, or removed.

`GridResourceScatterComponent` places resource nodes from a seeded scatter pass. Point it at `GridCellDataComponent` to avoid water/lava/blocked terrain or to require an `AllowedTerrainKinds` list, and enable `MarkGeneratedCellsOccupied` when generated trees, rocks, or props should reserve placement cells until cleared or depleted.

`GridJobQueueComponent` stores queued, claimed, completed, cancelled, and failed jobs.

`GridJobBoardComponent` shows current job queue status.

`GridJobEffectComponent` applies job completion effects such as clear land, gather, road work, plant, water, and harvest. With `ClearLandGathersResourceNode` enabled, a clear-land job gathers/depletes the resource node on that cell and releases its placement reservation.

`GridSelectionJobCommandComponent` turns selected cells into queued jobs. Wire `CellDataPath` and `NavigationPath` when command buttons should skip water/lava/out-of-bounds cells before jobs reach workers.

## Workers And Movement

`GridPathFollowerComponent` moves a `Node2D` or `CharacterBody2D` along a cell path.

`GridWorkerComponent` claims jobs, asks for paths, moves to target cells, works for a duration, and completes or releases jobs.

`GridWorkerSpawnerComponent` spawns workers or trucks from a base/depot and wires movement/job components. Set `CellDataPath` and `PlacementPath` so spawn cells reject blocked flags, impassable terrain kinds such as water/deep_water/lava, optional allowed terrain filters, navigation bounds, and occupied placement cells before a unit is created.

`GridWorkerSpawnerPanelComponent` binds a base HUD panel to a spawner. By default it expects `TitleLabelPath`, `CountLabelPath`, and `SpawnButtonPath` to point to authored controls.

`GridWorkerStatusPanelComponent` scans worker units and displays idle/active/job state.

## Goals And Save State

`GridObjectiveDefinition` describes an objective id, title, target count, starting state, and display behavior.

`GridObjectiveTrackerComponent` tracks active objectives and progress.

`GridObjectivePanelComponent` binds objectives to an authored HUD panel.

`GridObjectiveEventBinderComponent` connects job, build, resource, and production events to objective progress.

`GridWorldStateComponent` captures and restores cells, roads, jobs, selection, placement, navigation blocks, and authored `GridObjectComponent` state. When `ObjectsRootPath` is set, it releases old object footprint reservations before restore and reapplies the saved object cell, metadata, and reservations so stale occupied or blocked cells do not survive save/load.

## HUD And UI Kit Practice

Use Beep kit controls for authored HUDs:

- `KitPanelContainer` for compact HUD frames.
- `KitPushButton` for authored tool/build/action buttons.
- `KitLabel` for themed labels.
- `KitLabelValue` for dense readouts.
- `KitToast`, `KitTooltip`, and `KitSpeechBubble` for message surfaces that resize from content.

Avoid runtime construction for normal game HUDs. Runtime fallback generation should be used for quick prototypes, debug tools, and smoke tests only.

## Verification

Run these after changing the toolkit:

```powershell
dotnet build Beep.Godot.csproj --no-restore
powershell -ExecutionPolicy Bypass -File tests/runtime_smoke.ps1 -GodotCommand 'H:\dev\Godot\Godot_v4.7-stable_mono_win64.exe'
powershell -ExecutionPolicy Bypass -File tests/render_scene_capture.ps1 -GodotCommand 'H:\dev\Godot\Godot_v4.7-stable_mono_win64.exe' -ScenePath 'res://addons/beep_game_builder_cs/templates/scenes/kit_browser.tscn' -OutputPath 'res://tmp/kit_browser.png' -Width 1280 -Height 720 -TimeoutSeconds 120
```
