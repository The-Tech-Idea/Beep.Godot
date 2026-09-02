# TerrainFeatureRendererComponent

Renderer: a `[Tool][GlobalClass]` `Node2D` that draws terrain features (woods, jungle, marsh, oasis) as sprite "props" standing on top of the tile-based ground rendering.

`TerrainFeatureRendererComponent` reads the per-tile feature assigned by `TerrainFeatureStage` (via a `TerrainGeneratorComponent`) and, for every tile carrying a feature, stamps one or more sprites from a sheet (woods/jungle/oasis/marsh, each its own configurable atlas) into a batched draw list. It picks the sprite frame, position jitter and scale jitter deterministically from a seeded hash of the tile coordinates, so the same map always draws the same trees, and a "wood" isn't visibly the same tree copy-pasted. Everything is drawn from a single node's `_Draw()` call (one `DrawTextureRectRegion` per stamp, sorted back-to-front by Y) rather than one `Sprite2D` per tree, keeping a map with thousands of trees cheap to build and walk.

## Public API

- `[Export] public NodePath TerrainGeneratorPath { get; set; } = new("")` — path to the `TerrainGeneratorComponent` this renderer reads feature data from; required for `Rebuild()` to do anything.
- `[Export] public Vector2I BoundsSize { get; set; } = new(96, 60)` — how many tiles wide/high to scan for features.
- `[Export(Range 1..256)] public int TileSize { get; set; } = 64` — pixel size of one tile, used to place and scale sprites.
- `[Export] public int Seed { get; set; } = 31415` — seed mixed into the per-stamp hash (frame choice, position jitter, scale jitter).
- `[Export(File)] public string WoodsSheetPath/JungleSheetPath/OasisSheetPath/MarshSheetPath { get; set; } = ""` — paths to the four feature sprite sheets; a sheet left blank is simply not loaded.
- `[Export(Range 1..16)] public int WoodsColumns/WoodsRows/JungleColumns/JungleRows/OasisColumns/OasisRows/MarshColumns/MarshRows { get; set; } = 4` — atlas grid size for each sheet, used to slice out a random frame.
- `[Export(Range 0.2..3)] public float SpriteScale { get; set; } = 0.62f` — global scale multiplier applied to every drawn sprite (after fitting the frame to one tile).
- `[Export(Range 1..8)] public int SpritesPerTile { get; set; } = 4` — base number of sprites stamped per feature tile.
- `[Export(Range 0..8)] public int ForestExtraSprites { get; set; } = 3` — additional sprites stamped on `Forest`/`Jungle` tiles (dense stands) on top of `SpritesPerTile`, so dense vs. scattered vegetation actually differ in sprite count.
- `[Export(Range 0..1)] public float PositionJitter { get; set; } = 0.18f` — fraction of a tile that a sprite's centre is randomly offset by, per axis.
- `[Export(Range 0..0.6)] public float ScaleJitter { get; set; } = 0.18f` — fractional random scale variance applied per sprite.
- `[Export] public bool RefreshOnReady { get; set; } = true` — if true and not in the editor, calls `Rebuild()` deferred on `_Ready()`; turned off when an external controller drives generation-then-`Rebuild()` explicitly, to avoid building the map twice.
- `public override void _Ready()` — conditionally schedules `Rebuild()` per `RefreshOnReady`.
- `public override string[] _GetConfigurationWarnings()` — editor warning if `TerrainGeneratorPath` is unset.
- `public void Rebuild()` — sets the node's `ZIndex` (via `TerrainLayers.ZForProps(TerrainLayers.Ground)`) and texture filter, resolves the generator, loads sheets, then for every tile in `BoundsSize` asks the generator for its feature string and, if a matching sheet exists, stamps `SpritesPerTile` (+ `ForestExtraSprites` for Forest/Jungle) sprites; sorts stamps by Y for painter's-order overlap; calls `QueueRedraw()`. Pushes a `GD.PushWarning` and returns early (still redrawing an empty list) if there is no generator or no sheets loaded.
- `public override void _Draw()` — draws every stamp in the sorted list via `DrawTextureRectRegion`.

## Dependencies

- Reads `TerrainGeneratorComponent.FeatureAt(Vector2I)` (from `TerrainGeneratorComponent.cs`, which delegates to `GeneratedTerrainField.FeatureAtCell`) to get each tile's feature string.
- Reads the feature-name constants `TerrainFeatureStage.Woods/Forest/Jungle/Oasis/Marsh` (from `TerrainFeatureStage.cs`) to decide which sheet/behaviour applies to a given feature string — this is the renderer's only coupling to how features are decided.
- Calls `TerrainLayers.ZForProps` and reads `TerrainLayers.Ground` (from `TerrainLayers.cs`) to set its draw order above ground tiles.
- Calls `TerrainTextures.Load` (from `TerrainTextures.cs`) to load each sheet with a mipmap chain.
- Does not write to `TerrainWorld`, `GeneratedTerrainField`, or any generation-stage data — purely a reader/renderer.

## Notes

- Dead `[Export]` removed, documented in place: the comment block above `_stamps` records that a `z index` export used to exist, was set to `-84` in three scenes, but was never assigned to anything in code, so it silently did nothing and caused trees to render under the tile view when layers moved to a shared stack. The export has since been deleted and the story kept as a comment — this is the exact "accepted setting that enforces nothing" failure mode, now fixed and documented as a lesson rather than left live.
- `TryDescribe`'s fallback rules are asymmetric and intentional, not a bug: `Jungle`/`Oasis` fall back to the `woods` sheet if their own sheet is missing (so vegetation still shows), but `Marsh` has no fallback and is simply not drawn if `MarshSheetPath` is unset, with an explicit comment reasoning that a forest canopy would misdescribe a bog.
- Sprite frame selection, position jitter and scale jitter each use `Hash01` with a different additive salt (`Seed + 811 + slot*97`, `Seed + 907 + slot*89`, etc.) — this is the same `Hash01`/hash-salting pattern used in `TerrainFeatureStage.cs`, duplicated rather than shared (each file has its own private `Hash01`, byte-for-byte identical implementation, and its own salt scheme).
- `RefreshOnReady` only fires outside the editor (`!Engine.IsEditorHint()`); in the editor `Rebuild()` must be invoked by something else (e.g. the terrain lab/controller), matching the class doc's stated split of responsibility.
