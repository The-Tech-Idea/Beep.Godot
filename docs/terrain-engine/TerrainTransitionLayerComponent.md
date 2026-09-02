# TerrainTransitionLayerComponent

Renderer: a `[Tool][GlobalClass]` `Node` that maintains one display `TileMapLayer` for a single logical terrain kind, painting autotiled transition ("15-piece" or Godot terrain-set) tiles at its edges.

Each instance represents one biome's presence across the map — e.g. "water" or "grass" — as a dual-grid autotiled layer: for every point where up to four gameplay cells meet, it decides (via `IsTransitionTerrain`) which of those four count as "this layer's terrain" and paints the matching edge/corner/solid tile so biome boundaries read as smooth coastlines/shorelines instead of a hard grid. It supports two selection mechanisms: the modern, default path delegates to Godot 4's own `TileSet` terrain-connect API (`SetCellsTerrainConnect`), and a legacy manual path computes a 4-bit corner mask itself and looks the tile up in a hand-verified 15-piece atlas layout — kept only for atlases whose numeric tile order has been confirmed to match `CanonicalMaskToAtlasIndex`. `TerrainTileRendererComponent` builds and owns one of these per configured biome; it is not typically hand-placed standalone, though it is usable as such.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — the `TerrainGeneratorComponent` this layer reads terrain kinds from; documented as the *only* source (the layer used to read a `GridCellDataComponent` copy instead, which could drift from the generator's live field — this was fixed).
- `[Export] NodePath DisplayLayerPath` / `[Export] NodePath DetailDisplayLayerPath` — the `TileMapLayer`(s) this component paints into; `DetailDisplayLayerPath` is optional, for a second overlay pass (e.g. grass detail tufts) on the same terrain kind.
- `[ExportGroup("Map")] [Export] Vector2I BoundsOrigin`, `[Export] Vector2I BoundsSize = (64,64)` — the cell rectangle this layer paints.
- `[Export] string TransitionTerrainKind = "water"` — which logical terrain kind this instance represents (matched via `IsTransitionTerrain`, with several kind aliases folded together — see Notes).
- `[Export] bool RenderFilledBase = false` — when true (manual-mask path only), every cell is treated as fully this terrain (mask 15) rather than testing neighbours — used for the renderer's filled base/floor layer.
- `[ExportGroup("Godot Terrain Set")] [Export] bool UseTileSetTerrains = true` — selects the modern path (Godot `TileSet` terrain-connect) vs. the legacy manual dual-grid mask path.
- `[Export] int TerrainSet = 0`, `[Export] int Terrain = 0` — which authored Godot `TileSet` terrain set/terrain index to connect against, used only when `UseTileSetTerrains` is true.
- `[Export] bool IgnoreEmptyTerrains = true` — passed straight through to `SetCellsTerrainConnect`.
- `[ExportGroup("Atlas")] [Export] int SourceId = 0`, `[Export] int DetailSourceId = 1` — atlas source indices for the main/detail `TileSet`.
- `[Export] Vector2I AtlasOrigin`, `[Export] int AtlasColumns = 4` (range 1–16) — atlas grid geometry for the manual-mask path.
- `[Export] bool UseCanonical15PieceLayout = true` — when true, a computed mask is remapped through `CanonicalMaskToAtlasIndex` before indexing the atlas; when false, the mask is used as the atlas index directly (an arbitrary row-major layout).
- `[Export] int AlternativeTile = 0` — the alternate-tile id passed to `SetCell` on the manual path.
- `[Export(File)] string AtlasTexturePath`, `[Export] bool BuildTileSetFromAtlasPath = false`, `[Export] Vector2I AtlasTileSize = (64,64)`, `[Export] int AtlasTileRows = 4` (range 1–16) — main-atlas texture and whether/how to auto-build a `TileSet` source from it.
- `[Export(File)] string DetailAtlasTexturePath`, `[Export] bool BuildDetailTileSetFromAtlasPath = false` — same, for the optional detail pass.
- `[ExportGroup("Refresh")] [Export] bool RefreshOnReady = true`, `[Export] bool RefreshInEditor = false` — whether `_Ready` schedules a refresh, and whether that happens even inside the editor.
- `Vector2I EffectiveBoundsSize` (get) — `BoundsSize` clamped to at least `(1,1)` componentwise.
- `void RequestRefresh()` — debounced (`_refreshQueued` guard) deferred call to `RefreshTransitions()`.
- `void RefreshTransitions()` — the main entry point: resolves node references, places the display layer(s) at the correct z-index (`PlaceDisplayLayer`), builds the `TileSet`(s) from atlas paths if configured (manual path only), then either delegates to `RefreshUsingTileSetTerrains()` (Godot terrain-connect path) or repaints every cell itself via `DualGridMaskAt`/`AtlasCoordinatesForMask` (manual path). Warns and returns early if the resolved display `TileSet` is null.
- `int DualGridMaskAt(Vector2I displayCell)` — computes the 4-bit corner mask (bit 1 = top-left neighbour, bit 2 = top-right, bit 4 = bottom-left, bit 8 = the cell itself/bottom-right) by testing `IsTransitionTerrain` on the four gameplay cells meeting at `displayCell`.
- `Vector2I AtlasCoordinatesForMask(int mask)` — clamps `mask` to 0–15, optionally remaps it through the canonical 15-piece index table, then converts to atlas (column, row) using `AtlasColumns`.
- `override void _Ready()` — resolves references, updates configuration warnings, and calls `RequestRefresh()` if `RefreshOnReady` and (not in editor, or `RefreshInEditor` is true).
- `override string[] _GetConfigurationWarnings()` — flags a missing `TerrainGeneratorPath`, missing `DisplayLayerPath`, blank `TransitionTerrainKind`, or (when `UseTileSetTerrains`) a negative `TerrainSet`/`Terrain` index.
- `override void _ExitTree()` — empty body (no cleanup performed).

