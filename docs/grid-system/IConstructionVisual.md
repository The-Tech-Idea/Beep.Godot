# IConstructionVisual

C# interface for a scene that PRESENTS a structure while it is under construction — a construction-stage scene listed in `GridBuildDefinition.ConstructionStages`, or the build's own `Scene` root. The grid layer never draws: `GridBuildStageVisualComponent` tells the scene the facts of the site through this contract, and the scene renders them however it likes. The shipped `templates/art/construction_stages/construction_stage.gd` is one implementation; a project's own scene is another.

The facts are the ones real construction sites are read by (`CONSTRUCTION_VISUALS_RESEARCH.md`): how big the site is, what **state** it is in, what materials are on it, how far along it is, and when a hit of work lands. Nothing about the worker is part of it — a truck, a person or a robot is the worker scene's business, as `IWorker` already keeps it.

## Public API
- `Vector2I SiteFootprint { get; set; }` — cells the site occupies. Set before the scene enters the tree.
- `Vector2 CellSize { get; set; }` — world pixels per cell. Set before the scene enters the tree.
- `int StageIndex { get; set; }`, `int StageCount { get; set; }` — which listed stage this instance is. A scene listed once (`StageCount` 1) draws the whole build from `BuildProgress`; one listed among several is shown for `BuildProgress` in `[StageIndex/StageCount, (StageIndex+1)/StageCount)`.
- `string SiteState { get; set; }` — `"pending"` (waiting for `RequiredMaterials`), `"queued"` (job exists, nobody holds it), `"working"`, `"complete"` (finished; the scene lives on for the teardown beat).
- `Godot.Collections.Array SiteMaterials { get; set; }` — one `Dictionary` per `RequiredMaterials` entry: `id`, `required`, `delivered`. While pending, `delivered` is what the site's storage holds; once the job starts the stock is built in and reads as fully delivered.
- `float BuildProgress { get; set; }` — 0..1, pushed as work advances and set to 1 on completion.
- `void WorkPulse()` — a hit of work landed (progress advanced while a worker held the job). The scene plays whatever a hit looks and sounds like for its material at the point being worked.

## Dependencies
None of its own. Read by `GridBuildStageVisualComponent` through `GridConstructionVisualPorts`.

## Notes
- GDScript cannot implement a C# interface; the grid layer also recognizes a scene that exposes these members by NAME — see `GridConstructionVisualPorts`. Every member is optional to a scene: what it does not expose it is simply not told.
- The shipped `shaders/construction_reveal.gdshader` is a helper a scene may use: it hides everything above a line that climbs along the part's own texture axis (`v_axis`, `v_scale`), quantised to material rows (`row_v`) with a raw/wet band below the line (`cut_v`, `cut_color`). The scene sets the bounds; nothing in the grid layer applies materials.
