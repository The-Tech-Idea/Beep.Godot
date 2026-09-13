# FIX-05 — GetOrCreate spurious TerrainRevision: a gameplay first-touch forces a full-map terrain rebuild

**Type:** fix · **Area:** `GridCellDataComponent.GetOrCreate` (`ecs/grid/GridCellDataComponent.cs`), `tests/TerrainChangeKindSmoke.cs` · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** XS (~¼ day) · **Risk:** low

## Outcome (2026-09-11)

Removed `TerrainRevision++` from `GridCellDataComponent.GetOrCreate`; `MarkCellChanged(cell)` stays, so the per-chunk content token (read by the archive's save-current check) still advances while the global terrain token now moves only on real terrain edits — closing the ENH-02 contract one layer down. The virgin-cell assertion was added to `tests/TerrainChangeKindSmoke.cs` (run via `tests/terrain_change_kind_probe.gd`): `Till` of a never-touched cell in chunk (1,1) must leave `TerrainRevision` unchanged and advance the chunk token. Mutation-proven: restoring the bump makes the guard fail ("Till of a virgin cell bumped TerrainRevision…"), and deleting `MarkCellChanged` makes the second assertion fail. Build clean (0 warnings); `terrain_change_kind` probe green.

## Finding

`GetOrCreate` bumps the global `TerrainRevision` every time it lazily materialises a cell record, even when the caller's edit is a gameplay-only change to virgin ground. The two surface renderers gate a whole-map rebuild on `TerrainRevision`, so the first Till/Water/Plant/flag on an ungenerated cell repaints the entire terrain surface for a change that alters nothing a terrain renderer can see.

1. **The unconditional bump.** `GridCellDataComponent.cs:560-571` — `GetOrCreate` creates the record at `DefaultTerrainKind` with no metadata and no water patches, then runs `MarkCellChanged(cell)` (the per-chunk content token) *and* `TerrainRevision++` (the global token):

   ```
   record = new CellRecord(DefaultTerrainKind);
   _cells[cell] = record;
   MarkCellChanged(cell);   // :568  chunk revision — correct
   TerrainRevision++;       // :569  global revision — the defect
   ```

2. **Gameplay mutators route through it and classify as non-Terrain.** `Till` (`:265-274`), `Water` (`:277-286`), `PlantCrop` (`:288-306`), `AddFlag`/`SetFlags` (`:232-239`, `:218-230`) all call `GetOrCreate` and then `NotifyCellChanged(cell, TerrainChangeKind.Gameplay)` (Till/Water/Plant) or `TerrainChangeKind.Navigation` (flags) — never `Terrain`. So on a cell that did not previously exist, a gameplay action still bumps the *terrain-visual* revision through `GetOrCreate`, one layer below the classification the mutators were careful to set.

3. **The bump repaints nothing.** The renderers gate on `TerrainRevision`: `TerrainIsometricAutotileRendererComponent.cs:293` (`(_cells?.TerrainRevision ?? 0) != _buildRevision` ⇒ `RequestRebuild`) and `TerrainPaintedRendererComponent.cs:211,333-339` (the prepared coast/lake pixels carry `TerrainRevision` in `_preparedKey`; when it changes they are discarded and the whole coast field is re-prepared, defeating the windowed-edit path the comment at `:208` set up). The isometric rebuild loops the full bounds (`:370-385`) reading `GridCellRules.TerrainKindAt` (`:376`), and `GetTerrainKind` returns `DefaultTerrainKind` for an *absent* cell (`:136`) — so a freshly created default-kind cell is byte-for-byte identical to the absent cell it replaced. The rebuild it triggers produces the same pixels.

**Why it happens / who is hit.** On sparse or farming-style worlds where cells are materialised on demand, every first-touch of never-generated ground drives a full painted + isometric rebuild (coast field, id/shade/elevation textures). This is exactly the "a gameplay change must not read as a terrain change" contract ENH-02 established at the mutator layer — but ENH-02 landed the classification on `NotifyCellChanged` and never covered the revision bump inside `GetOrCreate` beneath it. Fully pre-generated dense maps are unaffected: their records already exist, so `GetOrCreate` returns early at `:563-564` and never reaches the bump. This is a distinct issue from ENH-02, not a regression of it.

4. **The existing guard cannot catch it.** `tests/TerrainChangeKindSmoke.cs:35` pre-creates cell `(6,6)` via `SetTerrainKind` *before* the Till/Water assertions at `:39,:44`, so the create-a-new-record branch of `GetOrCreate` — and its `TerrainRevision` bump — is never exercised or asserted. The smoke's `TerrainRevision` check (`:68-73`) only ever runs against `FillTerrain`, a genuine terrain edit.

## Design

Drop `TerrainRevision++` from `GetOrCreate`; keep `MarkCellChanged(cell)`. The global terrain token then advances only when a caller genuinely changes terrain, while the per-chunk content token (which the archive's save-current check reads) still advances for a newly created cell.

```
record = new CellRecord(DefaultTerrainKind);
_cells[cell] = record;
MarkCellChanged(cell);
return record;
```

No genuine terrain change loses its invalidation, because every terrain-visual caller bumps `TerrainRevision` itself, after its own `GetOrCreate`:

- `SetTerrainKind` — `:174`.
- `FillTerrain` — `:210`.
- `SetMetadata` for the four surface-cached keys (`terrain_elevation`/`terrain_shore_inland`/`terrain_beach_width`/`terrain_lake_shore_width`) — `:381`.
- `LoadCells` — `:499` and `:553`.
- `PublishInto` (streaming publication) — `GridCellDataComponent.Publication.cs:64`.

So for real terrain edits the `GetOrCreate` bump is redundant; for gameplay-only first-touch it is the only bump, and it is the bug. This is **one owner per fact**: "the terrain surface changed" is owned by the terrain mutators, not by the storage primitive that happens to allocate a record.

One edge is worth stating because it looks like a hole and is not. `SetMetadata` with `terrain_feature` (a Terrain-classified key that is *not* one of the four cached-field keys) does **not** bump `TerrainRevision` today even for an existing cell — the comment at `:375-379` is explicit that the surface renderers read only the four cached fields off `TerrainRevision`, while `terrain_feature` is picked up by the feature renderer through the per-cell/`CellsChanged` path. Removing the `GetOrCreate` bump therefore makes the *new-cell* case match the *existing-cell* case that already ships, rather than opening a gap: a brand-new default-kind cell with only `terrain_feature` set renders identically on the surface, and the feature renderer invalidates the same way it already does for an existing cell.

No renames, no compat shim: one line is deleted and the compiler needs nothing swept.

## Guards (fail first)

Extend `tests/TerrainChangeKindSmoke.cs` (the ENH-02 classification smoke, same component, same signal wiring) with a virgin-cell assertion, placed after the daily-index section (~`:65`), using a coordinate in a chunk the test has not touched or evicted:

```
// A gameplay first-touch of never-generated ground is NOT a terrain change:
// GetOrCreate must not bump the global terrain revision, only the chunk token.
var virginCell = new Vector2I(40, 40);                       // an untouched chunk (ChunkSize 32 -> chunk (1,1))
var virginChunk = GridCellDataComponent.ChunkOf(virginCell); // public helper (Pins.cs)
ulong terrainMark2 = cells.TerrainRevision;
long chunkMark2 = cells.GetChunkRevision(virginChunk);
if (!cells.Till(virginCell)) return Fail("Till of a never-touched cell reported no change");
if (cells.TerrainRevision != terrainMark2)
    return Fail("Till of a virgin cell bumped TerrainRevision; a gameplay first-touch is not a terrain change");
if (cells.GetChunkRevision(virginChunk) == chunkMark2)
    return Fail("Till of a virgin cell did not advance the chunk content token");
```

- **Assertion 1** pins that the global terrain revision is unchanged by a gameplay first-touch. **Mutation that trips it:** restore `TerrainRevision++` inside `GetOrCreate` → `cells.TerrainRevision != terrainMark2` fires. (Confirmed capable of failing: it fails against the code as it stands today, before the fix — which is why the assertion is written against a *virgin* cell the existing smoke never creates fresh.)
- **Assertion 2** pins that the fix does not over-correct into a silent no-op — the chunk token must still move so the archive and any per-chunk listener see the new cell. **Mutation that trips it:** also delete `MarkCellChanged(cell)` from `GetOrCreate` → the chunk token stalls and this fires. This keeps the fix from swinging past its target (the standing rule: turn it down, don't tear it out).

The contract scan already forbids `TerrainRevision++` inside `GridCellDataComponent.Eviction.cs` (`tests/addon_contract_scan.ps1:2095-2099`); this fix is the same "a non-content change must not bump the content token" rule one method over, and the smoke assertion above is its enforcement point. No baseline-probe change is needed — generation output is unchanged (dense/pre-generated maps never reach the removed line).

## Dependencies / collisions

- **ENH-02** (edit-kind classification, DONE) — this completes ENH-02's intent one layer down: ENH-02 classified the *signal*, this stops the *revision* from contradicting it. No code overlap; ENH-02's smoke is the file this extends.
- **ENH-01** (typed `CellsChanged` + affected-chunks payload, DONE) — unaffected: `GetOrCreate` does not emit `CellsChanged`; the callers do, with their existing kinds and chunks.
- **DUP-13** (`TerrainKindCatalog`) — independent; touches no shared code.
- **ENH-12** (job-queue spatial claim index) — unrelated subsystem.
- **Concurrent-session collision:** a separate session owns `TerrainWorldComponent`/streaming/archive. This change is confined to `GridCellDataComponent.GetOrCreate` and the smoke test; it touches neither the streaming path nor `Publication.cs`/archive files. `GetChunkRevision` semantics (which the archive reads) are unchanged — `MarkCellChanged` stays.

## Out of scope

- The renderers' gate condition itself (`TerrainRevision != _buildRevision`) — it is correct; the fix is that the revision must only move on real terrain changes, not that renderers should gate differently.
- Windowed/incremental rebuild of the surface renderers on a real terrain edit — a separate performance concern, untouched here.
- `SetMetadata`'s split between the four cached-field keys and other `terrain_*` keys — pre-existing and correct; this plan only makes new-cell behaviour match it.
- Any change to the per-chunk content token, eviction, or publication paths.
