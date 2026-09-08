# GridConstructionVisualPorts

The duck-typing fallback for `IConstructionVisual`, mirroring `GridWorkerPorts` exactly: GDScript cannot implement a C# interface, so the grid layer also recognizes a scene that exposes the contract's members by NAME. The shipped `construction_stage.gd` participates this way, and so does the pure-GDScript probe stage in `tests/grid_worker_build_effects_probe.gd`.

## Public API
- `static bool AnswersConstructionVisualShape(Node? node)` — implements the interface, or exposes `BuildProgress` by name.
- `static void ConfigureSite(Node node, Vector2I footprint, Vector2 cellSize, int stageIndex, int stageCount)`.
- `static void SetSiteState(Node node, string state)`, `static void SetSiteMaterials(Node node, Array materials)`, `static void SetBuildProgress(Node node, float progress)`.
- `static void WorkPulse(Node node)` — calls the method if the node has it.

## Dependencies
`IConstructionVisual`. `internal`, used by `GridBuildStageVisualComponent`.

## Notes
- Every member is optional: each setter writes only a property the node actually exposes, so a scene that wants `BuildProgress` but has no use for `SiteMaterials` need not declare it.
