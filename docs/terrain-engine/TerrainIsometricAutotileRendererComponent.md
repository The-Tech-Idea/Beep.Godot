# TerrainIsometricAutotileRendererComponent

Automatic live-cell rebuilds pause while hidden and resume with a deferred rebuild when a previously
built/attempted view is shown. Initial visibility does not override RefreshOnReady=false.
Explicit Rebuild remains available regardless of visibility. TerrainWorldComponent updates inactive
bounds and origin, so the next activation uses the current world's size. Invalid authored terrain
assignments still report diagnostics when the view is actually built; they are not suppressed.

Current authored-isometric view for TerrainWorldComponent. This Node2D paints a
native TileMapLayer using an authored TileSet. It is
a renderer, not another world model or terrain generator.

## Sources And Coordinates

- CellDataPath selects the authoritative GridCellDataComponent. Runtime live edits
  schedule a coalesced rebuild; individual edits outside the bounds are ignored.
- TerrainGeneratorPath provides a generated preview when CellDataPath is empty.
  A configured but missing live source does not silently fall back to generation.
- BoundsOrigin is the absolute starting cell; BoundsSize is the cell count.
  Live reads and tile writes use absolute cells. Generated field samples remain
  local to the recipe and are translated on output.
- TerrainWorldComponent supplies source paths and bounds. Standalone scenes must
  set them explicitly. Default bounds are origin (0, 0), size (48, 48).
- Source paths resolve on Rebuild. Runtime cell subscriptions detach on source
  replacement or tree exit.

## Art And Initialization

Tiles must be an isometric TileSet with the selected TerrainSet. TerrainBindings
contains ordered entries such as `grass,dry_grass=0` and `water=1`. Unmapped
terrain kinds are not substituted. Bindings are normalized and validated as one
configuration before painting. Malformed entries, negative indices and one kind
assigned to conflicting terrain indices invalidate the entire paint with a reason;
they are not skipped while a misleading partial map is reported as valid.
Repeated aliases pointing to the same index are allowed and grouped together.

`UseTerrainConnections` defaults to true: every bound terrain needs assigned
peering bits and Godot's `SetCellsTerrainConnect` chooses transitions. This check
does not prove every combination exists; coverage is measured after painting.

For complete terrain tiles without transition artwork, set it to false. Atlas
tiles are selected by their authored `TileData.TerrainSet` and `Terrain` values.
Multiple matching tiles vary deterministically by absolute cell coordinate.
This mode calls native `SetCell`; it does not invent transition artwork or
silently fall back when terrain-connect configuration is invalid.

RefreshOnReady defers a runtime rebuild. Disable it when TerrainWorldComponent
owns initialization. Invalid sources or art clear the old view rather than
displaying stale terrain. The managed layer uses the shared ground Z slot,
Y sorting and mip-aware filtering.

The first explicit rebuild discovers an already-authored `IsoTerrain` child
before validation, so a missing source cannot leave its old tiles on screen.
Only that owned layer is cleared, not unrelated authored children.

## Sea

This view gained a sea in VIEW-04 (implemented 2026-09-16); before it, it drew ground and
nothing else. Set `WaterShaderPath` and the renderer builds a `TileWater` layer over the terrain
it just painted, running the same shader the other three views use. Leave it empty and the tiles
are all that draws — the contract the flat tile view already states, and what a game wanting a
flat stylised sea asks for.

The water layer fills its own cells from (0,0) — one rendering quadrant, so the shader's `VERTEX`
does not restart mid-map — and is then MOVED so its cell (0,0) lands exactly where the terrain
layer draws `BoundsOrigin`. The offset is taken by asking both layers where a cell is
(`terrain.MapToLocal(BoundsOrigin) - water.MapToLocal(Vector2I.Zero)`) rather than re-deriving
the isometric projection, so it stays aligned whatever an authored TileSet's layout is. Its blank
tile is a diamond, sized from the terrain TileSet's own `TileSize`; a terrain layer with no
TileSet warns and draws no water rather than guessing a cell shape.

Under a `LibraryPack` the sea is cleared. A pack brings its own shoreline tiles, so the shader
sea would draw a second one over them — the same rule the flat tile view applies.

