# ITransporter

C# interface for both ports plus MOBILITY: a truck, a boat, a train, a drone. Because a transporter is also a load and an unload port, it hands cargo to any other port — truck to tank, boat to wharf, segment to segment. `GridHaulerComponent` is the shipped implementation, and `GridTransportManagerComponent` is the registry that dispatches hauls to whichever registered transporter accepts.

Like `IExtractor` and `IStorage`, this composes the two atomic port interfaces rather than redeclaring load/unload behavior, but adds the three members a mover needs that a fixed container does not: whether it is busy, how fast it moves, and how to request a haul. `GridTransportManagerComponent.RequestHaul` offers a haul to the fastest accepting registered candidate first, giving the manager-dispatch pattern a shape unrelated to construction (which has no such registry any more - see `IWorker`).

## Public API
- `bool IsBusy { get; }` — whether the transporter is mid-haul and must not be asked.
- `float TransportRate { get; }` — effective throughput, in units per second; a pipeline flows differently than a truck, a truck than a mule. The transport manager offers a haul to the fastest accepting transporter first, so authoring this is how a fleet gets a pecking order.
- `bool RequestHaul(Vector2I fromCell, string resourceId, int amount)` — asks the transporter to move a load from a cell to wherever it delivers; true means it took the whole job, false leaves the manager to try the next one.
- (inherited from `ILoadPort`) `Capacity`, `CurrentLoad`, `CanAccept(string)`, `Load(string, int)`.
- (inherited from `IUnloadPort`) `Stored(string)`, `StoredIds()`, `Unload(string, int)`.

## Dependencies
Composes `ILoadPort` and `IUnloadPort` from this same batch, and uses `Godot.Vector2I`. Not established from this batch alone what concrete type implements it or calls into it; `GridHaulerComponent` and `GridTransportManagerComponent` are named in the doc comment but are outside this batch's file set.

## Notes
- `RequestHaul` returning "true means it took the whole job" (no partial-haul return) is a strict all-or-nothing contract — worth noting if a caller ever needs partial-haul semantics, since none is exposed here.
