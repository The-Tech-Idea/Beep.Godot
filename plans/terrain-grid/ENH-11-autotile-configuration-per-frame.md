# ENH-11 — Autotile renderer: configuration key computed on change, not per frame

**Type:** enhancement (per-frame cost) · **Area:** `TerrainIsometricAutotileRendererComponent` · **Status:** proposed 2026-09-08 · **Effort:** XS (½ day) · **Risk:** none

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
