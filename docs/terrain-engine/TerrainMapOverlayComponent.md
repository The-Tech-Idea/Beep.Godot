# TerrainMapOverlayComponent

A Node2D that batches player start rings, start-area borders and underground survey patches.
TerrainWorldComponent shows it under Painted, Tiles and IsometricAutotile (every view that binds the
gameplay grid to a native layer). It is a view, not a resource store.

It draws **no surface or liquid resources**. `TerrainResourceRendererComponent` is the one resource
drawer, for generated and live resources alike. Until VIEW-13 (2026-09-15) this overlay also drew
the same cells as category-coloured discs (`ShowResources`, `ResourceRootPath`,
`ResourceRadiusTiles`). A scene wiring both showed discs under icons, and a scene wiring only the
overlay showed a Strategic/Luxury/Bonus classification the icon sheet does not.

## Sources

- `TerrainGeneratorPath`: required while `ShowStartPositions`, `ShowStartAreas` or
  `ShowUndergroundResources` is on. An overlay with all three off needs no generator.
- `GridPath`: optional gameplay grid for native cell centers and polygon corners. Missing configured
  grids clear the overlay rather than drawing using an unrelated square fallback.
- `ProspectingPath`: optional GridProspectingComponent controlling discovery. Empty means no
  discovery restriction. A configured but missing node hides underground patches.
- `SubsurfaceStorePath`: optional GridSubsurfaceStoreComponent controlling remaining stock.
  Cells with no remaining units are hidden. A configured but missing store hides underground patches.
  Empty means the overlay shows generated deposits without depletion filtering.
- `StartAreaPath` (FEAT-10): optional `GridStartAreaComponent`. Wired, a start is drawn in the colour
  of the **faction** that holds it rather than in this overlay's fixed per-index palette, so a
  player's region here, their tint on the minimap and their colour in a lobby are one fact. Empty (or
  a configured node that is missing) simply leaves the palette in charge — this path is an upgrade,
  not a requirement.

Wire the store and prospecting component to the same generated data layers as this overlay's world.
Prospecting owns discovery; the store owns drawdown; the overlay does not copy either state.

## Display

`BoundsOrigin` and `BoundsSize` define the logical cell rectangle. Generated queries use local
sample coordinates; discovery, drawdown and geometry queries use absolute logical cells.
`TileSize` provides square spacing only when GridPath is empty. With a grid, patch polygons follow
its corners through both nodes' transforms; ring size uses the transformed cell edge lengths.
`ShowStartPositions`, `ShowStartAreas` and `ShowUndergroundResources` choose the batches;
`StartRadiusTiles` sets the ring size. The shared TerrainLayers marker slot owns Z order.

Underground hue is stable per resource ID; alpha represents generated richness, not remaining
quantity. Depleted patches disappear when a store is wired. Starts are generated locations, not live
unit positions.

### Start areas (FEAT-09)

`ShowStartAreas` (default false) outlines each start's generated area and its headquarters
footprint. A map generated with `StartAreaRadius` 0 has no areas and draws nothing here.

- **Borders.** `TerrainOverlayEdges.Collect` walks the bounds and returns every side where a cell's
  `StartAreaAtCell` id differs from its neighbour's. Cells outside the bounds read as 0, so an area
  touching the map edge is closed there.
- **Headquarters outlines.** The same walk runs over each report's footprint rectangle
  (`TerrainStartAreaReport.Origin` and `Footprint`).
- **Geometry.** `TerrainOverlayEdges.SharedSide` turns each (cell, neighbour) pair into the side
  their two `CellOutline` polygons share, so the lines follow a square or diamond grid.
- **Colour.** `StartColour(k)` answers the faction's colour when a `GridStartAreaComponent` is wired
  *and* `ColourOfStart(k)` comes back with a non-zero alpha; otherwise entry `k mod 24` of a fixed
  palette. Headquarters outlines are drawn thicker and lightened. A shadow pass is drawn under every
  segment first.
- **Rings.** A start ring takes its start's colour only while `ShowStartAreas` is on. With it off
  the rings stay cream, as before.

`StartAreaSegmentCount` reports the baked border and outline segments; it is 0 with
`ShowStartAreas` off. Segments are baked in `Rebuild` and drawn under the start rings. Each baked
`AreaSegment` carries the start index it belongs to alongside its two endpoints, its colour and its
headquarters flag, and the two internal test hooks `StartAreaSegmentColour(i)` and
`StartAreaSegmentStart(i)` read them back (transparent and `-1` for an index that is not a segment).
They exist so a probe can assert *which start* a segment is drawn for and *what colour* it got,
without re-deriving either from the geometry.

