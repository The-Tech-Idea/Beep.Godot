# TerrainIsometricRendererComponent

Pipeline position: **renderer** — one of three views (isometric, alongside the flat/painted and orthogonal-tile renderers) that draw the world a `TerrainGeneratorComponent` already decided; it makes no terrain decisions of its own.

`TerrainIsometricRendererComponent` is a `Node2D` (`[Tool]`, `[GlobalClass]`) that draws the generated map as a stack of isometric blocks, one `TileMapLayer` per elevation level (ground, hills, mountains, summits) plus a seabed layer and a shader-driven sea-surface quad. It reads per-cell terrain kind and relief from the generator, converts that into a level via `TerrainLayers`, and paints either a full block (`SourceId`) or a flat top (`TopSourceId`) depending on whether a neighbouring cell is lower (i.e. a visible side face exists). It builds its own `TileSet` from a block sheet and an optional flat-top sheet, supports per-terrain frame "variants" for breaking up repeated coastline runs, and shares its water shader/material dials with the flat renderer via a coast distance field it builds itself (`TerrainCoastField`).

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads cell data from.
- `[Export] Vector2I BoundsSize = (48,48)` — map size in cells used for the coast field, water-depth sweep, and the draw loop.
- `[Export] string BlockSheetPath` — file path to the block sprite sheet (`.png`/`.webp`); required or `Rebuild` bails out.
- `[Export] int SheetColumns = 4`, `[Export] int SheetRows = 4` — grid layout of the block/top sheets.
- `[Export] Vector2I CellSize = (462,308)` — the diamond footprint of one cell in sheet pixels (not the taller block image).
- `[Export] int BlockLift = 79` — vertical offset applied to a block's `TextureOrigin` so its top diamond lands on the cell.
- `[Export] string TopSheetPath` — optional flat-top sheet used for cells with no visible side, avoiding a shaded seam on level ground.
- `[Export] int TopLift` — vertical offset for the flat-top sheet's `TextureOrigin`.
- `[Export] int LevelHeight = 158` — pixel rise per elevation step; also the visible height of a block's side face.
- `[Export] int GrassFrame/DryGrassFrame/DesertFrame/SandFrame/TundraFrame/SnowFrame/IceFrame/JungleFrame/SwampFrame/GravelFrame/RockFrame/ShallowWaterFrame/DeepWaterFrame` — frame indices in the block sheet for each terrain kind (`mud` also maps to `SwampFrame`).
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
- `void Rebuild()` — public entry point: resolves the generator, builds/reuses the `TileSet`, rebuilds the coast field, clears all layers, measures water depth and the summit floor, then paints every cell (land as a level stack of blocks, sea as seabed + hole, rivers as a single ground-level flat tile).
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
