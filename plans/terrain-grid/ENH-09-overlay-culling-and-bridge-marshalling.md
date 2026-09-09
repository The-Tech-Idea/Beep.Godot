# ENH-09 — Cell overlay culls to the view; TileMapLayer bridge stops marshalling the map

**Type:** enhancement (per-frame cost on large maps) · **Area:** `GridCellOverlayComponent`, `GridTileMapLayerBridgeComponent` · **Status:** **IMPLEMENTED 2026-09-09** · **Effort:** S (1 day) · **Risk:** low

## Outcome (2026-09-09)

- **Overlay culls to the camera.** `GridProjectionComponent.TryGetVisibleCellRect(out rect, margin)` maps the viewport's visible rect to a cell rectangle (world corners -> `WorldToCell`, one-cell margin) and returns false - draw everything - when there is no viewport or a corner does not resolve (an off-surface elevated corner), so culling never hides a cell it cannot place. `GridCellOverlayComponent._Draw` and the culling guard share one `VisibleCells()` iterator (the "what draws" owner), which skips cells outside the window unless the new `DrawAll` export (default off) is set. Because the shared iterator does the culling, the probe counts exactly what would paint without a render pass, so the guard is **headless** (unlike the pixel probes). The chunk-window iteration and residency-skipping from the design sketch were **not** built - they need the streaming session's chunk-residency API; culling the draw is the guarded win and iterating `EnumerateFlags` (already typed, sparse - only flagged cells) is cheap.
- **Bridge stops marshalling.** `GridTileMapLayerBridgeComponent.Rebuild` iterated `_cells.GetCells()` (a Godot `Array<Dictionary>` of every record) and read the coordinate back with `GridVariantReader`; it now iterates the typed `EnumerateFlags()` (the same stored cells) and paints each coordinate. The per-cell `ResolveReferences` inside `PaintCell`/`AtlasForCell` was left as-is: `EntityComponent.Resolve` is cached-while-valid, so it is a validity check, not a re-resolution, and avoiding it would mean duplicating the atlas logic.

Guards, both proven to fail first: `grid_cell_overlay_probe` seeds one flagged cell inside the window and two far outside and asserts the overlay paints only the inside one (all three under `DrawAll`); the mutation dropping the window filter paints three. The bridge's `GridVariantReader` pin was rewritten to require `EnumerateFlags` and forbid `GetCells`, mutation-proven by block extraction (it sits past the scan's line-180 abort). Build clean; `showcase_interaction` and the full headless smoke (`overlays`, `tilemap-layer-bridge`) stay green.

### Not built (needs the streaming session's API)

Per-chunk overlay iteration and residency-skipping, and redraw scoping to `chunks ∩ visible` on `CellsChanged` - all need the chunk-residency surface owned by the streaming session. The draw cull already keeps repaint work proportional to the view; these are follow-ons.

## Finding

Both are used by `grid_world_2d_iso.tscn` (the reference grid template) and the bridge also by `terrain/grid_world_kit_hud_example.tscn`.

1. **`GridCellOverlayComponent._Draw`** (`:71-78`) iterates **every stored cell** and draws its polygon (fill and/or outline; the outline branch never skips), with no viewport culling and no residency awareness. On a 1024² map with the overlay on, `_Draw` walks a million records per redraw, and ENH-01 shows a redraw is currently triggered by any `CellsChanged`. Phase 2.4 of the tracker made it update at runtime; it still draws the whole map to do so.
2. **`GridTileMapLayerBridgeComponent.Rebuild`** (`:95-105`) calls `cells.GetCells()` — a Godot `Array<Dictionary>` marshal of every record — then per cell `AtlasForCell(record)` → `ResolveReferences()` (re-resolving the cell store and layer per cell). Phase 1.5 fixed the per-cell `UpdateInternals` storm; the marshal remains.

## Design

- **Overlay:** draw only cells inside the camera's cell window (`TerrainCameraWindow` from ENH-07, or the projection's `VisibleCellRect(viewport)`), iterating the store by chunk (`ChunkedCellStore` exposes chunks) and skipping non-resident chunks. Redraw on `CellsChanged` only when `chunks ∩ visible ≠ ∅` (ENH-01 payload). Keep a `DrawAll` export (default off) for tiny debug maps.
- **Bridge:** iterate the typed store (`GridCellDataComponent.EnumerateRecords(chunk)` — typed `CellRecord`, no Dictionary), resolve references once per `Rebuild`, and apply `Terrain` changes per chunk (`RefreshChunk(chunk)` → `SetCell` for that chunk's cells). Above the streaming threshold the bridge follows residency: it paints resident chunks and clears evicted ones (it is a *bridge* to a hand-authored TileMapLayer, so per-cell `SetCell` is the correct mechanism here; the layer stays one because authored scenes reference it by path).

## Guards (fail first)

- Probe: 512×512 map, viewport covering 40×30 cells → `_Draw` issues ≤ (40+2)×(30+2) polygon draws (count via a test hook). **Mutation:** remove the window filter → count = stored cells.
- Probe: bridge `Rebuild` on 128×80 performs zero `GetCells()` calls (pin: `GetCells()` not referenced from the bridge) and a single-cell `SetTerrainKind` triggers exactly one `SetCell` on the layer. Mutation: restore the marshal → pin fails.

## Dependencies / collisions

Depends on ENH-01 (payload) for the change-scoped path; the culling itself is independent. `ecs/grid/` — coordinate; these two files are unlikely to be in the other session's path.

## Out of scope

Overlay styling, the authored TileMapLayer's TileSet.
