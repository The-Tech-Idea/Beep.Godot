# TerrainTileRendererComponent

## Repeating Ground Detail

`GroundTexturePaths` maps exact biome names to optional repeating textures.
`GroundRepeatCells` defaults to 6 and `GroundDetailStrength` to **0 — off**.
The material adds restrained luminance variation in layer-local coordinates;
it preserves the authored transition atlas's alpha and terrain boundaries.

It is off because of what it multiplied by. The shader took the sampled texture's **absolute**
luminance, so everything painted into that ground art came through the tiles as a bright or dark
shape at its authored size: the cartoon sand's 12-to-18-texel shells read as objects lying on the
beach. It is now a high pass against a blurred sample of the same texture — surface, not picture,
matching `terrain_splat.gdshader` — but this art still has objects in it, so the default is zero.
Raise it for ground art that is only surface. See `plans/terrain-grid/FIX-17-*.md`.
Removing a binding clears its material on the next rebuild. The lab configures
grass, dry grass, sand, desert, jungle, tundra and snow with existing terrain
textures. This does not change the painted renderer or promise new transition art.

The water material binds `BoundsOrigin` as `map_origin`: texture repeat, seabed
sand, shoreline noise and foam phase use absolute grid coordinates, while coast
texture UVs stay view-local. Cropping no longer restarts the sea pattern. The
GPU `terrain_material_origin_probe.gd` verifies the production water layer under
rotation and three zooms at positive/negative origins. It does not guarantee
crop-edge field continuity without neighbour data.

Live water fields use `TerrainCoastField.LiveCache`; unrelated metadata edits
reuse the field. Water display uses the shared bounded cubic `RenderCache`
described in `TerrainCoastField.md`. This changes neither authored tile art nor
live grid classification and adds no additional shader texture samples.

Renderer: a `[Tool][GlobalClass]` `Node2D` that draws generated terrain as a stack of autotiled, dual-grid `TileMapLayer`s — one per configured biome — plus an optional shader-driven sea surface.

This is the renderer a game with hand-authored 15-piece tileset art uses (as opposed to the painted or isometric renderers). It is a single scene node that internally builds and owns one `TerrainTransitionLayerComponent` (plus its backing `TileMapLayer`) per biome atlas the designer has assigned, in a fixed draw order (sea, then ground biomes, then hills/mountains) taken from the shared `TerrainLayers` stack rather than decided locally. It also lays a shader-textured sea (the same water shader the isometric view uses, switched to top-down projection) over the water tiles so the coastline has shading, waves and foam instead of a flat blue field.

## Public API

- Generated biome displays carry persistent `_terrain_tile_biome_display` metadata. Reconfiguration
  removes marked direct children, including displays restored from a `PackedScene`; it does not
  infer ownership from node names. New displays use unique names rather than taking over an
  authored `GrassTiles` or other matching layer. The marker is reserved for renderer-owned nodes.

- Hidden views defer automatic coastline work and their transition displays defer cell repainting.
  Showing a previously attempted view queues a full refresh; initial visibility does not override
  RefreshOnReady. World-managed bounds/origin stay current while inactive. Explicit Rebuild is
  available for callers intentionally preparing a hidden view.

- Source lifecycle: an explicit missing CellDataPath clears owned biome displays and the shader
  overlay; water must not fall back to the generator while that explicit live source is missing.
  Restoring the source and calling Rebuild reconstructs the owned displays. Source instance identity
  participates in the layer cache signature, so same-path replacement updates child transitions too.
- TerrainGeneratorPath resolves freshly rather than retaining a previously valid node. Clearing
  WaterShaderPath and rebuilding removes the old water overlay while retaining biome water tiles.
  A `LibraryPack` build also clears the `TileWater` layer: a pack brings its own shoreline tiles,
  so the shader sea would draw a second one over them. The isometric autotile view applies the
  same rule (VIEW-04).

