# Required terrain output: editable TileMapLayer maps

Owner clarification: 2026-09-11. Status: mandatory design requirement; implementation work is Proposed. The terrain generator must create populated Godot `TileMapLayer` nodes that developers can use as ordinary map assets.

## Deliverable

**Generate → populated layers → edit in Godot → save `.tscn` → instance in a game.** This is the minimum useful workflow. It must not require a Beep runtime controller to regenerate or draw the saved map.

Proposed output:

```text
res://scenes/maps/valley.tscn
Valley (Node2D)
├─ Ground (TileMapLayer)
├─ Water (TileMapLayer)             optional separate role
├─ Cliffs (TileMapLayer)            optional elevation/edge role
├─ Detail (TileMapLayer)            optional decoration
├─ Objects (Node2D)                optional authored props
└─ Spawns (Node2D)                 optional Marker2D children

res://resources/maps/valley/tileset.tres
res://resources/maps/valley/definition.tres        editor provenance
res://resources/maps/valley/gameplay_baseline.*    optional Beep profile
```

### Spawns (implemented, FEAT-09/FEAT-12)

`Spawns` holds one `Marker2D` per player start, named `Start_<k>` where k is the start's index in
the generator's start order. Each marker stands on that start's headquarters anchor, positioned in
the **map root's** space, and carries metadata:

| Key | Type | Meaning |
|---|---|---|
| `start_index` | int | Which start this is. Read in preference to the node name, which a designer may rename. |
| `hq_footprint` | Vector2I | The headquarters footprint the start was validated for. |
| `unusable` | bool | Present only on a start the generator reported as unplayable (too small, no exit). |

`TerrainSpawnMarkers` writes them and `TerrainWorldComponent.SpawnsPath` publishes them on every
build, so a generated map saved as a `.tscn` keeps its starts with no generator present. A designer
can author the same nodes by hand. `GridStartAreaComponent.SpawnsRootPath` reads them back, and a
marker wins over the generated start order where a map has both.

Markers are the native profile's whole record of a start: the per-cell reservation
(`terrain_start_area`) belongs to the Beep gameplay baseline. A native map therefore has starts and
no reserved ground, which `GridStartAreaComponent.HasAreas` reports as false, and a build
restriction keyed on areas goes inert rather than refusing the entire map.

The playable area is the navigation bounds inset by `GridNavigationComponent.PlayableInset` — the
map's cordon, zero by default. Everything already reading `IsInBounds` inherits it.

Layer roles are explicit. They are not a requirement to emit empty layers or exactly one layer per biome. Existing transition rendering may need additional display layers; list all populated output layers, offsets and purposes in the generated manifest. An empty `LogicalGrid` helper is not the output map.

## Two output profiles

| Profile | Required contents | Dependency boundary |
|---|---|---|
| Native Godot map | Populated TileMapLayer nodes, TileSets/textures, tile physics/navigation where configured, optional native object/marker nodes | No Beep generator, GameApp, C# drawing callback or temporary preview resource needed to open/render/collide |
| Beep gameplay map | Native output plus versioned gameplay baseline, stable objects and explicit loader bindings | Beep supplies simulation, resources, jobs, grid navigation and save integration |

Plain native output is a first-class result, not a degraded preview. Generator settings can remain in a separate authoring definition. Optional effects may be included if their dependencies are saved; a painted surface or water shader is not a substitute for required tile cells.

## TileSet mapping

Proposed `TerrainTileBinding` maps a semantic terrain ID and layer role to either an authored terrain-set/terrain-index rule or explicit `(source_id, atlas_coords, alternative_tile)` choices. Validate atlas sources and coordinates, tile size, shape/layout, terrain peering bits and required terrain coverage before generation. Report missing bindings; never silently replace an unmapped biome with grass.

