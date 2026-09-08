# ILoadPort

C# interface for the RECEIVING port: anything material can be pushed INTO. It is one of the two atomic connectors the whole logistics layer is built from — `IExtractor`, `IStorage` and `ITransporter` all implement both `ILoadPort` and `IUnloadPort`, which is what lets a developer connect anything to anything: extractor to pipeline, pipeline to tank, tank to truck.

The interface is written down as a real C# contract, but the codebase does not rely on it being *implemented* to make things interoperate — managers (see `GridPorts`) read this same shape by name via duck typing (`HasMethod`/`Get`/`Call`) as well, so a GDScript node with matching members participates without implementing anything, since GDScript cannot implement a C# interface. The interface is the contract written down; duck typing is how it is read at runtime by code that must also work with non-C# nodes.

## Public API
- `int Capacity { get; }` — total units this port's hold takes, across everything in it.
- `int CurrentLoad { get; }` — units currently held. Free space is `Capacity - CurrentLoad`, which is what a planner (a pump deciding whether to push, a manager choosing a receiver) actually asks.
- `bool CanAccept(string resourceId)` — whether this port takes the given resource at all.
- `int Load(string resourceId, int amount)` — pushes material in and returns how much was actually accepted; capacity may cut it short, and the remainder stays the giver's.

## Dependencies
No dependencies of its own (pure interface, no `using` needed beyond the implicit namespace). Within this batch, `GridPorts.AnswersLoadPort` and `GridPorts.FreeSpace` read this exact shape by name (duck typing) rather than casting to the interface, and `GridPorts.Transfer` calls `CanAccept` and `Load` on whatever answers this shape. `IExtractor`, `IStorage`, and `ITransporter` (the latter two by declaration, extractor also in this batch) all compose it.

## Notes
- The class doc comment explicitly frames this and `IUnloadPort` as "the two atomic connectors the whole logistics layer is built from" — every higher-level port type in this batch (`IExtractor`, `IStorage`, `ITransporter`) is defined purely as a composition of these two, no other members added at that level.
