# ENH-05 — Painted view at 1M cells: snapshot memory, coast texture uploads, surface verification

**Type:** enhancement (huge-world memory/perf) · **Area:** `TerrainVisualSnapshot`, `TerrainCoastField` (`FieldCache`/`RenderCache`), `TerrainPaintedRendererComponent`, `TerrainShaderSurface` · **Status:** proposed 2026-09-08 · **Effort:** M (2–3 days) · **Risk:** medium (the probes for chunk-scoped repaint exist and must stay green)

## Finding

1. **`TerrainVisualSnapshot` ≈ 48 MB at 1M cells.** Each sample holds `Kind` (string reference), `Water` and `Lake` patches (`GridTerrainWaterPatch?` references), `Shore` record and shade — ~48 bytes/cell as managed objects, retained for the whole map even though only resident chunks can change and only the window the camera sees is drawn. `TerrainSampleKinds`/`TerrainSampleValues<T>` (`ecs/terrain/`) already implement 32-sample chunked, uniform-collapsing, palette-indexed storage for the *generated* field (`PayloadBytes`, `UniformChunkCount`) — the live snapshot does not use them.
2. **Whole `RenderCache` texture upload per shoreline edit.** `TerrainCoastField.RenderCache` stores the Rgbaf coast field (R signed distance, G ocean flag, B ocean distance, A smooth-cell) as one `Image`; a shoreline edit recomputes a halo window (this session) but then `ImageTexture.CreateFromImage` re-uploads the **whole** image (headless semantics force `CreateFromImage`; see memory note "headless textures alias their image"). At 1024² × detail 2 × 16 B = 64 MB per upload.
3. **`TerrainShaderSurface.Fill` verification.** `TerrainShaderSurface.cs:126-133` calls `GetUsedCellsById` after filling to verify the tile count — a full TileMapLayer query on every coast refresh under 65 536 cells (above that the streaming component draws quads, so it is skipped).

## Design

1. **Chunked live snapshot.** `TerrainVisualSnapshot` stores per-chunk blocks: a `string[]` palette + `ushort[]` kind indices, a `GridTerrainWaterPatch[]` palette + `byte[]` indices (patches repeat heavily — every generated inland cell has the same uniform dry patch), `Half`/`byte` shade, and a per-chunk `IsUniform` fast path — the `TerrainSampleKinds` shape reused for live data with a `Set(cell, sample)` that re-derives the chunk palette lazily. Target ≤ 8 bytes/cell non-uniform, ~0 for uniform chunks (open ocean, plains).
2. **Windowed texture updates on native, whole on headless.** `TerrainCoastField.RenderCache` keeps the CPU `Image`; on a windowed change it uploads via `RenderingServer.Texture2DUpdate` (partial update) when `DisplayServer.GetName() != "headless"`, else falls back to `CreateFromImage` (the probes read back through `GetImage`, which only works on the stored image in headless). One code path chooses; both are exercised by the probes (the headless one) and the native benchmark (the other).
3. **Verification as a probe, not a hot path.** Move the `GetUsedCellsById` check out of `Fill` into `terrain_guards.ps1`'s surface guard; `Fill` trusts its own loop (it wrote the cells).

## Guards (fail first)

- `terrain_painted_archive_probe.gd` (eviction/reload/inland/shoreline/windowed-equals-whole) stays green.
- Memory probe: generate 1024×1024, take `TerrainVisualSnapshot`, assert `PayloadBytes < 12 MB` and `UniformChunkCount > 0`. **Mutation:** store per-cell sample objects → assertion fails.
- Native-only benchmark (documented as such, per the headless-probe memory): shoreline edit at 1024² uploads < 1 MB (`RenderingServer` texture update counters or timing < 2 ms). Headless probe asserts byte-equality of windowed vs whole result as today.
- Pin: `GetUsedCellsById` not called from `TerrainShaderSurface.Fill`.

## Dependencies / collisions

Painted renderer and coast field were reworked this session (uncommitted) — build on that. Independent of the `ecs/grid` session except the `CellsChanged` payload (ENH-01) which lets the snapshot update only the named chunks.

## Out of scope

Shader changes, out-of-core fields (FEAT-07).
