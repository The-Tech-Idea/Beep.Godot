# TerrainIsometricAutotileRendererComponent

Renderer: a `[Tool][GlobalClass] Node2D` that paints the generated terrain field into an isometric `TileMapLayer` using an author-supplied `TileSet`'s built-in terrain-connect/peering-bit system, rather than picking tile indices itself.

This renderer deliberately does not compute transition tiles from a corner mask or from sampled pixel colors — its own doc comment explains that an earlier version tried deriving tile placement from an atlas's pixel content and only matched a known-correct mapping "on barely a third of tiles" for textured art. Instead it groups generated cells by which authored "terrain" (in the Godot TileSet-terrain sense) each maps to, and hands each whole group to `TileMapLayer.SetCellsTerrainConnect` in one call so Godot's own terrain-matching resolves the transitions across the run. It requires an isometric-shaped `TileSet` with peering bits painted on the transition tiles by a human in the TileSet editor.

## Public API

- `[Export] NodePath TerrainGeneratorPath` — path to the `TerrainGeneratorComponent` this renderer reads from.
- `[Export] Vector2I BoundsSize` — the map size in cells this renderer paints, default `(48, 48)`; independent of the generator's own `BoundsSize` export (no cross-validation between the two is performed here).
- `[Export] TileSet? Tiles` — the authored isometric TileSet with terrain peering bits; assigned directly onto the managed `TileMapLayer` on every `Rebuild()`.
- `[Export] int TerrainSet` (0-8) — which terrain-set index within `Tiles` carries the terrains this renderer binds to.
- `[Export] string[] TerrainBindings` — draw-order list of `"kind[,kind...]=terrainIndex"` entries (e.g. `"grass,dry_grass=0"`) mapping generator terrain-kind strings to a TileSet terrain index; kinds absent from every entry are simply never painted (by design — the doc comment calls a silent substitution a misdescription of the map).
- `[Export] bool RefreshOnReady` — if true, `_Ready()` defers a `Rebuild()` call (skipped in the editor via `Engine.IsEditorHint()` check); turn off when an external controller generates the world first and calls `Rebuild()` itself.
- `override void _Ready()` — conditionally schedules `Rebuild()`.
- `override string[] _GetConfigurationWarnings()` — editor warnings for empty `TerrainGeneratorPath`, missing `Tiles`, or empty `TerrainBindings`.
- `void Rebuild()` — resolves the generator, bails with a warning if the generator or `Tiles` is missing, otherwise clears and repaints the managed `TileMapLayer`: for each parsed binding it collects every cell in `BoundsSize` whose `TerrainKindAt` result is in that binding's kind set, then calls `SetCellsTerrainConnect` once per binding group. Also warns (without erroring) if cells were requested but the layer ends up with zero used cells — the documented "TileSet has no peering bits, nothing painted, no error" failure mode.

## Dependencies

- Reads `TerrainGeneratorComponent.TerrainKindAt(Vector2I)` per cell, resolved via `TerrainGeneratorPath` (`ResolveGenerator()`), to decide what to paint.
- Calls `TerrainAuthoring.EnsureLayer(this, "IsoTerrain")` to get/create its managed `TileMapLayer`, and `TerrainLayers.ZFor(TerrainLayers.Ground)` to set that layer's `ZIndex` — placing it at the shared cross-renderer Z stack's ground slot (documented as previously colliding with the "sea" slot at the Node2D default Z of 0).
- Does not read `TerrainGenerationSettings`, `GeneratedTerrainField`, or any `Terrain*Stage` file directly — all generation data comes through the `TerrainGeneratorComponent` public API only.

## Notes

- `BoundsSize` here is a second, independently-set map-size export that duplicates `TerrainGeneratorComponent.BoundsSize` in purpose; nothing in this file validates the two agree, so a mismatch would silently paint only part of the generated map (if this renderer's `BoundsSize` is smaller) or query out-of-range cells (if larger) without any warning specific to that condition.
- The "painted no tiles" warning in `Rebuild()` is a real, deliberately-added guard against a documented silent-failure mode (TileSet exists, terrain set is selected, but no peering bits are painted) — this is a good example of turning a previously-silent failure into a reported one, per the project's own exception/failure-reporting conventions, implemented here via `GD.PushWarning` rather than a return value, since this is a void editor/tool method with no caller expecting a result.
- `RenderingQuadrantSize = 1` and the accompanying comment explicitly call out that this is the same fix `TerrainIsometricRendererComponent` (outside this batch) applies for the same reason (per-tile Y-sort correctness) — duplicated *reasoning*, not duplicated code; each renderer must set it on its own managed layer.
- Malformed `TerrainBindings` entries are reported via `GD.PushWarning` and skipped rather than silently ignored, consistent with the project's "fail loud, not silent" convention.
