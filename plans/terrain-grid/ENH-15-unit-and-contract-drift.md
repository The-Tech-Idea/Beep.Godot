# ENH-15 — Unit and contract drift: seconds vs turns, `GatherSeconds`, catalog lookups

**Type:** correctness / doc drift · **Area:** `ITransporter`, `GridHaulerComponent`, `GridTransportChainComponent`, `ResourceDefinition`, `GridExtractorComponent`, `GridResourceNodeComponent`, `ResourceCatalog` · **Status:** proposed 2026-09-08 · **Effort:** XS–S (½–1 day) · **Risk:** low

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