### Faction colours (FEAT-10)

**The fixed palette is not a fallback for missing configuration.** It is the answer on a map with no
factions at all — which is most maps — and it is why an unassigned start still reads as a distinct
start rather than as nothing. `ColourOfStart` answers transparent for a start nobody holds, and the
alpha test above is what turns that into "use the palette" instead of "draw an invisible border".
So there are two separate questions here and each has a real answer: *nobody is assigned* gets the
palette, *this faction is assigned* gets its colour.

**The export is a start-area path, not a catalog path.** The design called for a `FactionCatalogPath`
overriding the palette, and a catalog alone cannot answer the question this overlay asks: it maps a
faction to a colour, and only the assignment knows which faction holds start k. Reading
`ColourOf(k + 1)` would have assumed catalog order **is** start order, which is exactly what
`LockedStart` breaks — and it would have been a second, divergent copy of the resolution the minimap
already does. Both views ask `GridStartAreaComponent.ColourOfStart(k)`, and
`tests/addon_contract_scan.ps1` refuses either of them containing the bare call `ColourOf(` at all.

The segments are baked **with** their colours, so a start changing hands has to re-bake rather than
merely redraw: `ResolveSources` subscribes to `GridStartAreaComponent.AssignmentChanged` with
`QueueRebuild`, and `DisconnectSources` releases it alongside the prospecting, grid and store wires.

## Updates

Automatic refresh pauses while hidden, then catches up when a previously attempted view is shown.
Reattachment restores survey, drawdown, grid and start-assignment bindings. Explicit Rebuild remains
valid while hidden. World-managed inactive bounds/origin remain synchronized. Across an assembly
reload (`ISerializationListener`) the grid, prospecting, store and start-area events are released and
rebound. `ResolveSources` re-reads all four paths and rebinds only when one of the resolved nodes
actually changed.

`Rebuild()` resolves current paths, clears stale batches, reads the source and requests redraw.
`RefreshOnReady` schedules the initial runtime rebuild. Disable it when TerrainWorldComponent
drives the renderer. There is no public Refresh method.

DiscoveryChanged covers surveys, RevealAll changes and discovery restore. DepositChanged and
StateRestored cover extraction and subsurface restore. Grid GeometryChanged refreshes cached
positions after projection changes. AssignmentChanged covers a start changing hands, an AutoAssign
and a restored save. The overlay coalesces these events into a
deferred rebuild and disconnects sources on exit. Call Rebuild after changing source paths or
display settings; the next rebuild binds the new nodes even if the previous nodes remain alive.

`CellPosition` and `CellOutline` return overlay-local centers and polygons using the same geometry
as drawing. TerrainWorldComponent binds the grid before rebuilding this overlay in each grid-bound view.
`UndergroundPatchCount`, `StartMarkerCount` and `StartAreaSegmentCount` report the current baked batches. Draw only renders
cached batches: it does not scan the generator or emit missing-source warnings every frame.

## Verification And Remaining Work

`tests/terrain_survey_overlay_probe.gd` verifies hidden deposits, survey updates, depletion,
both restore paths, RevealAll, missing configured sources, rectangular/isometric cells, transformed
parents and nonzero origins. The lab probe checks marker/grid agreement after view switches.
`tests/terrain_view_parity_probe.gd` asserts one start ring per generated start position under every
grid-bound projection of the lab. `tests/addon_contract_scan.ps1` forbids the removed resource-disc
members from regrowing, requires this file to contain `ColourOfStart(` and refuses it containing the
bare call `ColourOf(`. `tests/terrain_start_area_play_probe.gd` requires `StartAreaSegmentCount`
to equal every area border side plus every headquarters footprint perimeter, and 0 with
`ShowStartAreas` off.

`tests/terrain_faction_assignment_probe.gd` (FEAT-10) checks both halves of the colour rule on one
generated map: with `StartAreaPath` wired, every segment whose start is held is drawn in exactly that
faction's authored colour — `alpha`'s red on the start it was assigned and `bravo`'s blue on the
start it is *locked* to, which catalog order would not have given it — and then, with the path
cleared and the overlay rebuilt, segment 0 is still an opaque palette colour rather than nothing.
**Mutation:** an overlay that ignores the assignment fails the first half.
Other terrain renderers still need their own nonzero-origin audit. Arbitrary transform edits require
a grid geometry notification or explicit Rebuild; this view does not monitor every scene transform.
