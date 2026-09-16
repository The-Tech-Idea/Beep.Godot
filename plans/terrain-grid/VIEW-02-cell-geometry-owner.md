# VIEW-02 — One owner of cell geometry: the grid binding (and collision merges again)

**Type:** duplication fix (ten places answer "how big is a cell"; the answer that is read disagrees with the one that is drawn) · **Area:** `GridProjectionComponent`, `TerrainCollisionComponent`, `TerrainSurfaceStreamingComponent`, `TerrainPropResidency`, `TerrainWorldComponent.Drawing.cs`, the four flat companion renderers · **Status:** Implemented 2026-09-15 · **Effort:** S (1–2 days) · **Risk:** low–medium (collision shape counts change on purpose; every probe that reads them is listed)

## As landed (2026-09-15)

Consumers were mapped before any change (grid `TileSize`/`EffectiveTileSize` reads, data-layer
and companion `TileSize` reads and writes, `FlatViewTileSize`/extent callers, worlds per scene,
the affine-run rule, collision shape-count tests, `GeometryChanged` emitters, residency constructions).

1. **`EffectiveTileSize` answers from the binding**, in grid-local units: the elevated surface's
   `CellSize` or the native layer's `TileSet.TileSize`, carried through that surface's transform
   relative to the grid. The export answers only for an unbound grid. An unresolvable binding
   reports **zero**, matching `GridBuildStageVisualComponent`'s existing no-geometry convention
   and the way `CellToWorld` reports NaN. The grid's own unbound math uses a private `ManualTileSize`.
   **Found on the way:** `GeometryChanged` does not fire when a bound layer's TileSet changes
   (only `BindGameplayGrid` and the elevated surface's `SurfaceRebuilt` fire it). No design step
   required it and it is recorded, not changed.
2. **One affine-run rule:** `GridProjectionComponent.HasAffineCellRuns(TileSet)` (moved from
   `TerrainSurfaceStreamingComponent.HasExactChunkOutline`, which is gone) plus `CellsFormAffineRuns`.
   **Deviation from design 3:** the moved rule already accepted diamond-down isometric TileSets,
   and a diamond-down run's outline from its corner cells is exact. So collision merges under
   IsometricAutotile too, not only top-down. The probe proves exactness: every cell centre in the
   run collides and no neighbouring cell does.
3. **Collision:** `merge = _grid.CellsFormAffineRuns`.
4. **`Draw()` owns no tile size:**
   - `flatTileSize`, its data-layer write and rebuild clause, the four companion writes and
     `FlatViewTileSize` are gone.
   - `PreviewExtent`/`StartPositionView` read the active flat view's `GetTerrainLayer()`: the layer
     the grid binds, whose TileSet the surface builds from its own export. They report an empty
     rect or zero when the projection's renderer is not wired, instead of the painted size or 64.
   - `BindGameplayGrid` shares the same `FlatTerrainLayer()`.
5. **Companions read the grid.** They already placed and sized through the grid's corners. Their
   `TileSize` is now documented as the no-grid value. `TerrainPropResidency.CellPixels` uses
   `EffectiveTileSize`. The only shipped world with companions and no grid,
   `terrain_splat_demo.tscn`, gained `World/Grid` and `GridPath`. `terrain_grid_playground.tscn`,
   which inherits it and used to add that node itself, now overrides nothing but its other wiring.
6. **Other readers of the raw export:** `actor_lab.gd` read `$Grid.TileSize` for follow distance
   (now `EffectiveTileSize`) and re-derived map framing from it (now `$World.WorldExtent()`, the
   framing owner).

**Guards, each run against the unfixed code first:**
- `GridProjectionSmoke` bound case: 96×48 layer → `(96,48)`; the layer scaled (0.5, 2) →
  `(48,96)`, agreeing with its `CellCorners`; unresolved → zero; unbound → export. Unfixed: failed
  at the first check.
- `terrain_feature_streaming_probe.gd`: grid bound to a 96×48 layer, zoom 0.02, so cells are
  1.92 px against a 1.5 px cutoff; detail must not be suppressed. Unfixed, both new checks failed
  ("reported (64, 64)"; "measured the grid's manual TileSize").
- `terrain_collision_probe.gd`: 32×32 water run on square / diamond-down / stacked-isometric
  native layers → 1 / 1 / 1024 shapes, every cell centre hit, six neighbours missed. With the old
  predicate: "square layer built 1024 water shapes for one 32x32 run, expected 1".
- `terrain_collision_chunks`, `_budget`, `_readiness` and `terrain_world_collision_publication`
  pass unchanged. Their asserted counts are on unbound grids or are merge-independent. None
  needed re-recording.

**Regression sweep after landing:**
- `run_terrain_integration.ps1`: 61 passed, 11 failed. Every failure is on the pre-existing list,
  failing at the same line with the same message: `prop_sizing :40`, `lab_grid`/`lab_capture :14`,
  `ground_cover`, `lab_styles :28`, `art_styles`/`style_beach` (Splat not published), coast cell
  (21,3), `IsoSeabed` missing, and the headless lake-bank finalizer. The view-grid, world live
  source, collision, playground and lab tile-views probes pass.
- The 15 terrain guards: 11 pass. `stack_order` fails only the road-catalog scene rule;
  `addon_selfcontained`, `demo_scenes` and `perf` fail exactly as recorded.
- `terrain_splat_demo.tscn` with its new grid builds 5760 drawn items.

## Gap

**Ten owners of one fact.** The surfaces each declare their own cell size — Painted `TileSize` (`TerrainPaintedRendererComponent.cs:56`), Tiles `AtlasTileSize` (`TerrainTileRendererComponent.cs:58`), the block view `CellSize` (`TerrainIsometricRendererComponent.cs:53`), the autotile view's layer `TileSet.TileSize` (`TerrainIsometricAutotileRendererComponent.cs:155`) — which is right: a surface owns its geometry. But four companions declare a square `TileSize` of their own (`TerrainFeatureRendererComponent.cs:37`, `TerrainReliefRendererComponent.cs:37`, `TerrainResourceRendererComponent.cs:69`, `TerrainMapOverlayComponent.cs:27`), `GridProjectionComponent` has a manual `TileSize` export, and `TerrainWorldComponent.Draw()` computes `flatTileSize` from whichever flat renderer is active (`TerrainWorldComponent.Drawing.cs:58-63`), pushes it into `TerrainDataLayersComponent.TileSize` (`:76,80-81`) and exposes it again as `FlatViewTileSize` (`:330-332`) and through `PreviewExtent` (`:311-328`). The data layers' `TileSize` is documented as the size of their *metadata atlas tiles* — "logical cell queries do not depend on the view's projection" (`TerrainDataLayersComponent.cs:41-44`) — so a view fact is being written into a data fact.

**The grid gives two answers.** `GridProjectionComponent.EffectiveTileSize` returns the manual export (`GridProjectionComponent.cs:240-242`) even when a native layer is bound, while `CellToWorld` on the same component answers from the bound layer's `MapToLocal` (`:249-258`). `TerrainPropResidency.CellPixels` sizes its chunk windows from `_grid?.TileSize ?? Vector2.One * _tile` (`TerrainPropResidency.cs:122-128`) — the export, not the layer — so a renderer bound to a 96×48 layer computes residency from the wrong pixel extents.

**Collision never merges.** `TerrainCollisionComponent` merges same-class cell runs into one `ConvexPolygonShape2D` only when `_grid.TileMapLayerPath.IsEmpty && _grid.ElevatedTerrainPath.IsEmpty` (`TerrainCollisionComponent.cs:242`); `BindGameplayGrid` fills one of those two paths under every terrain projection (`TerrainWorldComponent.Drawing.cs:245-262`, the layer at `:253-261`). So under a terrain view the merge is dead code and every cell gets its own shape (`:266-274`, `AddShape :290-320`): 10,240 shapes on a Huge 128×80 map. The predicate the merge needs — "are cells affine runs of the same rectangle" — already exists elsewhere: `TerrainSurfaceStreamingComponent.HasExactChunkOutline(TileSet)` (`TerrainSurfaceStreamingComponent.cs:229-232`) decides it for streaming quads. A second owner of the same rule, one of them unreachable.

## Design

1. **`GridProjectionComponent.EffectiveTileSize` answers from the binding.** Bound elevated surface → its cell size; bound native layer → `TileSet.TileSize`; nothing bound → the `TileSize` export, documented as the no-binding value. `GeometryChanged` already fires when the binding moves (the relief and resource renderers subscribe to it, per DUP-01's note); it fires for a TileSet size change too.
2. **`HasAffineCellRuns` moves onto the grid.** `HasExactChunkOutline`'s rule becomes `GridProjectionComponent.HasAffineCellRuns(TileSet?)` (static, one implementation) plus an instance property over the bound layer; the streaming component calls the static. Unbound/top-down grids are affine; an isometric or offset-axis native layer is not.
3. **Collision merges when geometry allows it:** `merge = _grid.ElevatedTerrainPath.IsEmpty && _grid.HasAffineCellRuns` (`:242`). The merged rectangle's corners still come from `CellCorners` of the run's end cells (`:270-272`), so a native top-down layer's transform is honoured; an isometric binding keeps one diamond per cell as today.
4. **`Draw()` stops owning a tile size.** `flatTileSize` (`:58-63`), the write into `_dataLayers.TileSize` (`:76,80-81`) and `FlatViewTileSize` (`:330-332`) go; `PreviewExtent` (`:311-328`) reads the active renderer's `GetTerrainLayer().TileSet.TileSize` the way the autotile view's `GridExtent` already does (`TerrainIsometricAutotileRendererComponent.cs:150-163`). The data layers keep their own `TileSize` for what it is — their atlas tile size.
5. **Companions read the grid.** The four `TileSize` exports become the documented no-grid fallback; when a grid is bound, sprite sizes are `TerrainPropSizing.SizeInCells × EffectiveTileSize` and placement stays through `CellToWorld/CellCorners`. `TerrainPropResidency.CellPixels` (`:122-128`) uses `EffectiveTileSize`.
6. **Surfaces keep their exports** (`:56`, `:58`, `:53`): the surface owns its geometry, the binding reports it, everything else reads the binding. That is the one-owner shape, not "delete every TileSize".

## Guards (fail first)

- `tests/GridProjectionSmoke.cs` (ENH-08's probe) gains a bound case: a grid whose `TileMapLayerPath` names a native layer with `TileSet.TileSize = (96, 48)` reports `EffectiveTileSize == (96, 48)` and its `CellCorners` span matches the layer's tile. **Mutation:** return the export → `(48, 32)` from the probe's own grid.
- `tests/terrain_collision_probe.gd` (already binds a native 96×48 layer, `:29-38`) gains a top-down case: a square-tile native layer over a 32×32 all-grass chunk → `ShapeCount` (`TerrainCollisionComponent.cs:322`) equals the chunk count (one merged rectangle per chunk); the existing isometric case keeps one shape per cell. **Mutation:** restore the old `merge` predicate → 1024 shapes.
- Residency, headless: a feature renderer bound to a 96×48 native layer reports the resident chunk set the camera window at 96×48-pixel cells implies (`terrain_feature_streaming` fixture). **Mutation:** `CellPixels` back to `_tile` → a different resident set.
- `tests/terrain_view_grid_probe.gd:86-94` stays green (`PreviewExtent` of 32×32 at `AtlasTileSize (96,48)` is still `(3072, 1536)` — now read from the layer); `terrain_collision_chunks`, `terrain_collision_budget`, `terrain_collision_readiness`, `terrain_world_collision_publication` re-run because shape counts change by design; their expected counts are re-recorded once and the reason written beside them.

## Dependencies / collisions

VIEW-01 lands first (it reads `flat` one last time). ENH-08 (projection hot paths, landed) owns `GridProjectionSmoke`. DUP-08 (grid geometry helpers) — the affine-runs rule is a geometry helper and belongs beside them. Collision: `TerrainPropResidency` and `TerrainSurfaceStreamingComponent` are the streaming session's files (ENH-05/06/07) — the two moves here are small and named; sequence with that session.

## Out of scope

Residency algorithm changes (ENH-06); chunked tile and block streaming (ENH-07); collision layer masks and budgets; the data layers' materialised TileSets.
