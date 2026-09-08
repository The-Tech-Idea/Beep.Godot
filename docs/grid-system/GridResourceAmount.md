# GridResourceAmount

A `[Tool][GlobalClass]` Godot `Resource`: a resource id plus a quantity, used by grid build costs and starting wallets. It is the smallest data primitive in the grid system's economy — one `.tres` asset (or duck-typed equivalent) says "N units of resource X."

Beyond being an authorable `[Export]`-backed resource for the editor, the class carries the static reading logic that lets any list of amounts — a build's material cost, a starting wallet — accept entries in more than one shape: a real `GridResourceAmount` resource, a duck-typed `Resource` with matching `ResourceId`/`Amount` (or `resource_id`/`amount`) properties, or a plain dictionary. This mirrors the same dual-shape acceptance pattern (`GridDefinitionReader`, `GridVariantReader`) used throughout this batch, applied specifically to the one data shape ("id + amount") that recurs across build costs and wallets.

## Public API
- `[Export] string ResourceId { get; set; } = "wood"` — which resource this entry counts.
- `[Export(PropertyHint.Range, "0,999999,1")] int Amount { get; set; } = 1` — how many units.
- `static IEnumerable<(string ResourceId, int Amount)> Enumerate(Godot.Collections.Array amounts)` — yields a `(ResourceId, Amount)` tuple for every entry in `amounts` that `TryRead` successfully parses, silently skipping entries that fail.
- `static bool TryRead(Variant entry, out string resourceId, out int amount)` — parses one entry, which may be a dictionary (via `GridVariantReader.TryDictionary` then `GridDefinitionReader.ReadString`/`ReadInt` with PascalCase/snake_case keys) or a `Resource` object (a real `GridResourceAmount` read directly through its typed properties, or any other `Resource` read via `GridDefinitionReader`'s dual-key lookup). Returns false, with `resourceId` empty and `amount` 0, for anything else or for a blank/whitespace resource id.

## Dependencies
Depends on `GridVariantReader.TryDictionary` and `GridDefinitionReader.ReadString`/`ReadInt` to parse the non-typed-resource entry shapes. Populated by `GridBuildDefinition.Costs`/`RequiredMaterials` and `GridProductionRecipe`'s input/output lists, among others.

## Notes
- `TryRead` special-cases the exact-type match (`resource is GridResourceAmount typed`) to read the strongly-typed properties directly, and only falls through to `GridDefinitionReader`'s reflection-based dual-key lookup for a duck-typed `Resource` that isn't actually a `GridResourceAmount` — the fast, typed path is tried first, the general path is the fallback, not the other way around.
- A malformed entry (unparseable dictionary, wrong object type, or blank id) is dropped silently by `Enumerate` rather than surfaced — a caller iterating a mostly-valid list with one bad entry gets a shorter sequence with no signal that anything was skipped.
