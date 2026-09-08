# GridVariantReader

Internal static helper class for coercing loosely-typed Godot `Variant` values — as they arrive from dictionaries, exported resource fields, or GDScript callers — into concrete C# types (`int`, `float`, `bool`, `Vector2I`, dictionaries, arrays, cells, world points). It is the grid system's general-purpose "read this value defensively, whatever shape it actually turns out to be" layer.

The class exists because grid data does not arrive in one canonical shape: a dictionary key might hold an `int`, a `float` that should round to one, or a numeric string; a cell might arrive as a `Vector2I`, a `Vector2` that needs rounding, or a dictionary with `"cell"`, `"x"/"y"`, or `"X"/"Y"` keys. Every method here picks the value apart by `VariantType` and falls back safely (to a caller-supplied default, or to `false`/failure for the `TryRead*` family) rather than throwing or silently coercing garbage. The comment above the cell/point-parsing section is explicit about why this consolidation happened: the same parsing logic was "pasted, byte for byte, into `GridPathFollowerComponent`, `GridToolActionComponent`, and `GridSelectionJobCommandComponent`" — a three-way duplication mirroring one the terrain side already had before it was pulled into `TerrainGeometry`. This file is that same fix applied to the grid side.

## Public API
- `static bool TryDictionary(Variant value, out Godot.Collections.Dictionary dictionary)` — true and populates `dictionary` if `value` is a `Dictionary`; otherwise false with an empty dictionary out.
- `static Godot.Collections.Array Array(Godot.Collections.Dictionary data, string key)` — the array at `key`, or an empty array if missing or not an array.
- `static string String(Godot.Collections.Dictionary data, string key, string fallback = "")` — the string at `key`, or `fallback`.
- `static int Int(Godot.Collections.Dictionary data, string key, int fallback = 0)` / `static int Int(Variant value, int fallback = 0)` — coerces int, float (rounded, finite-checked), or numeric string to `int`; anything else (or non-finite) returns `fallback`.
- `static float Float(Godot.Collections.Dictionary data, string key, float fallback = 0f)` / `static float Float(Variant value, float fallback = 0f)` — coerces int/float/numeric-string to `float`, finite-checked; else `fallback`.
- `static bool Bool(Godot.Collections.Dictionary data, string key, bool fallback = false)` / `static bool Bool(Variant value, bool fallback = false)` — coerces bool, nonzero-int, or parseable string to `bool`; else `fallback`.
- `static Vector2I Vector2I(Godot.Collections.Dictionary data, string key, Vector2I fallback)` / `static Vector2I Vector2I(Variant value, Vector2I fallback)` — coerces `Vector2I`, rounded `Vector2`, or a dictionary (via nested `"cell"`, or `"x"/"y"`, or `"X"/"Y"` keys) to `Vector2I`; else `fallback`.
- `static bool TryReadCell(Variant value, out Vector2I cell)` — reads a cell from `Vector2I`, rounded finite `Vector2`, or a dictionary with `"cell"` / `x,y` / `X,Y`; false for anything else and for the sentinel `int.MinValue` coordinates that mean "no cell."
- `static bool TryReadWorldPoint(Variant value, out Vector2 point)` — reads a world point from `Vector2`, `Vector2I` (converted), or an x/y dictionary; false if not finite or not one of those shapes.
- `static bool TryReadInt(Variant value, out int result)` — strict int-read (int, finite float rounded, or parseable string); false otherwise.
- `static bool TryReadFloat(Variant value, out float result)` — strict float-read (finite float/int, or parseable finite string); false otherwise.

## Dependencies
Uses `Godot.Variant`, `Godot.Mathf`, `Godot.Vector2`/`Vector2I`, `Godot.Collections.Dictionary`/`Array`, and `System.Globalization.CultureInfo` for invariant-culture numeric parsing. Within this batch, `GridDefinitionReader` calls `GridVariantReader.Int`/`Float`/`Bool`/`Vector2I` directly as its underlying coercion layer, and `GridResourceAmount.TryRead` calls `GridVariantReader.TryDictionary`. Not established from this batch alone whether `GridPathFollowerComponent`, `GridToolActionComponent`, or `GridSelectionJobCommandComponent` (named in the code comment as the files this was extracted from) now call into `TryReadCell`/`TryReadWorldPoint` — those files are outside this batch's file set, though the comment strongly implies they are the intended callers post-refactor.

## Notes
- The in-code comment candidly documents its own reason for existing as a fix for triplicated logic, naming the exact three files the duplicate was pasted into and drawing a direct parallel to an identical prior fix on the terrain side (`Hash01`/`TerrainGeometry`) — strong evidence this is deliberate consolidation, not new speculative infrastructure.
- All culture-sensitive string parsing consistently uses `CultureInfo.InvariantCulture`, avoiding locale-dependent decimal-separator bugs that a bare `float.TryParse`/`int.TryParse` would risk.
- `ReadEither` (private) prefers the first key over the second when both are present, used consistently by the dictionary-based cell/point readers to prefer lowercase `x`/`y` over uppercase `X`/`Y`.
