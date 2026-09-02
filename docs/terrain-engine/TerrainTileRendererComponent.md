# TerrainTileRendererComponent

Renderer: a `[Tool][GlobalClass]` `Node2D` that draws generated terrain as a stack of autotiled, dual-grid `TileMapLayer`s — one per configured biome — plus an optional shader-driven sea surface.

This is the renderer a game with hand-authored 15-piece tileset art uses (as opposed to the painted or isometric renderers). It is a single scene node that internally builds and owns one `TerrainTransitionLayerComponent` (plus its backing `TileMapLayer`) per biome atlas the designer has assigned, in a fixed draw order (sea, then ground biomes, then hills/mountains) taken from the shared `TerrainLayers` stack rather than decided locally. It also lays a shader-textured sea (the same water shader the isometric view uses, switched to top-down projection) over the water tiles so the coastline has shading, waves and foam instead of a flat blue field.

## Public API

- `[Export] Vector2I BoundsOrigin` / `[Export] Vector2I BoundsSize = (48,30)` — the map rectangle (in cells) this renderer draws, forwarded to every child transition layer.
- `[Export] Vector2I AtlasTileSize = (64,64)`, `[Export] int AtlasColumns = 4` (range 1–16), `[Export] int AtlasTileRows = 4` (range 1–16) — the shared 15-piece atlas layout (tile pixel size and sheet grid) applied to every biome atlas.
- `[Export(File)] string BaseAtlasPath` — an optional filled base layer drawn under everything, so a gap between biome layers shows this texture (or the sea, per the class comment) rather than a hole.
- `[Export(File)] string {Grass,GrassDetail,DryGrass,Sand,Desert,DesertDetail,Jungle,Swamp,Tundra,Rock,Gravel,Snow,Ice,Water,WaterDetail}AtlasPath` — one 15-piece PNG/WEBP atlas path per biome (plus optional "detail" overlay atlases for grass/desert/water); leaving one blank omits that biome's layer entirely.
- `[Export] bool RefreshOnReady = true` — when true and not running in the editor, defers a call to `Rebuild()` on `_Ready`.
- `[Export] NodePath GeneratorPath` — path to the `TerrainGeneratorComponent` this view reads (both cell data and, for the sea shader, the coastline field).
- `[Export(File,*.gdshader)] string WaterShaderPath` — if set, the shader used to paint the sea over the water tiles; if empty, no shader sea is built and only the water tiles themselves draw (a flat, stylised sea).
- `[Export] float CoastRangeTiles = 5.0`, `[Export] int CoastDetail = 2` — controls the resolution/range of the coast-distance field baked into `_coastMap` and fed to the shader.
- `[Export] float MaxOpacity`, `ShoreOpacity`, `LakeOpacity`, `ClarityTiles`, `WaveIntensity`, `FoamStrength`, `DeepTiles`, `ShallowTiles` — forwarded verbatim as shader parameters (`max_opacity`, `shore_opacity`, `lake_opacity`, `clarity_tiles`, `wave_intensity`, `foam_strength`, `deep_tiles`, `shallow_tiles`).
- `[Export(File)] string ShallowTexturePath`, `DeepTexturePath`, `SandTexturePath`, `FoamSheetPath` — optional authored textures for the water shader; each is loaded via `TerrainTextures.Load` and only set as a shader parameter if it actually loads. `FoamSheetPath` additionally flips a `use_foam_sheet` shader flag on success so the shader falls back to procedural foam when no sheet is supplied.
- `void Rebuild()` — public entry point. Ensures the biome `TerrainTransitionLayerComponent`s exist (building or reusing them per a computed configuration "signature"), calls `RefreshTransitions()` on each, pushes a warning if zero biome layers are configured, then builds/updates the shader sea via `EnsureWaterSurface()`.
- `override string[] _GetConfigurationWarnings()` — editor warnings: missing `GeneratorPath`, or zero configured biome atlases.
- `override void _Ready()` — defers `Rebuild()` when `RefreshOnReady` is true and this is not the editor.

