# TerrainPaintedRendererComponent

Renderer: a `Node2D` component that is one of four alternative ground-projection renderers (painted / tile / isometric / isometric-tile) a scene picks between via `TerrainProjection`.

`TerrainPaintedRendererComponent` renders the generated terrain as one continuous shader-blended surface — a single large material texture sampled in world space, per Factorio's ground-rendering approach — rather than one sprite/tile per cell. Because a fragment shader cannot read a `TileMapLayer` directly, it uploads the terrain grid as two textures (a terrain-id + hillshade map, and a signed coast-distance map) that the `terrain_splat.gdshader` material samples per pixel to blend materials at runtime, add sand at the shoreline, and animate foam/surf. It draws onto a single-cell `TileMapLayer` (via `TerrainShaderSurface`) so the result is still a real, scene-saved tile layer with collision/navigation rather than a stretched quad, while the actual gameplay grid (features, objects, collision data) remains owned elsewhere.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads.
- `[Export] Vector2I BoundsSize = (96, 60)` — map dimensions in tiles rendered.
- `[Export(Range 1,256,1)] int TileSize = 64` — pixel size of one tile.
- `[Export(Range 1,32,0.5)] float TextureTiles = 6.0f` — how many tile-widths one repeat of a material texture covers.
- `[Export(Range 0,0.9,0.01)] float BlendWidth = 0.42f` — width of the blend band between adjacent materials, passed to the shader.
- `[Export(Range 0,1,0.01)] float EdgeNoise = 0.55f` — how much noise perturbs blend edges (breaks up straight seams).
- `[Export(Range 0.5,24,0.5)] float NoiseScale = 5.0f` — frequency of that edge noise.
- `[Export(Range 0,2,0.05)] float ShadeStrength = 1.0f` — hillshade contrast multiplier.
- `[Export(Range 1,16,0.5)] float CoastRangeTiles = 5.0f` — how many tiles of coast distance the shader can see (clamp range of the distance field).
- `[Export(Range 1,8,1)] int CoastDetail = 4` — sub-tile resolution of the coast distance field; at 1 the field is one value per tile (square contours), higher values give smoother/curved shoreline contours for surf.
- `[Export(File *.png,*.webp)] string FoamSheetPath = ""` — authored foam strip (equal frames, sampled by distance from waterline); empty falls back to procedurally generated crests.
- `[Export(Range 0,2,0.05)] float WaveIntensity = 1.0f` — single "sea state" dial (0 calm, 1 normal, 2 storm) that jointly scales surf reach, crest width, and wash-up distance.
- `[Export(Range 1,48,0.5)] float FoamTilesAlong = 11.0f` — tiles covered by one repeat of the foam texture along the shore.
- `[Export(Range 0.3,8,0.1)] float FoamTilesAcross = 7.0f` — tiles covered by one repeat across the shore.
- `[Export(Range 0,4,0.01)] float FoamScroll = 0.055f` — speed authored crests advance onto the beach.
- `[Export(Range 0,1,0.05)] float FoamPulse = 0.34f` — how strongly surf pulses as crests arrive (0 = steady band).
- `[Export(Range 0,4,0.05)] float FoamArrivalRate = 0.9f` — how fast crests follow one another.
- `[Export(File *.png,*.webp)] string GrassTexturePath`, `DryGrassTexturePath`, `SandTexturePath`, `DirtTexturePath`, `SnowTexturePath`, `MudTexturePath`, `GravelTexturePath`, `RockTexturePath`, `ShallowWaterTexturePath`, `DeepWaterTexturePath` — material texture sources for the ten shader material slots; empty paths leave that shader parameter unset.
- `[Export] bool RefreshOnReady = true` — when true and not running in the editor, `_Ready()` defers a call to `Rebuild()`; set false when an external controller drives generation first and calls `Rebuild()` itself, to avoid building twice.
- `void Rebuild()` — the main entry point. Resolves the generator, builds the id/shade/coast maps, ensures the backing `TileMapLayer` and `ShaderMaterial` exist, and pushes every export above onto the shader as parameters (`id_map`, `shade_map`, `coast_map`, `coast_range`, `beach_tiles` from `_generator.BeachWidth`, `map_size`, `cell_size`, `texture_tiles`, `blend_width`, `edge_noise`, `noise_scale`, `shade_strength`, foam parameters). Pushes a warning and returns early if no generator is resolved.
- `override string[] _GetConfigurationWarnings()` — warns when `TerrainGeneratorPath` is empty.
- `override void _Ready()` — calls `CallDeferred(nameof(Rebuild))` if `RefreshOnReady` and not in the editor.

