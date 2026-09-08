# TerrainIsometricRendererComponent

The water material binds `BoundsOrigin` as `map_origin`: texture repeat, seabed
sand, shoreline noise and foam phase use absolute grid coordinates, while coast
texture UVs stay view-local. Rivers inherit this uniform from the shared water
material. The GPU `terrain_material_origin_probe.gd` checks cropped native water
polygons under rotation and three zooms at positive/negative origins. It does not
guarantee crop-edge field continuity without neighbour data.

Live water fields use `TerrainCoastField.LiveCache`; unrelated metadata edits
reuse the field. Water display uses the shared bounded cubic `RenderCache`
described in `TerrainCoastField.md`. Terrain block geometry and live grid state
remain unchanged; reconstruction adds no additional shader texture samples.

## Water Texture Scale

`GroundTextureTiles` (default 12) sets the stationary sand texture repeat under
water, matching the painted view's beach material scale. `WaterTextureTiles`
(default 6) controls animated shallow/deep water texture sampling independently.
Both bind to the shared water shader and are copied to river materials during
rebuild. Neither resizes native cell geometry or block/top atlas frames.

## Built Surface Lifecycle

Automatic cell-change rebuilds are deferred while the view is not visible in the scene tree.
Showing a previously built/attempted view schedules a refresh; initial visibility does not bypass
RefreshOnReady=false. Explicit Rebuild calls still work while hidden. TerrainWorldComponent
keeps inactive bounds/origin synchronized with the generated world and the isometric feature view.
This avoids stale-size prop scans when another projection is active during regeneration.

Rebuild reuses the TileSet only while sheet paths, atlas dimensions, native cell size,
texture lifts, elevation step, primary frame mappings and terrain variants are unchanged. Configuration
changes rebuild the atlas. Missing explicitly configured sheets and zero-sized frames
fail and clear the surface. Unchanged-path image-content hot replacement is not covered
by this configuration cache.

An omitted TopSheetPath is supported: flat land cells use block source 0 instead
of referencing nonexistent source 1. Rivers do not use either atlas source.
A single-frame TerrainVariants entry overrides
the primary frame just as a multi-frame entry restricts its choices.

SurfaceExtent is the cached renderer-local logical bound of the built map. It
includes the native base plane and all elevated top-face corners, including summits.
TerrainWorldComponent uses it for preview/camera framing. It is measured once per
rebuild, not per camera query. Decorative sprite overhang and ocean overscan are
excluded so background water does not shrink the playable map's framing.

HasSurface requires a successful build and an available source. Failed rebuilds
clear blocks/seabed, hide water, reset bounds and emit SurfaceRebuilt. Picking and
public surface-corner queries then return no surface; attached features and collision
clear through the same notification. Rebuilding with a restored source recovers it.

The surface-height probe checks native summit containment and failed-source cleanup
and recovery. Collision, view-grid and world-live-source probes cover consumers.

Pipeline position: **renderer** — one of three views (isometric, alongside the flat/painted and orthogonal-tile renderers) that draw the world a `TerrainGeneratorComponent` already decided; it makes no terrain decisions of its own.

`TerrainIsometricRendererComponent` is a `Node2D` (`[Tool]`, `[GlobalClass]`) that draws the generated map as a stack of isometric blocks, one `TileMapLayer` per elevation level (ground, hills, mountains, summits) plus a seabed layer and a shader-driven sea-surface quad. It reads per-cell terrain kind and relief from the generator, converts that into a level via `TerrainLayers`, and paints either a full block (`SourceId`) or a flat top (`TopSourceId`) depending on whether a neighbouring cell is lower (i.e. a visible side face exists). It builds its own `TileSet` from a block sheet and an optional flat-top sheet, supports per-terrain frame "variants" for breaking up repeated coastline runs, and shares its water shader/material dials with the flat renderer via a coast distance field it builds itself (`TerrainCoastField`).

## Public API

### Water Geometry

