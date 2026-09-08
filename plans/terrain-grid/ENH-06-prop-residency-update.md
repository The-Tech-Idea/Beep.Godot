# ENH-06 — Prop residency: allocation-free merge, chunk-revision awareness, overview LOD

**Type:** enhancement (huge-world performance) · **Area:** `TerrainPropResidency<T>`, the three prop renderers (via DUP-07), `TerrainSurfaceStreamingComponent` (overview decision) · **Status:** proposed 2026-09-08 · **Effort:** M (2 days) · **Risk:** low–medium

## Finding

1. **Per-frame allocations and a full re-sort.** `TerrainPropResidency.Update` (`TerrainPropResidency.cs:493-516`) builds the wanted set with LINQ (`Except`/`ToList`), then after merging newly resident chunks' stamps into `target` runs `target.Sort(compare)` over **all** resident stamps — O(n log n) per frame with n in the tens of thousands on a dense forest, even when the merge added one chunk. Stamps are already sorted per chunk when built.
2. **Not chunk-revision aware.** Residency knows which chunks are resident, not which have changed. A `Terrain` change in one resident chunk (ENH-01) has no entry point narrower than "drop everything"; the renderers' `Rebuild()` does exactly that (`ResetStreaming(); BeginStreaming()`).
3. **Archived-out chunks lose their stamps.** When a chunk leaves residency its stamps are discarded and re-scattered on return. Scatter is deterministic (`TerrainGeometry.Hash01` + seed) so the result is identical — but the work is repeated every time the camera crosses a chunk boundary and back.
4. **No overview LOD.** `TerrainSurfaceStreamingComponent` draws a single overview quad when a cell is ≤ `OverviewCellPixels` (default 4 px); the prop renderers keep scattering per-cell sprites at that zoom, where a tree is under one pixel. They also keep the full `PreloadChunks` margin.

## Design

1. **k-way merge, no LINQ.** Keep per-chunk stamp lists sorted at build time; the resident set is an ordered list of chunk lists; the draw order is produced by merging (or, simpler, drawn per chunk — z-order within `TerrainLayers.ZForProps` levels is per level, not per stamp, so per-chunk draw is already correct). Wanted/resident diffing uses two `HashSet<Vector2I>` swapped each frame (the `TerrainSurfaceStreamingComponent` shape, `_wanted`/`_resident`).
2. **`Invalidate(IReadOnlyList<Vector2I> chunks)`** on the residency: rebuilds only those chunks' stamp lists; the DUP-01 hook routes `Terrain` changes here.
3. **Stamp retention.** A small LRU of recently-non-resident chunks' stamp lists (e.g. 64 chunks) so a camera oscillating at a boundary does not re-scatter; invalidated by `Terrain` changes for that chunk.
4. **Overview LOD.** Residency reads `TerrainSurfaceStreamingComponent.IsOverviewVisible` (signal `OverviewChanged`) — when the overview is on, prop renderers hide their stamps (or draw a per-chunk impostor: one pre-baked density tint quad per chunk, future work) and release residency; when it turns off, the wanted set repopulates from the camera as now.

## Guards (fail first)

- Timing probe (existing streaming lab, 1024²): pan one chunk per frame through dense woods; per-frame `Update` main-thread cost < 0.5 ms and zero `List<T>` allocations of stamp arrays (use `GC.GetAllocatedBytesForCurrentThread` delta). **Mutation:** restore `target.Sort` → time assertion fails.
- Probe: edit a cell's kind in chunk A; assert chunk B's stamp instances (ids) are unchanged. Mutation: route `Terrain` to `ResetStreaming` → fails.
- Probe: zoom to overview → `ResidentChunkCount == 0` on all three prop renderers; zoom back → repopulated.

## Dependencies / collisions

Depends on DUP-07 (one façade) and ENH-01 (chunk payload). Terrain-only; no `ecs/grid` collision beyond reading the signal.

## Out of scope

Prop art, scatter density rules, `SeededTerrainPropScatterComponent` (authoring-time, small maps).
