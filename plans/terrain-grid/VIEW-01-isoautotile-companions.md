# VIEW-01 — IsometricAutotile draws its companions through the grid binding

**Type:** fix (view parity: vegetation, relief props, resource icons and the start/survey overlay are missing from one of the four shipped views) · **Area:** `TerrainWorldComponent.Drawing.cs`, `docs/terrain-engine/TerrainWorldComponent.Drawing.md`, `tests/examples/stack_order.gd` · **Status:** Implemented 2026-09-15 · **Effort:** S (1 day) · **Risk:** low (one gate changes; no placement code moves)

## As landed (2026-09-15)

- `Draw()` replaces `flat` with `gridBound = Projection is not TerrainProjection.Isometric` for the
  four companions' visibility and rebuilds. `flat` had no other reader and is gone. `flatTileSize`
  keeps its own switch (VIEW-02). Before the gate changed, the four companions were read to confirm
  they need nothing else under a bound diamond. Each already places and sizes through the grid's
  corners: features `TerrainFeatureRendererComponent.cs:285-294`, relief
  `TerrainReliefRendererComponent.cs:226-236`, resources `TerrainResourceRendererComponent.cs:254-260`,
  overlay `TerrainMapOverlayComponent.cs:171-174,304-326`.
- **The prerequisite, fixed first.** `tests/examples/stack_order.gd` now waits for
  `IsGenerating` to clear and asserts each `GenerationFinished` succeeded. The fixed 15-frame wait
  did not only read an unbuilt lab: the lab ignores `Generate()` while a build runs, so every later
  "view switch" was dropped. Before the fix, 13 checks failed in 4 s. After it, one check fails: the
  pre-existing `terrain_road_tiles_demo.tscn` scene rule, unrelated. The rows also now **fail** on a
  missing renderer instead of skipping it, which had let a renamed node pass unmeasured.
- **Guards as built.** Both live in `stack_order.gd` on the real lab, not in a separate Tiny-world
  probe:
  - a fourth row (view 3), with `IsoAutotile`, `Features`, `RockObjects` and `MapOverlay` on and
    `Iso`, `IsoFeatures`, `Splat` and `TileRenderer` off, added to all four rows;
  - `autotile_companions()`: the grid is bound to the autotile layer, every companion's z is above the
    autotile ground, at least 20 trees are stamped, and every tree's ground anchor falls (via
    `Grid.WorldToCell`) in a cell whose `terrain_feature` is set. Result: 180 trees, 0 elsewhere.
  - **Mutations run together:** `_features.Visible` restored to the old `flat` expression, and the
    feature renderer's grid placement block disabled so it places from its square `TileSize`. Result:
    "Isometric tiles: Features is drawn" failed, and "every tree stands on a diamond the generator
    wooded" failed with 180 of 180 elsewhere. Reverted; the guard passes again.
- `terrain_lab_tile_views_probe` (which exercises IsometricAutotile retention) passes.
- **Order:** this item and VIEW-02 both state VIEW-01 lands first. The index's suggested order had
  them swapped, and they were implemented VIEW-01 then VIEW-02.

## Gap

`TerrainWorldComponent.Draw()` decides which companion renderers a projection shows with one boolean, `flat = Projection is not (Isometric or IsometricAutotile)` (`TerrainWorldComponent.Drawing.cs:36`). Every flat companion follows it: vegetation `_features.Visible = flat` (`:105`), relief props (`:163`), resource icons (`:170`), the map overlay with its start rings and survey (`:180`), and the companion rebuilds are gated the same way (`:195-229`). The block view gets its own vegetation instead — `_isometricFeatures.Visible = Projection == Isometric` (`:49`). So under **IsometricAutotile** nothing draws but the ground and, when a `TerrainStructureLayerComponent` is wired, structures (`TerrainWorldComponent.Structures.cs:28-30` shows Tiles and IsometricAutotile as the two views that draw them). No trees, no rocks, no resource icons, no start markers — one of the four shipped views is a bare map, and `tests/examples/stack_order.gd:74-81` pins the expected companion table for three views only, so no guard notices.

