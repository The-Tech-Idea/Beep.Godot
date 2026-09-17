# TerrainStartKitRules

Support type: an `internal sealed class` holding a `TerrainStartKit` detached from Godot, captured on the main thread before generation runs (FEAT-09). A generation worker never reads the authored resource.

It follows the same contract as `TerrainResourceRules`: authored `Resource` objects are copied into plain values before the pipeline starts, so a background build never touches a Godot object. `TerrainGenerationJob` captures both rule sets on the main thread and then clears `ResourceCatalog` and `StartKit` from the settings it hands to the worker.

## Public API

- `internal readonly record struct Entry(string ResourceId, int Count, int MinDistance, int MaxDistance, bool Critical, TerrainStartKitScope Scope)` — one captured `TerrainStartKitEntry`. `Scope` (FEAT-14) decides whether `TerrainStartAreaStage` places the entry in every start's area (`PerPlayer`) or between the starts (`Neutral`).
- `Vector2I HqFootprint { get; }` — clamped to 1–16 per axis.
- `int ExitCount { get; }` — clamped to 0–16.
- `int AreaGap { get; }` — clamped to 0–8.
- `int MinAreaCells { get; }` — clamped to 0–4096.
- `IReadOnlyList<Entry> Entries { get; }` — in authored order, both scopes in one list. Null entries are skipped; `ResourceId` is trimmed; `Count` is clamped to 1–8 and both distances to 0–32; `Critical` and `Scope` are copied unchanged.
- `static TerrainStartKitRules Capture(TerrainGenerationSettings settings)` — the kit's rules, or a default `TerrainStartKit`'s rules (3×3 footprint, 2 exits, gap 1, minimum 0, no entries) when `settings.StartKit` is null. Throws `InvalidOperationException` naming the entry and the value when an entry's `Scope` is neither `PerPlayer` nor `Neutral`.

## Dependencies

- Reads `TerrainGenerationSettings.StartKit`, `TerrainStartKit` and `TerrainStartKitEntry`.
- Created by `TerrainFieldBuilder.Build` (through `BuildPrepared`) and by the `TerrainGenerationJob` constructor.
- Read by `TerrainStartPositionStage.Apply` (`HqFootprint`, only when `StartAreaRadius` is above 0) and `TerrainStartAreaStage.Apply` (every member).

## Notes

- The clamps here are the only clamps on kit values. The stages trust the captured numbers.
- `Scope` is validated, not clamped. The Inspector offers only the two members, but a script or a hand-edited resource can store another number. `TerrainStartAreaStage` would place such an entry in neither pass, which is an entry accepted and never placed, so capture refuses it by name. A synchronous build (`TerrainGeneratorComponent`'s queries) surfaces the exception to its caller. An asynchronous one ends in `GenerationFinished(false, message)`. `terrain_start_distance_probe` checks the message through `tests/TerrainStartKitSmoke.cs`; removing the check fails it.
