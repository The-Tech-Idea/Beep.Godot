# GridTransportManagerComponent

The dispatcher between "something needs moving" and "something that moves things." A producer — the shipped extractor delivering via `TransportManager`, or any game script — calls `RequestHaul`; the manager offers the job to its registered transporters, fastest (`TransportRate`) first, and the first one that accepts takes it. It never decides what hauling *means* — each transporter does; the manager only orders candidates and hands off the job.

Like `GridExtractionManagerComponent`, it is expandable by registration rather than by type: `Register` accepts any `Node` that answers the `IGridTransporter` shape by name — `IsBusy`, `CanAccept`, `Load`, `Unload`, `RequestHaul` — so a GDScript truck, train, drone or conveyor head participates exactly like the shipped `GridHaulerComponent`. Unlike `GridExtractionManagerComponent.Register`, this one *does* validate the shape before accepting a registrant and pushes a `GD.PushWarning` and refuses if it's missing any of the four required methods.

## Public API
- `void Register(Node transporter)` — validates the transporter answers `RequestHaul`, `CanAccept`, `Load`, `Unload` (all via `HasMethod`); rejects with a `GD.PushWarning` naming the missing contract if not. Ignores null, freed, or duplicate registrants. Emits `TransporterRegistered`.
- `void Unregister(Node transporter)` — removes a transporter; emits `TransporterUnregistered` only if still a valid instance.
- `int TransporterCount { get; }` — count after pruning freed nodes.
- `bool RequestHaul(Vector2I fromCell, string resourceId, int amount)` — offers the haul to every non-busy (`Get("IsBusy")`), accepting (`Call("CanAccept", resourceId)`) registered transporter, ordered by `OrderCandidates`, and calls `Call("RequestHaul", ...)` on each in order until one returns true. Emits `HaulAssigned` on success or `HaulUnassigned` if none took it — a `false` return leaves the load the caller's problem (the shipped extractor falls back to the wallet).
- `protected virtual void OrderCandidates(List<(Node Transporter, float Rate)> candidates)` — HOOK: dispatch policy. Default sorts fastest-first by `TransportRate`; override for nearest-first, round-robin, cost models, or any other notion of "the right vehicle."
- `int Transfer(Node from, Node to, string resourceId, int amount)` — thin public wrapper over `GridPorts.Transfer`; the primitive a pipeline (or anything hand-rolled) is built from. Unloads from the giver, loads into the receiver, and returns any remainder to the giver so cargo is never duplicated or lost.
- Signals: `TransporterRegistered(Node transporter)`, `TransporterUnregistered(Node transporter)`, `HaulAssigned(Node transporter, int x, int y, string resourceId, int amount)`, `HaulUnassigned(int x, int y, string resourceId, int amount)`.

## Dependencies
- Resolves nothing via `NodePath` — a pure registry/dispatcher, same shape as `GridExtractionManagerComponent`.
- `RateOf(Node)` reads the `TransportRate` property via `Get`, treating a `Nil` result (a registrant without the property) as `1f` rather than throwing — a real, deliberate difference from `GridExtractionManagerComponent.IsActivelyExtracting`'s unguarded `Get("IsExtracting").AsBool()` in this same batch.
- Registered by `GridHaulerComponent._Ready()` (this batch, via `TryRegister()`/`ResolveReferences()`) and unregistered on `_ExitTree()`.
- `RequestHaul` is called by `GridExtractorComponent.DeliverYield` (this batch) when `DeliverVia == ExtractorDelivery.TransportManager`.
- `Transfer` mirrors (does not wrap through) `GridHaulerComponent.TryDeliverCargo`'s own direct call to `GridPorts.Transfer` — both end up at the same static method in `GridPorts.cs` (outside this batch), so there is no divergence, just two call sites reaching the same one implementation.

## Notes
- `Register`'s contract check (`HasMethod("RequestHaul")`, `"CanAccept"`, `"Load"`, `"Unload"`) does not check for `IsBusy` or `TransportRate` — both are only read later via `Get(...)`. `RateOf` explicitly guards a missing `TransportRate` (`Variant.Type.Nil` → defaults to `1f`), but `RequestHaul`'s own `transporter.Get("IsBusy").AsBool()` has no equivalent guard — a registrant that passes the four-method check but never exposes an `IsBusy` property is read through `AsBool()` on a `Nil` variant with no explicit fallback coded here, unlike every other duck-typed property read in this file.
- That asymmetry (`RateOf` guards `Nil`, the inline `IsBusy` read does not) is the one inconsistency worth flagging inside this file — everywhere else, this file's own convention is to guard a `Nil` variant explicitly before trusting it.