## Dependencies

- Reads terrain kinds exclusively from `TerrainGeneratorComponent.TerrainKindAt(Vector2I)` via the private `TerrainKindAt` wrapper — the class doc comment states this is deliberately the *only* source (versus a `GridCellDataComponent` copy) so the tile view cannot drift from the generator's live data.
- Reads `TerrainLayers.ZForFloor()` (when `RenderFilledBase`) or `TerrainLayers.ZForKind(Normalize(TransitionTerrainKind))` (otherwise) in `PlaceDisplayLayer` to set the display layer's (and detail layer's, +1) z-index — the sole place this component's stack order is decided, replacing per-scene hand-authored z values the class comment says used to drift (six hand-written z values in the 15-piece demo scene kept water stacked above grass/desert after that exact bug was fixed elsewhere).
- Calls `TerrainTextures.Load` (not a bare `GD.Load`) in `EnsureDisplayTileSet` to load atlas textures through the shared import/mipmap pipeline.
- Owned/instantiated by `TerrainTileRendererComponent`, which sets every one of its `[Export]`s programmatically per biome (see that file's `CreateLayer`).
- Does not read or write `TerrainWorld`, `TerrainTileSets`, or `GridCellDataComponent` directly.

## Notes

- `IsTransitionTerrain`'s kind-matching (`Normalize` + explicit alias table) folds `"deep_water"`/`"shallow_water"` into `"water"`, `"grassland"`/`"dry_grass"` into `"grass"`, `"sand"` into `"desert"`, and `"swamp"`/`"earth"`/`"dirt"`/`"soil"` into `"mud"` — this alias list is local to this file; a new terrain kind added to `TerrainTileSets.Kinds` (e.g. a second desert-like kind) would need a matching addition here or it silently falls through to an exact-string match only.
- The class-level doc comment ("By default it delegates connection selection to Godot TileSet terrains; the legacy manual dual-grid mapping is available only for authored atlases with a verified mask-to-tile layout") accurately describes the code: `UseTileSetTerrains` defaults `true`, and the manual path is explicitly gated behind `UseCanonical15PieceLayout`'s verified table. Not stale.
- `override void _ExitTree()` is an empty override with no comment explaining why it exists — likely a no-op leftover from removed cleanup logic (e.g. disconnecting a signal); harmless but dead.
- `EnsureDisplayTileSet` early-returns if the layer's `TileSet` already `HasSource(sourceId)`, meaning a texture path changed after the first build is silently ignored on the manual path until the `TileSet` is cleared externally — this mirrors the same "atlas rebuilt once, not on every refresh" behavior documented in `TerrainTileRendererComponent`.
- `RefreshUsingTileSetTerrains` iterates `y < size.Y` / `x < size.X` (cell-exact), while the manual-mask path in `RefreshTransitions` iterates `y <= size.Y` / `x <= size.X` (one extra row/column) — correct and consistent with the dual-grid comment in `TerrainTileRendererComponent.CreateLayer` ("a dual-grid renderer paints one MORE row and column than the map has, because each tile straddles the corner between four cells"), but the loop-bound difference between the two code paths in this same method is easy to misread as a bug if the reader doesn't already know the two paths address different grids (display cells vs. terrain-set cells).
