# GridSitePorts

Internal static helper: reads the three site facts off anything that answers them — a C# [IGridSite](IGridSite.md), a GDScript node with matching member names, or a plain authored Dictionary. Same duck-typing rule as [GridPorts](GridPorts.md) and [GridConstructionVisualPorts](GridConstructionVisualPorts.md): ask by name, so no participant has to be a C# type.

One call site reads "3x2, 40 wood, 3 turns" whether the answer comes from a typed `GridBuildDefinition`, a modder's GDScript definition, or a dictionary loaded from JSON.

## Public API
- `static bool AnswersSiteShape(Variant value)` — whether the value answers the site questions: an `IGridSite`, an object with `SiteFootprint` and `SiteTurns`, or a dictionary with `SiteFootprint`/`site_footprint`.
- `static Vector2I Footprint(Variant value)` — the ground the site takes, never smaller than one cell.
- `static Godot.Collections.Array Materials(Variant value)` — the material that must physically arrive before work starts; empty when none is declared.
- `static int Turns(Variant value)` — turns of work, never negative. Zero is a legitimate answer meaning the site needs no work at all.
- `static string Describe(Variant value)` — the three facts as one line for a tooltip or a build menu, e.g. `3x2, planks 4, 3 turns`.

## Dependencies
`GridVariantReader` for dictionary coercion; `GridResourceAmount.Enumerate` to read materials. Consumed by `GridBuildToolbarComponent`'s tooltip and by `GridPlacementSmoke.VerifySiteContract`.

## Notes
- Every read is bounded on the way out: a malformed `0×-4` footprint reads as `1×1`, a negative turn count as `0`. The test that guards this fails when either bound is removed.
- PascalCase and snake_case keys are both accepted on dictionaries (`SiteTurns` / `site_turns`), matching the dual-key convention `GridDefinitionReader` uses for every authored definition.
- `internal static class` — a pure helper, invisible to the editor and to GDScript callers; GDScript participates by *answering* the shape, not by calling this.