- `[Export] Vector2I BoundsOrigin` / `[Export] Vector2I BoundsSize = (48,30)` — the map rectangle (in cells) this renderer draws, forwarded to every child transition layer.
- `[Export] Vector2I AtlasTileSize = (64,64)`, `[Export] int AtlasColumns = 4` (range 1–16), `[Export] int AtlasTileRows = 4` (range 1–16) — the shared 15-piece atlas layout (tile pixel size and sheet grid) applied to every biome atlas.
- `[Export(File)] string BaseAtlasPath` — an optional filled base layer drawn under everything, so a gap between biome layers shows this texture (or the sea, per the class comment) rather than a hole.
- `[Export(File)] string {Grass,GrassDetail,DryGrass,Sand,Desert,DesertDetail,Jungle,Swamp,Tundra,Rock,Gravel,Snow,Ice,Water,WaterDetail}AtlasPath` — one 15-piece PNG/WEBP atlas path per biome (plus optional "detail" overlay atlases for grass/desert/water); leaving one blank omits that biome's layer entirely.
- `[Export] bool RefreshOnReady = true` — when true and not running in the editor, defers a call to `Rebuild()` on `_Ready`.
- `[Export] NodePath TerrainGeneratorPath` — generated recipe source for previews without live cells.
- `[Export] NodePath CellDataPath` — authoritative live `GridCellDataComponent`; when assigned,
  transitions and coastline read this source and observe its cell-change signals.
