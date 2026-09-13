# Beep Game Builder: scenes and terrain workflow

Reviewed 2026-09-11 against the current working tree, including uncommitted changes. This is a design and implementation plan, not a claim that the proposed tools already exist.

The recommendation is to build one map lifecycle around the existing terrain and grid engine: **author a definition, preview it, publish a playable map, open it in a game session, and save that session separately**. Keep the current generator, grid, renderers, archive and GameApp; connect their existing responsibilities instead of replacing them.

**Required output, clarified by the owner on 2026-09-11:** terrain generation must produce populated, editable Godot `TileMapLayer` nodes that a developer can save, instance and use. A recipe, logical snapshot, shader surface or empty coordinate layer does not satisfy that requirement. Native TileMapLayer scene output is the first delivery; additional Beep gameplay data is an optional output profile.

| Document | Purpose |
|---|---|
| [Source review](REVIEW.md) | What exists, confirmed gaps, evidence and limits |
| [Scene creation and management](SCENE_LIFECYCLE.md) | Project generation, scene catalog, upgrades and transitions |
| [Editor terrain authoring](TERRAIN_AUTHORING.md) | Current steps and proposed preview/edit/bake/publish workflow |
| [TileMapLayer output contract](TILEMAP_OUTPUT.md) | Required native output, TileSet mapping, editing and reusable scenes |
| [Runtime terrain and saves](RUNTIME_TERRAIN.md) | Generated, authored and hybrid worlds; loading, streaming and persistence |
| [Implementation plan](../../plans/game-builder/IMPLEMENTATION_PLAN.md) | Ordered work packages, dependencies, acceptance gates and estimates |
| [Master tracker](../ENGINE_ENHANCEMENT_PLAN.md#game-builder-scenes-and-terrain-lifecycle-review-2026-09-11) | Canonical delivery status for BGB-01 through BGB-13 |

## Choose the map mode

| Mode | Game loads | Generation policy | Typical use |
|---|---|---|---|
| Authored | Published gameplay baseline, authored objects, scene/presentation references | No new-world generation on load | Campaign, puzzle, tactical mission |
| Procedural | Complete versioned recipe and explicit seed | Generate once when starting a new world | Strategy skirmish, randomized adventure |
| Hybrid | Recipe or baseline plus authored override layer and objects | Generate the base if required, then apply authored overrides | Designed towns on a procedural landscape |

All three can have mutable gameplay. “Authored” does not mean that players cannot dig, farm or build. The published baseline stays immutable; the session owns changes.

## First delivery

Deliver a small populated TileMapLayer map that can be edited with Godot's tile tools, saved as a reusable `.tscn`, and instanced twice without a generator. Prove its native scene profile works without Beep runtime scripts. Then add the Beep gameplay profile and prove identical terrain, navigation, collision, resources and placed objects across a fresh-process load. Ship these before expanding the dock into a large map editor. Next deliver a cancellable procedural new-game flow and a save/load round trip. Streamed save support is a separate release gate, not something implied by a successful small-map save.

Status: review and planning documents complete; implementation of the new BGB work packages is **Proposed**. Earlier fixes and existing capabilities are recorded as foundations, not counted as completion of these packages.