Reuse the existing transition and isometric-autotile implementations. Begin with square/top-down native tiles, then the authored isometric TileSet path. Preserve cell origin, transforms and tile layout. Dual-grid display cells and half-cell offsets need an explicit mapping to logical cells; do not infer gameplay by reading an empty coordinate layer or treating display corners as cells.

Persist exact tile tuples so normal scene reopen preserves tile variations. If rebuilding terrain connections can choose different alternatives, distinguish gameplay determinism from visual determinism and store the selected variants when exact visual identity is required. Chunk repainting includes the neighbor halo required by the binding; test transitions at chunk edges.

TileSet custom data describes tile types shared by many cells. Use it for semantic kind, default walkability or surface tags; do not put per-cell crop ages, remaining ore or unique entity IDs into shared TileData. Those belong to the optional gameplay baseline/session data.

## Editing and authority

- Native output is editable with Godot's TileMapLayer tools and can be used with `SetCell`, `EraseCell`, terrain painting and patterns.
- A developer can save, duplicate and instance the scene; inspect populated layers directly; and remove the authoring generator without losing the map.
- Native tile edits remain intact on reopen, Play and addon rebuild. Generator components are absent or disabled in baked output.
- Beep mode exposes **Sync gameplay from tiles** and validates reverse bindings. Ambiguous multiple-layer mappings, unknown tiles or unrepresentable changes block gameplay publication with a cell/layer diagnostic. Native-only export remains possible if its own requirements pass.
- Semantic Beep brushes and native tile tools share an editor transaction boundary. The last committed authored representation drives the other; stale pending edits require resolution before regeneration.
- Regenerate into staging. Show a diff and preserve/reapply authored edits by default. Replace affects only the generated layer set, never unrelated developer children.

## Save and reuse

Pack a new output root with correct ownership for every generated descendant. Save TileSets, atlas textures/materials and other dependencies under project-owned paths; reuse shared immutable source assets unless copying is requested. Do not mutate a shared source TileSet while customizing one map. Make changed resources local or create a project-owned copy.

Use ordinary Godot scene serialization for native cell data. Never serialize those cells only into a Beep dictionary and then reconstruct the only visible map through C# at startup. The optional logical snapshot complements, rather than replaces, the populated TileMapLayer scene.

## Runtime terrain

Use the same validated binding/emission core for runtime generation, with a runtime publisher that edits TileMapLayer nodes on the main thread in bounded batches. Detach generation math from Node and Resource access. Local terrain changes update affected cells and adjacency halos; avoid full-map reconstruction for one edit.

Save semantic changes and any required exact tile overrides in the session format. Restore the immutable base first, then session changes, then validate navigation/collision before enabling play. Runtime saving writes under `user://`; it does not overwrite the authored `.tscn` under `res://`.

## Acceptance gates

1. Generate a 64×64 square map. Ground has valid populated cells, not only a rectangle, shader texture or empty helper grid.
2. Paint and erase cells using Godot's standard tools; save and reopen. Exact cell tuples and chosen TileSet survive.
3. Export Native Godot map, remove Beep dependencies from the fixture project and instance the result. Rendering and configured collision still work. Repeat in a non-.NET Godot fixture when available; otherwise record that portability test as outstanding.
4. Instance the output twice; per-instance cell edits do not mutate the other instance or the source TileSet.
5. Generate with an authored isometric TileSet. Verify origin, `MapToLocal`/`LocalToMap`, terrain joins and edge tiles.
6. Change a tile in Beep mode, sync and publish. Gameplay queries and the visible tile agree after a fresh-process load; an ambiguous binding fails explicitly.
7. Runtime generation and a local terrain edit produce TileMapLayer changes with no worker-thread Godot node calls. Save/reload preserves gameplay and required tile overrides.

Primary implementation: BGB-13. Publication: BGB-04. Editor experience: BGB-03/BGB-10. Dynamic updates: BGB-06/BGB-08. All are tracked in the [master tracker](../ENGINE_ENHANCEMENT_PLAN.md#game-builder-scenes-and-terrain-lifecycle-review-2026-09-11).
