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
  cells before using CellPosition for camera targeting.
- GetPaintDiagnostics returns a copy containing `valid`, `requested`,
  `missing` and `unmapped`. Early validation failures also provide `reason`.
  Valid requires both missing and unmapped counts to be zero.

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
longer used by the lab. This is a flat isometric tile view; elevated blocks and
isometric props remain in the separate Isometric view. Hard tile boundaries
are expected here; smooth transition artwork has not been authored.

`terrain_lab_styles_probe.gd` verifies all 1,024 cells are painted, no terrain
is missing/unmapped, redraw is deterministic, and changing style preserves
the live terrain revision. A coverage check is not an art-quality approval.
