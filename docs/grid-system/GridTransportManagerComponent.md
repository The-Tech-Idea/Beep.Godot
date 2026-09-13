# GridTransportManagerComponent

The dispatcher between "something needs moving" and "something that moves things." A producer — the shipped extractor delivering via `TransportManager`, or any game script — calls `RequestHaul`; the manager offers the job to its registered transporters, fastest (`TransportRate`) first, and the first one that accepts takes it. It never decides what hauling *means* — each transporter does; the manager only orders candidates and hands off the job.

Like `GridExtractionManagerComponent`, it is expandable by registration rather than by type: `Register` accepts any `Node` that answers the `IGridTransporter` shape by name — `IsBusy`, `CanAccept`, `Load`, `Unload`, `RequestHaul` — so a GDScript truck, train, drone or conveyor head participates exactly like the shipped `GridHaulerComponent`. Both managers' registry mechanics — the list, the duplicate guard, the count-with-prune and the prune — come from `DuckTypedNodeRegistry`; what this class supplies is its own contract question (`HasMethod`, not a property read), its own refusal wording and its own signals. A registrant missing any of the four required methods is refused with a `GD.PushWarning` that names the contract.

## Public API

Inherited from `DuckTypedNodeRegistry`: `bool Register(Node transporter)`, `void Unregister(Node transporter)`, `int Count` — see that class's doc for the shared contract (null/freed → not accepted; duplicate → no-op that reports success; contract failure → named warning and `false`; both signals emitted from the subclass's `OnRegistered`/`OnUnregistered` overrides).

- `int TransporterCount { get; }` — the domain-named forwarder to `Count`: how many transporters are registered, freed ones pruned first. Kept because HUDs and the GDScript probes read this name.
- `protected override bool AnswersContract(Node transporter)` — `HasMethod` for `RequestHaul`, `CanAccept`, `Load`, `Unload`.
- `bool RequestHaul(Vector2I fromCell, string resourceId, int amount)` — offers the haul to every non-busy (`Get("IsBusy")`), accepting (`Call("CanAccept", resourceId)`) registered transporter, ordered by `OrderCandidates`, and calls `Call("RequestHaul", ...)` on each in order until one returns true. Emits `HaulAssigned` on success or `HaulUnassigned` if none took it — a `false` return leaves the load the caller's problem (the shipped extractor falls back to the wallet).
- `protected virtual void OrderCandidates(List<(Node Transporter, float Rate)> candidates)` — HOOK: dispatch policy. Default sorts fastest-first by `TransportRatePerTurn`; override for nearest-first, round-robin, cost models, or any other notion of "the right vehicle."
- `int Transfer(Node from, Node to, string resourceId, int amount)` — thin public wrapper over `GridPorts.Transfer`; the primitive a pipeline (or anything hand-rolled) is built from. Unloads from the giver, loads into the receiver, and returns any remainder to the giver so cargo is never duplicated or lost.
- Signals: `TransporterRegistered(Node transporter)`, `TransporterUnregistered(Node transporter)`, `HaulAssigned(Node transporter, int x, int y, string resourceId, int amount)`, `HaulUnassigned(int x, int y, string resourceId, int amount)`.

## Dependencies
- Resolves nothing via `NodePath` — a pure registry/dispatcher, same shape as `GridExtractionManagerComponent`.
- `RateOf(Node)` reads the `TransportRatePerTurn` property via `Get`, treating a `Nil` result (a registrant without the property) as `1f` rather than throwing — still a real, deliberate difference from `GridExtractionManagerComponent.IsActivelyExtracting`'s `Get("IsExtracting").AsBool()`, which relies on `AnswersContract` having refused anything that does not expose the property rather than guarding the read itself.
- Registered by `GridHaulerComponent._Ready()` (this batch, via `TryRegister()`/`ResolveReferences()`) and unregistered on `_ExitTree()`.
- `RequestHaul` is called by `GridExtractorComponent.DeliverYield` (this batch) when `DeliverVia == ExtractorDelivery.TransportManager`.
- `Transfer` mirrors (does not wrap through) `GridHaulerComponent.TryDeliverCargo`'s own direct call to `GridPorts.Transfer` — both end up at the same static method in `GridPorts.cs` (outside this batch), so there is no divergence, just two call sites reaching the same one implementation.

## Notes
- `Register`'s contract check lives in `AnswersContract` (`HasMethod("RequestHaul")`, `"CanAccept"`, `"Load"`, `"Unload"`) and does not check for `IsBusy` or `TransportRatePerTurn` — both are only read later via `Get(...)`. `RateOf` explicitly guards a missing `TransportRatePerTurn` (`Variant.Type.Nil` → defaults to `1f`), but `RequestHaul`'s own `transporter.Get("IsBusy").AsBool()` has no equivalent guard — a registrant that passes the four-method check but never exposes an `IsBusy` property is read through `AsBool()` on a `Nil` variant with no explicit fallback coded here, unlike every other duck-typed property read in this file.
- That asymmetry (`RateOf` guards `Nil`, the inline `IsBusy` read does not) is the one inconsistency worth flagging inside this file — everywhere else, this file's own convention is to guard a `Nil` variant explicitly before trusting it.