The gate is wrong, not the geometry. The flat companions already place through the gameplay grid: `TerrainFeatureRendererComponent.cs:283-294` takes the cell centre and corners from `GridProjectionComponent.CellToWorld/CellCorners`, the relief renderer does the same (`TerrainReliefRendererComponent.cs:226-236`), resource icons at `TerrainResourceRendererComponent.cs:254-260`, and the overlay walks `CellOutline` (`TerrainMapOverlayComponent.cs:315-326`). `GridProjectionComponent.CellToWorld` answers from the bound native layer's `MapToLocal` (`GridProjectionComponent.cs:249-258`) and its corners handle the isometric layout (`:351-354`). `BindGameplayGrid` (`TerrainWorldComponent.Drawing.cs:245-262`) already binds the autotile view's `GetTerrainLayer()` (`:257`) as the grid's `TileMapLayerPath` (`:261`), and DUP-01's investigation recorded that the binding runs between the projection views and the flat overlays, i.e. before the companion rebuilds. The sprite anchor the flat renderer uses, `SpriteAnchor = (0.5, 0.92)` (`TerrainFeatureRendererComponent.cs:57`), puts the trunk base at the cell centre — which for a bound diamond cell is the diamond's centre. Everything needed to draw the companions under IsometricAutotile exists; `flat` hides it.

## Design

1. **Two facts instead of one boolean.** `Draw()` splits `flat` (`:36`) into `gridBound = Projection is not TerrainProjection.Isometric` — true for Painted, Tiles and IsometricAutotile — and uses it for the four flat companions' visibility (`:105,163,170,180`) and their rebuilds (`:195-229`). The block view keeps `_isometricFeatures` (`:49`) because its props stand on a stacked surface, not on the grid. The only place the old meaning "top-down cell size" survives is `flatTileSize` (`:58-63`), which VIEW-02 removes; until then it keeps its own switch and does not read `gridBound`.
2. **Order stays.** `BindGameplayGrid` runs before the companion rebuilds (DUP-01's note on the two-phase order); the autotile layer is bound at `:257`, so the companions rebuild against isometric cell geometry. No placement code changes.
3. **Z-order through the shared stack.** The companions already draw above the ground through `TerrainLayers` (the flat feature renderer's own comment at `TerrainFeatureRendererComponent.cs:69-77` records why it has no z export). The guard below asserts the z-span, it is not assumed.
4. **Elevated packs.** A `LibraryPack` with elevation profiles lifts tiles; the grid still binds the layer as flat until VIEW-09 binds it as an elevated surface. Props on a raised tile sit at the tile's `MapToLocal` until then — documented, not hidden.
5. **Documentation.** `docs/terrain-engine/TerrainWorldComponent.Drawing.md` gains the projection × companion table (which renderer is visible and rebuilt under each of the four projections), replacing the prose that describes `flat`.

## Guards (fail first)

- `tests/examples/stack_order.gd:74-81` gains a fourth row for view 3 (IsometricAutotile): `IsoAutotile`, `Features`, `RockObjects` and `MapOverlay` visible and drawn (`StampCount > 0` on the feature renderer, the overlay's start-marker count > 0), `Iso` and `IsoFeatures` hidden, and every companion's z above the autotile ground layer. Prerequisite recorded 2026-09-15: the probe waits 15 headless frames after `Generate()` while generation is asynchronous, so it reads an unbuilt lab today and can neither pass nor fail on this row — it awaits generation completion first, and that fix is made to fail (shorten the wait → the existing rows fail again) before the new row is trusted. **Mutation:** restore `flat` for `_features.Visible` (`:105`) → the view-3 row fails on `Features` hidden.
- Geometry, headless: a Tiny world drawn under IsometricAutotile, twenty wooded cells → every stamp anchor (`GetStampCenters` as `tests/terrain_feature_grid_probe.gd:54` reads it) lies inside the polygon `Grid.CellCorners(cell)` returns for its cell. **Mutation:** place from the renderer's square `TileSize` fallback instead of `CellToWorld` → anchors of off-diagonal cells fall outside their diamonds.
- Existing probes stay green: `terrain_feature_grid`, `terrain_view_grid_probe`, `terrain_lab_tile_views_probe` (its retention checks exercise the autotile view), `terrain_world_iso_publication_probe`.

## Dependencies / collisions

DUP-01's partial outcome leaves `Draw()` as explicit per-renderer orchestration on purpose; this is a surgical edit of that block, not a restructure. VIEW-02 (cell geometry owner) removes `flatTileSize` afterwards; VIEW-09 (pack elevation) binds the autotile view as an elevated surface and makes props follow raised tiles. VIEW-13 wires the resource-icon renderer into the lab and adds the per-view parity probe that keeps this row honest for every projection. Collision: `TerrainWorldComponent.Drawing.cs` is also touched by the streaming session (ENH-07) — land this first, it is small.

## Out of scope

Isometric art for the autotile companions (the flat sprite sheets are drawn as they are); prop elevation under packs (VIEW-09); the block view's own feature renderer (VIEW-03 unifies the stamp math); structures (already drawn under this view).
