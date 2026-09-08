# ENH-11 — Autotile renderer: configuration key computed on change, not per frame

**Type:** enhancement (per-frame cost) · **Area:** `TerrainIsometricAutotileRendererComponent` · **Status:** **IMPLEMENTED 2026-09-08** · **Effort:** XS (took ~2 hours) · **Risk:** none

## Outcome

`BuildConfiguration()` and the `_buildConfiguration` string are gone. `RequestRebuild` records what the paint starts from (window, paths, terrain set, connections flag, a copy of the bindings array, the `TileSet` reference and — in generated-preview mode — the generator's `TerrainGenerationSettings` record via `CaptureGenerationSettings()`), and `_Process` asks `BuildIsStale()`, a field-by-field compare with an early-out. Verified: `dotnet build` clean; `tests/terrain_autotile_staleness_probe.gd` green in the gate; **3 of 3 mutations trip a guard** (1 on the scan pin, 2 on the probe).

Two things differed from the plan:

1. **The generator check compares a record, not a hash of exports.** The plan proposed a `HashCode.Combine` over the exports. Reading the generator showed it already has the right key: `FieldFor(settings)` caches the field on `TerrainGenerationSettings` equality, and `CaptureGenerationSettings()` exposes that record. Comparing it is cheaper than any hash of raw exports *and* exactly the right question — only a change that moves the record can change the field being painted — where the old JSON key restarted the paint on export edits that never reached the field (`GenerateOnReady`, for instance).
2. **The probe's first version could not fail.** Two blind `await process_frame`s before changing `BoundsSize` were not "mid-paint": the diagnostic showed `CellsProcessedLastFrame == 0` after both, so the iterator had not yet read `BoundsSize` and simply started from the new value — a mutation that removed the size compare passed. The probe now waits for observed progress (`CellsProcessedLastFrame > 0`) before mutating, and the same mutation fails with "holds 1600 cells, not the 100 of the new window". The seed case had only tripped by timing luck and got the same fix.

The pin caught something too, live: it tripped on my own doc comments, which mentioned the removed API by name. The comments were reworded rather than the pin loosened — a pin that reads the whole file is the right shape for "this call must not come back".

## Finding

`TerrainIsometricAutotileRendererComponent.BuildConfiguration()` serialises the renderer's settings (biome → terrain-set assignments, tile set path, bounds, `UseTileSetTerrains`, …) with `Json.Stringify` into a string key used to decide whether the pending time-sliced paint must restart. It is called **every frame while a paint is in progress** (the pending-layer loop compares the key each `_Process`), so a 128×80 map paints while re-serialising its configuration ~60 times a second; on the streamed sizes ENH-07 targets that loop runs for many frames.

The same class already tracks `TerrainRevision` (2 sites) to detect content changes — the configuration key is only needed when an export changes.

## Design

- Compute the key in the export setters (or once in `Rebuild()`/`_Ready`) into `_configurationKey`; the paint loop compares the cached string.
- Replace `Json.Stringify` with a `HashCode.Combine` over the typed fields (a `record struct AutotileConfiguration` with value equality) — no string, no allocation, and the record is the single description of "what makes a paint stale".
- `BuildConfiguration` disappears; the record's constructor is the one place the fields are listed.

## Guards (fail first)

- Probe: start a paint on 128×80; during the pending loop assert zero calls to `Json.Stringify` (pin: not referenced from the file) and that changing `UseTileSetTerrains` mid-paint still restarts it (the record must catch a real change). **Mutation:** keep the key constant → the mid-paint change is missed.
- `terrain_guards.ps1` `iso_layers` stays green.

## Dependencies / collisions

None. Can land any time.

## Out of scope

Peering bits (art), `SetCellsTerrainConnect` batching (ENH-07).
