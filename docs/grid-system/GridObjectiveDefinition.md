# GridObjectiveDefinition

Authored data for one settlement/grid objective — a `Resource` (not a `Node`), so it is designed to be saved as a `.tres` and shared across many game instances or reused between an objective list and a UI. It carries only the static description of a goal (id, display name, description, target count, and a few activation flags); runtime progress is deliberately kept elsewhere.

The class comment states the reasoning directly: "Runtime progress lives on `GridObjectiveTrackerComponent` so this resource can be reused safely." Because a `Resource` in Godot can be shared by reference across scenes, storing mutable progress on it would mean two objective lists (or a saved game reloaded twice) silently sharing the same completion state. Keeping this file pure data and pushing progress into a `Dictionary<string, ObjectiveState>` on the tracker node avoids that trap. The other notable design choice is `TryRead`/`Enumerate`, which accept a `Godot.Collections.Array` of *either* `Dictionary` entries or `Resource` objects (typed `GridObjectiveDefinition` or duck-typed via reflection) — this lets objectives be authored as inline dictionaries (e.g. from JSON/GDScript data) or as proper `.tres` resources, normalized through the same dual-key (`PascalCase`/`snake_case`) reader every definition resource in the grid system uses.

## Public API
- `string ObjectiveId { get; set; } = "clear_land"` — raw authored id; compare via `NormalizedId()`/`Normalize()`, not this field directly, since case/spacing varies between authoring paths.
- `string DisplayName { get; set; } = ""` — UI label.
- `string Description { get; set; } = ""` — multiline UI description.
- `int TargetCount { get; set; } = 1` — raw authored target; use `EffectiveTargetCount` for the clamped value.
- `bool AutoComplete { get; set; } = true` — if true, the tracker completes the objective automatically once progress reaches target.
- `bool ActiveOnStart { get; set; } = true` — whether the tracker activates this objective by default.
- `bool HiddenUntilActive { get; set; } = false` — UI hint; not read anywhere in this batch, so its consumer is outside these four files.
- `string NormalizedId()` — returns `Normalize(ObjectiveId)`.
- `static string Normalize(string value)` — lowercases, trims, and replaces spaces with underscores; returns `""` for null/whitespace input. This is the canonical id form used everywhere objectives are looked up.
- `int EffectiveTargetCount` — `TargetCount` clamped to a minimum of 1, guarding against a misauthored 0 or negative target.
- `static IEnumerable<GridObjectiveDefinition> Enumerate(Godot.Collections.Array objectives)` — yields a `GridObjectiveDefinition` for every array entry that `TryRead` can parse; silently skips entries that fail to parse.
- `static bool TryRead(Variant entry, out GridObjectiveDefinition? definition)` — parses one array entry. Handles three shapes: a `Godot.Collections.Dictionary` (read via the dual-key reader), an already-typed `GridObjectiveDefinition` resource (returned as-is, not copied), or any other `Resource` (duck-typed field-by-field into a new definition). Returns `false` (and leaves `definition` null) if the entry is none of these or if the resulting `ObjectiveId` is blank.

## Dependencies
- Uses `GridVariantReader.TryDictionary` (from elsewhere in the grid system, not in this batch) to coerce a `Variant` into a `Godot.Collections.Dictionary`.
- Uses the static `ReadString`/`ReadInt`/`ReadBool` helpers from `Beep.ECS.GridDefinitionReader` (imported via `using static`) — the file's own trailing comment confirms this is "the shared dual-key (PascalCase / snake_case) reader all definition resources use," not code local to this file.
- Called into, within this batch: `GridObjectiveTrackerComponent` holds a `Godot.Collections.Array Objectives` and calls `GridObjectiveDefinition.Enumerate(Objectives)` throughout (`EnsureStates`, `GetActiveObjectives`, `DefinitionFor`) to resolve definitions, and calls `NormalizedId()`/`EffectiveTargetCount` when tracking progress. `GridObjectiveEventBinderComponent` calls the static `GridObjectiveDefinition.Normalize(...)` to build objective ids from job/build/resource/production identifiers, but never touches a `GridObjectiveDefinition` instance itself.

## Notes
- The duck-typed `Resource` branch of `TryRead` (any `Resource` that isn't already a `GridObjectiveDefinition`) reads seven fields by reflection through `GridDefinitionReader` even though nothing in this batch demonstrates a producer of such resources — likely meant for GDScript-authored resource types that structurally match but aren't the C# class, consistent with the addon-wide duck-typing convention described for managers elsewhere in the grid system.
- `HiddenUntilActive` is exported and read into every parsed definition but is not consumed by `GridObjectiveTrackerComponent` or `GridObjectiveEventBinderComponent` in this batch — its reader is presumably a UI/objective-list component outside these four files.
- When `TryRead` hits the already-typed `GridObjectiveDefinition` branch, it returns the same object reference rather than a copy, so mutating a definition obtained this way would mutate the shared authored resource — consistent with the class's own stated design (progress must never be written here), but worth knowing since nothing prevents a caller from doing it anyway.
