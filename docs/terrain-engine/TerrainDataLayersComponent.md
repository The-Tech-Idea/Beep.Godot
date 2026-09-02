# TerrainDataLayersComponent

Game-facing component (a `Node2D` addon component, `[Tool][GlobalClass]`) — the view-agnostic per-cell data API a game queries at runtime, separate from any of the renderers that draw the map.

`TerrainDataLayersComponent` mirrors the generator's output into three invisible `TileMapLayer`s (`TerrainData`, `ResourceData`, `FeatureData`), one tile-per-distinct-value each, so a game can read `terrain`/`resource`/`feature`/`relief`/`is_water`/`passable` custom tile data through Godot's own `get_cell_tile_data` API instead of an addon-specific query surface — and so the answer is the same regardless of which renderer (flat tile, isometric, painted) happens to be on screen, none of which expose a queryable per-cell API of their own. It also optionally gives the `TerrainData` layer a physics body and navigation polygon per ground kind (land/water/steep), driven by three exported collision-layer bitmasks, so a game can decide via ordinary collision masks whether e.g. water blocks a given body.

## Public API

- `[Export] public NodePath TerrainGeneratorPath { get; set; } = new("")` — path to the `TerrainGeneratorComponent` this component mirrors; resolved lazily on first `Rebuild()`.
- `[Export] public Vector2I BoundsSize { get; set; } = new(64, 64)` — map size in cells that `Rebuild()` iterates; clamped to at least 1 on each axis.
- `[Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64` — cell size in pixels; must match the cell size of whichever view is being queried alongside this component for a "cell" to mean the same thing in both.
- `[Export] public bool RefreshOnReady { get; set; } = true` — when true and not in the editor, defers a call to `Rebuild()` from `_Ready()`.
- `[Export] public bool GenerateCollision { get; set; } = true` — whether the `TerrainData` layer gets `CollisionEnabled = true` and per-tile collision shapes via `TerrainTileSets.ShapeCell`.
- `[Export] public bool GenerateNavigation { get; set; } = true` — whether the `TerrainData` layer gets `NavigationEnabled = true` (navigation polygons are set up via `TerrainTileSets.ShapeCell` alongside collision, gated by the same `GenerateCollision || GenerateNavigation` condition).
- `[Export(PropertyHint.Layers2DPhysics)] public uint LandCollisionLayer { get; set; } = 2` — collision bit assigned to open-land tiles.
- `[Export(PropertyHint.Layers2DPhysics)] public uint WaterCollisionLayer { get; set; } = 4` — collision bit assigned to sea/lake/river tiles.
- `[Export(PropertyHint.Layers2DPhysics)] public uint SteepCollisionLayer { get; set; } = 8` — collision bit assigned to rock/cliff tiles.
- `public override void _Ready()` — defers `Rebuild()` when `RefreshOnReady` is true and the node is not running in the editor.
- `public override string[] _GetConfigurationWarnings()` — editor warning when `TerrainGeneratorPath` is unset.
- `public void Rebuild()` — rewrites all three data layers from the generator: resolves `TerrainGeneratorPath` (warns and returns early via `GD.PushWarning` if it can't find a generator — this is a silent-to-the-caller failure, see Notes), scans the map once to find which resource/feature values actually occur, (re)builds each `TileMapLayer`'s `TileSetAtlasSource` with one transparent tile per distinct value found, then paints every cell.
- `public string TerrainAt(Vector2I cell)` — reads the `"terrain"` custom data field off the `TerrainData` layer at `cell`; empty string outside the map or before `Rebuild()` has run.
- `public string ResourceAt(Vector2I cell)` — reads `"resource"` custom data off the `ResourceData` layer; empty when the cell has no resource.
- `public string FeatureAt(Vector2I cell)` — reads `"feature"` custom data off the `FeatureData` layer; empty when the cell has no feature.
- `public int ReliefAt(Vector2I cell)` — reads the `"relief"` custom data (an int) off the `TerrainData` layer.
- `public bool IsWaterAt(Vector2I cell)` — reads the `"is_water"` custom data (bool) off the `TerrainData` layer.
- `public bool PassableAt(Vector2I cell)` — reads the `"passable"` custom data (bool) off the `TerrainData` layer; documented as a convention only — nothing else in the addon enforces it, since collision/navigation are generated per ground-kind on the same layer and a game's own rules (swimmers crossing water, climbers crossing rock) are expected to be expressed via that agent's own collision mask/navigation layers instead.
- `public TileMapLayer? TerrainLayer => _terrain` — the underlying terrain/relief/water/collision layer, exposed so a caller can query it directly (e.g. `get_cell_tile_data`) rather than through the convenience methods above.
- `public TileMapLayer? ResourceLayer => _resources` — the resource-id layer, same rationale.
- `public TileMapLayer? FeatureLayer => _features` — the feature layer, same rationale.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindsPresent()`, `TerrainKindAt(Vector2I)`, `ResourceAt(Vector2I)`, `FeatureAt(Vector2I)` (from `TerrainGeneratorComponent.cs`) — the entire source of per-cell truth this component mirrors.
- Calls `TerrainTileSets.Create(Vector2I cell)`, `TerrainTileSets.Describe(...)`, `TerrainTileSets.DefineBody(...)`, `TerrainTileSets.ShapeCell(...)`, and reads the `TerrainTileSets.Cell.*` custom-data field-name constants (`Terrain`, `Resource`, `Feature`, `Relief`, `IsWater`, `Passable`) (all from `TerrainTileSets.cs`) — every tile's custom data schema and physics/navigation shape come from there, not from logic local to this file.
- Calls `TerrainAuthoring.EnsureLayer(this, name)` (from `TerrainAuthoring.cs`) to create/find each child `TileMapLayer` node.
- Writes to its own child `TileMapLayer` nodes (`TerrainData`, `ResourceData`, `FeatureData`); these are the "data layers" other files in this batch do not touch directly — renderers and generation stages work through `TerrainGeneratorComponent`/`TerrainWorld` instead, and this component is the one place that turns that data into queryable Godot tile data.

## Notes

- `Rebuild()`'s failure path when no generator is resolved (`GetNodeOrNull` returns null) only calls `GD.PushWarning` and returns — the caller of `Rebuild()` gets no indication anything failed (no exception, no return value), which is a silent-to-the-caller failure mode per the project's own exception-handling standard (report through the return type or by throwing, not by logging and continuing quietly). A caller that invokes `Rebuild()` and then queries `TerrainAt`/etc. before a generator is ever wired up gets empty strings/false/0 indistinguishably from "cell has no data."
- `GenerateNavigation`'s doc comment says collision/navigation are set up "on the layer for the GROUND it is," but the actual gating in `Rebuild()` is `GenerateCollision || GenerateNavigation` for calling `TerrainTileSets.ShapeCell` at all — there's no way to get navigation polygons without collision shapes also being requested to compute, or vice versa; the two exported bools independently control `CollisionEnabled`/`NavigationEnabled` on the layer but jointly gate whether `ShapeCell` runs. Not a bug, just worth knowing: setting `GenerateCollision = false, GenerateNavigation = true` still causes shape computation to run (because the OR is true) but the layer's `CollisionEnabled` is false, so the computed shapes exist but don't collide.
- `PassableAt`'s doc comment is explicit that nothing in the addon reads it — confirmed true within this batch and consistent with the "convenience API, not enforced" framing; not dead code (it's public API a game is expected to call) but is an intentionally-unused-internally accessor, not an oversight.
- `EnsureLayer`'s texture atlas is filled with `Colors.Transparent` and explicitly never drawn (`layer.Visible = false`) — the `ImageTexture` exists purely because `TileSetAtlasSource` requires one; this is called out in the file's own doc comment and matches the code.
