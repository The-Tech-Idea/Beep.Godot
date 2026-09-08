# GridProductionRecipe

World-data model: one data-driven production recipe — id, display name, duration, input costs, output yields — read by `GridProductionComponent.Recipes` to convert wallet resources over time.

Like the terrain-engine batch's definition resources, this is a `[Tool][GlobalClass] Resource` so a designer authors one `.tres` per recipe in the Inspector rather than hard-coding building behavior. Reading is delegated to the shared `GridDefinitionReader` dual-key (PascalCase / snake_case) convention so a recipe can be authored either as a typed resource, a generic `Resource` with matching property names, or a plain dictionary — the same three-shape acceptance `GridCropDefinition` uses in this same batch.

## Public API
- `[Export] string RecipeId` — the id `GridProductionComponent.FindRecipe` matches against (normalized).
- `[Export] string DisplayName`.
- `[Export(Range 0.01,600,0.01)] float DurationTurns` — turns of work one cycle takes; a turn is a day, the same unit a build site's `BuildTurns` uses, so the authored number means the same amount of world time on both time axes. Replaced `DurationSeconds`.
- `[Export] Godot.Collections.Array Inputs / Outputs` — resource-id/amount pairs, in the shape `GridResourceAmount` reads.
- `float EffectiveDurationTurns { get; }` — `DurationTurns` clamped to at least `0.01`, and defaulted to `0.01` if non-finite.
- `bool HasOutputs()` — true if `Outputs` (via `GridResourceAmount.Enumerate`) contains at least one positive-amount, non-blank-id entry.
- `static IEnumerable<GridProductionRecipe> Enumerate(Godot.Collections.Array recipes)` — yields every entry in `recipes` that `TryRead` accepts.
- `static bool TryRead(Variant entry, out GridProductionRecipe? recipe)` — accepts a `Dictionary` (built via `GridDefinitionReader.ReadString/ReadFloat/ReadArray`), an already-typed `GridProductionRecipe` (returned as-is), or any other `Resource` (read reflectively by the same dual-key reader); returns `false` (with `recipe` left null) if the resulting `RecipeId` is blank.

## Dependencies
- Uses `GridDefinitionReader.ReadString/ReadFloat/ReadArray` and `GridVariantReader.TryDictionary` (shared helpers, outside this batch).
- **Consumed within this batch by `GridProductionComponent`**, via `GridProductionRecipe.Enumerate(Recipes)` in `FindRecipe`/`ResolveRecipe`, and via `HasOutputs()`/`EffectiveDurationTurns` in `StartProduction`. The dual-key reader accepts `DurationTurns` / `duration_turns`.

## Notes
- `TryRead`'s three-branch structure (Dictionary / already-typed / generic-Resource-by-reflection) is duplicated verbatim in `GridCropDefinition.TryRead` in this same batch — same shape, same three branches, same delegation to `GridDefinitionReader`, just against different property names. The shared reader functions are factored out already; the branching structure itself is not, so a third definition type would very likely copy this same boilerplate a third time.
- `HasOutputs()` checks only `Outputs`; `Inputs` may legitimately be empty (free production), and `GridProductionComponent.StartProduction` only rejects on a missing/output-less recipe, never on empty inputs — consistent with a recipe that produces something from nothing (e.g. a passive generator), not an oversight.
