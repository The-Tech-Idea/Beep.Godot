# TerrainPaintedRendererComponent

## Background World Preparation

`TerrainWorldComponent.BeginNewWorld` stages live visual samples before invoking
this renderer's normal draw path. `IsPreparingSnapshot` and `PreparedSnapshotCells`
expose this state. The world owns stepping at `PublicationCellsPerFrame`; scenes
do not need a second scheduler. Deferred cell-change rebuilds are suppressed while
the snapshot is pending. Explicit `Rebuild` cancels pending preparation and remains
synchronous. Scene removal releases the pending snapshot.

Preparation copies the same terrain/elevation/shore/water samples used by Rebuild,
checks source revisions and availability, and swaps only the complete snapshot.
The previous textures remain untouched while copying. New-world preparation
requires available terrain; ordinary archived-world redraws continue to use the
existing retained snapshot. This is not yet budgeted coast computation, texture
upload or memory-bounded world storage.

After sampling, `PreparationStage` reports `Computing painted coast`. A detached
`TerrainPaintedCoastJob` calculates coast and lake pixel buffers without reading
nodes or creating GPU resources. The main thread validates the source and settings
again before uploading. Source edits, changed CoastDetail/CoastRangeTiles, explicit
Rebuild or removal cancel/discard the pending job. Synchronous Rebuild shares the
same coast algorithm. Contour reconstruction and texture upload are still synchronous.

Water display uses the shared cached cubic reconstruction documented in
`TerrainCoastField.md`. `CoastDetail` controls raw samples per cell; the bound
display texture can be twice that resolution within its allocation cap. Raw
terrain IDs, shading and live water patches remain unchanged.

Renderer: a `Node2D` component that is one of four alternative ground-projection renderers (painted / tile / isometric / isometric-tile) a scene picks between via `TerrainProjection`.

`TerrainPaintedRendererComponent` renders live grid cells or a generated field as
one continuous shader-blended surface. Material textures repeat in absolute grid
coordinates rather than restarting on every tile or cropped view. The shader reads separate
terrain-ID, hillshade and signed coast-distance textures. `TerrainShaderSurface`
populates a native `TileMapLayer` with shader tiles; this visual layer does not
replace the live grid's state, navigation or terrain collision components.

## Public API

### Continuous Material Boundaries

Original, Pixel Art and Cartoon share one boundary algorithm in
`terrain_splat.gdshader`. A bounded 5x5 neighbourhood accumulates radial coverage
by terrain ID before sharpening. Sharpening individual cell contributions caused
the old square inland patches. Broad contour reconstruction is separate from a
narrow colour transition, so rounding does not require blurring ground textures.
All land material IDs use this path, not only grass and sand. Zero `BlendWidth`
retains explicit hard cell geometry. Edge variation is low-frequency and bounded.

Lake bank widths and fine lake membership travel with live grid data. The painter
uses these separately from ocean width, retaining the underlying biome instead
of blending a coarse lake-sand cell mask. Native tile/isometric atlases still use
their own authored transition art; this shader does not manufacture atlas pieces.

`terrain_painted_blend_probe.gd` checks coverage, sharpness, texture-detail
preservation at three zoom levels and rounded inland corners in all three styles.
The large lab probe captures the same seed at overview and close-up scale.

### Material bindings

Assign material texture paths for every terrain the scene can display. Snow and ice
share `SnowTexturePath`; leaving it empty on an unauthored material leaves the shader
sampler unbound and renders white. The shipped `textures/terrain/snow_ice.png` provides
textured snow/ice. Keep mipmaps enabled in its import settings for zoomed-out views.
The addon's authored terrain lab binds the material slots. Use its clean visual
probe to inspect the actual materials at overview and close range.

The authored painted demos now bind `meadow_ground.png` and
`dry_meadow_ground.png` for grass/dry grass, retaining the original texture files.
These use a quieter base and small grass strokes instead of broad tangled mottling.
See [Meadow Materials](MEADOW_MATERIALS.md) for provenance, exact prompts, import
settings, measured repeat edges and fixed-seed comparisons. Other projections
still use their own atlases; this is not a completed cross-view art replacement.

