# TerrainStartKit

World-data model: an authored `Resource` stating what a playable player start needs in one scenario (FEAT-09). Assigned to `TerrainGeneratorComponent.StartKit`. It has no effect on the map while the generator's `StartAreaRadius` is 0.

A start kit is the per-scenario answer to workflow review E01: "playable starts need a headquarters footprint, clear exits, and reachable essential resources ... report an unusable seed, never silently reroll it". It holds the headquarters footprint, how many exits the footprint needs, the gap kept between neighbouring areas, the smallest usable area, and the resources every start is guaranteed. Since FEAT-14 an entry can instead be a Neutral site placed between the starts. Every field is read by `TerrainStartPositionStage` (the footprint) or `TerrainStartAreaStage` (the rest). A generator with a radius and no kit uses the defaults below with no entries.

## Public API

- `[Export] Vector2I HqFootprint` (default 3×3) — the headquarters footprint, anchored at the start cell the way `GridPlacementComponent` anchors a footprint. With start areas on, a start is only chosen where every footprint cell is in bounds, dry, startable, not mountainous and level with the anchor. Clamped to 1–16 per axis when captured.
- `[Export] int ExitCount` (0–16, default 2) — area cells that must touch a side of the footprint. Fewer gives the problem `no_exit`.
- `[Export] int AreaGap` (0–8, default 1) — cells of unreserved ground kept between two start areas (a Chebyshev distance).
- `[Export] int MinAreaCells` (0–4096, default 0) — smallest usable area in cells. 0 means 60% of the radius disc, `ceil(0.6 × π × radius²)`. Smaller gives the problem `area_too_small`.
- `[Export] Godot.Collections.Array<TerrainStartKitEntry> Entries` — the resources the kit places, in array order. Every `PerPlayer` entry goes into each start's own area. Every `Neutral` entry (FEAT-14) is placed between the starts, after every start's kit (see `TerrainStartKitEntry.Scope`). Null entries are skipped.

## Dependencies

- Read by `TerrainStartKitRules.Capture`, which copies and clamps every field on the main thread before generation runs. No generation stage reads the resource itself.
- Holds `TerrainStartKitEntry` resources.
- Referenced by `TerrainGeneratorComponent.StartKit` and carried as a reference in `TerrainGenerationSettings.StartKit`.

## Notes

- Like `ResourceCatalog`, editing a kit already assigned to a generator does not invalidate a field that generator has built. Generate again.
- The kit is not part of `TerrainWorldComponent`'s saved recipe (only `start_area_radius` is), the same limitation as the resource catalogue. A restored world regenerates against whatever kit the scene's generator holds.
