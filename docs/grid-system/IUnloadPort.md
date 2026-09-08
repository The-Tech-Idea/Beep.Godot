# IUnloadPort

C# interface for the GIVING port: anything material can be drawn OUT of. It is the second of the two atomic connectors the logistics layer is built from (see `ILoadPort`). A hand-off is one `Unload` into one `Load`, and `GridPorts.Transfer` performs it safely — remainder back to the giver, cargo never duplicated or lost.

`StoredIds()` exists specifically so generic code can work a port without being told in advance what it holds. The doc comment frames this directly: it is "what lets a GENERIC mover — a transport chain, an inserter, an AI — work a port without being told what is inside: flexibility is asking, not authoring." That is, rather than a mover being configured up front with the resource id it expects, it can ask the port what it currently has and act on the answer.

## Public API
- `int Stored(string resourceId)` — units of the given resource currently held.
- `Godot.Collections.Array<string> StoredIds()` — the resource ids currently held; lets a generic mover work a port without being told what is inside.
- `int Unload(string resourceId, int amount)` — releases material and returns how much actually came out.

## Dependencies
Uses `Godot.Collections.Array<string>`. Within this batch, `GridPorts.AnswersUnloadPort` checks for `Unload` alone by name (not `Stored`/`StoredIds`), and `GridPorts.StoredIdsOf` and `GridPorts.Transfer` call into this shape by duck typing rather than an interface cast.

## Notes
- `GridPorts.AnswersUnloadPort` only demands `HasMethod("Unload")`, not `Stored` or `StoredIds`, even though this interface declares all three — the `GridPorts` doc comment explains this is deliberate ("a mechanism asks for exactly what it uses, and readers of `Stored` or `StoredIds` guard for themselves"), so a duck-typed node can satisfy the runtime contract while only implementing a subset of the interface's declared members.
