# GridExtractionManagerComponent

The registry of everything currently extracting. Every derrick, mine and custom rig announces itself here on `_Ready`, and anything that wants a picture of extraction activity — a HUD, an objective tracker, game logic gating on "is anyone working platinum" — asks this one node instead of crawling the scene tree for `GridExtractorComponent` instances.

It is expandable by registration, not by type: `Register` accepts any `Node` that answers the `IGridExtractor` shape by name (`Get("IsExtracting")`, `Get("ActiveResourceId")`, optionally `CurrentAmountPerCycle`/`CurrentCycleTurns` for rate queries), so a GDScript extractor participates exactly like the shipped C# `GridExtractorComponent` — which registers itself automatically once a manager exists in the scene (gated by its `RegisterOnReady` export). `Register` validates the shape before accepting a registrant, the same way `GridTransportManagerComponent.Register` does — by property presence rather than `HasMethod`, because the extractor contract is answered by property — and reports a refusal with a named warning and a `false` return. Without that, a malformed registrant joined silently and only failed later, at read time.

## Public API
- `bool Register(Node extractor)` — adds an extractor to the registry and reports whether it did. Returns `false` for null, a freed instance, or a node that does not answer `IsExtracting`/`ActiveResourceId` (with a `GD.PushWarning` naming it); returns `true` for a duplicate, which is a no-op. Emits `ExtractorRegistered` on a genuine add. The caller records the result — `GridExtractorComponent` sets `_registered` from it rather than assuming success, so a refused extractor keeps retrying instead of believing it was registered.
- `void Unregister(Node extractor)` — removes an extractor; emits `ExtractorUnregistered` only if the node is still a valid instance (a freed node unregisters silently).
- `int ExtractorCount` — count after pruning freed nodes.
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
- Pruning (`Prune()`) is lazy — run on every read (`ExtractorCount`, `Extractors()`, `ActiveCountFor`, `EstimatedRatePerTurn`), not on a timer or on `_ExitTree` of the tracked node itself (aside from the explicit `Unregister` call), so a manager that is never queried can carry stale freed-node entries indefinitely without harm (they're inert until the next call touches them).