## Dependencies

- Reads `TerrainGeneratorComponent` (via `GeneratorPath`): calls `TerrainKindsPresent()` to decide which biome layers are actually needed for the current map, and passes its node reference down to each `TerrainTransitionLayerComponent` it creates (as `TerrainGeneratorPath`).
- Creates and owns one `TerrainTransitionLayerComponent` per configured biome (`CreateLayer`), wiring `TerrainGeneratorPath`, `DisplayLayerPath`, `DetailDisplayLayerPath`, `TransitionTerrainKind`, `AtlasTexturePath`/`DetailAtlasTexturePath`, and forcing `UseTileSetTerrains = false` / `UseCanonical15PieceLayout = true` (hand-authored 15-piece sheets, not Godot `TileSet` terrain sets).
- Reads `TerrainLayers.ZFor(TerrainLayers.Sea)` to place the shader-sea `TileMapLayer`'s z-index; does **not** set z-index on the biome `TileMapLayer`s themselves — each `TerrainTransitionLayerComponent` places its own display layer via `TerrainLayers.ZForKind`/`ZForFloor`, by design (see Notes).
- Calls `TerrainAuthoring.EnsureLayer` / `TerrainAuthoring.Adopt` to create and register child `TileMapLayer` nodes (both the sea layer and each biome's display layer).
- Calls `TerrainCoastField.Build(_generator, size, CoastDetail, CoastRangeTiles)` to build the `_coastMap` texture fed to the water shader.
- Calls `TerrainShaderSurface.BuildTileSet` / `TerrainShaderSurface.Fill` to build and fill the blank-tile `TileMapLayer` the sea shader paints onto.
- Calls `TerrainTextures.Load` (not a bare `GD.Load`) for every optional water texture, so relative/absolute path resolution matches the isometric renderer's loader.
- Reads/writes nothing in `GridCellDataComponent`, `TerrainTileSets`, or `TerrainWaterStage`/`TerrainTileReductionStage` directly — it is a pure consumer of the already-generated `TerrainGeneratorComponent`.

## Notes

- The class comment on `BiomeLayer` and the `Configure()`/`CreateLayer()` method comments explicitly document a past bug: this renderer used to also record which vertical level (ground/hills/mountains) each biome belonged to, duplicating `TerrainLayers.LevelForKind`, and that duplication is why gravel/rock were once classified in two disagreeing places. The fix (removing the second copy, deferring entirely to `TerrainTransitionLayerComponent`'s own z-placement) is present in the current code — this is documentation of a resolved defect, not a live one.
- Similarly, `ConfiguredLayers()`'s doc comment records a second past bug: water used to draw *last* (on top), so its 15-piece transition tiles resolved the coastline by drawing over the beach sand layer underneath — "436 tiles of it, drawn and then buried." The current declaration order (sea first, then ground biomes ending with `sand`, then hills/mountains) is the fix; worth flagging because reordering this list without re-reading the comment could reintroduce the same bug.
- `EnsureLayers()`'s reuse/rebuild decision is driven entirely by a string `Signature()` (atlas paths + tile size/columns/rows) compared against `_builtSignature`; a signature match short-circuits into `Configure()`, which only re-applies `BoundsOrigin`/`BoundsSize` and explicitly does *not* re-apply atlas paths or z-index — both are deliberately load-once, documented inline as intentional rather than an oversight.
- `RefreshOnReady` combined with `Engine.IsEditorHint()` means the renderer does not auto-rebuild in the editor on scene load — only `RefreshInEditor`-style behavior lives on the child `TerrainTransitionLayerComponent`, not here; a designer relying on this renderer alone to preview in-editor must call `Rebuild()` manually or via the dock.
- No dead code or stale comments found beyond the two documented-as-fixed regressions noted above, which are intentionally left as warnings against regression rather than removed.