- `[Export(File,*.gdshader)] string WaterShaderPath` — the sea shader; empty means no sea.
- `[Export] TerrainWaterLook? WaterLook` — how the sea looks, shared with every other view of
  this world. `TerrainWorldComponent.Draw()` pushes the world's look here on every build, so
  assign it on the world; unassigned, `TerrainWaterLook.Shared` is used. See
  [TerrainWaterLook](TerrainWaterLook.md).
- `[Export] int CoastDetail`, `[Export] float CoastRangeTiles` — the coast window this view
  resolves for itself, from live cells when they are bound and from the generator otherwise.
- `[Export] float MaxOpacity = 1.0`, `ClarityTiles = 3.0`, `LakeOpacity = 0.42`,
  `ShoreOpacity = 0.55` — the transparent-sheet uniforms, per surface rather than per look.

The coast resolve, the material and the tile geometry all come from the shared
[`TerrainSeaSurface`](TerrainSeaSurface.md) (`TileBatched(isometric: true)`,
`BuildMaterial(flatProjection: false, tileBatch: true)`), so this sea cannot drift from the other
three.

## Public Methods

- Rebuild clears and validates the view, samples each source cell once, groups
  it by the resolved terrain index, and makes one native terrain-connect call
  per nonempty terrain group in first-binding order. It then measures coverage.
- GetTerrainLayer returns the managed IsoTerrain layer for GridProjectionComponent.
- CellPosition accepts an absolute cell and returns its renderer-local center
  using the native layer transform.
- GridExtent accepts dimensions and returns the logical tile extent starting at
  BoundsOrigin, in renderer-local coordinates. It includes staggered perimeter
  cells, but not sprite overhang. World adds BoundsOrigin to local generator start
  cells before using CellPosition for camera targeting. It returns an empty
  rectangle while the layer has never published a TileSet, for example on a
  first draw with a rejected `LibraryPack`.
- GetPaintDiagnostics returns a copy containing `valid`, `requested`,
  `missing` and `unmapped`. Validation failures, packs that cannot draw a cell and
  failed time-sliced builds also provide `reason`. Valid requires both missing and
  unmapped counts to be zero. A failed pack build keeps the published layer;
  TerrainWorldComponent reports the reason as `View incomplete: …` in its status
  line and fails world generation with it.

## Verification And Limits

terrain_live_cells_probe covers shifted origins, source replacement, live edits,
coverage diagnostics and invalid-art clearing using a complete synthetic TileSet.
It also rejects conflicting/malformed bindings, verifies first-rebuild clearing
of authored tiles, and checks detached-time and subsequent live edits after
reattachment. Native matching does not paint outside the tested bounds or fill
unbound cells in the complete fixture. That is a tested result, not a claimed
fix for a reproduced out-of-bounds bug.
terrain_view_grid_probe covers native layout geometry, shifted bounds and camera
starts under transforms. World-source and lab-grid probes also pass.

The lab now uses `textures/iso/lab_terrain_tileset.tres`: native 111x64 diamond
tiles from the existing Kenney tops atlas, with explicit terrain assignments
and bindings for all generated biomes, including water. It uses complete-tile
mode, not terrain-connect mode. The old unassigned grass-only resource is no
longer used by the lab. This is a flat isometric tile view. Elevated blocks and the
per-level isometric feature renderer stay with the separate Isometric view. This view draws the
grid companions (vegetation, rocks, resource icons, overlay) on its diamond cells through the
gameplay grid (VIEW-01), and its own shader sea
(VIEW-04). Hard tile boundaries
are expected here; smooth transition artwork has not been authored.

`terrain_view_parity_probe.gd` carries this view's sea row: its `IsoAutotile/TileWater` must be
drawn, carry a coast map, and agree with the other three views on the shared water uniforms.
`terrain_water_material_probe.gd` builds it on the lab's authored TileSet and checks it receives
all thirteen dials from one `TerrainWaterLook`. Neither is a judgement on the water artwork.

`terrain_lab_styles_probe.gd` verifies all 1,024 cells are painted, no terrain
is missing/unmapped, redraw is deterministic, and changing style preserves
the live terrain revision. A coverage check is not an art-quality approval.
