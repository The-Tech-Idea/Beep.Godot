# FEAT-12 — Spawn markers and playable cordon as map data

**Type:** feature (genre precedent: OpenRA maps carry `Bounds` plus a mandatory one-cell cordon and `mpspawn` actors assigned by `Spawn` index; BGB-13's output contract already foresees a `Spawns` node) · **Area:** `ecs/terrain/TerrainSpawnMarkers.cs` (new helper), `ecs/grid/GridStartAreaComponent.cs` (FEAT-09), `GridNavigationComponent.cs`, `docs/game-builder/TILEMAP_OUTPUT.md` · **Status:** Implemented 2026-09-16 · **Effort:** S–M (2–3 days) · **Risk:** low

## As landed (2026-09-16)

Built as designed, with these differences:

- **The emitter arrives with a caller.** The design left emission to BGB-13's publisher, which does
  not exist, so nothing would have called `Emit`. `TerrainWorldComponent.SpawnsPath` (optional) is
  the caller: `Draw` rewrites the markers after the gameplay grid is bound, on every build and
  redraw, so the markers always describe the world that was last built. BGB-13's publisher calls the
  same helper.
- **Markers carry `unusable`, not `faction_id`/`locked`.** A start the generator reported as
  unplayable is a fact a native map has no report to carry, so the marker carries it. A faction
  assignment is FEAT-10's and nothing reads it yet; a locked start has no consumer either, and an
  authored marker can still carry any metadata a game wants.
- **`GridStartAreaComponent.GridPath`** was needed: a marker's position becomes a cell through the
  grid. A `SpawnsRootPath` that does not resolve, or a missing grid, warns and answers `NoCell`
  rather than quietly reading the data layers, which is how a published map and a regenerated field
  come to disagree about where a player begins.
- **`HasAreas` asks the cells, through a new `GridCellDataComponent.HasStartAreas`.** The flag is
  set where a reservation can enter the store - a bulk load, a publication, a `SetMetadata` write -
  and cleared only when the store is replaced, so it costs nothing per query. Evicting a chunk does
  not clear it: the map still has areas.
- **Footprints are published but not re-exposed.** `OriginOf` is the anchor; a second accessor for
  the marker's `hq_footprint` would have had no engine consumer. The metadata is part of the
  published contract, which the output document now specifies.
- **Two fixes the inset dragged in**, both found by reading the navigation component:
  - `_GetConfigurationWarnings` returned on its first warning, so the inset warning this item needs
    could not be added without accumulating them. It now returns every warning it finds.
  - The async path-request staleness key (`Configuration`) tracked `UseBounds`, `BoundsOrigin` and
    `BoundsSize` but would not have tracked the inset, so a route found under the old cordon could
    still be handed back after the cordon moved. `EffectivePlayableInset` is now part of the key.

**Guards:** `tests/terrain_spawn_markers_probe.gd` (headless, registered), covering all three cases
the plan asks for. The `Spawns` node sits at a deliberate offset from the map root, so a marker
written in the wrong space cannot land on the right cell by accident.

- A generated world writes one marker per start; each carries its index and footprint;
  `GridStartAreaComponent` reads the same cells back with no generator and no data layers wired; a
  rebuild republishes them.
- An authored map - two hand-built markers, no reservations - answers `OriginOf(1)`, reports
  `HasAreas` false, and leaves the build restriction inert, warning exactly once across three
  queries (`GridPlacementComponent.StartAreaWarnings`).
- `PlayableInset = 1` puts the outer ring out of play, `IsInBounds` is false there and placement
  refuses it as `out_of_bounds`, while the cell inside is allowed; an oversized inset is bounded so
  the middle of the map stays playable.

**Mutations**, each failing its own check: emitting in global instead of map-root space (markers
read back ten cells out); ignoring the inset in `IsInBounds`; `HasAreas` ignoring the cells (the
restriction refused an unreserved map and warned zero times); and markers not winning over the data
layers.

**Found while documenting, fixed here:** `GridCellDataComponent.ClearCells()` emptied the store
without clearing `HasStartAreas`, so a cleared component reported a map with areas whose every cell
read as outside one - the exact answer the flag exists to prevent. The probe now clears the cells,
checks the flag follows, and regenerates. **Mutation:** removing the reset failed that check.
`TerrainSpawnMarkers.Count` was dropped: it was an orphan introduced by this change, with `Emit`,
`Clear` and `Find` each having a caller.

**Contract pin:** the published names and metadata keys (`Spawns`, `Start_`, `start_index`,
`hq_footprint`, `unusable`) and the map-root positioning are pinned as literals, and
`GridStartAreaComponent` must read markers through `TerrainSpawnMarkers.Find` rather than its own
name rule. **Mutation:** renaming `start_index` failed the scan (17 against the 16 pre-existing).

**Note on the warning check:** it lives in the GDScript probe rather than `GridPlacementSmoke`,
because `tests/runtime_smoke.ps1` treats any C# backtrace in its output as fatal and a deliberate
`GD.PushWarning` prints one. `StartAreaWarnings` is public for that reason.

## Gap

`TerrainStructureLayerComponent` never writes logical data (`TerrainStructureLayerComponent.cs:5`); the recipe has no authored-start slot (`TerrainWorldComponent.cs:429-448`); the native-map output contract foresees `Spawns (Node2D)` with `Marker2D` children (`docs/game-builder/TILEMAP_OUTPUT.md:19`) but nothing emits or reads them. The playable area is `GridNavigationComponent`'s bounds rectangle (`UseBounds`, `BoundsOrigin`, `BoundsSize`, `:30-32`; `IsInBounds`, `:355-364`) with no cordon; the generator's `OceanMarginTiles` (`TerrainGeneratorComponent.cs:176`) is terrain, not a playable inset. A designer therefore cannot author a fixed map with placed start areas, and a generated map published natively loses its starts.

## Design

1. **Convention (one owner, documented in the contract):** `Spawns/Start_<k>` `Marker2D`, position = the HQ anchor cell centre in the map root's space, metadata `start_index` (int), `hq_footprint` (Vector2I), optional `faction_id` and `locked` (bool).
2. **Emission:** `TerrainSpawnMarkers.Emit(root, field.StartAreas, grid)` — called by BGB-13's publisher (BGB-13 owns the publisher; this item owns the helper and the convention), so a generated map published natively keeps its spawns.
3. **Reading:** `GridStartAreaComponent.SpawnsRootPath` — when set, origins and footprints come from the markers (`WorldToCell` through the grid) and win over the data layers; membership still comes from cells (`terrain_start_area`, present in the Beep profile through BGB-04). Native-profile maps have spawns but no areas: `HasAreas == false`, and FEAT-09's placement restriction is inert with exactly one warning. `OriginOf(k)` therefore has one answer whatever the map's origin — no second carver for authored maps.
4. **Cordon:** `GridNavigationComponent.PlayableInset` (default 0), read by `IsInBounds`; every consumer already exists (`WhyNot` `out_of_bounds`, job bounds, the spawner, camera bounds). BGB-02's `MapDefinition` records it for authored maps.

## Guards (fail first)

- Emit from a generated field, read back with no generator wired: `OriginOf(k)` equals `GetStartPositions()[k]` for every k. **Mutation:** emit in local instead of map-root space → mismatch.
- Authored scene with two markers and no data layers: `OriginOf(1)` correct, `HasAreas == false`, the placement restriction inert with exactly one warning.
- `PlayableInset = 1`: an edge cell has `IsInBounds == false` and `WhyNot == "out_of_bounds"`. **Mutation:** ignore the inset → true.

## Dependencies / collisions

FEAT-09 (the report shape and the component). BGB-13 (the publisher calls `Emit`), BGB-04 (the Beep profile carries the cell key), BGB-02 (the definition slot for the inset).

## Out of scope

OpenRA's `Players` block (allies, enemies, starting cash) — session data owned by `PlayerContextComponent` and the game, not map data.