`LavaTexturePath` supplies a distinct lava material (shader ID 13), rather than
aliasing rock (10). Water IDs remain exactly 11 and 12. The painted demos bind
`lava_ground.png`, a static opaque basalt/fissure albedo with mipmaps; this adds
no lava-flow simulation or emission pass. See [Lava Material](LAVA_MATERIAL.md).
Live lava remains blocked for navigation and placement. Changing it to rock
updates those rules without changing the coast map.

### Texture scale

`MaterialTiling` optionally supplies a native `TerrainMaterialTiling` resource
with per-texture repeat sizes and narrow repeat-edge correction. Null/zero slots
inherit the common scale. The painted demos now use bedrock at six tiles per
repeat, with unchanged scale for other materials. See
[Material Tiling](TerrainMaterialTiling.md) and [Bedrock Material](BEDROCK_MATERIAL.md).

`GroundTextureTiles` (default 12) sets tiles per repeat of land materials and
beach/submerged sand unless overridden by MaterialTiling. Larger values display larger material detail;
they do not change map size or sharpen an image filter. `WaterTextureTiles`
(default 6) independently controls animated shallow/deep water texture sampling.
The obsolete combined `TextureTiles` property is removed, with no compatibility
alias. The lab and standalone painted demo use the new properties.

Mipmaps and explicit material UV gradients remain enabled. The shared water
shader samples stationary seabed sand at the ground scale, keeping it aligned
with beach sand while the water surface uses its own scale. Isometric water
exposes the same controls; they do not resize isometric atlas blocks.

`terrain_material_scale_probe.gd` verifies independent ground/deep-water pixels
at 0.5x, 1x and 2x zoom, full opacity and unchanged ID/shade/coast bytes. The clean
lab visual probe accepts `--material-tuning` for repeat-size/tint comparisons on
the same generated map; these diagnostic variants are not biome-generation tests.

### View lifecycle

The painted view retains one live coast texture and its exact water mask. Rebuilds
reuse that texture when water membership, dimensions, detail and distance range are
unchanged. Dry terrain, elevation and metadata edits therefore avoid distance-field
generation and coast upload. Flooding/draining and changed source contents still
rebuild it. There is no shared global cache or dependency on receiving every signal:
each lookup compares the current water mask. IDs and lighting still update normally.

Live-cell lighting is derived from current `terrain_elevation` values and their
neighbour gradients using the shared terrain shading formula. It is cell-resolution
lighting, not the generator's sub-cell shade field. Water uses neutral lighting and
zero elevation for neighbouring slope calculations, even if old elevation metadata
remains after flooding. Map-edge samples clamp to the view bounds.

Each repaint samples live terrain/elevation into a local buffer once, then reads
neighbour heights from that buffer. Terrain-ID and shade pixels are encoded as RGBA8
arrays and uploaded in bulk rather than written through a native call per pixel.
The renderer still scans the full view; this is not a dirty-region implementation.

Stored `terrain_shade` is not read for live lighting: it can be stale after leveling
or other terrain edits. Elevation and terrain-kind changes trigger the existing
coalesced repaint. `ShadeStrength = 0` disables the visible shading. Without a live
cell source, the renderer still samples the generated field's detailed shade.

Automatic live-cell rebuilds pause while hidden. Showing a previously built view refreshes it;
explicit `Rebuild()` remains available for preparing hidden views. Removal clears subscriptions
and pending work, and reattachment restores subscriptions. Initial visibility does not force
generation when `RefreshOnReady` is false. The world controller updates inactive painted bounds.

A missing explicit live source clears the existing surface instead of leaving an obsolete map
visible or falling back to generation. Restoring the path and rebuilding restores the surface.

Each renderer duplicates an adopted shader material before writing map uniforms. Authored uniforms
are preserved, but separate worlds/previews cannot overwrite each other's ID, shade or coast maps.
Immutable shader/texture resources may still be shared.

### Logical bounds and live cells

Ground materials, material-boundary noise, water texture phase, seabed sand and
shoreline noise are anchored to `BoundsOrigin + localTile`. Data-map UVs remain
local to the view. All three shader-backed views bind the shared `map_origin`
uniform; moving or scaling their scene parent does not resize the pattern in grid
units. No extra world object or render-time write to live cells is involved.

`terrain_material_origin_probe.gd` compares full and cropped production views at
positive/negative origins and three zooms, including rotation and nonuniform
painted scale. It covers grass, material transitions, coastal sand/water and open
water, plus native tiled/isometric water. Comparisons exclude outer crop edges:
coast fields and live hillshade still use bounded data, so this does not establish
seamless streaming chunks without neighbour data/halos. Live cells are unchanged.

