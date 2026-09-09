# ENH-02 — Farming edits stop rebuilding terrain; no-op writes stop notifying

**Type:** enhancement (correctness of invalidation) · **Area:** `GridCellDataComponent` mutators, `GridCalendarComponent.AdvanceDay`, `GridToolActionComponent`, `CropGrowth` path, per-cell listeners · **Status:** **IMPLEMENTED 2026-09-09** (per-cell kind, no-op early-outs, crop-tick index) · **Effort:** S–M (1–2 days) · **Risk:** low

## Outcome

Two of the three parts landed; the third is a scoped follow-up.

**Per-cell kind.** `CellChanged` now carries `(int x, int y, int kind)`. Every mutator classifies what it touched - terrain kind and the terrain_* metadata are `Terrain`, flags and relief/ramp are `Navigation`, tilled/watered/crop and any non-terrain metadata are `Gameplay` - and the eleven per-cell listeners filter on it. The seven surface renderers, the collision component and the minimap ignore a `Gameplay` change, so **watering a field cell no longer re-scatters that chunk's trees**: a `Water`, `Till`, `PlantCrop` or `HarvestCrop` reaches the feature, relief and isometric renderers as `Gameplay` and they early-return. The overlay, the tilemap bridge and the archive-read guard still react to every per-cell change, because they mirror or watch the whole cell state rather than only its terrain.

**No-op early-outs.** `SetTerrainKind`, `SetFlags`, `Till`, `Water` and `SetMetadata` now return `bool` (outcome in the signature) and emit nothing when the write changes nothing - `FillTerrain` over already-grass ground, `SetTerrainKind` to the kind a cell already is, `Water` on an already-watered cell, `SetMetadata` with the value already stored. Each compares against the record before touching it and returns `false` without bumping a revision or notifying.

Verified: `dotnet build` clean, zero warnings; the streaming/grid/rendering probe suite green (21 probes). `tests/terrain_change_kind_probe.gd` asserts the per-cell kinds (`Till`/`Water` are `Gameplay`) and the no-op early-outs (an unchanged write returns `false` and emits nothing) - and 2 of 2 behavioural mutations trip it (removing the `Water` early-out; misclassifying `Water` as `Terrain`). A scan pin requires the typed per-cell signature, the five bool-returning mutators, the terrain_* metadata classification, and the `Gameplay`-drop filter on all nine terrain-only listeners; 3 of 3 mutations trip it.

Two probes had encoded the old behaviour and were corrected: `terrain_chunk_revisions_probe` used `SetFlags(cell, 0)` on a zero-flags cell as an "invalidating" mutation, which is now correctly a no-op, so it was changed to a real flag change; and the same `terrain_feature` metadata that the feature renderer draws was, in a first cut, misclassified as `Gameplay` and skipped - `terrain_feature_streaming_probe` caught it, and the classification became "every terrain_* key is a terrain change."

**Crop-tick index (part 3).** `GridCellDataComponent` now keeps a `_dailyCells` set - every cell with a crop to age or standing water to evaporate - maintained wherever a crop or the Watered flag changes (the mutators) and rebuilt when the store is replaced (load, restore, publication) or a chunk is reloaded. `AdvanceDay` walks that set instead of every stored cell, so on a 1024x1024 farm it processes a few hundred entries rather than a million. The per-cell notifications it raises are byte-for-byte what they were - only the scan that found the cells is gone - so no listener contract changed and no probe that watched `AdvanceDay`'s emits needed touching.

Deliberately NOT taken: the plan's further step of collapsing those per-cell `CellChanged` emits into one `CellsChanged(Gameplay, chunks)` batch. That changes the notification contract - listeners that watch per-cell crop growth would have to move to the bulk signal, and the terrain renderers' bulk `CellsChanged` filter would have to narrow from `Content` to `Terrain|Navigation` so the batch does not re-trigger the storm - and it is not where the cost is: the million-cell scan was, and that is gone. Collapsing 200 emits into 1 is a separate, contract-changing optimisation left for when a listener actually needs it.

