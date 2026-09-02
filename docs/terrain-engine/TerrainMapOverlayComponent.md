# TerrainMapOverlayComponent

Renderer: a `Node2D` overlay component that sits alongside the ground renderers in the terrain scene and paints gameplay markers over whichever surface is drawn beneath it.

`TerrainMapOverlayComponent` draws the generator's resource deposits and player start positions as primitive circles/rings — no sprites, no art assets. It reads `TerrainGeneratorComponent` directly (the one owner of resource/start data) and draws with Godot's immediate canvas API (`_Draw`) rather than instancing nodes, so a full map's worth of markers costs one draw call's worth of shapes instead of one node per marker. It is deliberately a separate node from the painted/tile/isometric ground renderers so markers can be toggled or reparented without touching terrain art.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this overlay reads; empty disables drawing and raises a configuration warning.
- `[Export] Vector2I BoundsSize = (48, 30)` — map dimensions in tiles the overlay iterates when placing markers. Note this default does not match the 96x60 default used by the renderer components (see Notes).
- `[Export(Range 1,256,1)] int TileSize = 64` — pixel size of one tile, used to convert cell coordinates to draw-space positions.
- `[Export] bool ShowResources = true` — toggles drawing resource markers.
- `[Export] bool ShowStartPositions = true` — toggles drawing start-position rings.
- `[Export(Range 0.05,0.5,0.01)] float ResourceRadiusTiles = 0.16f` — resource marker radius, as a fraction of `TileSize`.
- `[Export(Range 0.1,1.0,0.01)] float StartRadiusTiles = 0.42f` — start-position ring radius, as a fraction of `TileSize`.
- `void Refresh()` — sets this node's `ZIndex` to `TerrainLayers.ZForMarkers()`, re-resolves `_generator` from `TerrainGeneratorPath` if it is null or invalid, then calls `QueueRedraw()`. Called automatically from `_Ready()`.
- `override string[] _GetConfigurationWarnings()` — returns a warning string when `TerrainGeneratorPath` is empty, otherwise an empty array.
- `override void _Draw()` — if no generator is resolved, pushes a warning and draws nothing; otherwise calls `DrawResources` (if enabled) then `DrawStartPositions` (if enabled).

Everything else (`DrawResources`, `DrawStartPositions`, `ColourFor`) is private implementation:
- Resource markers: for every cell in `BoundsSize`, calls `_generator.ResourceAt(cell)`; a non-empty result is drawn as a dark-rimmed filled circle coloured by `ColourFor`, which maps `TerrainResourceStage.CategoryOf(resource)` to one of three flat colours (Strategic = red, Luxury = purple/pink, everything else including Bonus = yellow).
- Start positions: for every cell in `_generator.GetStartPositions()`, draws a two-tone ring (dark outer arc + light inner arc) plus a filled dot at the centre.

## Dependencies

- Reads `TerrainGeneratorComponent.ResourceAt(Vector2I)` and `TerrainGeneratorComponent.GetStartPositions()` (world-data model, this batch's sibling file not included here but referenced directly).
- Reads `TerrainResourceStage.CategoryOf(string)` to bucket a resource id into a `ResourceCategory` for marker colour.
- Reads `TerrainLayers.ZForMarkers()` to place itself at the top of the shared draw-order stack.
- Writes nothing back to the generator or any other terrain file — purely a read-and-draw component.

## Notes

- `BoundsSize` defaults to `(48, 30)` here, while `TerrainPaintedRendererComponent` and `TerrainReliefRendererComponent` default to `(96, 60)`. If a scene leaves all three at their defaults, the overlay iterates a smaller area than the ground it sits over and silently omits markers on the outer part of the map. This is an accepted-but-easy-to-miss per-node setting rather than one shared owner (`TerrainLayers`/`TerrainMapSetup` own z-order and bounds-by-size respectively, but not this per-renderer `BoundsSize` export).
- The class doc comment explicitly calls out a past defect (a scene supplying `ZIndex = 60` externally instead of the component owning it) and the code now sets `ZIndex` itself every `Refresh()` — this is documentation of a fixed problem, not a live one.
- No caching of per-cell resource lookups: `_generator.ResourceAt` and `TerrainResourceStage.CategoryOf` run for every cell in `BoundsSize` on every `Refresh()`/redraw, which is fine at map scale but is an O(width × height) scan purely to find sparse resource cells.
