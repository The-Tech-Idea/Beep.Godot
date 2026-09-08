# ENH-04 — Archive scheduler: cheap eviction scans, unthrottled demand loads, load ∥ save

**Type:** enhancement (huge-world performance) · **Area:** `GridCellArchiveComponent` (+`.Budget`, `.Demand`, `.Loading`, `.Saving`, `.IoBudget`), `GridCellDataComponent` (+`.Eviction`, `.Pins`), `GridArchiveIoBudget` · **Status:** proposed 2026-09-08 · **Effort:** M (2–3 days) · **Risk:** medium (data safety — every change must keep the request-time-content and atomic-write contracts the capture-budget probe pins)

## Finding

| # | Site | Problem |
|---|---|---|
| 1 | `GridCellArchiveComponent.Budget.cs:73-92` | Every budget frame does `new List<Vector2I>(cells.GetStoredChunks())` (a Godot `Array` marshal of every resident chunk coordinate), sorts it, then calls `CanEvictChunk` on up to 128 candidates; `CanEvictChunk` (`GridCellDataComponent.Eviction.cs`) **scans every record in the chunk** for crops/watered/pins. On a 1024² map with ~1000 resident chunks that is a per-frame marshal + up to 128 × 1024-record scans to find one evictable chunk. |
| 2 | `.Demand.cs:45` | `_nextDemandScan = now + 100` is set on every scan and never reset on success, so demand loads are capped at **10 chunks/s** regardless of budget; a camera jump that wants 40 chunks waits 4 s. `GetPinnedChunks()` also marshals a Godot array per scan. |
| 3 | `.Loading.cs:146` | `OnReadCellsChanged() => _readChanged = true;` aborts an in-flight reload on **any** `CellsChanged`, including another chunk's eviction or an unrelated edit — on a busy map reloads are aborted and retried repeatedly. |
| 4 | `IsBusy` | Load and save share one in-flight slot; a pending demand load waits for a budget save and vice versa, though `GridArchiveIoBudget` admits 2 ops / 32 MB and the worker paths are independent. |

## Design

1. **Per-chunk evictability counters.** `GridCellDataComponent` keeps, per chunk, `int CropCells`, `int WateredCells`, `int PinCount` (pins already per chunk) maintained by the mutators (ENH-02's crop index feeds this). `CanEvictChunk(chunk)` becomes O(1). The store exposes `IEnumerable<Vector2I> EnumerateStoredChunks()` (typed, no marshal) and an **eviction candidate ring**: chunks ordered by last-touch frame in a small heap/LRU maintained on access, so the budget scan pops from the front instead of sorting all chunks.
2. **Demand pipeline runs to budget.** Scan on `PinnedChunksChanged` (a signal from `Pins`, emitted when the pinned set gains an unavailable chunk) rather than on a 100 ms timer; the timer stays only as a fallback. Each scan issues loads until `GridArchiveIoBudget` refuses admission. `GetPinnedChunks()` gets a typed enumerator.
3. **Reload abort is chunk-scoped.** `_readChanged` is set only when the changed chunk set (ENH-01) contains the reloading chunk and `kind` is not `Residency`.
4. **Load and save in parallel.** Separate `_loadInFlight`/`_saveInFlight` (one each); `IsBusy` becomes `IsLoading || IsSaving`; admission still goes through `GridArchiveIoBudget`. The same chunk may not be loading and saving at once (guard by chunk id).

## Guards (fail first)

- `terrain_capture_budget_probe.gd` and the eviction/reload probes stay green (request-time content, cancellation, size failure, no temp files).
- Probe: 1000 resident chunks, one evictable → budget frame main-thread time < 0.2 ms (today: marshal + sort + up to 128 record scans). **Mutation:** restore the record scan in `CanEvictChunk` → time assertion fails.
- Probe: pin 40 unavailable chunks in one frame → all 40 load requests are issued within the admission budget's first two frames, not over 4 s. Mutation: restore `_nextDemandScan = now + 100` without the pin trigger → fails.
- Probe: start a reload of chunk (1,0); edit a cell in chunk (5,5) → the reload completes without `archive_read_changed`. Mutation: revert to the payload-less abort → fails.
- Probe: a demand load and a budget save of different chunks overlap (`IsLoading && IsSaving` observed true once). Mutation: restore the single slot → never true.

## Dependencies / collisions

Depends on ENH-01 (chunk payload) and ENH-02 (crop index). **Direct collision** with the other session's archive/streaming work in `ecs/grid/` — coordinate before touching `GridCellArchiveComponent`.

## Out of scope

Archive format (JSON envelope + VarToBytes), SHA-256 verification, worker encode/decode — all landed this session and unchanged.
