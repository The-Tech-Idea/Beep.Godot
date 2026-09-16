# VIEW-13 — One resource drawer; the lab wires every companion; per-view parity probe

**Type:** duplication fix (D7) + guard · **Area:** `TerrainMapOverlayComponent`, `TerrainResourceRendererComponent`, `terrain_generator_lab.tscn`, `TerrainLabComponent.SetDiagnostics`, new `tests/terrain_view_parity_probe.gd`, `tests/terrain_lab_tile_views_probe.gd` · **Status:** Implemented 2026-09-15 · **Effort:** S (1–2 days) · **Risk:** low (deletions in the overlay; the lab gains two nodes; one new probe)

## As landed (2026-09-15)

Every reader of the removed members was mapped first. Deviations from the design, each for a
reason found on reading:

- **Diagnostics toggles a layer, not the renderer.** `Draw()` sets `_resources.Visible` per
  projection on every draw, so a `Visible` toggle from `SetDiagnostics` would be overwritten by the
  next view switch. The icon renderer lives at `Preview/Diagnostics/Resources`. The lab panel gains
  `DiagnosticsLayerPath` (its consumer is `SetDiagnostics`) and shows or hides that layer (Godot
  visibility inheritance). The layer is hidden by default, so the lab's default look and every
  pixel-measuring lab probe are unchanged.
- **`ISerializationListener` stays on the overlay.** It also releases and rebinds the grid,
  prospecting and store events across an assembly reload; only the live-resource binding lines went.
