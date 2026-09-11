# DUP-15 — Live-water sub-cell reconstruction: one reconstructor behind both streaming modes

**Type:** duplication · **Area:** `TerrainCoastField` (`SampleLiveWater`, `CreateLiveWaterSampler`, `CreateLiveWaterQuery`) · **Status:** **PROPOSED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low

## Finding

The sub-cell "is this position water" reconstruction — patch fallback, then half-cell bilinear over the four nearest cell centres, then a `>= 0.5` threshold — is implemented twice in `TerrainCoastField.cs`, and which copy runs is decided by whether the map streams.

1. **The snapshot copy** — `SampleLiveWater` (`TerrainCoastField.cs:724-745`). Backs `CreateLiveWaterSampler` (`:624-629`), which reads a whole-map snapshot (`ReadWater`/`ReadPatches`) into `bool[] wet` and `GridTerrainWaterPatch?[] patches` and closes over them. The formula: clamp `FloorToInt(at)` to a cell; if `patches[cell]` is non-null return `patch.IsWater(at - cellOffset, wet[cell])`; otherwise bilerp `wet[]` at the four centres around `at - 0.5` and threshold at `>= 0.5f`.

2. **The live copy** — the inline lambda inside `CreateLiveWaterQuery` (`:636-649`, formula body `:638-648`). Backs the streamed-prop path. Byte-identical control flow — same `FloorToInt` cell clamp, same `cells.WaterPatchAtCell(...) is { } patch` fallback returning `patch.IsWater(at - cellOffset, Wet(cx,cy))`, same `at - 0.5` half-cell bilinear over `Wet(x,y)`, same `>= 0.5f` threshold — differing only in its data source: it reads cells live through `Wet(int,int)` (`:635`) and `cells.WaterPatchAtCell` per query instead of from a captured array.

The reconstruction *geometry* is one fact expressed in two places; only the *data source* (captured snapshot vs. live per-query read) legitimately differs. Because a small map takes the Sampler and a map over the 1.25M-sample budget takes the Query, the two must stay bit-identical or the same coastline places props differently by map size. Any later change to the threshold, the `at - 0.5` centring, the clamp, or the patch fallback lands on one copy and silently diverges the two prop-placement paths — the rule-3 failure the addon has consolidated elsewhere (GridIds, TerrainKindCatalog).

Consumers select a copy by streaming state, so both are live in shipped code:
- `TerrainFeatureRendererComponent.cs:214` (Sampler) vs. `TerrainFeatureRendererComponent.Streaming.cs:28` (Query).
- `TerrainReliefRendererComponent.cs:157` (Sampler) vs. `TerrainReliefRendererComponent.Streaming.cs:28` (Query).
- `TerrainIsometricRendererComponent.cs:295-296` — `CreateWaterSampler(localQuery)` picks Query when `localQuery`, else Sampler.
- `TerrainShorelineField.cs:26` and `BuildLivePixels` (`:684-685`) also route through `SampleLiveWater`.

## Design

Extract the geometry into one private reconstructor on `TerrainCoastField` that takes the two data reads as delegates, and have both entry points call it:

```csharp
private static bool ReconstructWater(int width, int height, Vector2 at,
    Func<int, int, bool> wet, Func<int, int, GridTerrainWaterPatch?> patchAt)
```

The body is the current `SampleLiveWater` formula verbatim, reading `wet(x, y)` and `patchAt(x, y)` instead of array indices. Then:

- `SampleLiveWater(wet[], patches[], width, height, at)` becomes a thin wrapper that supplies array-backed delegates: `(x, y) => wet[y * width + x]` and `(x, y) => patches[y * width + x]`. Its callers (`CreateLiveWaterSampler`, `BuildLivePixels`, `TerrainShorelineField`) are untouched.
- `CreateLiveWaterQuery`'s inline lambda collapses to `at => ReconstructWater(width, height, at, Wet, (x, y) => cells.WaterPatchAtCell(origin + new Vector2I(x, y)))`, keeping its live `Wet` and per-query `WaterPatchAtCell`.

