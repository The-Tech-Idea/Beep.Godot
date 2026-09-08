# GridWorkerPorts

Internal static helper — the duck-typing fallback for `IWorker`, mirroring `GridPorts.cs` exactly. GDScript cannot implement a C# interface, so a system that must also recognize a duck-typed GDScript worker reads `IsWorking`/`CurrentJobId` by NAME instead of through the interface. A GDScript participant answers the shape by exposing members with these exact PascalCase names — the same convention this addon's own GDScript test doubles (`GdBuilder`, `GdTransporter`) already use for the port contracts.

## Public API
- `static bool AnswersWorkerShape(Node? node)` — true if the node implements `IWorker`, or exposes non-Nil `IsWorking`/`CurrentJobId` members by name.
- `static bool IsWorking(Node node)` — reads `IsWorking` off an `IWorker` directly, or via `Get("IsWorking")` otherwise.
- `static string CurrentJobId(Node node)` — same pattern for `CurrentJobId`.

## Dependencies
None beyond `Godot.Node`/`GodotObject`. Used by `GridWorkerBuildActivityEffectComponent` to find and read its sibling worker regardless of implementation language.

## Notes
- `internal`, matching `GridPorts`' own visibility — this is plumbing for this addon's own components, not a public API surface a consuming project is expected to call directly (though nothing prevents it).
