# ENH-15 — Unit and contract drift: seconds vs turns, `GatherSeconds`, catalog lookups

**Type:** correctness / doc drift · **Area:** `ITransporter`, `GridHaulerComponent`, `GridTransportChainComponent`, `ResourceDefinition`, `GridExtractorComponent`, `GridResourceNodeComponent`, `ResourceCatalog` · **Status:** **IMPLEMENTED 2026-09-09** (unit renames + ResourceCatalog index; only the minor ForTerrain index left) · **Effort:** XS–S (½–1 day) · **Risk:** low

## Outcome (finding 3: the catalog index, 2026-09-09)

`ResourceCatalog.Find` was a linear ordinal-`==` scan; it is now a lazily-built `Dictionary<string, ResourceDefinition>` keyed by `GridIds.Normalize(Id)` (DUP-05), rebuilt when `Resources.Count` changes (an authored catalog loads once; a runtime add/remove moves the count). Besides O(1), this closes the real bug the plan names: a cost spelled `"Crude Oil"` and a map id `"crude_oil"` now resolve to the one definition instead of reading as two, because both the keys and the query go through the same normaliser. "First wins" on a duplicated normalised id matches the scan it replaces. `Contains`/`CategoryOf`/`DisplayNameOf` ride the same index through `Find`.

`GridPlacementSmoke.VerifyResourceCatalogFind` (new) asserts `Find("Crude Oil")`, `Find("crude_oil")` and `Find("crude-oil")` all return the one definition and `Find("iron")` is null; mutation-proven (keying the index by the raw ordinal Id fails it). A scan pin requires the normalised index and forbids the `definition.Id == id` scan's return. Build clean; full HUD smoke green (past the two pre-existing placement reds, reverted).

**Outcome: the unit renames (findings 1, 2, 4), 2026-09-09.** `ResourceDefinition.GatherSeconds` -> `GatherTurns` (it was always read as turns; the "seconds" name was the lie), including the two mirror `[Export]`s on `GridResourceNodeComponent` and `GridResourceScatterComponent`, `GridResourceScatterComponent.EffectiveGatherSeconds` -> `EffectiveGatherTurns`, the `GridExtractorComponent` reader (whose comment no longer has to apologise for the name), and the one authored value in `grid_world_2d_iso.tscn` (`GatherSeconds = 1.5` -> `GatherTurns = 1.5`, same number, same meaning). `ITransporter.TransportRate` -> `TransportRatePerTurn` with its doc corrected from "units per second", plus the sole implementer `GridHaulerComponent`, the manager's `Get("TransportRatePerTurn")` string lookup, and the GDScript duck-typed transporter in `grid_terrain_subsurface_probe.gd` (`GdTransporter.TransportRatePerTurn`) - the exact "a GDScript transporter written to the interface" case the finding named, which the probe now proves aligns (the manager still ranks the fast transporter first). No `.tres` and no other `.gd` carried these names. Build clean; the subsurface, job-execution, haul-demand and economy probes green; a scan pin forbids `GatherSeconds` anywhere under `addons/` and `per second` in any logistics port interface, both mutation-proven.

**Still open (minor): `ForTerrain`'s nested-loop index** - lower value (the catalog is 12-20 entries) and it returns a fresh array each call, so caching it needs list invalidation; left for a dedicated pass.

## Finding

The tracker's "closing the structure plan" entry renamed the grid's work fields to turns and pinned that no grid file measures work in seconds. Three drifts survive at the seams:

1. **`ITransporter.TransportRate`** doc says "units per second"; `GridHaulerComponent` and `GridTransportChainComponent` implement it as **units per turn** (the whole-line backpressure math divides by turns). A GDScript transporter written to the interface doc will be 60× off on the turn axis.
2. **`ResourceDefinition.GatherSeconds`** (`ResourceDefinition.cs:507`) is read as **turns** by `GridExtractorComponent.CycleTurns` (`:576-586`, with a comment explaining the catalog "is not this component's to rename") and presumably by `GridResourceNodeComponent`'s gather timing. The field is authored in the Inspector as "seconds"; the extractor's own doc has to explain the lie. The catalog *is* the addon's (`ecs/terrain/ResourceDefinition.cs`), so renaming is in scope: `GatherTurns`, with `ResourceCatalogs` updated (the three shipped catalogs set `GatherSeconds` nowhere explicitly — defaults only — so the rename is mechanical).
3. **`ResourceCatalog.Find`** is a linear scan (`ResourceCatalog.cs:29-40`) called per extractor cycle (`Catalog?.Find(ActiveResourceId)` twice per `RunCycle` via `CycleTurns`/`AmountPerCycle`) and per resource node `_Ready`; with 12–20 entries it is cheap, but `ForTerrain` (nested loop) is called by the generator's rules capture and by HUD filters. A lazily built `Dictionary<string, ResourceDefinition>` keyed by `GridIds.Normalize(Id)` (DUP-05) invalidated when `Resources` changes gives O(1) and also fixes case/space mismatches between the map's ids and a game-authored catalog.
4. **Doc drift already noted in the tracker but not yet in the interface:** `GridWorkClockBinding.TurnsForDelta` treats one turn as one second on the real-time axis; `IExtractor`/`ITransporter`/`IStorage` interface docs should state the unit once ("turns; one turn is one second on the real-time axis") rather than each implementer re-explaining.

## Design

- Rename `ITransporter.TransportRate` → `TransportRatePerTurn` (or fix the doc and add `[Obsolete]`-free rename per the no-legacy rule: rename, compiler sweep, GDScript templates updated by scan).
- Rename `ResourceDefinition.GatherSeconds` → `GatherTurns`; update `GridExtractorComponent.CycleTurns`, `GridResourceNodeComponent`, any `.tres` catalog files (Python scan for `GatherSeconds` across `*.tres`/`*.tscn`).
- Index on `ResourceCatalog`; `ForTerrain` builds a per-kind list lazily.
- One "Units" section in `docs/grid-system/ENHANCEMENT_AND_FIX_PLAN.md`'s interface pages: turns everywhere; seconds only in `GridDispatchBoardComponent` (DUP-11).

## Guards (fail first)

- Pin: `GatherSeconds` appears nowhere under `addons/`; `units per second` appears in no `ecs/grid/I*.cs`. Mutation: restore the doc string → fails.
- Smoke: extractor with `GatherTurns = 2` on the turn axis draws exactly one cycle per two `EndTurn`s (existing extraction smoke extended). Mutation: read the field as seconds → cycles per turn wrong.
- Probe: `ResourceCatalog.Find("Crude Oil")` and `Find("crude_oil")` return the same definition. Mutation: keep ordinal `==` → the first returns null.

## Dependencies / collisions

DUP-05 for normalisation. Touches `ecs/terrain/ResourceDefinition.cs` and `ecs/grid/` logistics — small, but coordinate the rename.

## Out of scope

Economy balance; turn length semantics (`GridWorkClockComponent`).