- `[Export(File,*.gdshader)] string WaterShaderPath` — if set, the shader used to paint the sea over the water tiles; if empty, no shader sea is built and only the water tiles themselves draw (a flat, stylised sea).
- `[Export(Range 1,24,0.5)] float CoastRangeTiles = 5.0`, `[Export(Range 1,16,1)] int CoastDetail = 12` (both `TerrainCoastField`'s defaults) — the range and sub-tile resolution of the coast-distance field this view resolves for itself and feeds to the shader.
- `[Export] float MaxOpacity`, `ShoreOpacity`, `LakeOpacity`, `ClarityTiles` — the transparent-sheet uniforms (`max_opacity`, `shore_opacity`, `lake_opacity`, `clarity_tiles`), passed as a `TerrainSeaSurface.Sheet`. They describe this view's floating water surface, so they stay per view.
- `[Export] TerrainWaterLook? WaterLook` — how the sea *looks*: the thirteen shared dials and the four water texture paths, for every view of this world at once (VIEW-04, 2026-09-16). This view used to export its own copies of all thirteen, defaulted differently from the painted and block views (`FoamStrength 1.0 / DeepTiles 6.0 / ShallowTiles 6.0` against `0.50 / 4.5 / 1.8`), so one map drawn twice grew two seas; those exports are gone. `TerrainWorldComponent.Draw()` pushes the world's look onto this property on every build, so assign it on the world, not here. Unassigned, `TerrainWaterLook.Shared` — the shipped defaults — is used, which is still one sea rather than one per renderer. See [TerrainWaterLook](TerrainWaterLook.md).
- `void Rebuild()` — public entry point. Ensures the biome `TerrainTransitionLayerComponent`s exist (building or reusing them per a computed configuration "signature"), calls `RefreshTransitions()` on each, pushes a warning if zero biome layers are configured, then builds/updates the shader sea via `EnsureWaterSurface()`.
- `Godot.Collections.Dictionary GetPaintDiagnostics()` — a copy of the last build's report: `valid`, plus `reason` when the view did not draw (missing source, no biome atlas, or a `LibraryPack` that fails validation or cannot draw a cell). A failed pack build keeps the terrain it last published on screen, so the report is what says the view is out of date. While this view is active, `TerrainWorldComponent` shows the reason in its status line as `View incomplete: …` and fails world generation with it.
- `override string[] _GetConfigurationWarnings()` — editor warnings: neither source path assigned, or zero configured biome atlases.
- `override void _Ready()` — defers `Rebuild()` when `RefreshOnReady` is true and this is not the editor.

## Dependencies

- Reads `TerrainGeneratorComponent` via `TerrainGeneratorPath` for recipe-only previews. With live
  cells configured, keeps all configured biome layers ready for edits that introduce new kinds.
- Creates and owns one `TerrainTransitionLayerComponent` per configured biome (`CreateLayer`), wiring `TerrainGeneratorPath`, `DisplayLayerPath`, `DetailDisplayLayerPath`, `TransitionTerrainKind`, `AtlasTexturePath`/`DetailAtlasTexturePath`, and forcing `UseTileSetTerrains = false` / `UseCanonical15PieceLayout = true` (hand-authored 15-piece sheets, not Godot `TileSet` terrain sets).
- Reads `TerrainLayers.ZFor(TerrainLayers.Sea)` to place the shader-sea `TileMapLayer`'s z-index; does **not** set z-index on the biome `TileMapLayer`s themselves — each `TerrainTransitionLayerComponent` places its own display layer via `TerrainLayers.ZForKind`/`ZForFloor`, by design (see Notes).
- Calls `TerrainAuthoring.EnsureLayer` / `TerrainAuthoring.Adopt` to create and register child `TileMapLayer` nodes (both the sea layer and each biome's display layer).
- Owns one [`TerrainSeaSurface`](TerrainSeaSurface.md), which resolves the coast field (`TerrainCoastField.LiveCache` / `TerrainCoastField.Build`), builds and adopts the water material, and fills the blank-tile `TileMapLayer` through `TerrainShaderSurface.BuildTileSet`/`Fill`. `EnsureWaterSurface` is now the three calls that remain here: resolve the coast, `TileBatched("TileWater", isometric: false)` at `BoundsOrigin * AtlasTileSize`, and `BuildMaterial(..., flatProjection: true, tileBatch: true)`.
- Water textures are loaded by `TerrainWaterMaterial.ApplyTextures` from the look's paths, through `TerrainTextures.Load` (not a bare `GD.Load`), so every view resolves them the same way.
- Reads live `GridCellDataComponent` for coastline generation and forwards it to transitions.
  Rendering does not mutate gameplay cells or run terrain generation stages.

## Notes

- The class comment on `BiomeLayer` and the `Configure()`/`CreateLayer()` method comments explicitly document a past bug: this renderer used to also record which vertical level (ground/hills/mountains) each biome belonged to, duplicating `TerrainLayers.LevelForKind`, and that duplication is why gravel/rock were once classified in two disagreeing places. The fix (removing the second copy, deferring entirely to `TerrainTransitionLayerComponent`'s own z-placement) is present in the current code — this is documentation of a resolved defect, not a live one.
- Similarly, `ConfiguredLayers()`'s doc comment records a second past bug: water used to draw *last* (on top), so its 15-piece transition tiles resolved the coastline by drawing over the beach sand layer underneath — "436 tiles of it, drawn and then buried." The current declaration order (sea first, then ground biomes ending with `sand`, then hills/mountains) is the fix; worth flagging because reordering this list without re-reading the comment could reintroduce the same bug.
- `EnsureLayers()`'s reuse/rebuild decision is driven entirely by a string `Signature()` (atlas paths + tile size/columns/rows) compared against `_builtSignature`; a signature match short-circuits into `Configure()`, which only re-applies `BoundsOrigin`/`BoundsSize` and explicitly does *not* re-apply atlas paths or z-index — both are deliberately load-once, documented inline as intentional rather than an oversight.
- `RefreshOnReady` combined with `Engine.IsEditorHint()` means the renderer does not auto-rebuild in the editor on scene load — only `RefreshInEditor`-style behavior lives on the child `TerrainTransitionLayerComponent`, not here; a designer relying on this renderer alone to preview in-editor must call `Rebuild()` manually or via the dock.