`BoundsOrigin` is the absolute logical cell at the map's top-left; `BoundsSize` is its extent.
With `CellDataPath` set, terrain IDs and coast distance read live cells at `BoundsOrigin + localCell`.
Without live cells, generated fields are sampled in their local coordinates. Changes outside the
rendered bounds do not queue a repaint.

`GetTerrainLayer()` returns the empty native `LogicalGrid` used for gameplay coordinate conversion.
It does not duplicate terrain tiles. `SplatSurface` holds the actual shader tiles at local zero and
is positioned at `BoundsOrigin * TileSize`, keeping all shader cells in one rendering quadrant.
Bind `GridProjectionComponent` through `GetTerrainLayer()`, not directly to `SplatSurface`.
The world controller supplies the origin for both painted and tiled views; flat camera extents and
start positions include it. This does not establish origin support in every other renderer.

The shader uploads separate ID, shade and coast textures. Live cells take precedence over generation;
generated beach expansion is disabled for live maps so it cannot paint over explicit edits.

### Settings

`ShadeStrength` defaults to 0.35, including the authored lab and painted demo.
The hillshade map can span roughly 0.7 to 1.3; applying that range at full strength
over a flat surface made broad bright/dark patches dominate the material detail.
The lower presentation gain retains relief without changing generated or live
elevations, shade data, navigation or placement. Set it to 1 for full strength,
or 0 to inspect the original textures without hillshade.

Shader colour grading defaults to neutral saturation/contrast (1/1). There is no
global desaturation of authored materials or the shared sea by default.

Use the visual probe with `-- --diagnose` for unshaded/flat-material captures and
material-ID counts, or `-- --materials` to compare grass/desert/mud/snow on the
same land footprint. These material swaps are diagnostic, not generated biomes.

`BlendWidth` controls the reach of neighbouring land materials; `BlendSharpness`
(1 to 8, default 4) controls how concentrated their transition is. Higher sharpness
retains more of each material instead of mixing a broad colour band. It does not
sharpen the source artwork, move the waterline, or alter gameplay cells.

`MaterialEdgeDetail` (0 to 1, default 0.75) makes transitions follow the sampled
material's brightness detail. The shader multiplies each geometric blend weight
by a positive texture-dependent factor before normalizing. Bright flecks extend
into the transition and darker gaps admit the adjacent material. Identical
material contributions cancel during normalization, so this does not tint or
sharpen terrain interiors. It reuses the existing material sample, without an
extra texture fetch or generated noise overlay. Set it to 0 for purely geometric
blending. This is an albedo-based visual heuristic, not an authored height map:
brighter materials can advance slightly into darker neighbours at their border.

The GPU blend probe checks bright/dark boundary response, unchanged interiors,
opacity and unchanged ID/shade/coast bytes at 0.5x, 1x and 2x zoom. Run the clean
lab capture with `-- --edge-detail` for fixed-time comparisons with the setting
off/on at overview, normal and close zoom. Outputs are `edge-*-zoom-*.png` in
`tests/output/painter_visual`. The effect is subtle with the shipped low-contrast
grass and does not fix its olive palette, large mottling or art-style mismatch.

The shader uses separable square coverage rather than circular kernels, so even
zero-width blends cover tile corners. Noise shifts the lookup neighbourhood as well
as its weights, keeping transitions continuous when the displacement crosses a cell.
At water margins, a missing land weight uses the nearest sampled land material,
not an unconditional grass fallback. Material samples use explicit UV gradients
for mip selection inside terrain-ID branches; mipmaps remain enabled.

