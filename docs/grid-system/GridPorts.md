# GridPorts

Internal static helper class holding the one implementation of the safe port hand-off, shared by the transport manager, the pipeline, and the hauler's depot delivery: unload from the giver, load into the receiver, remainder BACK to the giver — cargo is never duplicated and never lost. Ports are read by NAME (duck typing) here, not by interface cast, so GDScript nodes participate on equal footing with C# `ILoadPort`/`IUnloadPort` implementers.

The class exists to give every caller that needs a port-to-port hand-off exactly one place to get it right, rather than each manager (transport, pipeline, hauler) reimplementing the give/take/remainder-return sequence itself and risking a subtly different (and possibly unsafe) version. Because ports are read by name via `HasMethod`/`Get`/`Call` rather than cast to `ILoadPort`/`IUnloadPort`, the same helper works uniformly whether the node on the other end is a C# component that implements the interface or a GDScript node that merely has matching method names — duck typing is what makes the interfaces in this batch usable across both languages.

## Public API
- `static bool AnswersLoadPort(Node? node)` — whether the node answers the receiving-port shape: non-null, instance-valid, and has both `Load` and `CanAccept` methods.
- `static bool AnswersUnloadPort(Node? node)` — whether the node can give material; only `Unload` is demanded (not `Stored`/`StoredIds`) — "a mechanism asks for exactly what it uses, and readers of `Stored` or `StoredIds` guard for themselves."
- `static int FreeSpace(Node node)` — free space in a load port, read as `Capacity - CurrentLoad`. A node missing either property is treated as open (returns `int.MaxValue`) — the contract asks for them "at least," but a duck-typed stand-in that omits them should still receive.
- `static Godot.Collections.Array<string> StoredIdsOf(Node node)` — what a port currently holds, asked rather than authored; a node without `StoredIds` answers empty, meaning it can still be worked by id even without enumerating its contents.
- `static int Transfer(Node? from, Node? to, string resourceId, int amount)` — hands material from one port to the next and returns how much moved: unloads from the giver, loads into the receiver, and if the receiver took less than was given, loads the remainder back into the giver (only if the giver itself also answers `Load`). Returns 0 for any invalid input (null nodes, `from == to`, non-positive amount, either side not answering its port shape, or the receiver rejecting the resource via `CanAccept`).

## Dependencies
Depends on `Godot.Node`, `GodotObject.IsInstanceValid`, and Godot's `Variant`/`HasMethod`/`Get`/`Call` reflection surface — no dependency on the `ILoadPort`/`IUnloadPort` interfaces themselves, deliberately, since it must also work with GDScript nodes that cannot implement a C# interface. Within this batch, this is the class that the doc comments on `ILoadPort` and `IUnloadPort` point to as the place duck typing is actually exercised. Not established from this batch alone which manager files call `GridPorts.Transfer` — the class doc comment names "the transport manager, the pipeline and the hauler's depot delivery" as callers, but those files are outside this batch's file set.

## Notes
- `internal static class` — not a `[Tool][GlobalClass]` component like most of the grid system; it is a pure helper, invisible to the Godot editor and to GDScript callers, used only from C#.
- `Transfer`'s remainder-return step is conditional on `from.HasMethod("Load")` — an unload-only giver (one that answers `IUnloadPort` but not `ILoadPort`) simply loses any remainder rather than erroring, since it has nowhere to put it back; this is a quiet edge case rather than a guarded one.
- `AnswersUnloadPort`'s intentionally partial check (only `Unload`, not the full interface) is a documented, deliberate looseness rather than an inconsistency — worth knowing before "fixing" it to match `IUnloadPort` exactly.