Private helpers worth noting for behaviour: `BuildIdMap` writes one texel per tile (red = terrain-id from a fixed `TerrainIds` dictionary contract with `terrain_splat.gdshader`, green = hillshade halved to fit 0..1) by calling `_generator.TerrainKindAt(cell)` and `_generator.ShadeAtCell(cell)` per cell; `BuildCoastMap` delegates to `TerrainCoastField.Build`; `EnsureSurface` creates/reuses a `TileMapLayer` named `"SplatSurface"` via `TerrainAuthoring.EnsureLayer`, assigns it a tileset sized to `TileSize` via `TerrainShaderSurface.BuildTileSet`, fills it via `TerrainShaderSurface.Fill`, and sets its `ZIndex` to `TerrainLayers.ZForFloor()`.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAt(Vector2I)`, `.ShadeAtCell(Vector2I)`, and `.BeachWidth` (property).
- Calls `TerrainCoastField.Build(generator, size, CoastDetail, CoastRangeTiles)` to build the coast distance texture.
- Calls `TerrainAuthoring.EnsureLayer(this, "SplatSurface")` to get/create the backing `TileMapLayer`.
- Calls `TerrainShaderSurface.BuildTileSet(cell, isometric: false)` and `TerrainShaderSurface.Fill(surface, size)` to set up and populate that layer.
- Reads `TerrainLayers.ZForFloor()` for draw order.
- Calls `TerrainTextures.Load(path, Name, description)` for every optional texture path (foam sheet + ten material textures).
- Loads `res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader` directly (not another C# file in this directory, but the shader this class is the sole owner/uploader for).
- Writes nothing back into the generator; all data flow is generator → this renderer → shader material.

## Notes

- The `TerrainIds` dictionary is an explicit, code-commented contract with `terrain_splat.gdshader`'s material indices; both `"swamp"` and `"mud"` map to id `8`, so the shader cannot visually distinguish those two terrain kinds — deliberate collapsing to one material slot, not a bug, but worth knowing if a swamp/mud visual split is ever wanted.
- A code comment on the `beach_tiles` shader parameter explicitly documents a known duplication defect: this renderer composites its own sand/beach band from the coast distance field using `_generator.BeachWidth`, while the tile and isometric renderers instead draw whatever sand *biome* the beach stage already assigned per-cell. Two independent sources for "how wide is the beach," and the comment records a real incident where they drifted (`BeachWidth = 0.028` produced no beach in the generator/tile/isometric views but this shader kept its own hardcoded default). This is exactly the class of defect flagged by the project's duplication rule — one fact, two owners — and is still present in the code as of this read, only worked around by this renderer now reading `BeachWidth` at least for its own contribution.
- No z-index export by design — a comment explains this is deliberate so `TerrainLayers` remains the single owner of draw order, citing a past bug where the feature renderer drew trees underneath the map because it had its own z dial.
- `FoamSheetPath` gets an extra, renderer-specific warning on top of the one `TerrainTextures.Load` already pushes: if the path is non-empty but fails to load, `Rebuild` additionally pushes "falling back to generated crests" and sets `use_foam_sheet = false` on the shader. The ten material textures rely solely on `TerrainTextures.Load`'s own warning (`Assign` just returns early on a null result) — both paths are reported, just at different granularity.
- `TerrainTextures.Load`'s own doc comment records a past duplication defect worth knowing when touching this file: texture loading (res:// vs. absolute-path handling, mipmap generation) used to be reimplemented per renderer, and one of the four copies (the tile renderer's water) was wrong, so that view alone drew unmipped/unimported art. This renderer is one of the three copies that were consolidated onto the shared helper; it is not exhibiting the defect itself.
