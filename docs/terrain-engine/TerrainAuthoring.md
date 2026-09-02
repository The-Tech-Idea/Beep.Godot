# TerrainAuthoring

Support utility: a static helper used by every terrain renderer/data-layer component to create or reuse `TileMapLayer` nodes and make generated nodes persist into the saved scene file.

Godot only writes a child node into a saved `PackedScene` if that node's `Owner` is the scene root; a node added purely with `AddChild` renders correctly in the live tree but silently vanishes on the next scene reload because it was never written to disk. `TerrainAuthoring` exists to make that ownership assignment a single, correct, shared code path instead of the eight ad hoc (and inconsistently correct) copies that previously existed across renderers, the data-layers component, and the mountain generator — one of which, the tile view's layers, set no owner at all and so could never be authored in the editor.

## Public API

- `static TileMapLayer EnsureLayer(Node owner, string name)` — the standard way any terrain view acquires a `TileMapLayer`: looks up an existing child of `owner` named `name`; if none exists (or it is no longer a valid instance), creates a new `TileMapLayer`, adds it as a child of `owner`, and calls `Adopt` on it so it is saved with the scene. Returns the (found or created) layer either way.
- `static void Adopt(Node generated, Node creator)` — gives `generated` an `Owner` so it will be serialized with the scene. Does nothing if `generated` is not yet inside the tree. In the editor, targets `creator.GetTree()?.EditedSceneRoot` (the scene currently being edited/about to be saved); at runtime falls back to `creator.Owner ?? creator.GetTree()?.CurrentScene`. If no root is found, the root IS the generated node itself, or the root is not an ancestor of `generated` (setting `Owner` to a non-ancestor throws in Godot), the call is a silent no-op — ownership is simply left unset.

## Dependencies

None — this file defines only static helpers over core Godot types (`Node`, `TileMapLayer`, `GodotObject`) and does not read or write any other file in `addons/beep_game_builder_cs/ecs/terrain/`. It is, however, called *from* at least six other files in this batch's directory: `TerrainTileRendererComponent.cs`, `TerrainDataLayersComponent.cs`, `TerrainPaintedRendererComponent.cs`, `TerrainIsometricAutotileRendererComponent.cs`, `TerrainIsometricRendererComponent.cs`, `TerrainIsometricFeatureRendererComponent.cs` — each calling `TerrainAuthoring.EnsureLayer`/`Adopt` to acquire and persist its own `TileMapLayer`.

## Notes

- `Adopt`'s failure paths (not in tree, no root found, root is the generated node, root is not an ancestor) are all silent no-ops with no logging — a caller that expects ownership to have been set (and therefore the node to be saved) has no signal when it wasn't. This is a deliberate, documented trade-off ("the ordinary way that happens" at runtime under no scene root) rather than an oversight, but it is still a silent-failure path worth knowing about when a generated map mysteriously fails to persist.
- `Adopt` deliberately does not check `Engine.IsEditorHint()` before running — the doc comment explains this is intentional (one code path instead of two, and it lets a guard/test prove persistence by packing and reading back a generated map without driving the editor).
- This file is a good example of rule-3-style deduplication already having happened: the doc comment itself describes the prior duplicated state (eight copies, one of which was actually broken) that this consolidation replaced.
