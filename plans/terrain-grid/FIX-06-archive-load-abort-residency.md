# FIX-06 — Archive auto-load abort ignores the CellsChanged payload: a Residency move of an unrelated chunk kills a demand load

**Type:** fix · **Area:** `GridCellArchiveComponent.Loading.cs` (`OnReadCellsChanged`), against the ENH-01 `CellsChanged(kind, chunks)` contract · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** S (½ day) · **Risk:** low

## Outcome (2026-09-11)

`OnReadCellsChanged` now reads the ENH-01 payload instead of unconditionally setting `_readChanged`: it skips Residency-only moves (`kind & Content == 0`), and for a scoped change only aborts when `chunks.Contains(_readCoordinate)`; an empty chunk list (whole-map) still aborts. New probe `tests/terrain_chunk_load_abort_probe.gd` (registered in `run_actor_checks.ps1`) drives all three cases against a real in-flight read: evicting an unrelated chunk (Residency) and `FillTerrain` on an unrelated chunk (scoped content) both let the read complete and publish, while `ClearCells` (whole-map, empty chunks) aborts with `cell_data_changed`. Mutation-proven: reverting the handler to `=> _readChanged = true;` fails the probe ("Scoped content change of an unrelated chunk aborted the read"). The existing `terrain_chunk_loading`/`demand`/`eviction`/`revisions` probes stay green.

## Finding

An in-flight demand load of one chunk is thrown away by **any** `CellsChanged` emission, regardless of what that change touched. The bulk handler reads neither the `kind` nor the `chunks` payload that ENH-01 added precisely so a listener can tell a residency move (or an edit of a different chunk) apart from a change it must react to.

1. **The bulk handler discards the whole payload.** `GridCellArchiveComponent.Loading.cs:146` is
   `private void OnReadCellsChanged(int kind, Godot.Collections.Array<Vector2I> chunks) => _readChanged = true;`
   — both parameters are ignored; every `CellsChanged` sets `_readChanged`.

2. **`_readChanged` drops the freshly decoded chunk.** In `ProcessPendingLoad`
   (`Loading.cs:110-114`) the error is computed as `_readChanged ? "cell_data_changed"`,
   and the publish gate (`Loading.cs:127-131`) only calls `cells.PublishParsedChunk(...)`
   when `error.Length == 0`. So a set `_readChanged` means the worker's decoded records are
   discarded unpublished.

3. **The single-cell handler already does this correctly.** `OnReadCellChanged`
   (`Loading.cs:142-145`) trips only when
   `GridCellDataComponent.ChunkOf(new Vector2I(x, y)) == _readCoordinate` — chunk-scoped,
   exactly the granularity the bulk handler lacks. The two handlers listen to the same source
   (`Loading.cs:45-46`) but honour the payload inconsistently.

4. **Eviction is the routine trigger, and it is always a *different* chunk.**
   `GridCellDataComponent.Eviction.cs:36` emits
   `EmitCellsChanged(TerrainChangeKind.Residency, { coordinate })` when a chunk is evicted.
   `CanEvictChunk` (`Eviction.cs:14-16`) refuses to evict a pinned chunk (`IsChunkPinned`),
   while an auto-load is only wanted for a chunk that *is* pinned
   (`Demand.cs:29-36`, `AutomaticReadStillWanted` requires `_readCells.IsChunkPinned(_readCoordinate)`).
   So an eviction's chunk is provably never the chunk being read — yet its Residency signal aborts
   the read. `TerrainChangeKind.cs:14-28` states a Residency-only change "moves nothing a listener
   should act on"; this listener acts on it anyway.

5. **A bulk edit of an unrelated chunk aborts it too.** `FillTerrain` (`GridCellDataComponent.cs:183-216`)
   builds a scoped `touchedChunks` set and emits a **non-empty** chunks array
   (`GridCellDataComponent.cs:212-214`). A `FillTerrain` confined to a chunk other than
   `_readCoordinate` therefore carries a chunks array that does not contain the read chunk, but the
   handler aborts regardless.

6. **The cost of each spurious abort is a retry delay.** `FinishAutomaticRead`
   (`Demand.cs:84-90`) calls `DelayDemandRetry` on failure, which pushes the chunk's next attempt
   out by `DemandRetrySeconds` (default 2, `Demand.cs:21`). During streamed play — area terrain
   edits, an eviction storm, or a second archive/generator emitting bulk changes over other cells —
   a demand load near the camera can be repeatedly aborted and re-delayed 2s, stalling that chunk's
   streaming while nothing it depends on has actually changed.