`tests/terrain_change_kind_probe.gd` now also asserts a planted crop ages and a watered cell dries across `AdvanceDay` (the index found them); 2 of 2 mutations trip it (dropping the index update in `PlantCrop` or `Water`). The farming-and-streaming probe suite - chunk revisions, eviction, availability, loading, saving, budget, navigation invalidation, painted origin - stays green, exercising crops through plant, water, advance, evict and reload.

## Finding

`CellChanged(x, y)` carries no kind. Consequences seen in source:

- `Till`, `Water`, `PlantCrop`, `Harvest`, `SetFlags` all raise `CellChanged`; the isometric renderer, the three prop renderers and the minimap subscribe to it and rebuild terrain visuals for a **gameplay** change that never altered the terrain kind. Watering one field cell re-scatters that chunk's trees.
- `GridCalendarComponent.AdvanceDay` walks **every stored cell** to age crops and emits a per-cell `CellChanged` for each grown crop — O(all cells) work and O(crops) full-visual invalidations once a day; on a 1024² map that is a million-record scan per day for a few hundred crops.
- `SetTerrainKind(cell, kind)` has no early-out when the kind is unchanged: `FillTerrain` over an already-grass area bumps `TerrainRevision` and notifies for nothing. (`SetMetadata` at `GridCellDataComponent.cs:291-292` *does* compare before marking navigation — the pattern exists, applied to one key.)
- `GridTerrainWaterPatch` value-equality already exists (`IsUniform`, equality) and the painted renderer uses it to skip identical patches; the same idea is missing at the mutator.

## Design

1. **Kind on the per-cell signal:** `CellChanged(int x, int y, int kind)` with `TerrainChangeKind` from ENH-01. Mutators classify by what they touched: kind/elevation/shore → `Terrain`; flags/relief/ramp/occupancy → `Navigation`; tilled/watered/crop/metadata → `Gameplay`.
2. **No-op early-outs** on every mutator: compare the incoming value with the record (`string.Equals` ordinal for kind, patch equality for water, flag mask compare); return `false` and emit nothing when unchanged. `SetTerrainKind`/`SetFlags`/`Till`/`Water`/`SetMetadata` return `bool changed` (rule: outcome in the signature).
3. **Crop ticking index:** `GridCellDataComponent` keeps a per-chunk `HashSet<Vector2I> _cropCells` (maintained by `PlantCrop`/`RemoveCrop`/eviction/reload — `CanEvictChunk` already scans records for crops, so the count is computed anyway, see ENH-04). `AdvanceDay` iterates `_cropCells`, not all cells, and emits one `CellsChanged(Gameplay, chunks)` for the day's batch instead of N `CellChanged`.
4. Listeners that only care about terrain (`Terrain` flag) ignore `Gameplay` — the DUP-01 base does the filtering so each renderer declares `ListensTo = TerrainChangeKind.Terrain`.

## Guards (fail first)

- Probe: generate 64×32, attach isometric + feature renderers, `Water(cell)` → assert the isometric layer's `GetUsedCells().Count` and the feature renderer's stamp count are unchanged and `TerrainRevision` unchanged. **Mutation:** remove the kind filter on the base → fails.
- Probe: `SetTerrainKind(cell, GetTerrainKind(cell))` returns `false`, emits nothing, `TerrainRevision` unchanged. Mutation: remove the early-out → fails.
- Timing probe: `AdvanceDay` on a 256×256 map with 200 crops completes in < 1 ms and emits exactly one `CellsChanged`. Mutation: revert to the full scan → time assertion fails.

## Dependencies / collisions

Depends on ENH-01 (kind enum). `GridCellDataComponent` collision with the other session. The farming components (`GridToolActionComponent`, `GridCalendarComponent`) are in `ecs/grid/`.

## Out of scope

Crop growth rules themselves; save format (crop cells are re-indexed on load/reload).
