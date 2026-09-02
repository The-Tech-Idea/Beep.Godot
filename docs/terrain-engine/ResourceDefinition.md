# ResourceDefinition

World-data model: an authored `Resource` asset that is the single, shared definition of one map resource (iron, fish, deer, ...), consumed both by map generation and by the gathering economy.

`ResourceDefinition` merges what used to be two disconnected facts about a resource — where the generator places it, and what the economy pays out for gathering it — into one `[Tool][GlobalClass]` Godot `Resource` keyed by a single `Id`. A designer creates one `.tres` per resource and fills in identity, placement rules and gather economics in one place; the terrain generator and the wallet/job systems both read the same asset instead of maintaining parallel string-keyed tables that can silently drift apart.

## Public API

- `enum ResourceCategory : byte { Bonus, Luxury, Strategic }` — Civilization-style classification of what the resource is for; carried on the definition but not consumed anywhere in this file.
- `enum TerrainReliefKind : byte { Flat, Hills, Mountains }` — a public, exportable mirror of the generator's internal `TerrainRelief` enum, used so a `Resource` asset can reference relief without depending on the internal pipeline type.
- `[Export] string Id { get; set; } = "iron"` — the one id both the map and the wallet key on; deliberately the only identity field (no separate "wallet id").
- `[Export] string DisplayName { get; set; } = "Iron"` — UI label for the resource.
- `[Export] ResourceCategory Category { get; set; } = Strategic` — Bonus/Luxury/Strategic classification.
- `[Export] Texture2D? Icon { get; set; }` — icon shown in UI for this resource.
- `[Export] Godot.Collections.Array<string> TerrainKinds { get; set; } = new()` — the terrain-kind strings (e.g. "hills", "sea") the generator will actually place this resource on; an empty array means the generator never places it (gathered/refined-only good).
- `[Export(Range 0,4,0.05)] float Weight { get; set; } = 1.0f` — relative placement chance against other resources competing for the same ground.
- `[Export] bool RequiresRelief { get; set; } = false` — whether placement is restricted to a specific relief band.
- `[Export] TerrainReliefKind RequiredRelief { get; set; } = Hills` — which relief band, when `RequiresRelief` is true.
- `[Export(Range 1,9999,1)] int Amount { get; set; } = 8` — total units one deposit holds before it is exhausted.
- `[Export(Range 1,9999,1)] int AmountPerGather { get; set; } = 1` — units removed per gather action.
- `[Export(Range 0.01,600,0.01)] float GatherSeconds { get; set; } = 1.5f` — time one gather action takes.
- `[Export] string GatherJobKind { get; set; } = "gather"` — the job-kind string a worker/dispatch system matches against to gather this resource.
- `[Export] PackedScene? NodeScene { get; set; }` — the scene instanced on the map cell where this resource occurs; null means the resource is shown on the map but is not a harvestable node.
- `[Export] bool OccupiesCell { get; set; } = false` — whether a placed deposit blocks the cell it stands on.

## Dependencies

None within `addons/beep_game_builder_cs/ecs/terrain/` — this file defines a standalone data type and enums. It is consumed by other terrain files outside this batch (`TerrainResourceStage.cs`, `ResourceCatalog.cs`, `ResourceCatalogs.cs`, `GridResourceNodeComponent.cs`), but reads and writes nothing itself.

## Notes

- `ResourceCategory` (Bonus/Luxury/Strategic) is exported and presumably shown in editor/UI, but nothing in this batch or its listed consumers was checked to confirm gameplay logic branches on it beyond display — worth confirming it is not an accepted-but-unread classification.
- The class doc comment is unusually detailed about *why* the type exists (the old two-owners-of-one-id bug); this is design history, not a description of current behavior, and matches the code as written.
