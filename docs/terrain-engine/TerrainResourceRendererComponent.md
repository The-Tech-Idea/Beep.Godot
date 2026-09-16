# TerrainResourceRendererComponent

## Live Resource Binding

Automatic refresh pauses while hidden and catches up when a previously attempted view is shown.
Reattachment restores resource and grid bindings; explicit `Rebuild()` still works while hidden.
The world controller keeps inactive bounds/origin current.

`ResourceRootPath` optionally selects a subtree of live `GridResourceNodeComponent` instances.
When configured, icons read those nodes' IDs and remaining amounts, not the generated field.
Gathering, restoring, adding and removing nodes refreshes the view through a shared observer.
A missing configured root draws no icons. Empty keeps the generated surface/liquid preview.
The renderer does not own resource balances. `IconCount` exposes the baked icon count.

Custom sheet-path changes reload textures; custom IconOrder changes rebuild frame mappings.
Mappings beyond the configured sheet capacity are ignored. Generator path changes take effect
on Rebuild even if the previous generator remains alive. Standalone callers changing paths or
properties directly must call Rebuild. Optional GridPath uses native cell centers and cell edges
through the grid/renderer transforms. BoundsOrigin defines the absolute logical rectangle; generated
queries remain local. TileSize supplies square spacing only without a grid. VerticalOffset is a
renderer-local vertical lift scaled by the cell edge length. Missing explicit grids clear icons.
GeometryChanged refreshes cached positions; GetIconCenters returns those baked local centers.
TerrainWorldComponent binds the selected grid before rebuilding this view under Painted, Tiles and
IsometricAutotile; it is hidden only under the block Isometric view.

This is the ONE resource drawer (VIEW-13, 2026-09-15). `TerrainMapOverlayComponent` no longer draws
surface or liquid resources as discs. The terrain lab wires this renderer at
`Preview/Diagnostics/Resources`: its world rebuilds it on every projection switch, and the lab's map
diagnostics toggle shows and hides the `Diagnostics` layer. `tests/terrain_view_parity_probe.gd`
asserts `IconCount` equals the number of cells carrying a surface or liquid resource under every
grid-bound projection.

`tests/terrain_live_resource_view_probe.gd` covers live depletion/restore, subtree isolation,
node addition/removal, missing roots and changed custom mappings.
It also verifies baked icon centers under rectangular/isometric native layouts, transformed parents,
negative logical origins and grid geometry notifications. World live-source tests verify grid binding.

Renderer / game-facing component in the terrain pipeline — a `Node2D` a scene places alongside a `TerrainGeneratorComponent` to draw the resources the generator already assigned per tile.

