# TerrainPaintedRendererComponent

## How much material the ground shows, and why it is not flat colour

Two separate dials decide this, and they are easy to confuse because both sound like "ground detail":

- **`TerrainMapArt.GroundGrainStrength`** (shader `ground_detail_strength`) — a brightness grain taken
  from the material texture at about one texel a pixel. **Off by default**, and it stays off: it
  multiplies by whatever the artist drew at that size, so objects painted into ground art — the
  cartoon atlas had a 22-texel starfish and 12-to-18-texel shells — came through on every tile as
  bush-sized marks. Reported three times by the owner on 2026-09-18.
- **`TerrainMapArt.GroundDetail`** (shader `art_ground_detail`) — how much of the material texture
  survives against a flat per-terrain style colour. A different thing entirely, and still on.

`GroundDetail` applies **only to the styled profiles**: `material()` returns the sampled texture
unchanged when `art_style == 0`, so the Original look always draws its full material and this dial
does nothing there. Cartoon and Low Poly sit at 0.16 (84% flat colour), Pixel Art at 0.24 before
quantising to 24 levels.

**Asked on 2026-09-18 whether to take it to zero — "just make grass plain green" — the answer is no**,
and the reason is what the renderer is for. Its whole design, after Factorio's FFF-214, is a seamless
material sampled in world space so that every grass tile is not the same pixels; at zero, every grass
pixel is literally one colour and the tile grid is the only structure left. Published RTS practice
agrees on the balance rather than the extreme: at an overhead camera's distance the risk is visible
tiling and wasted fine detail, not the presence of material, so the guidance is a subtle texture at
the right scale. 0.16 already is that — the styled profiles read as near-flat colour with a hint of
surface.

