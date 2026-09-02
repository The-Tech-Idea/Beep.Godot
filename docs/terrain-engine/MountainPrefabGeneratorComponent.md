# MountainPrefabGeneratorComponent

Game-facing component: an editor/runtime tool node that instantiates a complete authored mountain/island prefab (layered art, walkable areas, route connectors, anchors) from a generated prefab manifest — a set-piece authoring tool, not part of the procedural map-generation pipeline.

`MountainPrefabGeneratorComponent` reads a `prefab_manifest.json` produced by an external asset pipeline and turns it into a `Node2D` subtree: `Sprite2D` parts for the art (sourced from a single baked image, a manifest's layered placements, or a separate prefab-chunk manifest, selectable/auto-detected via `SourceMode`), `Area2D` regions with `CollisionPolygon2D`/`CollisionShape2D` for walkable levels, `Area2D` route-connector regions linking levels, and `Marker2D` anchors for castle/player placement. It also tracks whether the mountain's levels are actually reachable from each other (a BFS over the route graph) and can pack the generated subtree into a `.tscn` scene file. It is entirely independent of `GridCellDataComponent`/gameplay terrain queries — it draws and lays out a hand-authored object, nothing more.

## Public API

- `[Signal] PrefabGeneratedEventHandler(int partCount)` — emitted at the end of `GeneratePrefab()` with the visual part count.
- `[Export] string PrefabManifestPath` — `res://` path to the `prefab_manifest.json` to load; default points at a bundled reference asset.
- `[Export] bool GenerateOnReady` — if true, calls `GeneratePrefab()` deferred from `_Ready()`.
- `[Export] bool GenerateInEditor` — gates whether `GenerateOnReady` also fires inside the editor (`Engine.IsEditorHint()`), not just at runtime.
- `[Export] bool ClearExistingGeneratedParts` — if true, `GeneratePrefab()` first removes and frees any previously generated children (matched by group membership).
- `[Export] bool UseSingleBakedPrefabImage` — forces `SourceMode.BakedPrefabImage` regardless of the `SourceMode` value.
- `[Export] MountainPrefabSourceMode SourceMode` — `Auto | BakedPrefabImage | ManifestPlacements | PrefabChunks`; picks which of three visual-loading paths `AddVisualSprites` takes.
- `[Export] string PrefabChunkManifestPath` — explicit path to a separate chunk-asset manifest; when empty, `Auto`/`PrefabChunks` mode falls back to the `prefab_chunk_manifest` field inside the main manifest.
- `[Export] MountainPrefabLayoutPreset LayoutPreset` — `Reference | Compact | Wide | HighCastle`; applies a hardcoded per-role position offset table (`ApplyLayoutPreset`) on top of chunk placements.
- `[Export] bool IncludeCompletePrefabChunk` — whether the `"complete_prefab"`-category chunk asset is instantiated alongside the individual chunks.
- `[Export] string SaveGeneratedScenePath` — default target path for `SaveGeneratedScene()`.
- `[Export] string GeneratedPartGroup` — group name tagged on generated `Sprite2D` parts, used both to mark and to later find-and-clear them.
- `[Export] bool CreateWalkableAreas`, `[Export] string WalkableAreaGroup`, `[Export] uint WalkableCollisionLayer`, `[Export] uint WalkableCollisionMask` — whether/how walkable-region `Area2D`s are built from the manifest's `walkable_regions`.
- `[Export] bool CreateAnchorNodes`, `[Export] string AnchorGroup` — whether/how anchor `Marker2D`s are built from the manifest's `anchors` object.
- `[Export] bool CreateRouteConnectorAreas`, `[Export] string RouteConnectorGroup`, `[Export] float RouteConnectorWidth` — whether/how route-connector `Area2D`s are built, either from explicit `route_regions` polygons or synthesized as capsule-like rectangles between level regions using `route_edges`.
- `[ExportGroup("Placement")] Vector2 PrefabOffset`, `float PrefabScale`, `int BaseZIndex`, `bool UseHeightForZIndex`, `int HeightZIndexStep`, `CanvasItem.TextureFilterEnum TextureFilter` — global placement/scale/z-ordering/filtering applied to every generated node.
- `int GeneratePrefab()` — loads the manifest, optionally clears prior output, builds visual sprites + gameplay data + walkable areas + route connectors + connectivity check + anchors, emits `PrefabGenerated`, and returns the visual part count (0 on any failure, logged via `GD.PushWarning`).
- `Godot.Collections.Dictionary GetLastGenerationSummary()` — a dictionary snapshot of counts and flags from the most recent `GeneratePrefab()` call.
- `Godot.Collections.Array<Dictionary> GetMountainLevels()` / `GetWalkableRegions()` / `GetRouteEdges()` / `GetRouteRegions()` — deep-duplicated copies of the manifest's raw `levels`/`walkable_regions`/`route_edges`/`route_regions` arrays as read at generation time.
- `Godot.Collections.Dictionary GetAnchors()` — deep-duplicated copy of the manifest's `anchors` object.
- `Godot.Collections.Array<Dictionary> GetPrefabChunkAssets()` — the raw asset entries from the last-used prefab-chunk manifest (empty unless chunk mode was used).
- `bool IsRouteConnected()` — result of the last BFS connectivity check across mountain levels via their route edges.
- `Error SaveGeneratedScene()` — packs and saves this node's subtree to `SaveGeneratedScenePath` (generating first if nothing has been generated yet).
- `Error SaveGeneratedSceneToPath(string scenePath)` — same, to an explicit path; creates the containing directory, reparents every descendant's `Owner` to `this`, then `PackedScene.Pack` + `ResourceSaver.Save`.
- `Godot.Collections.Dictionary GetRouteConnectivitySummary()` — a smaller dictionary of just the connectivity-related counts/flag.
- `Vector2 GetAnchorPosition(string anchorId)` — world-local position of a named anchor (offset+scaled), or `Vector2.Zero` if the id is unknown.
- `int GetHeightLevelAtLocalPosition(Vector2 localPosition)` — looks up which route region, then which walkable region, contains a local point, and returns its height level (`-1` if the point is in neither).
- `Godot.Collections.Dictionary GetWalkableRegionAtLocalPosition(Vector2 localPosition)` / `GetRouteRegionAtLocalPosition(Vector2 localPosition)` — point-in-polygon (falling back to point-in-rect) lookup against the cached region lists; returns an empty dictionary when no region contains the point.
- `override string[] _GetConfigurationWarnings()` — flags a missing `PrefabManifestPath` or a non-positive/non-finite `PrefabScale`.

## Dependencies

None. This file reads only its own JSON manifest input (via `System.Text.Json`) and Godot node/resource APIs; it does not read from or write to any other file in `addons/beep_game_builder_cs/ecs/terrain/`, and in particular never touches `GridCellDataComponent` or `TerrainGeneratorComponent`/`GeneratedTerrainField` — the class doc comment is explicit that this is deliberate.

## Notes

- The class doc comment states plainly that this is "NOT A MAP GENERATOR" and "Not yet wired to the cell data" — accurate as written; nothing in this file writes gameplay-visible terrain state, so any game that wants a generated mountain to block movement or read as a terrain kind must wire that up separately (the comment names this as future work for "the mountain asset-pack workstream," not a bug here).
- `LayoutPreset` (`Compact`/`Wide`/`HighCastle`) applies string-`Contains` matches against hardcoded role-name substrings (`"level_1_right"`, `"route_2_to_3"`, etc. in `ApplyLayoutPreset`) that are specific to one particular reference mountain's chunk role naming; a manifest whose chunks use different role names silently gets no offset for any preset other than `Reference` — not a crash, just a no-op layout tweak.
- `AddRouteConnectorAreas` has two independent code paths (`AddExplicitRouteRegionAreas` for manifests with `route_regions` polygons, vs. edge/level synthesis for manifests that only have `route_edges` + `walkable_regions`) selected by whether `_lastRouteRegions.Count > 0`; the synthesized path silently drops any edge whose `from`/`to` level isn't in the level→region map rather than reporting it (contributes to `_lastMissingRouteEdgeCount` but does not warn).
- `AddWalkableAreas`, `AddExplicitRouteRegionAreas`, and `AddAnchorNodes` all set `child.Owner = Owner` (not `= this`) when running in the editor, while `NewPartSprite` does the same; `Owner` is whatever this node's own owner is (often null for a freshly-instantiated, unsaved scene), so nodes generated before this component is itself saved into an owned scene won't be visible in the Scene dock until a save/reload — `SaveGeneratedSceneToPath` works around this separately by explicitly reparenting every descendant's `Owner` to `this` right before packing.
- Both mountain generator components (`MountainPrefabGeneratorComponent`, `MountainTileMapLayerGeneratorComponent`) independently reimplement a JSON `JsonElement`→`Variant`/`Godot.Collections.Dictionary` conversion (`JsonObjectToDictionary`/`JsonValueToVariant`/`JsonArrayToGodotArray` here) and the same family of `ReadString`/`ReadInt`/`ReadBool`/`ReadFloat`/`DiskPath`/`ResolvePath` JSON-reading helpers — near-identical logic duplicated rather than shared.
- Referenced only from template scenes (`templates/scenes/reference_mountain_prefab_creator.tscn`, `front_2_5d_mountain_prefab_creator.tscn`) and `tests/*_probe.gd` scripts — not instantiated anywhere in the generation pipeline or a gameplay scene in this repo.
