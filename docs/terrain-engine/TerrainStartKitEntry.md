# TerrainStartKitEntry

World-data model: one authored `Resource` naming a resource every player start must have within reach (FEAT-09) or, with `Scope` Neutral, a contested site placed between the starts (FEAT-14). Held in `TerrainStartKit.Entries`. The file also declares the `TerrainStartKitScope` enum.

This is the per-player object kit of Age of Empires' player lands and 0 A.D.'s bases: the same set, at matched distances, for every start. `TerrainStartAreaStage` places each entry inside the start's reserved area, relaxing its constraints in a fixed order when the ground will not take it, and reports every placement and every shortfall. A Neutral entry is Age of Empires' contested gold between player lands: the stage places it after every start's kit, in the band between the starts, and reports it on the neutral-site report.

## Public API

- `enum TerrainStartKitScope { PerPlayer, Neutral }` — whose an entry is. `PerPlayer`: every start gets `Count` of it inside its own area. `Neutral`: `Count` per start, placed between the starts — outside every area and its gap, where the two nearest starts are about equally far (within 2 cells).
- `[Export] string ResourceId` (default `""`) — the catalog id to place, the same id the map writes and the wallet counts. Trimmed when captured. An id the generator's resource catalog does not hold gives the problem `unknown_resource:<id>`, critical or not. With `ResourceDensity` at 0 the captured catalog is empty, so every entry reports it.
- `[Export] int Count` (1–8, default 1) — how many to place per start for a Surface resource, or how many deposits to stamp for an Underground one. A Neutral entry places `Count` for every start, so a four-player map gets four times as many.
- `[Export] int MinDistance` (0–32, default 2) — nearest a placement may be to the start origin, in cells. For a Neutral entry the distance is to the nearest start.
- `[Export] int MaxDistance` (0–32, default 0) — farthest a placement may be from the origin, in cells. 0 means the generator's `StartAreaRadius`. For a Neutral entry the distance is to the nearest start, and 0 means no upper bound inside the band.
- `[Export] bool Critical` — a start short of this entry is unusable and reports `missing_critical:<id>`. A non-critical shortfall reports `missing:<id>` and leaves the start usable. A Neutral entry belongs to no start: its shortfall is reported on the neutral-site report (`missing_critical:<id>` or `missing:<id>`) and never makes a start unusable.
- `[Export] TerrainStartKitScope Scope` (default `PerPlayer`) — whether every start gets this entry in its own area, or it goes between them (FEAT-14). Like the rest of the kit, a Neutral entry is read only while the generator's `StartAreaRadius` is above 0. With fewer than two starts it places nothing and reports `neutral_needs_two_starts:<id>`.

## Dependencies

- Copied and clamped into `TerrainStartKitRules.Entry` by `TerrainStartKitRules.Capture`; `Scope` is copied unchanged.
- Resolved at generation time against the world's own catalog through `TerrainResourceRules.Find`. The entry's `ResourceStratum` decides how it is placed: Surface writes the tile's resource, Underground stamps a deposit, Liquid is refused (`kit_entry_unplaceable:<id>`) because neither a start area nor the neutral band holds water.

## Notes

- The distance band is a Euclidean band from the start origin (the headquarters anchor), measured on squared cell distance. A Neutral entry's band is the Euclidean distance from the tile to the nearest start.
- Relaxation widens the band to the whole area (for a Neutral entry, to the whole neutral band), then drops the same-resource spacing, then (Surface only) overwrites a Bonus-category resource. See `TerrainStartAreaStage`.
