# IStorage

C# interface for a stationary hold — a tank, a silo, a warehouse, a pipeline buffer. It composes `ILoadPort` and `IUnloadPort` and adds nothing of its own; that emptiness is the point.

The interface is deliberately body-less: storage is defined purely as "both ports and nothing else," which is exactly why it is not an `ITransporter` — it cannot be asked to move. Where `ITransporter` adds mobility members (`IsBusy`, `TransportRate`, `RequestHaul`) on top of the same two ports, `IStorage` adds nothing, making the distinction between a fixed buffer and a mobile one purely about which members are present. `GridStorageComponent` is the shipped implementation.

## Public API
- (inherited from `ILoadPort`) `Capacity`, `CurrentLoad`, `CanAccept(string)`, `Load(string, int)`.
- (inherited from `IUnloadPort`) `Stored(string)`, `StoredIds()`, `Unload(string, int)`.

No members declared directly on `IStorage` itself.

## Dependencies
Composes `ILoadPort` and `IUnloadPort` from this same batch. Not established from this batch alone what concrete type implements it or calls into it; `GridStorageComponent` is named in the doc comment but is outside this batch's file set.

## Notes
- The entire interface body is empty (`{ }`) — this is not a stub or an oversight, the doc comment states it directly: storage is "both ports and nothing else," and the emptiness is what distinguishes it from `ITransporter` in the same batch, which adds mobility on top of the identical two-port composition.
