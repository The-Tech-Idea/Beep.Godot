# ENH-01 — Cell change notifications say what changed and where

**Type:** enhancement (huge-world performance) · **Area:** `GridCellDataComponent` (+`.Eviction`, `.Snapshots`, `.Publication`), all 12 `CellsChanged` listeners · **Status:** proposed 2026-09-08 · **Effort:** M–L (3–4 days incl. listener ports) · **Risk:** medium

## Finding

`GridCellDataComponent` has one bulk signal, `CellsChanged()`, with no payload. It is emitted for three unrelated events:

| Event | Site | Also bumps |
|---|---|---|
| A chunk **evicted** to the archive (content unchanged, only residency) | `GridCellDataComponent.Eviction.cs:50-62` `TryEvictChunk` | `TerrainRevision++`, `NavigationRevision++` |
| A chunk **reloaded** from the archive (content identical to when it left) | `.Snapshots.cs:236-245` `PublishRecords` | `MarkNavigationChanged()` |
| A real **edit** of many cells (`FillTerrain`, `LoadCells`) | `.cs` mutators | `TerrainRevision++` |

Twelve listeners subscribe (`CellsChanged +=` scan): the painted renderer and `TerrainCollisionComponent` are chunk-aware (this session's work); the other ten treat every signal as "the map changed":

- `TerrainFeatureRendererComponent`, `TerrainReliefRendererComponent`, `TerrainIsometricFeatureRendererComponent` → `Rebuild()` → `ResetStreaming(); BeginStreaming()` (`TerrainFeatureRendererComponent.cs:186`): **every resident prop chunk is dropped and re-scattered** when an unrelated chunk far away is evicted.
- `TerrainIsometricRendererComponent` → full block rebuild (5 layers).
- `TerrainTransitionLayerComponent` → full 15-piece dual-grid refresh.
- `TerrainTileRendererComponent` → coast rebuild + `TerrainShaderSurface.Fill` verification.
- `TerrainIsometricAutotileRendererComponent` → repaint (guarded by `TerrainRevision`, which eviction bumps, so the guard never holds).
- `GridTileMapLayerBridgeComponent` → `GetCells()` marshal of every cell.
- `GridCellOverlayComponent` → redraw all stored cells.
- `ui/GridMinimapComponent` → whole-map `BakeTerrain`.

Measured in this session before the painted fix: a no-change rebuild at edge 240 cost 4.658 ms; the same eviction storm hits all ten of the above on a huge streamed map, every time an actor walks out of a chunk's pin radius.

## Design

Replace the payload-less signal with a typed one and keep the revisions honest:

```csharp
public enum TerrainChangeKind : byte { Residency = 1, Terrain = 2, Navigation = 4, Gameplay = 8 }  // flags

[Signal] public delegate void CellsChangedEventHandler(int kind, Godot.Collections.Array<Vector2I> chunks);
```

- **Eviction** emits `Residency` with the one chunk and bumps **neither** `TerrainRevision` nor `NavigationRevision` — residency is not content. Chunk revisions already exist (`GridCellDataComponent.Revisions`) and stay untouched by eviction.
- **Reload** compares the decoded records with the archived revision (`IsChunkSaveCurrent` already exists on the archive); an identical reload emits `Residency` only. A reload whose content differs (edited while archived — impossible today, but the archive's `_readChanged` path allows it) emits `Terrain | Navigation` for that chunk.
- **Edits** emit `Terrain` (kind/elevation/shore keys), `Navigation` (blocked/relief/ramp keys), `Gameplay` (crop/tilled/watered/metadata) per the key classification already present at `GridCellDataComponent.cs:291-294` — with the affected chunk list.

Listeners (through DUP-01's `OnCellsChanged(kind, chunks)` hook) then do the minimum:

| Listener | Residency | Terrain (chunks) | Navigation | Gameplay |
|---|---|---|---|---|
| Painted, Collision | already chunk-scoped | chunk update | — | — |
| Feature / Relief / Iso-feature props | **nothing** (resident stamps stay; archived-out chunks are simply not resident) | rebuild those chunks' stamps only | — | — |
| Isometric blocks, Autotile, Transition, Tile | nothing | rebuild the tiles of those chunks (+1 cell halo for autotile/dual-grid) | — | — |
| TileMapLayer bridge | nothing | `RefreshCell` for the chunks' cells | — | — |
| Overlay | nothing (culling in ENH-09) | redraw | redraw | redraw |
| Minimap | nothing | re-bake those chunks' pixels (ENH-13) | — | — |
| Navigation | nothing | chunk-scoped invalidation (ENH-03) | chunk-scoped | — |
| Archive `_readChanged` | ignore Residency; abort reload only when its own chunk is in `chunks` (ENH-04) | | | |

`CellChanged(x, y)` (single cell) gains the same `kind` argument (ENH-02).

## Steps

1. Add the typed signal beside the old one; classify every emitter; make eviction/identical-reload emit `Residency` only and stop bumping the global revisions.
2. Port listeners via the DUP-01 hook, most expensive first (props, isometric, transition, bridge, minimap).
3. Remove the payload-less signal (compiler sweep; GDScript callers in `templates/**/*.gd` found by Python scan and updated).

## Guards (fail first)

- `terrain_painted_archive_probe.gd` already asserts eviction/reload keep the painted textures; extend to the isometric, tile and feature renderers: after `EvictSavedChunk(0,0)`, assert the isometric layer's `GetUsedCellsById` count for chunk (1,0) is unchanged and the feature renderer's `ResidentChunkCount` did not drop. **Mutation:** re-add `TerrainRevision++` in `TryEvictChunk` → the autotile/iso assertions fail; make props `ResetStreaming` on Residency → resident count assertion fails.
- Unit probe on the classifier: `SetTerrainKind` → `Terrain`; `SetFlags(Blocked)` → `Navigation`; `Till`/`Water` → `Gameplay`; eviction → `Residency`, `TerrainRevision` unchanged.
- Timing probe (extends this session's benchmark): eviction of one chunk at edge 240 with all renderers attached costs < 0.5 ms main-thread (today's painted-only figure was 4.658 ms before the fix).

## Dependencies / collisions

Requires DUP-01 (the hook). **Directly overlaps** the streaming work in `ecs/grid/` by the other session — the emitter changes in `GridCellDataComponent` must be coordinated. ENH-02/03/04/06/09/13 build on this signal.

## Out of scope

Navigation search algorithm (FEAT-01), painted renderer internals (done this session).
