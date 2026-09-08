# IGridSite

C# interface: what a site tells you before you commission it — the ground it takes, what it costs in material, and how long it takes in turns. Three facts, asked one way, so a build menu, a tooltip, a planner or an AI reads them from one contract instead of knowing each definition type by name.

**A site is something you commission onto the grid that occupies cells.** That is the whole test, and it is deliberately narrow:

- A build definition is a site. So is the placed build site it becomes.
- A production recipe and an extractor are *processes hosted in* a site. They declare turns and materials and take their footprint from the `GridObjectComponent` they sit on, which is what `GridExtractorComponent.FootprintCells()` already does.
- A storage hold, a road and a resource node are **not** sites. Storage has capacity and its host's area; a road is per-cell and is commissioned through an ordinary 1×1 build; a resource node is found, not built. Giving them invented 1×1 footprints would buy uniformity by making the contract mean less.

## Public API
- `Vector2I SiteFootprint` — cells the site occupies, width by height. At least 1×1.
- `Godot.Collections.Array SiteMaterials` — material that must physically arrive before work starts, as `GridResourceAmount` entries. Distinct from a wallet cost, which is currency spent the instant the build is confirmed.
- `int SiteTurns` — turns of work. A turn is a day: five turns is five end-turns in a turn-based game and five in-game days in a real-time one, so the authored number means the same thing on both axes.

## Dependencies
None beyond Godot types. Implemented by [GridBuildDefinition](GridBuildDefinition.md). Read through [GridSitePorts](GridSitePorts.md), never by casting — a GDScript definition or a plain Dictionary answers the same three questions by name.

## Notes
- Follows the same interface-plus-port pattern as [IConstructionVisual](IConstructionVisual.md) / [GridConstructionVisualPorts](GridConstructionVisualPorts.md) and [IWorker](IWorker.md) / [GridWorkerPorts](GridWorkerPorts.md): the interface serves C#, the port lets anything else participate.
- Turns, not seconds, on purpose. `BuildSeconds` used to live on the build definition and was only correct on the real-time axis.
