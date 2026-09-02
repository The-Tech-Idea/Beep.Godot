# MountainTileMapLayerGeneratorComponent

Game-facing component: an editor/runtime tool node that paints a deterministic, roughly-circular top-down mountain footprint into a `TileMapLayer`, sourcing its tile art from a generated asset-pack manifest — a visuals-only authoring tool, not part of the procedural map-generation pipeline.

`MountainTileMapLayerGeneratorComponent` loads a manifest listing individual mountain-art assets (each tagged with a `role` like `top_center`/`cliff_front_left`/`ramp_south` and a `category` fallback), composites them into one runtime `TileSetAtlasSource` texture, creates or reuses a `TileMapLayer`, and paints an ellipse-shaped mountain (or, for a manifest whose roles indicate a rectangular "floor" layout, a rectangle) by picking a role-appropriate asset per cell via a deterministic hash. It also punches an optional winding road/ramp cut through the mountain and scatters boulder/vegetation props. Like its prefab-generator sibling, the class doc comment is explicit that this writes nothing to `GridCellDataComponent` — gameplay terrain (where `TerrainRelief.Mountains` actually is) is decided upstream by `TerrainGeneratorComponent`, and this component is only for dropping an authored set-piece on top of that.

## Public API

- `[Signal] MountainGeneratedEventHandler(int paintedCells)` — emitted at the end of `GenerateMountain()` with the number of cells painted.
- `[Export] string ManifestPath` — `res://` path to the mountain asset-pack `manifest.json`; default points at a temp/dev asset path.
- `[Export] NodePath TileMapLayerPath` — path to an existing `TileMapLayer` to paint into; if empty and `CreateLayerIfMissing` is true, one is created as a child instead.
- `[Export] bool CreateLayerIfMissing` — whether to auto-create a `TileMapLayer` child when `TileMapLayerPath` doesn't resolve.
- `[Export] string CreatedLayerName` — name given to an auto-created layer.
- `[Export] bool GenerateOnReady`, `[Export] bool GenerateInEditor` — same on-ready/editor-gating pattern as the prefab generator: calls `GenerateMountain()` deferred from `_Ready()` when appropriate.
- `[ExportGroup("TileMap")] int SourceId` — the `TileSet` source id painted cells reference.
- `[Export] int AlternativeTile` — the alternative-tile index passed to every `SetCell` call (always the same value for every painted cell).
- `[Export] Vector2I TileSize` — the `TileSet`'s logical tile size.
- `[Export] Vector2I RuntimeAtlasSlotSize` — the per-tile slot size reserved in the composited runtime atlas.
- `[Export] bool AutoExpandRuntimeAtlasSlot` — if true, `RuntimeSlotSize()` grows the slot to fit the largest loaded asset's source rect rather than clipping it.
- `[Export] Vector2I MaxSourceSpriteSize` — assets whose source rect exceeds this on either axis are silently skipped when the manifest is loaded.
- `[Export] bool RebuildTileSetFromManifest` — whether `GenerateMountain()` rebuilds the `TileSet`/atlas every call, or reuses an existing one on the layer.
- `[Export] bool ClearLayerBeforeGenerate` — whether the layer is cleared before painting.
- `[ExportGroup("Mountain")] Vector2I OriginCell` — top-left cell offset where the footprint is painted.
- `[Export] Vector2I MountainSize` — footprint width/height in cells.
- `[Export] int Seed` — seed for every deterministic hash used in asset selection, road-cut placement and prop scatter.
- `[Export] float EdgeThickness` — fraction (0–1) of the ellipse radius counted as "edge" (cliff) band.
- `[Export] float InnerPlateauRadius` — fraction (0–1) of the ellipse radius counted as flat "top center" plateau.
- `[Export] float PropDensity` — 0–1 probability a given non-road, mid-radius cell gets a boulder/vegetation prop.
- `[Export] bool AddRoadCut` — whether a winding road/ramp path is cut through the mountain (only takes effect if the manifest actually has `road_vertical` or `ramp_south` roled assets).
- `[Export] int RoadOffset` — horizontal offset of the road's centerline from the footprint's midline.
- `int GenerateMountain()` — resolves the target layer, loads the manifest if needed, (re)builds the `TileSet` if configured to, clears the layer if configured to, paints the footprint, calls `TileMapLayer.UpdateInternals()`, emits `MountainGenerated`, and returns the painted-cell count (0 on any failure).
- `TileMapLayer? GetTileMapLayer()` — resolves (creating if needed) and returns the target layer.
- `Godot.Collections.Dictionary GetLastGenerationSummary()` — manifest path, total asset count, per-category and per-role counts, and the layer's current used-cell count.
- `override string[] _GetConfigurationWarnings()` — flags a missing `ManifestPath`, a missing `TileMapLayerPath` when auto-create is disabled, or non-positive `TileSize`/`RuntimeAtlasSlotSize`/`MountainSize`.

## Dependencies

- Reads `TerrainLayers.ZFor(TerrainLayers.Mountains)` (`TerrainLayers.cs`) to set the `ZIndex` of an auto-created `TileMapLayer`, so an authored mountain sits at the same visual stacking level a generated mountain would.
- Otherwise reads only its own JSON manifest input; does not read from or write to `GridCellDataComponent`, `TerrainGeneratorComponent`, or `GeneratedTerrainField`.

## Notes

- The `PaintMountain` dispatch (`if (HasRole("floor_center")) return PaintFloor17Rectangle(layer);`) means the component silently switches between an elliptical-mountain layout and an entirely different rectangular-floor layout based purely on whether the loaded manifest happens to define a `floor_center` role — there is no explicit mode setting for this; a manifest author who reuses that role name for something else would unknowingly trigger the floor layout instead of the mountain one.
- `IsRoadCell` similarly depends on role presence (`road_vertical` or `ramp_south`) to decide whether `AddRoadCut` has any visible effect — `AddRoadCut = true` with a manifest lacking both roles paints no road and gives no warning that the setting did nothing.
- `PickRoleAsset` degrades role → category-fallback → any-available-asset without any warning at each step, so a manifest missing expected roles/categories still "works" but silently paints wrong-looking tiles instead of failing loudly.
- Shares the same JSON-reading helper pattern (`ReadString`/`ReadInt`/`ReadBool`/`ReadFloat`/`DiskPath`) and a near-identical `HashInt`/`Hash01` deterministic-noise implementation with `MountainPrefabGeneratorComponent.cs` — duplicated rather than factored into one shared utility.
- `AlternativeTile` is exported but always used as a single fixed value on every painted cell (never varied per-tile), unlike `SourceId`/`TileCoords` which do vary — effectively a global constant rather than a per-placement choice; not wrong, just narrower than its per-`[Export]` presence might suggest.
- Referenced only from `tests/mountain_tilemap_layer_generator_probe.gd` and documentation — not instantiated anywhere in the generation pipeline or a gameplay scene in this repo.
