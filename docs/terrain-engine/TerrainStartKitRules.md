# TerrainStartKitRules

Support type: an `internal sealed class` holding a `TerrainStartKit` detached from Godot, captured on the main thread before generation runs (FEAT-09). A generation worker never reads the authored resource.

It follows the same contract as `TerrainResourceRules`: authored `Resource` objects are copied into plain values before the pipeline starts, so a background build never touches a Godot object. `TerrainGenerationJob` captures both rule sets on the main thread and then clears `ResourceCatalog` and `StartKit` from the settings it hands to the worker.

## Public API

- `internal readonly record struct Entry(string ResourceId, int Count, int MinDistance, int MaxDistance, bool Critical)` — one captured `TerrainStartKitEntry`.
- `Vector2I HqFootprint { get; }` — clamped to 1–16 per axis.
- `int ExitCount { get; }` — clamped to 0–16.
- `int AreaGap { get; }` — clamped to 0–8.
- `int MinAreaCells { get; }` — clamped to 0–4096.
- `IReadOnlyList<Entry> Entries { get; }` — in authored order. Null entries are skipped; `ResourceId` is trimmed; `Count` is clamped to 1–8 and both distances to 0–32.
- `static TerrainStartKitRules Capture(TerrainGenerationSettings settings)` — the kit's rules, or a default `TerrainStartKit`'s rules (3×3 footprint, 2 exits, gap 1, minimum 0, no entries) when `settings.StartKit` is null.

## Dependencies

- Reads `TerrainGenerationSettings.StartKit`, `TerrainStartKit` and `TerrainStartKitEntry`.
- Created by `TerrainFieldBuilder.Build` (through `BuildPrepared`) and by the `TerrainGenerationJob` constructor.
- Read by `TerrainStartPositionStage.Apply` (`HqFootprint`, only when `StartAreaRadius` is above 0) and `TerrainStartAreaStage.Apply` (every member).

## Notes

- The clamps here are the only clamps on kit values. The stages trust the captured numbers.
