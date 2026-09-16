# FEAT-11 — Zones and district range: named cell sets consumers query

**Type:** feature (genre precedent: RimWorld zones and areas — the home area auto-expands around anything built, allowed areas restrict work not pathing, growing zones only on fertile terrain; Timberborn districts — buildings beyond 70 *path* tiles of the centre cannot be worked; Northgard zones with a per-zone building capacity; Anno 1800 street-distance influence) · **Area:** `ecs/grid/GridZoneComponent.cs` (new), `GridWorldStateComponent.cs` (`CaptureZones`), `GridWorkerComponent.cs`, `GridPlacementComponent.cs`, `GridCellOverlayComponent.cs` · **Status:** proposed 2026-09-15 · **Effort:** M (3–4 days) · **Risk:** low–medium

## Gap

The only work restriction is by kind — `GridWorkerComponent.AllowedJobKinds` (`:48`) — never by place. Scatter bounds are a rectangle (`GridResourceScatterComponent.cs:66-67`). "Reachable cells near home" is computed ad hoc in a shipped scene (`templates/scenes/actors/actor_lab.gd:65-71`). `CellFlags` (`GridCellDataComponent.cs:16-26`) is the workflow bitmask (`Blocked, Cleared, Tilled, Watered, Planted, HarvestReady`); it has no zone, and a cell can carry only one set of flags, whereas RimWorld lets a cell be in a home area *and* a growing zone. Timberborn's district — a building beyond the centre's path range cannot be worked — has no engine expression.

## Design

1. **Model.** `ZoneRecord { ZoneId, Kind, Owner (faction id, "" = none), Cells : HashSet<Vector2I>, Metadata }` with an index per kind `cell → zoneId`: one zone per cell *per kind*; kinds overlap freely. Zones are **not** on `CellRecord` — the road precedent applies: `GridRoadComponent` owns its own cell set and `GridWorldStateComponent` is the sole save owner (`GridRoadComponent.cs:211-226`). `GridWorldStateComponent` gains `ZonesPath` and `CaptureZones` (default true); `SnapshotVersion` 2 → 3 (`GridWorldStateComponent.cs:36`). Signals `ZoneChanged(zoneId, kind)` and `ZoneRemoved(zoneId)`; no new `TerrainChangeKind` bit — zones are not cell content, and terrain renderers must not react to them.
2. **API.** `CreateZone(id, kind, owner)`, `AddCells`, `RemoveCells`, `DeleteZone`, `ZoneAt(cell, kind)`, `IsInZone(cell, id)`, `CellsOf(id)`, `ZonesOfKind(kind)`; `GrowReachable(id, origin, maxPathCost)` — a Dijkstra over `GridNavigationComponent` costs bounded by cost (the district), revalidated lazily against `NavigationRevision` and `OccupancyRevision`; `AutoExpandKind` / `AutoExpandRadius` (default `""`, 0): on `GridPlacementComponent.PlacementPlaced` (`GridPlacementComponent.cs:307`) add a disc around the footprint to the zone of that kind (the RimWorld home area); `CreateFromStartArea(id, kind, startIndex)` seeds a zone from FEAT-09's reservation — one carver, one zone model.
3. **Consumers in the same change.** `GridWorkerComponent.AllowedZoneId` + `ZonesPath`: the claim filter skips jobs whose cell is outside the zone, in the same place `AllowedJobKinds` filters; pathing is not restricted (RimWorld's rule). `GridPlacementComponent.RequiredZoneKind` (`""` = off) → `WhyNot` reason `outside_zone`, checked after `outside_start_area`. `GridCellOverlayComponent` gains `ZonesPath`, `ShowZones` (false), per-kind colours, and redraws on `ZoneChanged`.

## Guards (fail first)

- Worker with `AllowedZoneId = "north"`, two queued jobs (one inside, one outside): only the inside job is claimed. **Mutation:** skip the zone filter → the outside job is claimed.
- `GrowReachable(origin, 10)` with a wall of `Blocked` cells: cells behind the wall are excluded although within 10 Euclidean. **Mutation:** Euclidean instead of path cost → included.
- `RequiredZoneKind = "district"` → `outside_zone` off-district, `""` inside; `AutoExpand("home", 2)` → after a placement the 5×5 is in "home". **Mutation:** drop the `PlacementPlaced` subscription → not in the zone.
- Save round-trip through `GridWorldStateComponent`; evicting a chunk does not drop its zone cells.

## Dependencies / collisions

ENH-03 (chunk-scoped invalidation makes `GrowReachable` refresh cheaper; not required). DUP-08 (the footprint helper). FEAT-09 (optional bridge through `CreateFromStartArea`). `GridWorldStateComponent` and `GridWorkerComponent` are the grid session's files.

## Out of scope

Engine-owned here: the zone model, its save, path-distance growth, auto-expansion, the two consumers and the overlay. Game-owned or follow-on: zone painting tools and UI (a `GridToolActionComponent` brush), stockpile/growing semantics, Northgard's per-zone building capacity and colonisation cost, scatter-in-zone as a further consumer.
