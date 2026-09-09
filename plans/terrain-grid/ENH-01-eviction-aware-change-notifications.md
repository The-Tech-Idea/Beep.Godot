# ENH-01 — Cell change notifications say what changed and where

**Type:** enhancement (huge-world performance) · **Area:** `GridCellDataComponent` (+`.Eviction`, `.Snapshots`, `.Publication`), all 12 `CellsChanged` listeners · **Status:** **IMPLEMENTED 2026-09-09** (typed signal, classified emitters, eviction storm removed; per-chunk minimisation investigated 2026-09-09 and confirmed blocked on ENH-03/ENH-13 - see the follow-up note) · **Effort:** M–L (3–4 days incl. listener ports) · **Risk:** medium

## Outcome

`GridCellDataComponent.CellsChanged` now carries `(int kind, Godot.Collections.Array<Vector2I> chunks)`: a `TerrainChangeKind` bit set (`Residency`, `Terrain`, `Navigation`, `Gameplay`, and the combined `Content`) and the affected chunk coordinates, empty meaning the whole map. The nine emit sites are classified, and the one that mattered is fixed: **`TryEvictChunk` emits `Residency` for its one chunk and no longer bumps `TerrainRevision` or `NavigationRevision`.** That is the eviction storm gone - an evicted chunk's cells are unchanged, only no longer resident, and every listener now skips a residency move instead of rebuilding the whole map.

Every listener consumes `kind`: the eleven `CellsChanged` subscribers (five surface renderers via the DUP-01 base's new `OnCellsChangedSignal`, the tile view's coast requeue and the transition layer's dual-grid refresh as overrides, the collision component, and the grid-side overlay, tilemap bridge, minimap and archive-read handlers) act on a `Content` change and ignore a `Residency` one. The collision component also consumes `chunks`: a content change with a chunk list rebuilds only those chunks instead of rescanning the map.

Verified: `dotnet build` clean, zero warnings; the whole streaming/eviction probe suite green (chunk eviction, revisions, archive, availability, loading, saving, budget, demand, pins; relief/feature/surface streaming; painted archive) - 21 probes including the autotile-staleness guard that eviction used to break by bumping the very revision its guard reads. The new `tests/terrain_change_kind_probe.gd` asserts a `FillTerrain` edit is `Terrain` (plus `Navigation` on a land/water flip), names only its chunk and bumps `TerrainRevision`, while an eviction is `Residency` alone, names its one chunk, and moves neither global revision - and 4 of 4 mutations trip it (re-adding either revision bump, misclassifying eviction as `Terrain`, or dropping the chunk list). Two scan-pin mutations trip: an eviction revision bump, and any terrain/grid file raising the payload-less signal.

**Classification of the emitters:**

| Site | Kind | Chunks | Revisions |
|---|---|---|---|
| `FillTerrain` | `Terrain` (+`Navigation` on a kind change) | the cells' chunks | Terrain++ (Nav on change) |
| `LoadCells` / `LoadGeneratedCells` / `ClearCells` | `Terrain\|Navigation` (Clear adds `Gameplay`) | empty (whole map) | Terrain++, Nav |
| `TryEvictChunk` | **`Residency`** | `[coordinate]` | **neither** |
| `PublishRecords` (a reloaded chunk) | `Terrain\|Navigation` | `[coordinate]` | Terrain++, Nav |
| `RestoreChunkState` / publication commit | `Terrain\|Navigation` | empty (whole map) | Terrain++, Nav |
| `SetChunkAvailable` | `Terrain\|Navigation` | `[coordinate]` | Terrain++, Nav |

**Scope taken and deliberately deferred.** The change kept the risk on one behaviour: only eviction's semantics moved. Every other emitter keeps today's content behaviour, so no chunk can render stale - a reloaded or newly-available chunk still bumps the revision and rebuilds. The plan's broader per-chunk minimisation - the surface renderers rebuilding only the listed chunks on a `Terrain` edit rather than the whole view, and the "identical reload emits `Residency`" optimisation - is the honest follow-up: it needs each renderer's own chunk-scoped rebuild path (ENH-03/ENH-13 territory) and, for the reload optimisation, proof that a renderer never holds a cleared chunk it would then fail to redraw. The collision component, which already builds per chunk, takes the chunk list today; the others full-rebuild on a content change, exactly as before. The per-cell `CellChanged(x, y)` signal is unchanged here - it gains its kind in ENH-02.

### Follow-up investigated 2026-09-09: per-chunk minimisation is blocked, and the reload optimisation is unsafe as things stand

The two remaining pieces were traced against the renderers, and both turn on the same missing capability - a per-renderer chunk-scoped rebuild - so neither can land here:

- **Renderers rebuilding only the listed chunks on a `Terrain` edit** is the ENH-03/ENH-13 feature across eight-plus renderers, not a tweak to this signal. Each non-chunk-aware renderer clears its whole surface and redraws from the store on `Rebuild()`; giving each a `RebuildChunks(chunks)` path is that work, not this one.

- **"Identical reload emits `Residency`" is not merely unproven - it is unsafe today.** The proof the plan asked for ("a renderer never holds a cleared chunk it would then fail to redraw") fails against the code: `TerrainIsometricRendererComponent.Rebuild()` calls `ClearSurface()` -> `layer.Clear()`, wiping the entire surface and repainting only the cells still resident in the store. So the sequence *evict chunk C -> any `Terrain` edit anywhere triggers a full rebuild (C is no longer resident, so C is cleared and not repainted) -> reload C* leaves C visually blank. Reload today emits `Terrain | Navigation`, whose full rebuild repaints C, which is why it is correct. Switching reload to `Residency` would skip that rebuild and leave a blanked C blank permanently. The optimisation is only safe once reload can repaint just C's chunk - i.e. after the per-renderer chunk rebuild above exists.

So the eviction storm - the high-value, verifiable part - is done and stays; the per-chunk minimisation is genuinely gated on ENH-03/ENH-13 and is left until that infrastructure lands. Forcing the reload optimisation now would trade the storm fix for an intermittent blank-chunk bug.

The timing probe the plan lists (< 0.5 ms eviction with all renderers attached) was not added: the behavioural proof - eviction emits `Residency`, the revisions hold, and every listener's handler early-returns on a non-`Content` kind - establishes that no listener does work on an eviction, which is what the timing figure was a proxy for. A microbenchmark asserting a wall-clock threshold headless is flakier than the invariant it stands in for.

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