One owner for the geometry; each entry point keeps its own data source and remains a real, in-the-same-change consumer of the extracted helper (no interface-with-one-caller). No genre split is involved — this is one reconstruction, not a per-genre look. No legacy shim: rename/extract and let the compiler sweep the two former bodies; do not keep either old copy behind a flag.

This does not merge the snapshot vs. live *strategy* (that split is the point of the two entry points and is real); it merges only the shared math both strategies run.

## Guards (fail first)

Because this is a behaviour-preserving consolidation, the equivalence probe is green both before and after the fix; its worth is proven by a deliberate-drift mutation, per the repo's "verification must be able to fail" rule.

1. **Headless equivalence probe** (`tests/terrain_live_water_equivalence_probe.gd`, or extend an existing terrain `.gd` probe). Build a small deterministic map (~24×24) with a spaced/one-cell-wide water channel and at least one authored `WaterPatch` cell. Obtain both `TerrainCoastField.CreateLiveWaterSampler(cells, origin, size)` and `CreateLiveWaterQuery(cells, origin, size)`, then sample a fixed grid of sub-cell positions — cell centres, `x + 0.5`/`y + 0.5` half-cell boundaries, and inside a patch cell — and assert the two delegates return the identical `bool` at every position.
   - **Mutation that trips it:** in one copy only (pre-fix, the Query lambda; post-fix, deliberately re-inline a second body) change the threshold `>= 0.5f` to `> 0.5f`, or `at - 0.5f` to `at`. The probe goes red on the half-cell boundary samples — proving it actually compares the two paths. With the shared helper in place that drift is unrepresentable (it would land in `ReconstructWater` and move both paths together).

2. **Contract-scan pin** (`tests/addon_contract_scan.ps1`). Assert the half-cell bilinear reconstruction body (`Mathf.Lerp(upper, lower, y - y0) >= 0.5f` preceded by an `at - 0.5f` clamp) appears exactly once under `addons/beep_game_builder_cs/ecs/terrain/`, and that `CreateLiveWaterQuery` no longer contains its own `>= 0.5f` bilinear block (it must delegate to `ReconstructWater`).
   - **Mutation that trips it:** reintroduce the inline lambda body into `CreateLiveWaterQuery` → the single-occurrence assertion fails.

## Dependencies / collisions

- **ENH-01** (typed `CellsChanged` + affected-chunks payload) and **ENH-02** (edit-kind classification) — unrelated to the reconstruction geometry; no overlap in the touched methods.
- **DUP-13** (`TerrainKindCatalog`) — `Wet`/`ReadWater` reach water-ness through `TerrainTileSets.IsWaterKind(GridCellRules.TerrainKindAt(...))`; this plan does not alter that path, only where the bilinear that consumes it lives.
- **Concurrent-session collision:** a separate session owns `TerrainWorldComponent`/streaming/archive. This change is confined to `TerrainCoastField` reconstruction internals and does not touch the streaming lifecycle, chunk residency, or archive load — but the `*.Streaming.cs` call sites (`TerrainFeatureRendererComponent.Streaming.cs:28`, `TerrainReliefRendererComponent.Streaming.cs:28`) are read here only as callers of `CreateLiveWaterQuery`, whose public signature is unchanged, so no coordination is required beyond leaving those files' call lines intact.

## Out of scope

- Merging the snapshot-vs-live *strategies* or the two public entry points (`CreateLiveWaterSampler`/`CreateLiveWaterQuery`) — the split is intentional and each has real, distinct callers.
- Any change to the reconstruction geometry itself (threshold, centring, patch fallback) — this is a pure extraction; behaviour is preserved byte-for-byte.
- The `BuildPixels`/`BuildLivePixels` texture bake path and `OceanMask`/`EffectiveDetail`, which already share single implementations.
