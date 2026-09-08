# ENH-09 — Cell overlay culls to the view; TileMapLayer bridge stops marshalling the map

**Type:** enhancement (per-frame cost on large maps) · **Area:** `GridCellOverlayComponent`, `GridTileMapLayerBridgeComponent` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

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
