# ENH-10 — Saving a streamed world: the archive is the save, not a throw

**Type:** enhancement / correctness (save of huge worlds) · **Area:** `GridWorldStateComponent`, `GridCellDataComponent.Snapshots`, `GridCellArchiveComponent`, `GameStateManagerComponent` (`ISaveable`) · **Status:** proposed 2026-09-08 · **Effort:** M (2–3 days) · **Risk:** medium (save format; data safety)

## Finding

`GridWorldStateComponent.CaptureState` (`:75-76`) writes `state["cell_chunks"] = _cellData.CaptureChunkState()`. `CaptureChunkState` (`GridCellDataComponent.Snapshots.cs:162-163`) **throws** when any chunk is unavailable — i.e. on any streamed world with at least one chunk archived out. So a huge world cannot be saved through the game's normal `ISaveable` path at all; the throw surfaces as a failed save in `GameStateManagerComponent`.

Two other facts make the fix straightforward:

- The archive already persists every non-resident chunk as a verified file (`GridCellArchiveComponent`, SHA-256 eviction verify, atomic write), and knows per chunk whether the file is current (`IsChunkSaveCurrent`).
- A save of a streamed world therefore needs: the resident chunks' content (as now) **plus** a manifest naming the archive directory and each archived chunk's revision/hash — not the archived content itself.

The `SAVE_SNAPSHOTS.md` doc and the campaign plan both assume one active-level payload; a manifest fits that shape.

## Design

```
grid_world.state
├─ cell_chunks: [ …resident chunks as today… ]
└─ archive:
   ├─ directory: "user://saves/<slot>/world_<levelId>/"     # save-slot scoped, not the live archive dir
   ├─ chunks: { "3,7": { revision, sha256, bytes }, … }      # every non-resident chunk
   └─ format: 2
```

- **Capture:** `GridCellDataComponent.CaptureChunkState(includeUnavailable: false)` returns resident chunks; `GridCellArchiveComponent.CaptureManifest()` returns the archived set with hashes. `GridWorldStateComponent` writes both. Before capture it calls `archive.FlushDirty()` — every resident chunk whose archive file is stale is saved (through the existing worker pipeline, awaited by the save's completion since `GameStateManagerComponent` saves are already asynchronous-capable per `SAVE_SNAPSHOTS.md`; if not, the save requests it and reports `pending`).
- **Slot isolation:** the live archive directory belongs to the running world; a save **copies** (hard-links where the OS allows) the referenced chunk files into the slot directory so loading slot A cannot read chunks slot B has since overwritten. Copy is by manifest (only referenced files), verified by hash.
- **Restore:** `RestoreState` loads resident chunks as now, points the archive at the slot's directory (read-only source; writes go to a fresh live directory copy-on-evict), and marks manifest chunks *available-in-archive* so demand loads fetch them. `GridCellDataComponent` gets `SetChunkArchived(chunk)` for that.
- **Failure reporting:** a manifest whose hash does not match on load fails the load with `archive_chunk_corrupt:<chunk>` through the save manager's failure path — never a silent partial world.

## Guards (fail first)

- Probe: 128×128 world, evict 10 chunks, `Saves.Save(0)` succeeds (today: throws). **Mutation:** restore the `includeUnavailable` throw → save fails.
- Round-trip probe: edit a cell in a resident chunk and one in an archived chunk (load → edit → evict), save, clear, load: both edits present; the archived chunk arrives via demand load; `GetCell` equal to pre-save.
- Isolation probe: save slot 0, evict/edit/save slot 1, load slot 0 → slot 0's content. Mutation: reference the live directory instead of copying → slot 0 shows slot 1's edit.
- Corruption probe: flip a byte in a slot chunk file → load fails with `archive_chunk_corrupt`, world untouched.

## Dependencies / collisions

**Direct collision** with the other session (`GridCellArchiveComponent`, `GridCellDataComponent.Snapshots`) and with the campaign session's save/level work (`GameStateManagerComponent`, stable entity ids). Sequence after both have landed their current work. Uses ENH-04's parallel load/save.

## Out of scope

Entity (worker/hauler) persistence — the campaign plan owns it. Archive format.
