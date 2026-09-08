# FEAT-06 — Huge worlds through the recipe: named sizes beyond 128×80 and an out-of-core generated field

**Type:** feature (completes the huge-world streaming work) · **Area:** `TerrainMapSetup`, `TerrainMapSize`, `TerrainRecipe`/`TerrainWorldComponent`, `TerrainGenerationJob`, `TerrainFieldBuilder`, `GeneratedTerrainField`, `TerrainSampleKinds/Values`, `TerrainGeneratorComponent.GeneratedCells` handoff, `TerrainDataLayersComponent` · **Status:** proposed 2026-09-08 · **Effort:** L (1–2 weeks; the field work is the bulk) · **Risk:** high (generation memory; must keep the pure-function contract)

## Gap

The streaming stack (archive, painted surface quads, prop residency, this session's chunk-scoped repaint) runs at 1024² in the labs by setting `TerrainGeneratorComponent.BoundsSize` directly. The **product path** cannot reach it:

- `TerrainMapSize` stops at `Huge = 128×80` (`TerrainMapSetup.BoundsFor`, `MapSizeNames`); `TerrainRecipe.Bounds => BoundsFor(MapSize)`, so no level scene can author a larger world.
- Even if it could, generation keeps the **whole sample field** in memory: `TerrainGenerationBuffer` holds ~15 arrays over `Width × Height` samples (`TopologySamplesPerCell²` × cells — at 4 samples/cell and 1024² that is 16M samples × ~40 B ≈ 640 MB) plus the reduced cell arrays, then `GeneratedTerrainField` retains the chunked/palette-compressed result (`TerrainSampleKinds`, `TerrainSampleValues<T>`) for the field-backed queries. `docs/huge-world-research.md` §3 already surveys the out-of-core options.
- `TerrainDataLayersComponent` optionally materialises whole-map TileData layers (`Materialize*`), which at 1M cells is the same TileMapLayer problem as ENH-07.

## Design

1. **Sizes.** `TerrainMapSize` gains `Giant = 256×160`, `Colossal = 512×320`, `Continental = 1024×640` (aspect kept 1.6, like Large/Huge); `MapSizeNames` and the chooser follow the enum order (the existing "chooser index IS the enum" rule). `TerrainScaleRules` and `TerrainLandmassStage.FeatureTiles` already scale by size; `RequestedLandmassCount` clamps at 6 for Mainland — raise the clamp with size (`2 + size.Y / 80`, capped 12) so a continental map is not two continents. Sizes ≥ `Giant` require the streaming components in the scene (`TerrainWorldComponent` validates: painted surface streaming + archive present, else `PushError` and refuse — no silent whole-map fallback).
2. **Tiled generation.** `TerrainFieldBuilder` runs the stage pipeline per **super-tile** (e.g. 256×256 cells) with a halo wide enough for the widest stage kernel (erosion/river drainage need the whole landmass — see 3), writing each finished super-tile straight into the chunked `GeneratedTerrainField` (`TerrainSampleKinds` per 32-sample chunk) and releasing the dense buffer. Stages classify: **local** (biome, shading, coherence, feature, resource, subsurface, tile reduction, shoreline) run per tile with halo; **global** (landmass growth, water classification, elevation/coast distance, erosion, rivers, continents, start positions) run on a **reduced** field first (1 sample/cell or coarser) whose result seeds the local passes (coast distance and drainage are interpolated from the coarse pass, then refined inside the tile). This is the standard coarse-to-fine split (Dwarf Fortress world gen, Songs of Syx, No Man's Sky's planet LOD) and keeps generation a pure function of settings.
3. **Out-of-core field.** `GeneratedTerrainField` already addresses 32-sample chunks; make the chunk store **evictable**: chunks not needed by any resident cell (the archive/`GridCellDataComponent` chunk set) are serialised to the archive directory (same worker pipeline as ENH-04) and re-read on `ResolveField` queries for that chunk. `TerrainDataLayersComponent` queries go through the field's chunk accessor (they already do — `Field-backed queries`); materialised TileData layers become chunk-group patterns (ENH-07) or are refused above the threshold.
4. **Handoff streamed.** `TerrainGeneratorComponent.GeneratedCells` already streams to `GridCellDataComponent`; with tiled generation the handoff publishes each super-tile as it finishes (staged publication exists in `TerrainWorldComponent.Generation`), so the first chunks are playable while the rest generates — `TerrainWorldStatusComponent` reports progress per tile.

## Guards (fail first)

- Probe: `TerrainRecipe { MapSize = Continental }` on `grid_level.tscn` → builds, `BuiltSize == 1024×640`, peak managed memory during generation < 300 MB (measure `GC.GetTotalMemory` in the job; record today's Huge number first). **Mutation:** keep the dense buffer for the whole map → memory assertion fails.
- Determinism: for Tiny–Huge, tiled generation produces **byte-identical** fields to today (the three-seed snapshot). This is the hard requirement; if a stage cannot be tiled identically, the plan documents which and why before changing output.
- Probe: `Giant` without streaming components → `PushError` + `WorldBuildFailed("streaming_required")`, no partial world.
- Existing huge-world labs (`streaming_lab`, painted archive probe) green at 1024².

## Dependencies / collisions

ENH-04, ENH-05, ENH-07 (streamed views), DUP-13 (kind registry reduces per-stage tables). `TerrainRecipe`/`TerrainWorldComponent` are the campaign session's files — the size enum extension must be coordinated there.

## Out of scope

Spherical/wrapping maps, infinite worlds (the recipe is a finite bounds by design), multiplayer generation.
