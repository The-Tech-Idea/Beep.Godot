# ENH-13 — Minimap bake and resource scatter on huge maps: no silent 1024 cap

**Type:** enhancement + accepted-then-ignored fix · **Area:** `ui/GridMinimapComponent`, `GridResourceScatterComponent`, `SeededTerrainPropScatterComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (minimap downsample done; resource scatter deferred - streaming collision) · **Effort:** S–M (1–2 days) · **Risk:** low

## Outcome (minimap, 2026-09-09)

`GridMinimapComponent.BakeTerrain` silently returned when `size.X > 1024 || size.Y > 1024`, so `ShowTerrain` was accepted and drew nothing on a large map (rule 7). It now chooses the coarsest power-of-two scale that fits the texture under 1024 texels per axis and bakes one texel per s×s block coloured by the block's majority terrain kind (`TerrainGeometry.MostCommon`, the DUP-04 reduction, over a reused counts dict). Below 1024 the scale is 1 and the bake is byte-for-byte the old path (a 1×1 block is that cell's kind). The scale is readable through a new `TerrainScale` property and reported once through a print, so the downsample is observable rather than silent.

Guard: `grid_minimap_probe` bakes a 1500×1000 map and asserts 1/2 scale, a 750-wide non-blank texture (grass painted at the origin), and that a 64×64 map still bakes 1:1. Mutation-proven: restoring the silent `>1024` return leaves scale 1 and a blank texture, and the probe fails. Build clean.

**Deliberately not done in this pass** (mirrors the design's split): the byte-buffer/`SetData` blit and the **chunk-scoped rebake** (guard #2, re-bake only the changed chunks) - the downsampled `SetPixel` bake on change is correct and the chunk-scoped rebake is a perf follow-on that wants ENH-01's chunk payload wired through the minimap. The kind→colour table stays local until `TerrainKindCatalog` (DUP-13) lands.

### Still pending: resource scatter (streaming collision)

`GridResourceScatterComponent`'s silent 1024 clamp and its scatter-the-whole-map-at-once behaviour need chunk-resident placement and eviction - the streaming session's residency area. Left for that coordination, per the collision note below.

## Finding

1. **`GridMinimapComponent.BakeTerrain` (`:301-325`)** writes the terrain background with `Image.SetPixel` per cell, **silently returns** when `size.X > 1024 || size.Y > 1024` (the minimap is blank on a 1200-wide map with no warning — the export `ShowTerrain` is accepted and does nothing), and rebakes the **whole** image on any `CellChanged`/`CellsChanged` (13/12 subscribers include it). It also uses its own `NormalizeKind` (DUP-05) for the kind→colour table.
2. **`GridResourceScatterComponent`** clamps its scan area to 1024 per axis silently — on a larger map the far region never receives resource nodes, again with no warning; and it scatters the whole map at once (no residency), instantiating a `GridResourceNodeComponent` scene per placed node up front. For a 1024² map at the generator's 8.5 % land density that is tens of thousands of nodes resident from frame one.
3. **`SeededTerrainPropScatterComponent`** is authoring-scale by design (`MaxProps ≤ 256`, `SizeInTiles` default 20×12) — fine; noted only so it is not mistaken for the runtime scatter.

## Design

**Minimap**

- Bake into a `byte[]` RGBA buffer per **chunk** (32×32 texels) and blit with `Image.BlitRect`/`SetData` from the buffer — no `SetPixel`. Above 1024 texels per axis, bake at a **downsampled scale** (2, 4, …) chosen so the image ≤ 1024: one texel = majority kind of an s×s block (the `TerrainTileReductionStage.MostCommon` rule, shared via DUP-04). No silent return: the export stays honoured and the scale is reported through `TerrainScale` (readable) and a one-time warning if the export asked for full detail.
- Re-bake only the chunks in `CellsChanged(kind, chunks)` with `Terrain` (ENH-01); ignore `Gameplay`/`Residency`.
- Kind→colour through the `TerrainKindCatalog` (DUP-13) when it lands; until then through `GridIds.Normalize` + the existing table.

**Resource scatter**

- Remove the clamp; scatter is **chunk-resident**: `GridResourceScatterComponent` computes placements deterministically per chunk (hash + catalog weights — the generator already wrote `Resource` per cell in `GeneratedTerrainField`, so the scatter reads *where* from the field and only decides *node instances*), instantiates node scenes for resident chunks and frees them on eviction, persisting per-node depletion in `GridCellDataComponent` metadata (the resource node already writes `remaining` to the cell; make that the one owner so a freed node loses nothing). Below the streaming threshold behaviour is unchanged (all chunks resident).
- A map larger than the component can handle fails loudly (`PushError` + `ScatterRejected` signal), never partially.

## Guards (fail first)

- Probe: 1500×1000 map, `ShowTerrain = true` → minimap texture non-blank, `TerrainScale == 2`, one warning. **Mutation:** restore the silent return → blank texture, no warning.
- Probe: 256×256 map, edit one cell → exactly one 32×32 blit (count via hook), not a full bake. Mutation: revert to whole-map bake → count of written texels = 65 536.
- Probe: 1200×600 map, scatter → nodes present in chunk (36, 10) (beyond 1024). Mutation: restore the clamp → none.
- Probe: evict a chunk with a half-depleted node, reload → node re-instantiated with the same `remaining`. Mutation: keep depletion only on the node → resets.

## Dependencies / collisions

ENH-01 payload for chunk-scoped rebake; DUP-05 for the normaliser; DUP-13 optional. `ecs/grid/` — the scatter touches residency (other session's area); the minimap is HUD-only.

## Out of scope

Minimap fog/exploration (FEAT-03 adds a layer), minimap interaction.