The sea is an authored/reused `Polygon2D`, not a tile batch. Its shader receives
`tile_batch = false`: polygon vertices already use local coordinates and must not
receive the half-cell origin correction required by a `TileMapLayer` batch. The
flat tiled water renderer uses `tile_batch = true` with the same shader.

River cells use one `IsoRivers` Polygon2D containing indexed native-grid diamonds,
not static water tiles or one node per cell. It uses the sea shader and textures,
with a separate material instance so river opacity and foam settings cannot alter
the ocean or another scene's authored material. Godot's
[Polygon2D.polygons](https://docs.godotengine.org/en/4.4/classes/class_polygon2d.html#class-polygon2d-property-polygons)
stores the individual indexed polygons in one node.

Rivers sit at Ground level to remain visible in one-cell channels. Their vertices
use native MapToLocal coordinates; the node applies BoundsOrigin displacement and
the ground-height offset. SurfacePosition, SurfaceCorners and SurfaceCellAt use
the same height, independent of SeabedDepth. Obsolete water atlas frame exports
have been removed; water appearance comes from the shared shader and textures.
The river surface draws just above ground and below hills; raised cliffs may
still occlude it, unlike the ordinary ground blocks it replaces.

The elevated surface composites its sandy bed in the shared shader at full opacity.
It does not reveal a physical elevated riverbed through alpha: that would expose
foreground block sides. Ocean breakers are disabled on this surface; texture
motion and shimmer remain shared. These are renderer-specific compositing rules,
not generation or navigation changes. Shallow-water wading remains configurable
in GridNavigationComponent. Coast-edge bank art and river-mouth drops are not added.

Live edits rebuild the river batch; clearing/rebinding/missing sources clear it,
and hidden views catch up when shown. Clearing WaterShaderPath hides both sea and
rivers. GetLayerDiagnostics includes river_cells and river_surface in the water row.
The GPU river probe checks negative origins, disconnected and one-cell waterways,
wide rivers, live edits, picking, configured navigation, authored material isolation,
visible centre coverage and time-varying pixels from the actual shared shader.

`tests/terrain_iso_water_origin_probe.gd` instantiates the addon's terrain lab,
uses its actual water polygon/material settings and production shader coordinate
functions, and checks GPU cell-coordinate output against native `map_to_local`
positions after scaling and translation. This caught a one-cell sampling shift
that a tile-only shader fixture did not cover.

### Block Side Fitting

For tightly framed isometric blocks, when LevelHeight is shorter than the source
side height (frame height minus CellSize.Y), block TileData receives the
`terrain_block_sides.gdshader` material. It resamples the vertical faces below the
diamond's sloping front edge while retaining the top's pixels and native footprint.
The source atlas is not modified and mip filtering remains enabled. Natural-height
blocks and flat-top source tiles need no such material. Larger-than-source steps
still require appropriately taller artwork; the shader is not a cliff generator.

This fit assumes the top diamond begins at the frame's top and spans CellSize.Y.
Padded or unusually shaped block art needs a matching atlas layout, not just a
different elevation number. The authored demos use plain Kenney 111x128 frames with a
111x64 footprint and 32-pixel steps. The GPU cliff probe verifies top-detail
preservation and side area at three zoom levels.

The seabed uses the flat-top source when configured; its plane has no elevation
drops needing individual block faces. Without top artwork it uses block source 0.

### Logical coordinates

`BoundsOrigin` is the absolute logical top-left cell; `BoundsSize` is the extent.
`SurfacePosition`, `SurfaceLevel`, `SurfaceCellAt`, `SurfaceCorners`, `IsLandCell` and
`ContainsSurfaceCell` use absolute cells. Position/corner values are renderer-local; the gameplay
grid converts them to global coordinates. Native block and seabed layers also use absolute cells.

The internal field-taking overloads use local generation coordinates. A shared offset source
translates live-cell reads, including feature and water-source metadata, without changing the
generation algorithms. Isometric props use that same source and terrain-owned surface positions.

`OriginPosition` returns the native-grid displacement of the origin. The water polygon uses that
displacement while its shader stays local to the generated map. The world controller supplies
bounds and adjusts its camera targets accordingly. Authored autotile rendering is a separate view;
these changes do not establish origin support or valid artwork in that renderer.

### Settings

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads cell data from.
- `[Export] Vector2I BoundsSize = (48,48)` — map size in cells used for the coast field, water-depth sweep, and the draw loop.
- `[Export] string BlockSheetPath` — file path to the block sprite sheet (`.png`/`.webp`); required or `Rebuild` bails out.
- `[Export] int SheetColumns = 4`, `[Export] int SheetRows = 4` — grid layout of the block/top sheets.
- `[Export] Vector2I CellSize = (462,308)` — the diamond footprint of one cell in sheet pixels (not the taller block image).
- `[Export] int BlockLift = 79` — vertical offset applied to a block's `TextureOrigin` so its top diamond lands on the cell.
- `[Export] string TopSheetPath` — optional flat-top sheet used for cells with no visible side, avoiding a shaded seam on level ground.
- `[Export] int TopLift` — vertical offset for the flat-top sheet's `TextureOrigin`.
- `[Export] int LevelHeight = 158` — pixel rise per elevation step; also the visible height of a block's side face.
- `[Export] int GrassFrame/DryGrassFrame/DesertFrame/SandFrame/TundraFrame/SnowFrame/IceFrame/JungleFrame/SwampFrame/GravelFrame/RockFrame` — land frame indices in the block sheet (`mud` also maps to `SwampFrame`). Water uses no atlas frames.
- `[Export] string[] TerrainVariants` — `"kind=frame[,frame...]"` entries giving a terrain kind multiple interchangeable frames, selected deterministically per cell by a hash of its coordinates (repeat a frame number to weight it).
- `[Export] bool RefreshOnReady = true` — when true and not in the editor, calls `Rebuild()` deferred on `_Ready`; turn off when an external controller drives generation first.
- `[Export] int SeabedDepth = 5` (range 1-8) — how many tiles from shore the seabed is drawn, and how many material bands (sand/gravel/rock) it spans.
- `[Export] int SeabedStep = 12` — vertical pixel offset placing the seabed layer just under the water surface.
- `[Export] string WaterShaderPath` — `.gdshader` for the sea surface; without it `EnsureWaterSurface` returns early and there is no water.
- `[Export] string ShallowTexturePath/DeepTexturePath/SandTexturePath/FoamSheetPath` — optional water shader textures; unset falls back to flat shader colour (foam sheet additionally toggles `use_foam_sheet`).
- `[Export] int CoastDetail = 4` (range 1-8) — sub-tile samples per edge when building the shared coast distance field.
- `[Export] float CoastRangeTiles = 5.0` — distance at which the coast field saturates.
- `[Export] float MaxOpacity = 1.0`, `[Export] float ClarityTiles = 3.0` — deep-water opacity ceiling and how many tiles it takes to reach it (lets the seabed show near shore).
- `[Export] float LakeOpacity = 0.42` — opacity used for inland lakes, lower than open sea.
- `[Export] float ShoreOpacity = 0.55` — opacity at/inland of the waterline, kept above zero because an isometric block overhangs the tile below it.
- `[Export] float WaterOverscan = 2.5` — how far the sea quad extends past the map as a multiple of map size, clamped to `MaxWaterMarginCells` (72) in actual tiles.
- `[Export] float WaveIntensity = 1.0`, `[Export] float FoamStrength = 0.40`, `[Export] float DeepTiles = 4.5`, `[Export] float ShallowTiles = 1.8` — shader dials forwarded verbatim to the water material.
- `const int LevelCount` / `static int ZIndexForLevel(int)` / `static int ZIndexForProps(int)` — forward directly to `TerrainLayers` so callers can query the shared stack through the renderer without it owning the answer.
- `void Rebuild()` — resolves the terrain source, builds/reuses the `TileSet`, rebuilds the coast field, clears layers, measures water depth and summit floor, then paints land stacks, seabed and the batched animated river surface.
- `Vector2 SurfacePosition(Vector2I cell)` — returns the on-screen position of a cell's top face (grid projection plus elevation offset), the single source anything drawn on the map (props, units) must use to align with the terrain stack.
- `Godot.Collections.Array<Dictionary> GetLayerDiagnostics()` — reports each layer's kind/level/z-index/relative-z/painted-cell-count (plus the water surface's shading state and opacity), meant for a guard to catch a silently wrong z-order or an unshaded sea.
- `bool IsLandCell(Vector2I cell)` — true when the generator's terrain kind at that cell is a land kind (per `TerrainTileSets.IsLandKind`).
- `static int LevelFor(string terrain, int relief)` — thin forward to `TerrainLayers.LevelFor`.
- `override string[] _GetConfigurationWarnings()` — editor warning when `TerrainGeneratorPath` or `BlockSheetPath` is unset.
- `override void _Ready()` — schedules `Rebuild()` when `RefreshOnReady` is true and not running in the editor.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAt`, `.ReliefAt`, `.ElevationAt`, `.WaterSourceAt` for every cell (via `TerrainGeneratorPath`).
- Reads `TerrainLayers.Count/Sea/Ground/Hills/Mountains/Summits`, `.ZFor`, `.ZForProps`, `.ZForSeabed`, `.LevelFor` for the shared stack order and z-indexing.
- Calls `TerrainCoastField.Build` to compute the shared coast distance field, and `TerrainTileSets.IsWaterKind`/`IsLandKind` to classify cells.
- Uses `TerrainAuthoring.EnsureLayer`/`Adopt` to create and register the water `TileMapLayer` (and other layers via `MakeLayer`).
- Uses `TerrainShaderSurface.BuildTileSet`/`Fill` to build the diamond-cell blank `TileSet` and fill the overscanned sea quad.
- Uses `TerrainTextures.Load` (via `LoadTexture`/`LoadSheet`) to load the block sheet, top sheet, and water textures.
- Shares the same water shader parameter contract (`coast_map`, `coast_range`, `map_size`, `cell_size`, `tile_offset`, `max_opacity`, `clarity_tiles`, `lake_opacity`, `shore_opacity`, `wave_intensity`, `foam_strength`, `deep_tiles`, `shallow_tiles`, `tex_shallow`, `tex_deep`, `tex_sand`, `foam_sheet`, `use_foam_sheet`) as the flat/painted renderer, so the two views draw the same sea.

## Notes

- `ShowsSide` intentionally ignores its `level` parameter in the boundary case (`at.X >= size.X`) — any out-of-bounds neighbour is treated as lower regardless of level, which is correct for map edges but means the method's boundary branch does not use the `level` argument at all.
- The XML doc comment on the class describes the seabed as previously "five, stacked at descending offsets" and now "ONE layer" — this matches the current code, not a stale claim.
- `MeasureSummitFloor`/`SummitShare` (0.45f) determines summits from the top 45% of mountain-tile elevations map-wide; the comment explains this replaced a per-massif depth walk that starved narrow ridges — no leftover dead code from that approach remains in this file.
- `EnsureTileSet` short-circuits (`return true`) once `_tileSet` and `_frames` are populated, so changing `BlockSheetPath`/`SheetColumns`/`SheetRows`/frame exports at runtime after the first successful build has no effect until `_tileSet` is externally cleared — there is no invalidation path in this file; a caller must reconstruct the node or otherwise reset `_tileSet` to pick up sheet/column/row changes.
- Water textures failing to load are silently accepted as "no texture" (`SetTexture` returns false, shader falls back to flat colour) except for the shader itself and the coast map, both of which `GD.PushWarning`.
