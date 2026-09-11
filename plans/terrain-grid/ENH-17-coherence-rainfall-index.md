# ENH-17 — Coherence rainfall index: stop re-deriving every sample's rainfall byte by string scan each pass

**Type:** enhancement (generation time on the coherence path) · **Area:** `TerrainCoherenceStage` (`Smooth`, `RainfallIndex`) · **Status:** **PROPOSED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low — behaviour-preserving; gated by the generation baseline probe

## Finding

`TerrainCoherenceStage.Smooth` rebuilds the whole `before[]` rainfall-index field once per pass, and it does so through a per-sample linear string scan.

1. **`RainfallIndex` is an O(kinds) string scan** (`TerrainCoherenceStage.cs:190-198`): it walks `rainfallKinds` and returns `(byte)(i + 1)` on the first `rainfallKinds[i] == kind` match, else `0`. `rainfallKinds` is `TerrainKindCatalog.Standard.RainfallKinds` (`:218`) — the five rainfall kinds (desert, dry grass, grass, swamp, jungle). So each call is up to five ordinal string comparisons.

2. **It is called for every sample, every pass** (`TerrainCoherenceStage.cs:225-228`): the pass loop opens with
   ```
   for (int index = 0; index < world.Count; index++)
       before[index] = RainfallIndex(rainfallKinds, world.Terrain[index]);
   ```
   `world.Count == Width * Height` (`TerrainGenerationBuffer.cs:42`) — the sub-cell sample field, not the reduced gameplay grid. `Smooth` runs before `TerrainTileReductionStage` (`TerrainFieldBuilder.cs:97` "Biome coherence" is stage 12, reduction is stage 14 at `:103`), so the field is at full sample resolution, capped at `MaxFieldSamples = 1_250_000` (`TerrainFieldBuilder.cs:40`).

3. **The pass count is a 0..6 dial** (`TerrainGeneratorComponent.cs:121`, `[Export(PropertyHint.Range, "0,6,1")] BiomeCoherencePasses`, default 0 — opt-in). At the top of the range this is ~1.25M samples × 6 passes × up to 5 string compares ≈ 37M ordinal string comparisons per build, all to reproduce a fixed five-entry lookup.

Why it happens: the byte code a sample votes with is a fixed, tiny mapping (`kind → position-in-`RainfallKinds`-plus-one`, `0` for anything the rainfall table did not decide). The code derives that mapping by re-searching the list from scratch for every sample on every pass, instead of building the reverse map once. It is the same generation path ENH-16 profiled for allocations; ENH-16 removed the per-pass `string[]` clone that used to hold this snapshot (the `before = world.ByteScratch` at `:216` is that change) but did not touch the string scan that fills it.

Note the double-buffer semantics that any fix must preserve: within a pass the vote reads only `before[]` (`:258,260,266`) and writes `world.Terrain[index]` (`:290`); the next pass's `before[]` is therefore exactly the current `world.Terrain` after this pass. The full re-derive at `:227-228` is the code relying on that — it re-reads the whole terrain field each pass because a pass mutates some of it.

## Design

Build the reverse map once, from the one owner of the kind list, and index through it.

