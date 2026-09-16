# TerrainStartKitEntry

World-data model: one authored `Resource` naming a resource every player start must have within reach (FEAT-09). Held in `TerrainStartKit.Entries`.

This is the per-player object kit of Age of Empires' player lands and 0 A.D.'s bases: the same set, at matched distances, for every start. `TerrainStartAreaStage` places each entry inside the start's reserved area, relaxing its constraints in a fixed order when the ground will not take it, and reports every placement and every shortfall.

## Public API

- `[Export] string ResourceId` (default `""`) — the catalog id to place, the same id the map writes and the wallet counts. Trimmed when captured. An id the generator's resource catalog does not hold gives the problem `unknown_resource:<id>`, critical or not. With `ResourceDensity` at 0 the captured catalog is empty, so every entry reports it.
- `[Export] int Count` (1–8, default 1) — how many to place per start for a Surface resource, or how many deposits to stamp for an Underground one.
- `[Export] int MinDistance` (0–32, default 2) — nearest a placement may be to the start origin, in cells.
- `[Export] int MaxDistance` (0–32, default 0) — farthest a placement may be from the origin, in cells. 0 means the generator's `StartAreaRadius`.
- `[Export] bool Critical` — a start short of this entry is unusable and reports `missing_critical:<id>`. A non-critical shortfall reports `missing:<id>` and leaves the start usable.

## Dependencies

- Copied and clamped into `TerrainStartKitRules.Entry` by `TerrainStartKitRules.Capture`.
- Resolved at generation time against the world's own catalog through `TerrainResourceRules.Find`. The entry's `ResourceStratum` decides how it is placed: Surface writes the tile's resource, Underground stamps a deposit, Liquid is refused (`kit_entry_unplaceable:<id>`) because start areas exclude water.

## Notes

- The distance band is a Euclidean band from the start origin (the headquarters anchor), measured on squared cell distance.
- Relaxation widens the band to the whole area, then drops the same-resource spacing, then (Surface only) overwrites a Bonus-category resource. See `TerrainStartAreaStage`.