- **Two probes the design missed** read the removed members and were updated:
  `terrain_lab_styles_probe.gd` (now asserts the icon layer's visibility) and
  `terrain_resource_reload_probe.gd` (the overlay no longer holds a subtree binding).
  `terrain_live_resource_view_probe.gd` checks the icon renderer only, including recovery after a
  restored root. `terrain_survey_overlay_probe.gd` drops `ShowResources`.
- **`TerrainResourceStage.CategoryOf` lost its only caller** (the removed `ColourFor`). Resource
  categories are authored catalog data with a real presentation consumer to come (ENH-13 minimap
  tints), so it is marked in place, not deleted.
- **`StartMarkerCount`** was added to the overlay; `_startMarkers` was private and the probe needs
  the count. **`ResourceAtCell` is internal**, so the probe counts expected icons through
  `TerrainGeneratorComponent.ResourceAt`/`LiquidResourceAt`.
- **The autotile sea row** is not asserted; it waits for VIEW-04.

**Guards:**
- `tests/terrain_view_parity_probe.gd` (headless, registered in `run_terrain_integration.ps1`).
  Against the unchanged lab it failed 11 checks: no `Resources` renderer (all 4 views), no
  `StartMarkerCount` (3 views), no collision (4). Landed: 26 icons for 26 resource cells and 6 rings
  for 6 starts in every grid-bound view; collision ready at 54 shapes (Painted, Tiles, IA, merged
  per VIEW-02) and 646 (block Isometric).
  **Mutations:** Game tiles hiding the icons failed "Game tiles: Resources is drawn". Skipping
  `collision.RequestRebuild()` failed every build ("Terrain collision sources changed during
  publication") and every collision row.
- Contract pin: `ShowResources`, `ResourceRootPath`, `ResourceRadiusTiles`, `ResourceMarker`,
  `DrawResources(`, `ColourFor(` and `TerrainResourceViewBinding` must be absent from the overlay,
  anchored at a word start (the survey's `UndergroundColourFor(` tripped an unanchored first
  version on correct code). `StartMarkerCount` must be present. The lab must wire
  `ResourceRendererPath`, `CollisionPath` and `DiagnosticsLayerPath`. **Mutations:** re-adding
  `ShowResources` and removing the lab's `CollisionPath` each failed the scan (17 vs the 16
  pre-existing).
- Still green: `terrain_live_resource_view`, `terrain_survey_overlay`, `terrain_resource_reload`,
  rendered `lab_tile_views` (its warning check passes with the new nodes) and `stack_order` (only
  the pre-existing road-catalog rule). `terrain_lab_grid` fails at its pre-existing `:14`.

## Gap

1. **Two drawers of one fact.** Per-cell resources are drawn by `TerrainResourceRendererComponent` as icons from the resource-set sheet (`TerrainResourceRendererComponent.cs:212-269`, backplate `:276-282`) and by `TerrainMapOverlayComponent` as coloured discs (`TerrainMapOverlayComponent.cs:177-234`, `DrawResources` `:344-352`, category colours `ColourFor` `:378-389`). Both bind the same live subtree through `TerrainResourceViewBinding` (`:155`, `:341-342` / `:70`, `:223-234`), both export `ResourceRootPath` (`:63` / `:22`), both have bounds, `TileSize` and `GridPath`; the overlay adds `ShowResources` (`:30`) and `ResourceRadiusTiles` (`:43`). `TerrainWorldComponent` wires both (`MapOverlayPath` `:92`, `ResourceRendererPath` `:99`) and `Draw` rebuilds both under every grid-bound projection (Painted, Tiles and, since VIEW-01, IsometricAutotile), so a game scene with both draws discs under icons at the same cells; a scene with only the overlay shows a second classification (Strategic/Luxury/Bonus hues) that the icon sheet does not.
2. **The lab exercises the overlay and nothing else.** `terrain_generator_lab.tscn:3-28` lists every script it instances: `map_overlay` is there, `TerrainResourceRendererComponent` and `TerrainCollisionComponent` are not. `TerrainLabComponent.SetDiagnostics` (`TerrainLabComponent.cs:240-249`) toggles `overlay.ShowResources` — the lab's resources *are* the discs, so the icon renderer is never seen switching views, and native collision never builds in the one scene that wires all four projections.
3. **Per-view parity is unguarded.** `tests/examples/stack_order.gd` checks eight node names for all four views since VIEW-01, but not resource icons, the sea, collision or start rings; `terrain_lab_tile_views_probe.gd:18` `OVERLAY_PATHS` lists four companions; nothing asserts, per projection, that the icons were drawn (`IconCount`), the start rings, the sea node, which layer the gameplay grid bound to, or that collision is ready. DUP-01's `renderer_reporting` probe covers "reports when it cannot draw", not "draws what this projection should".

## Design

1. **The overlay draws starts and survey only.** Delete `ShowResources` (`:30`), `ResourceRootPath` (`:22`), `ResourceRadiusTiles` (`:43`), `_liveResources` (`:70`) and its binding, `ResourceMarker` (`:59`), `_resourceMarkers`/`ResourceMarkerCount` (`:71`), the resource scan (`:191-204`) and live-entries loop (`:223-234`), `DrawResources` (`:344-352`) and `ColourFor` (`:378-389`); `RequiresGenerator` (`:68-69`) becomes `ShowStartPositions || ShowUndergroundResources`. The survey patches (`ShowUndergroundResources`, `:206-218`, `UndergroundColourFor`) stay: deposits gated by prospecting are a different fact with no second drawer. The icon renderer is the one resource drawer and gains nothing (rule 6).
2. **The lab wires every companion.** `Preview/Resources` (`TerrainResourceRendererComponent`, `TerrainGeneratorPath`, `RefreshOnReady = false`, `IconSource = FollowGenerator` so the resource-set axis drives the sheet, `:79`) and `Collision` (`TerrainCollisionComponent`, `RefreshOnReady = false`) wired to `ResourceRendererPath` and `CollisionPath`; `Draw` then rebuilds them on every view switch (`:166-171`, `:187-194`, `:203-210`). `SetDiagnostics` toggles `ShowStartPositions`, `ShowUndergroundResources` and the icon renderer's `Visible` (through `_world.ResourceRendererPath`) — in the lab, icons are diagnostics.
3. **Scenes.** `terrain_splat_demo.tscn:111-117` already uses the icon renderer; every scene under `templates/scenes/` and `tests/` that sets a deleted overlay export is read and the line removed (the compiler is the sweep for C#; the scene list goes in the outcome).
4. **`tests/terrain_view_parity_probe.gd`** (headless): load the lab; for each projection 0..3 generate a fixed Tiny seed and wait for `WorldBuilt`; assert (a) the visible-in-tree companion set — Painted: `Splat`, `Features`, `RockObjects`, `Resources`, `MapOverlay`; Game tiles: `TileRenderer`, `Features`, `RockObjects`, `Resources`, `MapOverlay`; Isometric: `Iso`, `IsoFeatures`; Isometric tiles: `IsoAutotile`, `Features`, `RockObjects`, `Resources`, `MapOverlay` (VIEW-01 has landed); (b) `Features.StampCount > 0`, `Resources.IconCount` equals the number of cells with a resource (from the generator's `ResourceAtCell`), overlay start markers equal `StartPositionCount`; (c) the sea node for the view (`Splat/SplatSurface` material shaded, `TileRenderer/TileWater`, `Iso/IsoWater` visible, the autotile sea per VIEW-04); (d) `Grid.TileMapLayerPath`/`ElevatedTerrainPath` resolve to the active renderer's layer; (e) `Collision.IsReady`. `OVERLAY_PATHS` (`terrain_lab_tile_views_probe.gd:18`) gains `Preview/Resources`; the probe joins `tests/run_terrain_integration.ps1`'s headless list.
5. Docs: `docs/terrain-engine/TerrainMapOverlayComponent.md`, `TerrainResourceRendererComponent.md`, `TerrainLabComponent.md`; the projection × companion table VIEW-01 adds to `TerrainWorldComponent.Drawing.md` is the probe's specification.

## Guards (fail first)

- The parity probe. **Mutation:** drop `_resources.Visible = flat` for Game tiles in `Draw()` (`:170`) → (a)/(b) fail. **Mutation:** skip `collision.RequestRebuild()` (`:192`) → (e) fails.
- `terrain_live_resource_view_probe.gd` and `terrain_survey_overlay_probe.gd` (gate `:51-52`): whichever assertions read `ResourceMarkerCount` move to `IconCount` on the icon renderer; the survey assertions are unchanged. Both green.
- Pin: `ResourceMarker`, `ColourFor(`, `DrawResources(` and `ShowResources` absent from `TerrainMapOverlayComponent.cs`; `ResourceRendererPath` and `CollisionPath` assigned in `terrain_generator_lab.tscn`. **Mutation:** re-add `ShowResources` → fails.
- `stack_order.gd` (extended by VIEW-01 with a view-3 row) and `lab_tile_views` green with the fifth overlay path.

## Dependencies / collisions

VIEW-01 (the Isometric-tiles row of the probe is written against its outcome; whichever lands second updates the row), VIEW-04 (the autotile sea row), ENH-09 (the overlay's remaining drawers get the culling window), DUP-01's `renderer_reporting` probe (complement, not replaced). **Collision:** `terrain_generator_lab.tscn` and `TerrainLabComponent` are also edited by the lab session — coordinate; `Drawing.cs` by VIEW-01/02/10.

## Out of scope

Icon art; resource category tints on the minimap (ENH-13 owns the grid minimap); prospecting rules behind the survey view; HUD resource legends.
