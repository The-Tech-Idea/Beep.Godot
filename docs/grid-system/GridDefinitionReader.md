# GridDefinitionReader

Internal static helper class that reads definition data — the kind that backs build, crop, recipe, objective, and resource-amount definitions — regardless of whether it arrives as a typed `Resource`, a duck-typed `Resource` with matching property names, or a plain `Godot.Collections.Dictionary`, and regardless of whether its keys are PascalCase or snake_case.

The class exists to kill a specific, already-observed duplication: the doc comment states that "five definition resources (builds, crops, recipes, objectives, resource amounts) each carried a near-identical ~90-line private copy of these dual-key readers." Definition authors write PascalCase (`ResourceId`) in C#-authored `.tres` resources but the same data can also arrive as snake_case (`resource_id`) from a dictionary or a GDScript-authored source, so every reader accepts both spellings and tries the PascalCase key before the snake_case one. Consolidating this into one static reader means the accepted shapes (which keys, which fallback rules) cannot drift apart per definition type the way five hand-copied readers inevitably would.

## Public API
- `static string ReadString(Godot.Collections.Dictionary data, string pascal, string snake, string fallback)` / `static string ReadString(Resource resource, string pascal, string snake, string fallback)` — reads a string field by trying `pascal` then `snake`; `fallback` if neither is present (nil).
- `static int ReadInt(...)` (dictionary and `Resource` overloads) — delegates to `GridVariantReader.Int` on whichever key resolves.
- `static float ReadFloat(...)` (dictionary and `Resource` overloads) — delegates to `GridVariantReader.Float`.
- `static bool ReadBool(...)` (dictionary and `Resource` overloads) — delegates to `GridVariantReader.Bool`.
- `static Vector2I ReadVector2I(...)` (dictionary and `Resource` overloads) — delegates to `GridVariantReader.Vector2I`.
- `static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary data, string pascal, string snake)` / `static Godot.Collections.Array ReadArray(Resource resource, string pascal, string snake)` — the array at whichever key resolves, or an empty array if neither is an array.
- `static T? ReadObject<T>(Godot.Collections.Dictionary data, string pascal, string snake) where T : GodotObject` / `static T? ReadObject<T>(Resource resource, string pascal, string snake) where T : GodotObject` — the object at whichever key resolves, cast to `T`, or `null` if not an object or the cast fails.
- (private) `static Variant ReadVariant(Godot.Collections.Dictionary data, string pascal, string snake)` / `static Variant ReadVariant(Resource resource, string pascal, string snake)` — the actual dual-key lookup: PascalCase key first, snake_case second, `default` (nil) if neither resolves.

## Dependencies
Depends on `GridVariantReader` from this same batch for all numeric/bool/vector coercion — every typed `Read*` method (other than `ReadString`, `ReadArray`, `ReadObject`) is a thin wrapper around a `GridVariantReader` call fed by this class's own `ReadVariant`. Within this batch, `GridResourceAmount.TryRead` calls `GridDefinitionReader.ReadString` and `ReadInt` directly when reading a duck-typed `Resource` that is not itself a `GridResourceAmount`. Not established from this batch alone whether the five definition types the doc comment names (build, crop, recipe, objective, resource-amount definitions) beyond `GridResourceAmount` now route through this reader — those files are outside this batch's file set.

## Notes
- Every public method comes in a `Dictionary` overload and a `Resource` overload with identical dual-key semantics — a consistent, deliberate pairing rather than an accidental near-duplicate, since the two overloads share nothing but the pattern (a `Resource.Get` vs. a dictionary index lookup underneath).
- `ReadObject<T>`'s cast failure (a Variant of type Object that isn't actually a `T`) returns `null` silently rather than distinguishing "field absent" from "field present but wrong type" — a caller cannot tell those two cases apart from the return value alone.
- Confirmed by reading `GridResourceAmount.cs` in this same batch: it is an actual, wired-in consumer of this reader (not just a described-but-unused capability), which supports the doc comment's claim that this replaced real duplicated code rather than being unused scaffolding.
