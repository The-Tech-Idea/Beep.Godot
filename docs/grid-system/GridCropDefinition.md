# GridCropDefinition

World-data model: one crop's farming rules — maturity time, valid planting seasons, regrowth, seed cost, harvest yield — read by `GridCropCatalogComponent.Crops`, so a farming game configures crop behavior in the Inspector instead of hard-coding it into the plant tool.

`SeedItemId` defaults to `""` rather than to a named seed item, and the doc comment explains why: an earlier `"turnip_seed"` default would have silently priced *every* crop in turnip seeds the moment seed-spending was wired into the planting tool. A crop only costs a seed item when its author explicitly says so.

## Public API
- `[Export] string CropId`, `string DisplayName`.
- `[Export(Range 0,365,1)] int DaysToMature`.
- `[Export(Range -1,365,1)] int RegrowDays` — `-1` means the crop does not regrow after harvest.
- `[Export] string SeedItemId` — empty means planting costs no seed item (see above).
- `[Export] string YieldItemId`, `[Export(Range 1,999,1)] int YieldCount`.
- `[Export] bool Spring/Summer/Fall/Winter` — per-season planting flags.
- `int EffectiveDaysToMature { get; }` / `int EffectiveRegrowDays { get; }` / `int EffectiveYieldCount { get; }` — clamped read accessors (`≥0`, `≥-1`, `≥1` respectively).
- `bool CanPlantIn(GridCalendarComponent.GridSeason season)` — switches on the season flags; an unrecognized season value returns `false`.
- `static IEnumerable<GridCropDefinition> Enumerate(Godot.Collections.Array crops)` — yields every entry `TryRead` accepts.
- `static bool TryRead(Variant entry, out GridCropDefinition? crop)` — accepts a `Dictionary` (via `GridDefinitionReader`), an already-typed `GridCropDefinition`, or any other `Resource` read reflectively by the same dual-key reader; fails if the resulting `CropId` is blank.

## Dependencies
- Uses `GridDefinitionReader.ReadString/ReadInt/ReadBool` and `GridVariantReader.TryDictionary` (shared helpers, outside this batch).
- Uses `GridCalendarComponent.GridSeason` enum (outside this batch) in `CanPlantIn`.
- **Consumed within this batch by `GridCropCatalogComponent`**, via `GridCropDefinition.Enumerate(Crops)` in every one of its lookup methods.

## Notes
- `TryRead`'s three-branch structure (Dictionary / already-typed / generic-Resource-by-reflection) is identical in shape to `GridProductionRecipe.TryRead` in this same batch — the same pattern reimplemented against different property names rather than factored into one generic reader (see `GridProductionRecipe.md` Notes for the same observation from the other side).
- This `Resource` subtype carries only `[GlobalClass]`, not `[Tool]` — every other class in this batch (including the structurally-parallel `GridProductionRecipe`, also a `Resource`) is `[Tool][GlobalClass]`. `[Tool]` governs whether a script runs in the editor rather than being specific to Nodes, so a data-only `Resource` with no editor-time behavior likely doesn't need it — but the asymmetry with its closest sibling file is visible enough to flag rather than assume is deliberate.
