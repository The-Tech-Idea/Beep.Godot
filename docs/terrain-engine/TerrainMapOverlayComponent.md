# TerrainMapOverlayComponent

A Node2D that batches generated resource markers, start rings, and underground survey patches.
TerrainWorldComponent shows it for flat projections. It is a view, not a resource store.

## Sources

- `TerrainGeneratorPath`: required for generated resource markers, underground patches or starts.
  A live-resource-only overlay needs no generator: assign ResourceRootPath and disable
  ShowUndergroundResources and ShowStartPositions. All-disabled overlays also need no generator.
- `GridPath`: optional gameplay grid for native cell centers and polygon corners. Missing configured
  grids clear the overlay rather than drawing using an unrelated square fallback.
- `ResourceRootPath`: optional subtree of GridResourceNodeComponent instances. Surface/liquid
  markers then show live, nondepleted nodes within bounds rather than generated resources.
  Missing configured roots show no resource markers. Empty keeps the generated preview.
- `ProspectingPath`: optional GridProspectingComponent controlling discovery. Empty means no
  discovery restriction. A configured but missing node hides underground patches.
- `SubsurfaceStorePath`: optional GridSubsurfaceStoreComponent controlling remaining stock.
  Cells with no remaining units are hidden. A configured but missing store hides underground patches.
  Empty means the overlay shows generated deposits without depletion filtering.

Wire the store and prospecting component to the same generated data layers as this overlay's world.
Prospecting owns discovery; the store owns drawdown; the overlay does not copy either state.

## Display

`BoundsOrigin` and `BoundsSize` define the logical cell rectangle. Generated queries use local
sample coordinates; discovery, drawdown and geometry queries use absolute logical cells.
`TileSize` provides square spacing only when GridPath is empty. With a grid, patch polygons follow
its corners through both nodes' transforms; marker size uses the transformed cell edge lengths.
`ShowResources`, `ShowStartPositions`, and `ShowUndergroundResources` choose the batches.
`ResourceRadiusTiles` and `StartRadiusTiles` set marker sizes.
The shared TerrainLayers marker slot owns Z order.

Surface/liquid resources are colored by their resource category. Underground hue is stable per
resource ID; alpha represents generated richness, not remaining quantity. Depleted patches disappear
when a store is wired. Starts are generated locations, not live unit positions.

## Updates

Automatic refresh pauses while hidden, then catches up when a previously attempted view is shown.
Reattachment restores resource, survey, drawdown and grid bindings. Explicit Rebuild remains valid
while hidden. World-managed inactive bounds/origin remain synchronized.

`Rebuild()` resolves current paths, clears stale batches, reads the source and requests redraw.
`RefreshOnReady` schedules the initial runtime rebuild. Disable it when TerrainWorldComponent
drives the renderer. There is no public Refresh method.

DiscoveryChanged covers surveys, RevealAll changes and discovery restore. DepositChanged and
StateRestored cover extraction and subsurface restore. Grid GeometryChanged refreshes cached
positions after projection changes. The overlay coalesces these events into a
deferred rebuild and disconnects sources on exit. Call Rebuild after changing source paths or
display settings; the next rebuild binds the new nodes even if the previous nodes remain alive.

`CellPosition` and `CellOutline` return overlay-local centers and polygons using the same geometry
as drawing. TerrainWorldComponent binds the grid before rebuilding this overlay in flat views.
`UndergroundPatchCount` reports the current baked patch count. Draw only renders cached batches:
it does not scan the generator or emit missing-source warnings every frame.

## Verification And Remaining Work

`tests/terrain_survey_overlay_probe.gd` verifies hidden deposits, survey updates, depletion,
both restore paths, RevealAll, missing configured sources, rectangular/isometric cells, transformed
parents and nonzero origins. The lab probe checks marker/grid agreement after view switches.
Recipe restore and rendered OpenGL lab probes also pass with these bindings.

Live resource-root bindings refresh on gather, restore, node addition and removal, and disconnect
on exit. The shared TerrainResourceViewBinding reads balances from nodes; it does not store balances.
Direct property edits or source-path changes require Rebuild. A depletion QueueFree cannot be undone
by restoring a removed node; the game's object restoration must recreate that node.
Other terrain renderers still need their own nonzero-origin audit. Arbitrary transform edits require
a grid geometry notification or explicit Rebuild; this view does not monitor every scene transform.
