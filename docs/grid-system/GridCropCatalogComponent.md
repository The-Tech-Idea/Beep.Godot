# GridCropCatalogComponent

Gameplay component: a lookup table Node holding a `Crops` array of `GridCropDefinition`, queried by crop id for maturity time, valid planting seasons, regrowth, seed cost, and harvest yield. The class doc comment says it's meant to be paired with `GridToolActionComponent` so a plant tool can read farming rules from data instead of hard-coding them — that consumer is outside this batch and its call into this component is not confirmed here.

## Public API
- `[Export] Godot.Collections.Array Crops` — untyped array of `GridCropDefinition` resources and/or dictionary entries.
- `[Export] bool AllowUnknownCrops` — governs `CanPlant`'s fallback for an id not found in `Crops` (see Notes for how the other lookups fall back differently).
- `GridCropDefinition? FindCrop(string cropId)` — case-insensitive, trimmed linear scan over `GridCropDefinition.Enumerate(Crops)`.
- `bool CanPlant(string cropId, GridCalendarComponent.GridSeason season)` — `crop.CanPlantIn(season)` if found, else `AllowUnknownCrops`.
- `int DaysToMature(string cropId, int fallback)` — `crop.EffectiveDaysToMature` if found, else `Mathf.Max(0, fallback)`.
- `int RegrowDays(string cropId)` — `crop.EffectiveRegrowDays` if found, else `-1` (does not regrow).
- `string SeedItem(string cropId)` — `crop.SeedItemId.Trim()` if found and non-blank, else `""` (no seed cost).
- `string YieldItem(string cropId)` — `crop.YieldItemId` if found, else `cropId` itself.
- `int YieldCount(string cropId)` — `crop.EffectiveYieldCount` if found, else `1`.
- `Godot.Collections.Array<string> CropIdsForSeason(GridCalendarComponent.GridSeason season)` — every crop id in `Crops` for which `CanPlantIn(season)` is true.

## Dependencies
- Reads `GridCropDefinition.Enumerate`/`CanPlantIn` (this batch) over `Crops`.
- Uses `GridCalendarComponent.GridSeason` enum (outside this batch).
- Not established from this batch alone: which component actually calls `FindCrop`/`CanPlant`/etc. — only the class doc comment names `GridToolActionComponent` as the intended caller.

## Notes
- Every lookup method has its own fallback for an unrecognized crop id, and they don't agree: `CanPlant` defers to the `AllowUnknownCrops` export; `DaysToMature` defers to a caller-supplied `fallback` parameter regardless of `AllowUnknownCrops`; `RegrowDays` always falls back to `-1`; `SeedItem` always falls back to `""`; `YieldItem` falls back to echoing the input id back. A reader who assumes `AllowUnknownCrops` is the single knob governing "what happens for an unknown crop" across this whole API would be wrong for four of the five lookups.
- Every lookup (`FindCrop`, and `CropIdsForSeason` separately) re-scans `Crops` linearly via `GridCropDefinition.Enumerate`, which itself re-parses every entry through `TryRead` on each call — no cache or dictionary index. Fine at small crop-catalog sizes, but every method pays the same full-catalog parse cost despite this being named "a lookup table."
