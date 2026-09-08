# ENH-02 — Farming edits stop rebuilding terrain; no-op writes stop notifying

**Type:** enhancement (correctness of invalidation) · **Area:** `GridCellDataComponent` mutators, `GridCalendarComponent.AdvanceDay`, `GridToolActionComponent`, `CropGrowth` path, per-cell listeners · **Status:** proposed 2026-09-08 · **Effort:** S–M (1–2 days) · **Risk:** low

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