`TerrainResourceRendererComponent` reads `TerrainGeneratorComponent.ResourceAt(cell)` for every cell in its own `BoundsSize` and, for each cell that holds a resource id it has a frame for, draws that frame from an icon sheet on a dark circular backplate. Icons come from either a bundled preset sheet chosen by the generator's `ResourceSet` (`FollowGenerator`, the default) or a sheet/grid/order configured directly on the node (`Custom`). A resource id with no matching frame in the active `IconOrder` is deliberately drawn as nothing rather than substituted with a wrong icon. It exists because previously the only thing rendering the generator's per-tile resource assignments was a debug overlay of coloured circles in the terrain lab — in a running game the twenty-odd resource kinds the generator computes every run were invisible. That overlay drawer has since been removed (VIEW-13); this renderer is the only one.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads from; empty is flagged by `_GetConfigurationWarnings`.
- `[Export] Vector2I BoundsSize = (96, 60)` — how many map cells (in each axis) `Rebuild` iterates; independent of the generator's own bounds, so it must be kept in sync by hand.
- `[Export] int TileSize = 64` (range 1–256) — pixel size of one cell, used to place and scale icons.
- `[Export] ResourceIconSource IconSource = FollowGenerator` — `FollowGenerator` picks the bundled preset sheet matching `_generator.ResourceSet`; `Custom` uses `IconSheetPath`/`Columns`/`Rows`/`IconOrder` as configured on the node.
- `[Export] string IconSheetPath` (file filter `*.png,*.webp`) — sheet path used only when `IconSource == Custom`.
- `[Export] int Columns = 4`, `[Export] int Rows = 4` (range 1–16) — sheet grid used only when `IconSource == Custom`.
- `[Export] string[] IconOrder` — resource ids in frame order (left-to-right, top-to-bottom) for a `Custom` sheet; an id absent from the list is simply never drawn. An empty entry in the array skips a frame without shifting later ids.
- `[Export] float IconScale = 0.52f` (range 0.1–2) — icon size as a fraction of the tile-fitted frame size.
- `[Export] float VerticalOffset = -0.12f` (range −1–1) — shifts the icon vertically off tile centre (in tile units) so it doesn't sit on top of ground detail.
- `[Export] bool ShowBackplate = true` — draws a dark disc (`BackplateColour`) behind every icon before the icon itself.
- `[Export] Color BackplateColour = (0.09, 0.10, 0.13, 0.62)` — colour of that backplate disc.
- `[Export] bool RefreshOnReady = true` — when true and not in the editor, `_Ready` defers a call to `Rebuild`; turn off when an external controller drives generation/`Rebuild` order itself.
- `public override void _Ready()` — conditionally schedules `Rebuild` as described above.
- `public override string[] _GetConfigurationWarnings()` — returns a warning when `TerrainGeneratorPath` is unset.
- `public void Rebuild()` — sets `ZIndex`/`ZAsRelative`/`TextureFilter`, re-resolves the generator node, re-applies the icon preset (or custom config), loads the sheet if needed, then walks every cell in `BoundsSize`, building one `Icon` (region/target/centre/radius) per cell that has a resource with a known frame; calls `QueueRedraw()`. Pushes a `GD.PushWarning` and draws nothing if there is no resolved generator or no icon sheet/frame table could be loaded.
- `public override void _Draw()` — draws the backplate discs (if `ShowBackplate`) then the icon textures built by the last `Rebuild`; no-ops if no sheet is loaded.

Also defined in this file: the public `ResourceIconSource` enum (`FollowGenerator`, `Custom`), and private nested types `IconPreset` (path/columns/rows/order for a bundled sheet) and `Icon` (region/target/centre/radius per drawn marker), plus the private static `Presets` table of the three bundled sheets and their hand-verified `IconOrder`s.

## Dependencies

- Reads `TerrainGeneratorComponent.ResourceAt(Vector2I)` and `TerrainGeneratorComponent.ResourceSet` (`TerrainGeneratorComponent.cs`) — resolved once per `Rebuild`/lazily via `ResolveGenerator`, from the node at `TerrainGeneratorPath`.
- Reads the `ResourceSet` enum, defined in `TerrainResourceStage.cs`.
- Calls `TerrainLayers.ZForMarkers()` (`TerrainLayers.cs`) to place icons above props in the shared z-order stack.
- Calls `TerrainTextures.Load(path, name, description)` (`TerrainTextures.cs`) to load the icon sheet texture.
- Writes nothing back to the generator or `TerrainGenerationBuffer` — purely a reader/renderer.

## Notes

- `BoundsSize`/`TileSize` are declared independently here (defaults `(96, 60)` / `64`) rather than read from the generator, and other renderer components in this directory (e.g. `TerrainMapOverlayComponent`, default `(48, 30)`; `TerrainGeneratorComponent.BoundsSize` itself defaults to `(64, 64)`) each carry their own copies with different defaults. Nothing keeps these numbers in sync automatically — a scene where this renderer's `BoundsSize` doesn't match the generator's actual map size will silently under- or over-cover the map (icons missing at the edges, or wasted iterations past the real bounds) with no warning.
- A resource id with no frame in `IconOrder` draws nothing by design (documented at length in the class comment) — this is intentional and not a bug, but it is a silent-looking path worth knowing about when a new resource id is added to a catalogue without updating the matching preset's `IconOrder`.
- The bundled `IconOrder` arrays are asserted in the class doc comment to have been "read off the sheets rather than assumed"; this doc does not re-verify the PNG pixel layout, only that the code consumes the arrays as written.
- No dead code, stubs, or TODOs found in this file.