It would also not have addressed what was reported. The artifacts were in Original as well ("its in
all renders except isometric"), which this dial cannot touch. They were fixed at source instead: the
grain pass off by default, and the objects removed from the art itself.

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

### The ground's grain

A ground texture is authored at the pattern scale it has to sit at beside the vehicles
and buildings standing on it, and at that scale its source is minified about three times
— the meadow's 1254 pixels over six tiles, the cartoon atlas's 310-pixel regions over
1.6 — so mipmapping averages its grain away and the ground reads as a flat wash. The
owner reported exactly that on 2026-09-18: "original and cartoon renders is showing a
blury terrain".

`GroundDetailTiles` and `GroundDetailStrength` sample the same material texture a second
time, near one texel a pixel, and apply it as **brightness only, as a high pass** against
a blurred sample of that same texture — what is left is finer than four texels, which is
surface rather than picture. The pattern scale does not move, so nothing grows against the
vehicles. Centring the grain on mid-grey instead darkens every material brighter than it —
sand lost a fifth of its brightness, which the blend probe caught as lost coverage; and
measuring against the material's single average colour, as this first shipped, makes the
difference carry every scale the artist painted.

**The strength defaults to zero — the grain is off.** At one texel a pixel it carries
whatever the art holds at that size, and the shipped cartoon atlas's sand holds a 22-texel
starfish and 12-to-18-texel shells; they appeared on the beach as object-sized marks and
the owner reported them three times on 2026-09-18. Even as a high pass their outlines
survived. Raise `GroundDetailStrength`, or a profile's `GroundGrainStrength`, for ground
art that is only surface. A `TerrainMapArt` profile carries its own pair
(`GroundGrainTiles`, `GroundGrainStrength`) and overwrites both, because the repeat depends
on the resolution of the art being sampled. `terrain_painted_blend_probe.gd` guards it: one material,
a fine checker squeezed into one tile so the base sample is flat, must gain variation
(0.000 → 0.126) without its mean moving (0.498 either way).

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

Both are dials on [`TerrainWaterLook`](TerrainWaterLook.md) since VIEW-04 (2026-09-16), not
exports on this renderer: this view and the two tile/block views each carried their own copy,
and the tile view's differed. The defaults quoted above are the look's, which are what this view
already drew.

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
- `[Export] TerrainWaterLook? WaterLook` — how the sea looks: the thirteen shared dials (including `GroundTextureTiles` and `WaterTextureTiles`, which this composite reads for its land and water repeats) and the four water texture paths, for every view of this world at once (VIEW-04, 2026-09-16). This view exposed thirteen of them, the tile view the same thirteen with three different defaults, and the block view a third copy. `TerrainWorldComponent.Draw()` pushes the world's look here on every build, so assign it on the world; unassigned, `TerrainWaterLook.Shared` (the shipped defaults, which are the values this view carried) is used. A private `Water` property reads whichever applies. See [TerrainWaterLook](TerrainWaterLook.md).
- `[Export(Range 0,0.9,0.01)] float BlendWidth = 0.42f` — width of the blend band between adjacent materials, passed to the shader.
- `[Export(Range 1,8,0.25)] float BlendSharpness = 4.0f` - concentration of the geometric transition.
- `[Export(Range 0,1,0.05)] float MaterialEdgeDetail = 0.75f` - texture-brightness influence on material transition weights.
- `[Export(Range 0,1,0.01)] float EdgeNoise = 0.55f` — how much noise perturbs blend edges (breaks up straight seams).
- `[Export(Range 0.5,24,0.5)] float NoiseScale = 5.0f` — frequency of that edge noise.
- `[Export(Range 0,2,0.05)] float ShadeStrength = 0.35f` - hillshade contrast multiplier.
- `[Export(Range 5,16,0.5)] float CoastRangeTiles = TerrainCoastField.DefaultRangeTiles (5.0)` — how many tiles of coast distance the shader can see (clamp range of the distance field). `Rebuild` floors it at 5 when writing `coast_range`, because the uniform must agree with the range the field was actually built with.
- `[Export(Range 1,16,1)] int CoastDetail = 12` — sub-tile resolution of the coast distance field; at 1 the field is one value per tile (square contours), higher values give smoother/curved shoreline contours for surf.
- The surf and swell dials (`WaveIntensity`, `FoamStrength`, `ShallowTiles`, `DeepTiles`, `FoamTilesAlong`, `FoamTilesAcross`, `FoamScroll`, `FoamPulse`, `FoamArrivalRate`, `SwellDirectionDegrees`, `SwellDirectionality`) and `FoamSheetPath` moved to `WaterLook` above; they are no longer exports here. This view binds the look's foam sheet through `TerrainWaterMaterial.BindFoamSheet` and the rest through `TerrainWaterMaterial.Apply`. It does **not** read the look's three water-bed textures: it composites its own seabed from the LAND materials below.
- `[Export(File *.png,*.webp)] string GrassTexturePath`, `DryGrassTexturePath`, `SandTexturePath`, `DirtTexturePath`, `SnowTexturePath`, `MudTexturePath`, `GravelTexturePath`, `RockTexturePath`, `LavaTexturePath`, `ShallowWaterTexturePath`, `DeepWaterTexturePath` - material texture sources for the eleven shader material slots; empty paths leave that shader parameter unchanged/unset. These are this view's own LAND materials and stay here — its `SandTexturePath` is the beach a unit walks on (`textures/terrain/sand.png`, authored in four shipped scenes), not the seabed the look's `SeabedSandTexturePath` names. Both bind the same shader uniform from different views, which is why the look's is spelled differently.
- `[Export] bool RefreshOnReady = true` — when true and not running in the editor, `_Ready()` defers a call to `Rebuild()`; set false when an external controller drives generation first and calls `Rebuild()` itself, to avoid building twice.
- `void Rebuild()` - resolves the configured live-cell or generator source, uploads ID/shade/coast maps, ensures the native layer/material, and binds map, ground/water texture scale, blend, shading and surf settings. Live-cell beaches are explicit terrain; generated beaches use the generator's width. A missing explicit source clears the surface and reports a warning.
- `override string[] _GetConfigurationWarnings()` — warns when `TerrainGeneratorPath` is empty.
- `override void _Ready()` — calls `CallDeferred(nameof(Rebuild))` if `RefreshOnReady` and not in the editor.

Private helpers worth noting for behaviour: `BuildMaps` walks the view once per texel writer — `WriteCellTexels` for the id/lake-width texels (red = terrain-id from a fixed `TerrainIds` dictionary contract with `terrain_splat.gdshader`, blue = the inland terrain id, alpha = beach width) and `WriteShadeTexel` for lighting (green = hillshade halved to fit 0..1), reading the generated field's `TerrainAtCell`/`ShadeAtPosition` when there are no live cells; `UpdateMaps` rewrites only the changed chunks' texels plus a one-cell shade halo; `BuildCoastMap` delegates to `TerrainCoastField.Build`; `EnsureSurface` creates/reuses a `TileMapLayer` named `"SplatSurface"` via `TerrainAuthoring.EnsureLayer`, assigns it a tileset sized to `TileSize` via `TerrainShaderSurface.BuildTileSet`, fills it via `TerrainShaderSurface.Fill`, and sets its `ZIndex` to `TerrainLayers.ZForFloor()`.

## The beach and the lake bank

How wide a beach is, how wide a lake bank is, and what lies inland of either are
[`TerrainShorelineStage`](TerrainShorelineStage.md)'s numbers, not this view's (VIEW-07,
2026-09-16). `WriteCellTexels` carries them into two texels per cell: the ocean beach width in the
id map's alpha (`shore.Width / 4`, decoded in the shader as `texture(id_map, map_uv).a * 4.0`) and
the lake shore width in `lake_width_map`'s red (`shore.LakeWidth / 3`, decoded as `* 3.0`), beside
the inland terrain id in the id map's blue. The shader composites its band from those against the
coast field's ocean distance and the lake field's distance. It renders the stage's decision; it
does not make one. There is no shader-side default width left to fall back to either — a zero
texel draws no beach at all.

**The lake width fallback is no longer gated on flat ground** (FIX-14, 2026-09-16). Without live
cells this view reads the generated field directly, and that path used to pass the lake width
through only where the cell's relief was `TerrainRelief.Flat` — one of three copies of the same
gate, so a lake against rising land drew no bank here either. The fallback now takes
`field.LakeShoreWidth` for every cell, matching the stage and the generation handoff.

**A lake's waterline is about five times crisper than the sea's** (FIX-14, 2026-09-16).
`terrain_splat.gdshader` used one softness for both — `shore_blend_tiles` either side of the
waterline. The open sea's edge is soft on purpose (a beach, a wash and surf carry that transition),
but a lake has none of them, so the same blend read as the lake's water smeared into the ground.
The shader now takes the softness from `open_sea`, the coast field's own flag that already keeps
surf off a lake: `shore_softness = mix(shore_blend_tiles * 0.2, shore_blend_tiles, open_sea)`. The
sea's edge is unchanged. Only the Original style is affected: the two stylised paths override the
mask outright and are untouched — Cartoon (`art_style` 1) keeps its fixed
`smoothstep(-0.06, 0.06, sd)` and Pixel Art (`art_style` 2) its hard `step(0.0, sd)`. The
transparent surface the tile and isometric views draw applies the same lake/sea rule in
`iso_water.gdshader`; see [TerrainSeaSurface](TerrainSeaSurface.md).

**Open defect: a cell the map calls sand can still be painted otherwise at its centre.** The stage's
cell kind is the majority of that cell's samples; this view's band is a per-fragment test against a
distance field at a different resolution. The two can disagree in both directions, at the very pixel
a unit stands on. This is the sand counterpart of the water-centre discrepancy recorded at seed
31415, cell (21,3).

VIEW-07 proposed closing it with a cell-centre contract in the shader: inside the central half of a
cell, force `beach` to the id texel's verdict. It was built, shown to the owner, **rejected on sight
and reverted the same session** (2026-09-16). Forcing the verdict per cell makes the beach decision
piecewise constant, so the smooth band becomes cell-square cores with a thin transition ring, and
where a cell the stage did not call sand sat inside the band, the sand was pulled out from between
the grass and the waterline — the tile staircase this shader's own comments record fighting off.
The defect is therefore **open, with no guard**: what fixes it must not quantise the band per cell.

`tests/terrain_beach_centre_probe.gd` reproduces the disagreement — authored cells, a deliberately
over-wide width texel, flat material colours, and a count of the cells whose centre disagrees with
their own kind. It is deliberately **registered in no runner**: it reports the open defect rather
than asserting it away. With the rejected rule in place it read 0 disagreements; without it, 32.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAt(Vector2I)`, `.ShadeAtCell(Vector2I)`, and `.BeachWidth` (property).
- Calls `TerrainCoastField.Build(generator, size, CoastDetail, CoastRangeTiles)` to build the coast distance texture.
- Calls `TerrainAuthoring.EnsureLayer(this, "SplatSurface")` to get/create the backing `TileMapLayer`.
- Calls `TerrainShaderSurface.BuildTileSet(cell, isometric: false)` and `TerrainShaderSurface.Fill(surface, size)` to set up and populate that layer.
- Reads `TerrainLayers.ZForFloor()` for draw order.
- Calls `TerrainTextures.Load(path, Name, description)` for every optional material texture path (the eleven slots); the foam sheet is loaded by `TerrainWaterMaterial.BindFoamSheet` from the look's `FoamSheetPath`.
- Calls `TerrainWaterMaterial.Apply(_material, Water.Settings(size, BoundsOrigin, max(5, CoastRangeTiles)))` — the one writer of the shared water uniforms — and `TerrainWaterMaterial.BindFoamSheet(_material, Water.FoamSheetPath, Name)`. The coast range keeps this view's own floor of 5 tiles, because it must agree with the range `TerrainPaintedCoastJob` actually built the field with, not with the export alone. It is not a caller of `TerrainSeaSurface`: it has no water surface, only a composite.
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
- The beach used to have two owners, and the shader's own comments record the incident: this renderer composited its band from a `beach_tiles` dial of its own while the tile and isometric renderers drew whatever sand *biome* the beach stage had assigned per cell, so `BeachWidth = 0.028` produced no beach in the generator and two views while this shader kept drawing the 1.15 tiles it defaulted to. There is no `beach_tiles` uniform now: the width is the stage's number, uploaded per cell in the id map's alpha, and VIEW-07 states the stage as the owner (see [The beach and the lake bank](#the-beach-and-the-lake-bank) above). What survives of the defect is narrower and still open — the band and the cell kind can disagree at a cell's centre because they are decided at different resolutions.
- No z-index export by design — a comment explains this is deliberate so `TerrainLayers` remains the single owner of draw order, citing a past bug where the feature renderer drew trees underneath the map because it had its own z dial.
- The foam sheet gets an extra warning on top of the one `TerrainTextures.Load` already pushes: if the look's path is non-empty but fails to load, `TerrainWaterMaterial.BindFoamSheet` additionally pushes "falling back to generated crests" and sets `use_foam_sheet = false`. The material textures rely solely on `TerrainTextures.Load`'s own warning (`Assign` just returns early on a null result) — both paths are reported, just at different granularity.
- `TerrainTextures.Load`'s own doc comment records a past duplication defect worth knowing when touching this file: texture loading (res:// vs. absolute-path handling, mipmap generation) used to be reimplemented per renderer, and one of the four copies (the tile renderer's water) was wrong, so that view alone drew unmipped/unimported art. This renderer is one of the three copies that were consolidated onto the shared helper; it is not exhibiting the defect itself.
