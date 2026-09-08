# TerrainFeatureRendererComponent

Renderer: a `[Tool][GlobalClass]` `Node2D` that draws terrain features (woods, jungle, marsh, oasis) as sprite "props" standing on top of the tile-based ground rendering.

`TerrainFeatureRendererComponent` reads the per-tile feature assigned by `TerrainFeatureStage` (via a `TerrainGeneratorComponent`) and, for every tile carrying a feature, stamps one or more sprites from a sheet (woods/jungle/oasis/marsh, each its own configurable atlas) into a batched draw list. It picks the sprite frame, position jitter and scale jitter deterministically from a seeded hash of the tile coordinates, so the same map always draws the same trees, and a "wood" isn't visibly the same tree copy-pasted. Everything is drawn from a single node's `_Draw()` call (one `DrawTextureRectRegion` per stamp, sorted back-to-front by Y) rather than one `Sprite2D` per tree, keeping a map with thousands of trees cheap to build and walk.

## Public API

Automatic cell/grid-driven rebuilds pause while hidden. Showing a previously attempted view queues
one refresh; explicit `Rebuild()` remains available while hidden. Tree reattachment restores both
cell and grid subscriptions. `TerrainWorldComponent` keeps inactive feature bounds synchronized.

- `TerrainGeneratorPath` supplies recipe features when `CellDataPath` is empty.
- `CellDataPath` supplies authoritative live features; water, lava and cleared cells suppress them through the shared live-terrain reader.
- `BoundsOrigin` is the absolute top-left logical cell. Generated features are sampled locally, live features at origin plus local cell.
- `GridPath` optionally supplies native cell centers and corners. Jitter and sprite scale follow that geometry, including separately transformed renderer parents. A missing explicit grid clears the draw list rather than using a different grid.
- `GetStampCenters()` returns the actual cached sprite centers in renderer-local coordinates, including jitter.
- `GetStampAnchors()` returns ground anchors rather than artwork centres. Jittered
  anchors are checked against the shared fine water sampler and renderer bounds.
  Bounded candidate selection spreads each clump instead of stacking trees at the
  cell centre. A failed search can retain one dry centre, not repeated overlapping
  fallbacks. This checks the anchor, not the entire canopy or shader swash.
- `SpriteAnchor` is the normalized point of the sprite placed at its grid position. The default
  `(0.5, 0.92)` grounds visible trunk art; `(0.5, 1)` places its bottom edge on the cell.
- `PropSizing` uses the shared TerrainPropSizing resource for tree, oasis and reed dimensions.
- `GetStampBounds()` returns actual frame rectangles in renderer-local units for size diagnostics.
- `[Export] public Vector2I BoundsSize { get; set; } = new(96, 60)` — how many tiles wide/high to scan for features.
- `TileSize` is the square-cell fallback when `GridPath` is empty. The world controller binds the selected grid before rebuilding features.
- `[Export] public int Seed { get; set; } = 31415` — seed mixed into the per-stamp hash (frame choice, position jitter, scale jitter).
- `[Export(File)] public string WoodsSheetPath/JungleSheetPath/OasisSheetPath/MarshSheetPath { get; set; } = ""` — paths to the four feature sprite sheets; a sheet left blank is simply not loaded.
- `[Export(Range 1..16)] public int WoodsColumns/WoodsRows/JungleColumns/JungleRows/OasisColumns/OasisRows/MarshColumns/MarshRows { get; set; } = 4` — atlas grid size for each sheet, used to slice out a random frame.
- `WoodsFrameBindings` uses the same `kind[,kind...]=frame[,frame...]` format as
  the isometric renderer. Empty uses the whole sheet; repeated indices provide
  weights. It applies whenever the selected sheet is woods, including a missing
  jungle/oasis sheet's woods fallback. Separate sheets retain their own frames.
- Sprite sizes use visible alpha bounds, excluding frame padding, and enforce shared limits after jitter.
- `SpritesPerTile = 1`: base number of sprites per feature cell.
- `ForestExtraSprites = 1`: additional sprites for Forest/Jungle, producing two trees per dense forest cell rather than miniature tree icons.
- `PositionJitter = 0.85f` is the total scatter width in cell units per axis.
  Zero explicitly stacks anchors at the exact cell centre, without a hidden Y bias.
- `[Export(Range 0..0.6)] public float ScaleJitter { get; set; } = 0.18f` — fractional random scale variance applied per sprite.
- `[Export] public bool RefreshOnReady { get; set; } = true` — if true and not in the editor, calls `Rebuild()` deferred on `_Ready()`; turned off when an external controller drives generation-then-`Rebuild()` explicitly, to avoid building the map twice.
- `public override void _Ready()` — conditionally schedules `Rebuild()` per `RefreshOnReady`.
- `public override string[] _GetConfigurationWarnings()` — editor warning if `TerrainGeneratorPath` is unset.
- `public void Rebuild()` — sets the node's `ZIndex` (via `TerrainLayers.ZForProps(TerrainLayers.Ground)`) and texture filter, resolves the generator, loads sheets, then for every tile in `BoundsSize` asks the generator for its feature string and, if a matching sheet exists, stamps `SpritesPerTile` (+ `ForestExtraSprites` for Forest/Jungle) sprites; sorts stamps by Y for painter's-order overlap; calls `QueueRedraw()`. Pushes a `GD.PushWarning` and returns early (still redrawing an empty list) if there is no generator or no sheets loaded.
- `public override void _Draw()` — draws every stamp in the sorted list via `DrawTextureRectRegion`.

