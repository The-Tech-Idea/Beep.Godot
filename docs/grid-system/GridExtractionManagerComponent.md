# GridExtractionManagerComponent

The registry of everything currently extracting. Every derrick, mine and custom rig announces itself here on `_Ready`, and anything that wants a picture of extraction activity — a HUD, an objective tracker, game logic gating on "is anyone working platinum" — asks this one node instead of crawling the scene tree for `GridExtractorComponent` instances.

It is expandable by registration, not by type: `Register` accepts any `Node` that answers the `IGridExtractor` shape by name (`Get("IsExtracting")`, `Get("ActiveResourceId")`, optionally `CurrentAmountPerCycle`/`CurrentCycleTurns` for rate queries), so a GDScript extractor participates exactly like the shipped C# `GridExtractorComponent` — which registers itself automatically once a manager exists in the scene (gated by its `RegisterOnReady` export). The list, the duplicate guard, the count-with-prune and the prune are `DuckTypedNodeRegistry`'s; this class supplies its own contract question — by property presence rather than `HasMethod`, because the extractor contract is answered by property — its own refusal wording, and its own signals. Without the shape check a malformed registrant joined silently and only failed later, at read time.

## Public API

Inherited from `DuckTypedNodeRegistry`: `bool Register(Node extractor)`, `void Unregister(Node extractor)`, `int Count` — see that class's doc for the shared contract (null/freed → not accepted; duplicate → no-op that reports success; contract failure → named warning and `false`). The `bool` matters to this manager's caller: `GridExtractorComponent` sets `_registered` from it rather than assuming success, so a refused extractor keeps retrying instead of believing it was registered.

- `protected override bool AnswersContract(Node extractor)` — the by-property shape question (`IsExtracting`, `ActiveResourceId`; a missing property reads as a `Nil` Variant).
- `int ExtractorCount => Count` — the domain-named forwarder, kept because HUDs and the GDScript probes read this name.
- `Godot.Collections.Array<Node> Extractors()` — the registered extractors, pruned of freed nodes, as a fresh array.
- `int ActiveCountFor(string resourceId)` — how many registered extractors are actively working the given resource (empty `resourceId` counts every actively-extracting node, regardless of what it's extracting).
- `float EstimatedRatePerTurn(string resourceId)` — units per TURN summed over active extractors for a resource; the unit is the turn because a cycle is, so the number reads as units per in-game day on either time axis. An extractor missing `CurrentAmountPerCycle`/`CurrentCycleTurns` contributes 0 (counts as unknown, not a fault).
- Signals: `ExtractorRegistered(Node extractor)`, `ExtractorUnregistered(Node extractor)`.

## Dependencies
- Resolves nothing via `NodePath` — it is a pure registry with no `ResolveReferences()`.
- `GridExtractorComponent._Ready()` calls `TryRegisterWithManager()` (in this same batch), which resolves an `ExtractionManagerPath` and calls `Register(this)` — the one confirmed caller in this batch. `GridExtractorComponent._ExitTree()` calls `Unregister(this)`.
- Read by HUDs/objective logic outside this batch (not established from this batch alone).

## Notes
- The duck-typed reads in `IsActivelyExtracting` (`Get("IsExtracting")`, `Get("ActiveResourceId")`) are safe *because* `Register` refuses any node that does not answer both — the shape check happens once, at the door, instead of on every read. `tests/grid_terrain_subsurface_probe.gd` asserts a bare `Node` is refused and never counted.
- Pruning (`DuckTypedNodeRegistry.Prune()`) is lazy — run on every read (`Count`/`ExtractorCount`, `Registered`/`Extractors()`, `ActiveCountFor`, `EstimatedRatePerTurn`), not on a timer or on `_ExitTree` of the tracked node itself (aside from the explicit `Unregister` call), so a manager that is never queried can carry stale freed-node entries indefinitely without harm (they're inert until the next call touches them).
- What prune is actually FOR: a registrant that vanishes without unregistering. A freed rig is *not* that case — its `_ExitTree` calls `Unregister` first, so the entry never goes stale and the prune is never what removed it. `tests/grid_terrain_subsurface_probe.gd` asserts the real case with a registrant that has no `_exit_tree` cleanup: it is counted, freed, and the next read drops it. Without that assertion the shared `Prune` would have had no behavioural guard at all.
