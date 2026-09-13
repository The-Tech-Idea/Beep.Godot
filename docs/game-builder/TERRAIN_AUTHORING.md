# Terrain maps at design time

Status: current guidance plus proposed workflow, 2026-09-11. Proposed dock actions and asset types below require implementation.

**Owner requirement:** the primary deliverable is a generated map made of real, editable `TileMapLayer` nodes. See the mandatory [TileMapLayer output contract](TILEMAP_OUTPUT.md). Publishing must support a native Godot scene and, optionally, a scene with Beep gameplay integration.

## What can be done today

1. Duplicate a terrain example into a game-owned scene. Use [terrain_generator_lab.tscn](../../addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn) to inspect a complete setup; keep lab controls out of the shipping map scene.
2. Configure the `TerrainWorldComponent` generator, cells, grid/navigation and renderer paths. For native tile output, choose the tiled renderer or authored isometric autotile renderer and a compatible TileSet/art setup. Painted output alone does not produce the requested editable map. Select seed, world axes, bounds/origin through their existing owners, projection and artwork. Check its Inspector warnings.
3. Press **Generate map** on the world component. This is the existing editor action; `BuildOnReady` is intentionally ignored in the editor.
4. Save the game-owned scene. Generated nodes/layers adopted by `TerrainAuthoring` can be serialized with their scene owner.
5. Decide how the game will initialize gameplay cells. The current scene save alone does not persist the cell store. Setting `BuildOnReady=false` also does not create a loader for those cells.

For a recipe-driven game today, let a new session generate the base, and use the existing scoped recipe/grid save participants for supported resident worlds. Do not claim a hand-painted, saved `.tscn` is a fully baked gameplay map. Visual tile edits are not automatically edits to `GridCellDataComponent`. Full authored-map publishing is the first proposed delivery below.

## Proposed map assets

| Asset | Owns | Does not own |
|---|---|---|
| `MapDefinition` | Stable MapId, mode, recipe reference, authored override reference, content version, presentation and published baseline references | Mutable session state |
| `TerrainRecipe` | Complete effective generation inputs, explicit seed, origin/extents, generator/schema versions, terrain/resource catalog identities and hashes | Current player edits or simulation clocks |
| `MapAuthoringLayer` | Deliberate terrain/resource overrides, protected areas, spawn/encounter markers and stable placed-object records | Disposable preview geometry |
| `MapBakeManifest` | Content revision, populated TileMapLayer scene, TileSet dependencies and validation; optional Beep logical chunks/object manifest and recipe hashes | Mutable save slot |

Resolve authored settings once into a canonical effective recipe. A map recipe should not read mutable global GameInfo defaults each time it loads. Reuse `CaptureGenerationSettings` and configuration normalization, then serialize a versioned data contract; do not persist live Nodes, Callables or resource object pointers into worker/snapshot payloads.

Recommended split: generated base + explicit authored overrides + derived views. For the first authored release, store a complete logical baseline so play does not depend on reproducing an old generator version. Keep the recipe for provenance and intentional rebakes. Later procedural/hybrid modes can use a pinned recipe and deltas under the compatibility rules in [runtime terrain](RUNTIME_TERRAIN.md).

## Proposed editor workflow

1. **Create Map:** choose mode, initial extent, seed and projection. Create the definition and scene shell in the game's directories.
2. **Preview:** generate into an isolated preview world. Report progress and estimates; offer cancellation. Preview is excluded from game saves, authored ownership and shipped output.
3. **Compare:** show changed terrain, resources, bounds and affected authored objects. Highlight changes that would invalidate a spawn or building. Do not mutate the current authored map while calculating the preview.
4. **Apply base:** commit the selected preview as one editor undo action. Keep the previous logical base and authored overrides until commit succeeds. Large edits may use bounded temporary chunk snapshots rather than giant in-memory undo dictionaries.
5. **Edit tiles:** use Godot's normal TileMapLayer paint, erase, terrain-connect, selection and pattern tools. In the Beep profile, run **Sync gameplay from tiles** (automatic incremental sync is a later optimization) before gameplay validation/publish. Beep semantic brushes update the same authored map through the output adapter. Record a dirty direction and reject simultaneous unsynchronized writes; do not let a redraw overwrite native tile edits. Decorative-only layers are explicitly labeled.
6. **Validate:** check reachability, spawn clearance, bounds, terrain-kind/catalog IDs, resource placement, stable object IDs, NodePath wiring, owner chains, collision and dependencies.
7. **Bake and publish:** always write the populated native TileMapLayer scene and its TileSet/resource dependencies. The optional Beep profile additionally writes the synchronized logical baseline and object manifest. Save into a staged output directory, verify by reopening, then publish the manifest last. Keep the prior successful revision if a write or validation fails.
8. **Play published map:** launch the shipping map loader, not the preview tree. Compare baseline hashes and counts after a fresh process load.

`Preview`, `Apply`, `Save Definition` and `Publish` are different operations. Saving authoring settings does not silently publish a new playable revision. Reopening the scene must not regenerate the base. Regeneration must state whether authored overrides will be preserved, remapped or rejected; default to preservation and reject incompatible bounds changes.

## What a playable bake must preserve

- Logical terrain kind, elevation/ramp information, surface flags and mutable metadata needed by gameplay.
- Generated layers needed for resources, underground deposits and starts, with a documented loader/derivation rule.
- Authored roads, structures, spawn markers, encounter references and stable IDs.
- Projection, origin and cell-scale bindings needed to map logical positions to scene positions.
- Required scripts, textures, materials, tile sets and catalogs as exportable project dependencies.

The native output profile uses TileMapLayer cells and authored TileSet physics/navigation data directly. It is a usable Godot map without Beep. The Beep profile needs an additional synchronized logical baseline for features such as crops, jobs and underground resources that tile identities alone do not express. During runtime Beep cells own gameplay; render changes are derived. During native editor painting, the explicit tile-to-gameplay sync transfers authority at a transaction boundary. These are lifecycle stages, not competing simultaneous stores.

In the exported game, load published assets from `res://` and write player saves under `user://`; the latter is the writable persistence location. See [Godot data paths](https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html). Publish explicit resource references or an export inclusion manifest for dynamically addressed chunk files, and verify in an exported build without `.godot` from the development checkout.

## Editor safety and lifecycle

Use [EditorUndoRedoManager](https://docs.godotengine.org/en/stable/classes/class_editorundoredomanager.html) for authoring operations. Preserve correct node ownership when committing nodes, including descendants and nested scene boundaries; [PackedScene](https://docs.godotengine.org/en/stable/classes/class_packedscene.html) packs owned nodes. Mark the edited scene dirty after committed changes.

Every editor-facing custom resource needs appropriate `[Tool]` behavior. Disconnect managed helper callbacks before assembly serialization, restore bindings after reload, and avoid managed delegates attached to engine-owned singletons where a named callable suffices. Repeated build/reload tests must keep a scene open; restarting the editor between tests would hide this class of defect.

## First acceptance map

A 64×64 TileMapLayer map with a lake, one ramp, blocked cliff, three resource types, two spawn markers, a road and a placed building. Generate it, paint/erase with Godot's tile tools, undo/redo, sync gameplay, publish, close the editor, reopen and run the published scene. Tile cell tuples, terrain hash, object IDs, path queries, spawn validity and resource totals must agree. Change presentation and redraw: logical state must not change. Remove the generator from the authored shipping variant: the baseline must still load. Test the native profile separately without Beep scripts and with two instances of the saved map.

Delivery: BGB-13 first, then BGB-02, BGB-03, BGB-04, BGB-07 and BGB-10.