## Large-Map Residency

At runtime, maps larger than 65,536 cells use camera-driven prop chunks when
`StreamLargeMaps` is enabled (the default). Smaller maps and editor builds keep
the immediate full-view path. This changes rendering residency only, not feature
generation, live cell contents, jobs or simulation.

- `FeatureChunkSize`: 32 cells by default.
- `FeaturePreloadChunks`: one extra chunk around the viewport, plus canopy bounds.
- `FeatureCellsPerFrame`: at most 256 feature cells evaluated per update by default.
- `ResidentFeatureChunkCount`, `ResidentFeatureCellCount`, `PendingFeatureChunkCount`
  and `FeatureCellsProcessedLastFrame` report residency and work.
- `UpdateFeatureResidency()` is the explicit update entry point; `_Process` calls it.
- `StampCount` and stamp inspection APIs report resident props, not total world population.

Chunks share the full renderer's seeded scatter, frame selection, size limits and
dry-anchor checks. The live-water query samples nearby cells and patches instead
of allocating a whole-map water snapshot. Single-cell edits invalidate nearby
chunks; bulk map changes rebuild the streaming state. Hidden renderers retire
their resident stamps, and camera jumps discard stale partial chunks. Rotated
cameras and transformed grid bindings are supported.

The cell budget does not bound texture loading, alpha-bound discovery, resident
stamp sorting or draw calls in milliseconds. Zooming out across the entire world
can still request all its props. This is the flat feature renderer's residency,
not streaming for the separate isometric/rock renderers, collision or navigation.

`tests/terrain_feature_streaming_probe.gd` covers a logical 1024x1024 sparse live
map, budget limits, equality with full-view scatter, camera jumps, edits, hidden
views and transformed grids. Its rendered checks capture visible foliage and a
rotated map edge. The water-surface probe compares local queries with the existing
fine shoreline sampler across four seeds.

## Dependencies

- Reads `TerrainGeneratorComponent.FeatureAt(Vector2I)` (from `TerrainGeneratorComponent.cs`, which delegates to `GeneratedTerrainField.FeatureAtCell`) to get each tile's feature string.
- Reads the feature-name constants `TerrainFeatureStage.Woods/Forest/Jungle/Oasis/Marsh` (from `TerrainFeatureStage.cs`) to decide which sheet/behaviour applies to a given feature string — this is the renderer's only coupling to how features are decided.
- Calls `TerrainLayers.ZForProps` and reads `TerrainLayers.Ground` (from `TerrainLayers.cs`) to set its draw order above ground tiles.
- Calls `TerrainTextures.Load` (from `TerrainTextures.cs`) to load each sheet with a mipmap chain.
- Does not write to `TerrainGenerationBuffer`, `GeneratedTerrainField`, or any generation-stage data — purely a reader/renderer.

## Notes

- Dead `[Export]` removed, documented in place: the comment block above `_stamps` records that a `z index` export used to exist, was set to `-84` in three scenes, but was never assigned to anything in code, so it silently did nothing and caused trees to render under the tile view when layers moved to a shared stack. The export has since been deleted and the story kept as a comment — this is the exact "accepted setting that enforces nothing" failure mode, now fixed and documented as a lesson rather than left live.
- `TryDescribe`'s fallback rules are asymmetric and intentional, not a bug: `Jungle`/`Oasis` fall back to the `woods` sheet if their own sheet is missing (so vegetation still shows), but `Marsh` has no fallback and is simply not drawn if `MarshSheetPath` is unset, with an explicit comment reasoning that a forest canopy would misdescribe a bog.
- Frame and scale selection use the shared `TerrainGeometry.Hash01`. Position
  selection uses `TerrainFeatureScatter`, also shared with the isometric view.
  All three are keyed by absolute cell identity, not coordinates relative to
  view bounds. Cropping a live view therefore does not reseed the same cell.
- `RefreshOnReady` only fires outside the editor (`!Engine.IsEditorHint()`); in the editor `Rebuild()` must be invoked by something else (e.g. the terrain lab/controller), matching the class doc's stated split of responsibility.

See `FEATURE_SCATTER.md` for placement limits, demo art choices and regression coverage.

All presentations use the shared limits described in `TerrainPropSizing.md`.
The ground-cover regression checks actual cached drawing bounds. Terrain generation,
feature-cell coverage and camera zoom remain independent of the size policy.
