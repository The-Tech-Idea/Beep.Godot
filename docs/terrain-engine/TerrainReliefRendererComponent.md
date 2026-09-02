# TerrainReliefRendererComponent

Renderer: a `Node2D` component, sibling to the feature renderer, that draws terrain relief as objects standing on top of whichever ground surface (painted/tile/isometric) is drawn beneath it.

`TerrainReliefRendererComponent` surfaces relief data (hills/mountains) that the generator already computes per tile but that, until this component existed, only showed up as the ground material turning grey — height doesn't read as height from flat colour at any zoom. It draws lit, shadowed billboard sprites sampled from authored sprite sheets, batched into one `List<Stamp>` and drawn from a single node's `_Draw()` (Factorio/feature-renderer style) rather than one `Sprite2D` per rock, because a mountainous map can be thousands of stamps and that many nodes is real per-frame overhead to walk. A deterministic hash seeds per-cell frame choice, position jitter and scale jitter so results are stable across rebuilds for the same seed.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads.
- `[Export] Vector2I BoundsSize = (96, 60)` — map dimensions in tiles.
- `[Export(Range 1,256,1)] int TileSize = 64` — pixel size of one tile.
- `[Export] int Seed = 20261` — seeds the local jitter/frame-selection hash; independent of the generator's own seed, so retuning jitter never has to touch generation.
- `[Export(File *.png,*.webp)] string HillsSheetPath = ""` — sprite sheet of equal frames for hills; empty means hills are simply not drawn (deliberate, per the code comment, so a project can ship hills without mountains or vice versa rather than half-drawing both).
- `[Export(Range 1,16,1)] int HillsColumns = 4`, `[Export(Range 1,16,1)] int HillsRows = 4` — grid layout of the hills sheet.
- `[Export(File *.png,*.webp)] string MountainsSheetPath = ""` — sprite sheet for mountains; same empty-means-skip behaviour.
- `[Export(Range 1,16,1)] int MountainsColumns = 4`, `[Export(Range 1,16,1)] int MountainsRows = 4` — grid layout of the mountains sheet.
- `[Export(Range 0.2,4,0.05)] float HillsScale = 0.72f`, `[Export(Range 0.2,4,0.05)] float MountainsScale = 1.25f` — per-stamp size multiplier after fitting a frame to one tile.
- `[Export(Range 1,8,1)] int HillsPerTile = 2`, `[Export(Range 1,8,1)] int MountainsPerTile = 1` — how many independent stamps are placed per relief cell.
- `[Export(Range 0,1,0.01)] float PositionJitter = 0.22f` — random per-stamp offset from tile centre, as a fraction of tile size.
- `[Export(Range 0,0.6,0.01)] float ScaleJitter = 0.16f` — random per-stamp size variance, ± this fraction.
- `[Export] bool RefreshOnReady = true` — when true and not in the editor, `_Ready()` defers a call to `Rebuild()`; turned off when an external controller generates the world first and drives `Rebuild()` itself, avoiding a double build.
- `void Rebuild()` — sets `ZIndex = TerrainLayers.ZForProps(TerrainLayers.Mountains)` and `ZAsRelative = false`; sets `TextureFilter = LinearWithMipmaps` (sheets need mip filtering so a distant peak doesn't alias into noise); resolves the generator, clears `_stamps`, loads both sheets via `LoadSheets()`, then for every cell in `BoundsSize` reads `_generator.ReliefAt(cell)` and — if it's hills or mountains and the matching sheet loaded — adds `HillsPerTile`/`MountainsPerTile` stamps via `AddStamp`. Stamps are then sorted by `SortY` (their draw-space Y centre) for painter's-order depth (a nearer peak overlaps one behind it), and `QueueRedraw()` is called. Pushes a warning and returns early (after still calling `QueueRedraw()`) if no generator is resolved.
- `override string[] _GetConfigurationWarnings()` — warns when `TerrainGeneratorPath` is empty.
- `override void _Draw()` — draws every stamp in `_stamps` via `DrawTextureRectRegion(stamp.Sheet, stamp.Target, stamp.Region)`.

Private implementation of note: `AddStamp` picks a frame index from the sheet via `Hash01(x, y, Seed + fixed-offset)` (a different literal offset per hashed quantity — frame index, scale jitter, X jitter, Y jitter — so they don't correlate), fits the frame to the tile size, applies `scale` and jitter, and nudges the stamp's vertical anchor up (`y + 0.40` instead of `y + 0.5`) so the sprite's base sits at the tile centre and its mass rises above it, which is how the eye reads elevation. `Hash01(int x, int y, int seed)` is a private static integer hash (multiply-xor-shift) returning a value in `[0, 1)`.

## Dependencies

- Reads `TerrainGeneratorComponent.ReliefAt(Vector2I)` to get a relief level (0 = none, matched here against local consts `ReliefHills = 1` and `ReliefMountains = 2`).
- Reads `TerrainLayers.ZForProps(TerrainLayers.Mountains)` and the `TerrainLayers.Mountains` constant to place itself at the top prop slot of the shared draw-order stack (so relief occludes trees standing in front of it).
- Calls `TerrainTextures.Load(path, Name, "relief sheet")` for both the hills and mountains sheets.
- Writes nothing back to the generator or any other terrain file; all data flow is generator → this renderer → canvas draw calls.

## Notes

- `ReliefHills = 1` and `ReliefMountains = 2` are local private consts with a comment stating they "match `TerrainRelief`, which is internal to the generation layer" — i.e. this file keeps its own numeric copy of an enum owned elsewhere rather than referencing it directly (presumably because that enum is `internal` to a different assembly/scope boundary within the generation layer). This is the same *shape* of implicit cross-file contract flagged in `TerrainMapSetup.md`'s array-order note, just numeric instead of positional: if `TerrainRelief`'s values are ever renumbered, this file's stamps would silently start reading the wrong level with no compiler error.
- No z-index export, by the same convention documented in `TerrainPaintedRendererComponent` and enforced project-wide: `TerrainLayers` is the single owner of draw order, and the comment here explicitly ties the chosen slot (`ZForProps(Mountains)`, the *highest* relief level's slot) to the requirement that a peak be able to occlude a tree in front of it.
- Leaving `HillsSheetPath` or `MountainsSheetPath` empty is a deliberate no-op for that relief level (stated in the export's doc comment and confirmed by `AddStamp`'s `sheet is null → continue`), not a failure — distinct from `TerrainTextures.Load` returning null for a *non-empty* path that fails to load, which does push a warning.
- The per-cell `Hash01` calls (four per stamp slot, offset by fixed literals) mirror the general shape of deterministic per-cell hashing used elsewhere in the generator/renderers in this addon, but this file's implementation is local and private rather than a shared utility — no evidence within this batch of files that it duplicates another file's *logic* (the batch's `TerrainNoiseSet.cs` uses `FastNoiseLite`, not a hand-rolled hash), so this is noted as a pattern to watch rather than a confirmed duplicate.
