# IExtractor

C# interface for something that produces material out of the world — a mine, a well, a farm plot. It composes `ILoadPort` and `IUnloadPort` directly, so an extractor is, without any extra wiring, something the rest of the logistics chain connects to directly: its output buffer is an unload port a pipeline or hauler draws from, and (for the rare case of material injected back down a well) a load port too. `GridExtractorComponent` is the shipped implementation; `GridExtractionManagerComponent` is the registry of everything currently extracting.

The design folds "is an extractor" and "is a port pair" into one interface rather than requiring implementers to declare both separately, because in this codebase every extractor genuinely is both at once — there is no such thing as an extractor that cannot be drawn from or loaded into. Composing the port interfaces here means any code that only cares about port behavior (like `GridPorts.Transfer`) can treat an `IExtractor` exactly like an `IStorage` or `ITransporter` with no special-casing.

## Public API
- `string ActiveResourceId { get; }` — the resource currently being worked, or empty.
- `bool IsExtracting { get; }` — whether the extractor is currently drawing a deposit down.
- (inherited from `ILoadPort`) `Capacity`, `CurrentLoad`, `CanAccept(string)`, `Load(string, int)`.
- (inherited from `IUnloadPort`) `Stored(string)`, `StoredIds()`, `Unload(string, int)`.

## Dependencies
Composes `ILoadPort` and `IUnloadPort` from this same batch — no other dependencies. Not established from this batch alone which concrete type implements it or which manager calls into it; `GridExtractorComponent` and `GridExtractionManagerComponent` are named in the doc comment but are not part of this file set.

## Notes
- No members of its own beyond `ActiveResourceId` and `IsExtracting` — everything else an extractor needs to do (load/unload/capacity) comes for free from the two port interfaces, keeping this interface minimal and mirroring the same shape used by `IStorage` and `ITransporter`.