- In `Smooth`, before the pass loop, build a `Dictionary<string, byte>(StringComparer.Ordinal)` from `rainfallKinds`: `map[rainfallKinds[i]] = (byte)(i + 1)`. **Ordinal is load-bearing** — `RainfallIndex` compares with `==` (ordinal string equality), so the dictionary must use `StringComparer.Ordinal` to reproduce the exact same mapping and keep generation deterministic. `OrdinalIgnoreCase` would silently change which samples map to `0`.
- Change `RainfallIndex` to `map.TryGetValue(kind, out byte code) ? code : (byte)0` (or inline the `TryGetValue` at the `:228` fill site and delete the method). The per-sample cost drops from O(kinds) string compares to one hash lookup.
- The dictionary is a *derived* index of `TerrainKindCatalog.Standard.RainfallKinds` (DUP-13's owner), rebuilt each `Smooth` call from that list — it introduces no second owner of the kind set, and its consumer (`RainfallIndex`) lands in the same change, so it is not an orphan.

That alone removes the string-scan factor the finding names and is the whole of the required fix. An optional further step — maintain `before[]` incrementally instead of re-deriving all of `world.Count` each pass — is available because the pass writes `world.Terrain[index]` only at `:290`: fill `before[]` once before the loop, and after each pass set `before[index] = code` for exactly the indices that pass reassigned. This turns the per-pass rebuild from O(Count) into O(reassigned). It is a larger change to the double-buffer bookkeeping and is only worth taking if the baseline probe (below) proves it byte-identical; the dictionary is the low-risk core and the incremental step is opt-in on top of it.

No new configuration, no new public surface, no genre-variant merge — this is one internal method in one stage.

## Guards (fail first)

1. **Determinism case with coherence on (correctness / no-regression).** The existing `tests/terrain_generation_baseline_probe.gd` never sets `BiomeCoherencePasses`, so it runs the default 0 and **never exercises `Smooth` at all** — this whole path is currently unpinned. Add a case that does: extend `TerrainGenerationBaselineSmoke.Snapshot` (the C# the probe calls at `:48`) to set `BiomeCoherencePasses = 2` for a new named case, then record the fixture on known-good (pre-fix) code with `-- --record`. The probe hashes every published layer and fails on `"<case>: layer <terrain> changed"`.
   - Assertion: the coherence-smoothed terrain layer hashes identically before and after the refactor.
   - Mutation that trips it: build the reverse map with `StringComparer.OrdinalIgnoreCase` instead of `Ordinal`, or off-by-one the code (`(byte)i` instead of `(byte)(i + 1)`) — the mapping moves, samples vote differently, the terrain layer hash changes, the case fails. This proves the guard can fail on exactly the way the refactor could be got wrong.

2. **Scan pin (the performance property, fail-first on the current code).** Add a `tests/addon_contract_scan.ps1` pin that `TerrainCoherenceStage.cs` contains no per-sample linear rainfall-index scan — forbid the `rainfallKinds[i] == kind` loop body (`:194`) and require a `Dictionary<string, byte>` in `Smooth`.
   - Assertion: the string-scan pattern is absent and the dictionary lookup is present.
   - Mutation that trips it: revert the fix (restore the `for (int i = 0; i < rainfallKinds.Count; i++) { if (rainfallKinds[i] == kind) ... }` scan) → the forbidden pattern reappears → pin fails. Before the fix the pin fails; after it, it passes.

## Dependencies / collisions

- **DUP-13** (`TerrainKindCatalog`) owns `RainfallKinds`; the reverse map is built from it, so this rides on DUP-13 already having landed (it has — the `:31-35` comment and `:218` confirm the list lives in the catalog).
- **ENH-16** (generation-stage allocations, IMPLEMENTED) touched this same stage and removed the per-pass string-field clone (`before = world.ByteScratch`); ENH-17 finishes the same site by removing the string scan that fills that byte field. No overlap in the lines changed.
- Uses the **generation baseline probe** (`tests/terrain_generation_baseline_probe.gd` + `TerrainGenerationBaselineSmoke.cs`) as the determinism gate, adding a coherence-on case to it.
- No collision with the streaming / `TerrainWorldComponent` / archive session: this is offline map generation (`TerrainFieldBuilder.BuildPrepared`), not the residency/streaming path.

## Out of scope

- The `AbsorbSmallRegions` half of the stage (`:57-182`) — it does dictionary tallies and flood fills, not the per-sample string scan this targets.
- Any change to what the coherence filter *does* (the Moore-majority vote, the keep threshold, `SamplesPerCell` reach, the rainfall-kind set) — output must stay byte-identical.
- The incremental `before[]` maintenance is described as an optional follow-on, not a committed deliverable of this item; the dictionary lookup is the fix.