Reference: Godot documents `textureGrad` as a texture lookup with explicit
gradients in its [shading language reference](https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/shading_language.html).
Implicit derivatives inside non-uniform control flow are undefined in the
[GLSL specification](https://registry.khronos.org/OpenGL/specs/gl/GLSLangSpec.1.40.pdf).

`tests/terrain_painted_blend_probe.gd` verifies these settings on the real painted
TileMapLayer using GPU pixel readback, live cell edits and unchanged ID/coast data.

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads.
- `[Export] Vector2I BoundsSize = (96, 60)` — map dimensions in tiles rendered.
- `[Export(Range 1,256,1)] int TileSize = 64` — pixel size of one tile.
- `[Export(Range 1,32,0.5)] float GroundTextureTiles = 12.0f` - tiles per land/seabed texture repeat.
- `[Export(Range 1,32,0.5)] float WaterTextureTiles = 6.0f` - tiles per animated water texture repeat.
- `[Export(Range 0,0.9,0.01)] float BlendWidth = 0.42f` — width of the blend band between adjacent materials, passed to the shader.
- `[Export(Range 1,8,0.25)] float BlendSharpness = 4.0f` - concentration of the geometric transition.
- `[Export(Range 0,1,0.05)] float MaterialEdgeDetail = 0.75f` - texture-brightness influence on material transition weights.
- `[Export(Range 0,1,0.01)] float EdgeNoise = 0.55f` — how much noise perturbs blend edges (breaks up straight seams).
- `[Export(Range 0.5,24,0.5)] float NoiseScale = 5.0f` — frequency of that edge noise.
- `[Export(Range 0,2,0.05)] float ShadeStrength = 0.35f` - hillshade contrast multiplier.
- `[Export(Range 1,16,0.5)] float CoastRangeTiles = 5.0f` — how many tiles of coast distance the shader can see (clamp range of the distance field).
- `[Export(Range 1,8,1)] int CoastDetail = 4` — sub-tile resolution of the coast distance field; at 1 the field is one value per tile (square contours), higher values give smoother/curved shoreline contours for surf.
- `[Export(File *.png,*.webp)] string FoamSheetPath = ""` — authored foam strip (equal frames, sampled by distance from waterline); empty falls back to procedurally generated crests.
- `[Export(Range 0,2,0.05)] float WaveIntensity = 1.0f` — single "sea state" dial (0 calm, 1 normal, 2 storm) that jointly scales surf reach, crest width, and wash-up distance.
- `[Export(Range 1,48,0.5)] float FoamTilesAlong = 11.0f` — tiles covered by one repeat of the foam texture along the shore.
- `[Export(Range 0.3,8,0.1)] float FoamTilesAcross = 7.0f` — tiles covered by one repeat across the shore.
- `[Export(Range 0,4,0.01)] float FoamScroll = 0.055f` — speed authored crests advance onto the beach.
- `[Export(Range 0,1,0.05)] float FoamPulse = 0.34f` — how strongly surf pulses as crests arrive (0 = steady band).
- `[Export(Range 0,4,0.05)] float FoamArrivalRate = 0.9f` — how fast crests follow one another.
- `[Export(File *.png,*.webp)] string GrassTexturePath`, `DryGrassTexturePath`, `SandTexturePath`, `DirtTexturePath`, `SnowTexturePath`, `MudTexturePath`, `GravelTexturePath`, `RockTexturePath`, `LavaTexturePath`, `ShallowWaterTexturePath`, `DeepWaterTexturePath` - material texture sources for the eleven shader material slots; empty paths leave that shader parameter unchanged/unset.
- `[Export] bool RefreshOnReady = true` — when true and not running in the editor, `_Ready()` defers a call to `Rebuild()`; set false when an external controller drives generation first and calls `Rebuild()` itself, to avoid building twice.
- `void Rebuild()` - resolves the configured live-cell or generator source, uploads ID/shade/coast maps, ensures the native layer/material, and binds map, ground/water texture scale, blend, shading and surf settings. Live-cell beaches are explicit terrain; generated beaches use the generator's width. A missing explicit source clears the surface and reports a warning.
- `override string[] _GetConfigurationWarnings()` — warns when `TerrainGeneratorPath` is empty.
- `override void _Ready()` — calls `CallDeferred(nameof(Rebuild))` if `RefreshOnReady` and not in the editor.

Private helpers worth noting for behaviour: `BuildIdMap` writes one texel per tile (red = terrain-id from a fixed `TerrainIds` dictionary contract with `terrain_splat.gdshader`, green = hillshade halved to fit 0..1) by calling `_generator.TerrainKindAt(cell)` and `_generator.ShadeAtCell(cell)` per cell; `BuildCoastMap` delegates to `TerrainCoastField.Build`; `EnsureSurface` creates/reuses a `TileMapLayer` named `"SplatSurface"` via `TerrainAuthoring.EnsureLayer`, assigns it a tileset sized to `TileSize` via `TerrainShaderSurface.BuildTileSet`, fills it via `TerrainShaderSurface.Fill`, and sets its `ZIndex` to `TerrainLayers.ZForFloor()`.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAt(Vector2I)`, `.ShadeAtCell(Vector2I)`, and `.BeachWidth` (property).
- Calls `TerrainCoastField.Build(generator, size, CoastDetail, CoastRangeTiles)` to build the coast distance texture.
- Calls `TerrainAuthoring.EnsureLayer(this, "SplatSurface")` to get/create the backing `TileMapLayer`.
- Calls `TerrainShaderSurface.BuildTileSet(cell, isometric: false)` and `TerrainShaderSurface.Fill(surface, size)` to set up and populate that layer.
- Reads `TerrainLayers.ZForFloor()` for draw order.
- Calls `TerrainTextures.Load(path, Name, description)` for every optional texture path (foam sheet + eleven material textures).
- Loads `res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader` directly (not another C# file in this directory, but the shader this class is the sole owner/uploader for).
- Writes nothing back into the generator; all data flow is generator → this renderer → shader material.

## Notes

- Repeated refreshes reuse ID, shade and coast textures when the live store's
  terrain revision, default kind, source identity, bounds and coast settings are
  unchanged. Generator-only views key reuse by the resolved field identity.
  Existing-cell flags/crops/feature metadata do not re-upload ground data;
  terrain/elevation/patch edits and record loads invalidate it. Shader look
  parameters and actual tile geometry are still reconciled on every `Rebuild`.
- `tests/terrain_painter_visual_probe.gd --source-comparison` (pass this option
  after Godot's `--`) captures the same generated world through live-cell and
  generator-only bindings, at overview and close zoom. Wave time and extra
  beach compositing are held equal. Captures use `source-live-*` and
  `source-generated-*` under `tests/output/painter_visual`.
- The live grid now retains generated sub-tile water patches in its saved cell
  records. The seed-31415 comparison fell from 643 to 94 differing coast signs
  out of 16384 samples after this handoff. Central half-cell constraints deliberately
  reconcile raw fine water with cell-level movement targets. Explicit painting
  replaces the edited cell's patch; ordinary flag/crop edits preserve it.
  Keep `CellDataPath` bound so the renderer reflects those edits and restored masks.
- The `TerrainIds` dictionary is an explicit, code-commented contract with `terrain_splat.gdshader`'s material indices; both `"swamp"` and `"mud"` map to id `8`, so the shader cannot visually distinguish those two terrain kinds — deliberate collapsing to one material slot, not a bug, but worth knowing if a swamp/mud visual split is ever wanted.
- A code comment on the `beach_tiles` shader parameter explicitly documents a known duplication defect: this renderer composites its own sand/beach band from the coast distance field using `_generator.BeachWidth`, while the tile and isometric renderers instead draw whatever sand *biome* the beach stage already assigned per-cell. Two independent sources for "how wide is the beach," and the comment records a real incident where they drifted (`BeachWidth = 0.028` produced no beach in the generator/tile/isometric views but this shader kept its own hardcoded default). This is exactly the class of defect flagged by the project's duplication rule — one fact, two owners — and is still present in the code as of this read, only worked around by this renderer now reading `BeachWidth` at least for its own contribution.
- No z-index export by design — a comment explains this is deliberate so `TerrainLayers` remains the single owner of draw order, citing a past bug where the feature renderer drew trees underneath the map because it had its own z dial.
- `FoamSheetPath` gets an extra, renderer-specific warning on top of the one `TerrainTextures.Load` already pushes: if the path is non-empty but fails to load, `Rebuild` additionally pushes "falling back to generated crests" and sets `use_foam_sheet = false` on the shader. The ten material textures rely solely on `TerrainTextures.Load`'s own warning (`Assign` just returns early on a null result) — both paths are reported, just at different granularity.
- `TerrainTextures.Load`'s own doc comment records a past duplication defect worth knowing when touching this file: texture loading (res:// vs. absolute-path handling, mipmap generation) used to be reimplemented per renderer, and one of the four copies (the tile renderer's water) was wrong, so that view alone drew unmipped/unimported art. This renderer is one of the three copies that were consolidated onto the shared helper; it is not exhibiting the defect itself.