Whole-map changes are a different case and must still abort: `EmitCellsChanged` documents an **empty**
chunks array as "whole map" (`GridCellDataComponent.cs:101-103`), and bulk load / snapshot restore /
publication emit exactly that empty array (`GridCellDataComponent.cs:501,555`, `Snapshots.cs:45`,
`Publication.cs:66`). Those genuinely may have moved the chunk being read and correctly invalidate it.

## Design

Give `OnReadCellsChanged` the same discrimination `OnReadCellChanged` already has, reading the payload ENH-01 defined:

```csharp
private void OnReadCellsChanged(int kind, Godot.Collections.Array<Vector2I> chunks)
{
    // Residency-only move (eviction/unchanged reload) touches no content the read cares about.
    if (((TerrainChangeKind)kind & TerrainChangeKind.Content) == 0) return;
    // A scoped change lists its chunks; only abort if the chunk being read is among them.
    // An empty list is a whole-map change (bulk load / restore / publication) - still abort.
    if (chunks.Count > 0 && !chunks.Contains(_readCoordinate)) return;
    _readChanged = true;
}
```

- The Residency skip matches the established sibling listeners, which gate on
  `((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0`
  (`GridCellOverlayComponent.cs:200`, `GridTileMapLayerBridgeComponent.cs:253`) — one reading of the
  kind flag, not a new convention.
- The chunk-membership test mirrors `OnReadCellChanged`'s `ChunkOf(...) == _readCoordinate`, so both
  handlers now honour the same "did this touch *my* chunk" rule. One owner for that decision, applied
  to both the per-cell and the bulk edge.
- The empty-array-still-aborts branch preserves correctness for whole-map changes that may have moved
  the read chunk; no genuine invalidation is lost.

This is the whole change — a single method body in `Loading.cs`. No signature change, no new field, no new consumer needed: `_readChanged`, `_readCoordinate` and the `chunks` payload already exist and are already wired.

## Guards (fail first)

Add a headless `.gd` probe (the archive/demand path is exercised by the existing streaming probes; extend or add one alongside them) that drives the abort condition directly:

- **Assertion (eviction case):** pin and demand-load chunk A while it is mid-read; evict an unrelated,
  unpinned chunk C (which emits `CellsChanged(Residency, {C})`); assert chunk A's read still completes
  and `PublishParsedChunk` runs (A becomes available, `ChunkLoadFinished` reports `success == true`,
  `LastError == ""`).
  **Mutation that trips it:** revert `OnReadCellsChanged` to `=> _readChanged = true;` — the Residency
  signal sets `_readChanged`, `ProcessPendingLoad` reports `cell_data_changed`, A is not published, and
  the probe fails.

- **Assertion (whole-map still aborts):** with a read of chunk A in flight, emit a whole-map change
  (empty chunks array, e.g. a snapshot restore / `LoadCells` publication); assert the read is aborted
  (`LastError == "cell_data_changed"`, A not published this pass).
  **Mutation that trips it:** drop the `chunks.Count > 0` guard so an empty array is treated as
  "not my chunk" and skipped — the whole-map change no longer invalidates A and the probe fails.

The two assertions together pin both directions: a Residency/unrelated-chunk change must NOT abort, and a whole-map change MUST. A guard that only checked the first would pass against a handler that ignores every `CellsChanged`, so the second is what makes the pin able to fail on over-relaxation.

## Dependencies / collisions

- **ENH-01** (DONE) — typed `CellsChanged(kind, chunks)` and the affected-chunks payload. This fix is
  the archive load-abort listener finally honouring that contract; it depends on ENH-01's payload and
  the `TerrainChangeKind` flags but changes nothing ENH-01 owns.
- **ENH-02** (DONE) — gameplay-vs-terrain change classification. Same spirit one layer down: a change
  that a listener should not act on must not be acted on. No code overlap.
- **Streaming group (ENH-03/04/etc.)** — `GridCellArchiveComponent.Loading.cs` sits next to the demand
  streaming path a concurrent session owns via `TerrainWorldComponent`. This edit is confined to
  `OnReadCellsChanged` and does not touch demand scheduling, residency, or the archive I/O budget, but
  land it in coordination with that session to avoid a merge collision in the archive files.
- No dependency on DUP-05, DUP-13, or ENH-12; the resource/catalog and job-queue subsystems are untouched.

## Out of scope

- The `DemandRetrySeconds` retry cadence and the demand-scan scheduling (`Demand.cs`) — the fix removes
  spurious aborts, it does not change how a genuine failure is retried.
- `OnReadDayAdvanced` (`Loading.cs:147`) — a day advance is a distinct signal with its own
  invalidation meaning; this fix does not reinterpret it.
- The single-cell `OnReadCellChanged` handler, which is already correct.
- Any change to what `EmitCellsChanged` callers put in the payload; this fix only *reads* the existing
  contract.
