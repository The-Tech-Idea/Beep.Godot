# ENH-07 — Chunked residency for the tile and isometric block projections

**Type:** enhancement (huge worlds in every view) · **Area:** `TerrainTileRendererComponent`, `TerrainTransitionLayerComponent`, `TerrainIsometricRendererComponent`, `TerrainIsometricAutotileRendererComponent`, `TerrainSurfaceStreamingComponent`, `TerrainPropResidency<T>` · **Status:** proposed 2026-09-08 · **Effort:** L (4–6 days) · **Risk:** medium–high (TileMapLayer mutation cost is the whole problem; the streaming component's own header documents the failed per-cell-budget attempt)

## Finding

Streaming exists for two of the four projections:

| Projection | Ground | Props | Streamed? |
|---|---|---|---|
| Painted | shader surface (`TerrainShaderSurface` + `TerrainSurfaceStreamingComponent` per-chunk quads above 65 536 cells) | `TerrainFeature/Relief` via `TerrainPropResidency` | yes |
| Game tiles | `TerrainTileRendererComponent` — one TileMapLayer per kind + `TerrainTransitionLayerComponent` dual-grid per biome, **whole map placed** | flat props via residency | ground: **no** |
| Isometric | `TerrainIsometricRendererComponent` — 5 level layers + seabed BFS + summits, **whole map placed** | `TerrainIsometricFeature` via residency | ground: **no** |
| Isometric tiles | `TerrainIsometricAutotileRendererComponent` — `SetCellsTerrainConnect` batches, time-sliced pending layer, **whole map** | — | no |

`TerrainMapSetup.BoundsFor` caps named sizes at Huge 128×80, so today no *recipe* can request a streamed map in these views; but `TerrainGeneratorComponent.BoundsSize` accepts any size and the painted view already streams 1024². A 1024² isometric world is 1M × 5 layers of TileMapLayer cells resident at once — TileMapLayer's internal quadrant updates alone make that unusable (the tracker's Phase 1.5 measured 10 240 `UpdateInternals` as the problem at 128×80).

`TerrainSurfaceStreamingComponent`'s header records why "one tile per resident cell under a 2 048-cell-per-frame budget" failed for the painted view and why per-chunk quads won. Tiles cannot be quads — each cell has its own atlas coordinate — so this plan needs a different mechanism.

## Design

**Residency by TileMapLayer per chunk-group, not by cell.**

- A `TerrainTileChunkPool` owns TileMapLayers sized to a *group* of chunks (e.g. 4×4 chunks = 128×128 cells = 16 384 cells, one Godot quadrant of 16 per layer). Each pooled layer is positioned at its group origin; `SetCell` coordinates are local to the group. Filling one group = up to 16 384 `SetCell` calls, done **off the main thread into a `TileMapPattern`** (`TileMapPattern` is a `Resource`; `SetPattern` applies it in one call on the main thread). Godot applies a pattern as one internal update, not 16 384.
- The wanted group set comes from the camera (the same `CollectWantedChunks` logic as `TerrainSurfaceStreamingComponent`, at group granularity, `PreloadChunks` margin). Resident groups not wanted are returned to the pool (`Clear()` once, not per cell) — a `Clear()` of a whole layer is one internal update.
- Per-kind layers (tile view) become per-kind **patterns** inside the group's single layer set: one layer per `TerrainLayers` level per group (tile view: ground/hills/mountains/sea; isometric: 5 levels + seabed steps), which also removes the per-kind layer explosion (`TerrainTileRendererComponent.EnsureLayers` creates one layer per kind present).
- `TerrainTransitionLayerComponent` (dual-grid, +1 cell halo across group borders) and the autotile renderer (`SetCellsTerrainConnect` needs neighbours) build their patterns with a one-cell halo read from the snapshot; the halo cells are drawn by the group that owns them, so borders match.
- Overview: at `OverviewCellPixels` the pool releases everything and the painted-style overview quad (already implemented in `TerrainSurfaceStreamingComponent`, `CanSupplyOverview`) is shown for these views too — the shader surface can render the whole-map id map as the far view for any projection.
- Below 65 536 cells the renderers keep today's whole-map behaviour (same threshold as the painted view), so small maps and the existing guards are unaffected.

## Steps

1. `TerrainTileChunkPool` + pattern builder + camera-driven wanted set (reuse the streaming component's math; factor `CollectWantedChunks` into a shared `TerrainCameraWindow`).
2. Tile renderer on the pool (ground layers), then `TerrainTransitionLayerComponent` with halo.
3. Isometric block renderer on the pool (5 levels + seabed BFS per group — the seabed depth BFS becomes per group with a halo of `MaxSeabedSteps`).
4. Autotile renderer: `SetCellsTerrainConnect` per group pattern; drop the time-sliced pending layer (a group is one call).
5. Overview hand-off; benchmark; docs.

## Guards (fail first)

- `terrain_guards.ps1` `tile_layers`/`iso_layers` (small maps) stay green — the threshold keeps them on the old path.
- Streaming probe (1024×1024, isometric): camera jump → wanted groups resident within 2 frames; main-thread time per frame < 4 ms during a one-group-per-frame pan; resident TileMapLayer count ≤ pool size. **Mutation:** apply cells with `SetCell` instead of `SetPattern` → per-frame time fails.
- Border probe: a dual-grid transition tile straddling two groups is identical to the whole-map rendering of a 128×128 fixture (render both, compare atlas coords per cell). Mutation: drop the halo → mismatch.

## Dependencies / collisions

Depends on DUP-01 (base + `Configure`), ENH-01 (chunk-scoped `Terrain` changes → refill one group's pattern). Terrain-only. Feeds FEAT-07 (huge worlds through the recipe).

## Out of scope

Art, autotile peering bits (art task per the tracker), painted view (already streamed).
