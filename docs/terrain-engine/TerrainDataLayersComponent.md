# TerrainDataLayersComponent

Generated recipe metadata queried directly from the published generated field.
Runtime mode creates no TileMapLayer children and does not copy the field arrays.
Set `MaterializeTileLayers = true` and rebuild for an explicit native authoring
view with eight invisible TileMapLayer children and ordinary Godot TileData.
TerrainWorldComponent owns the recipe; GridCellDataComponent owns mutable terrain.
These layers do not generate collision bodies or navigation regions.

## Configuration

TerrainGeneratorPath selects the recipe source. BoundsOrigin is the absolute
starting cell and BoundsSize the number of cells. TileSize controls metadata atlas
dimensions, not gameplay projection. RefreshOnReady schedules a runtime rebuild;
disable it when TerrainWorldComponent drives initialization. TileSize only
affects the optional native view. Changing storage mode requires Rebuild.

Rebuild publishes the field with its absolute bounds. Native mode additionally
writes tiles at BoundsOrigin + local cell. It resolves the current generator path every time;
a missing source clears previous layer contents. World supplies the generator,
origin, size and tile size and invalidates its cache when those bindings change.

## Layers And Queries

Queries work in both modes. Layer properties are null in default runtime mode.
Native mode supports direct `get_cell_tile_data` inspection and TileSet authoring;
it intentionally carries additional storage and rebuild cost.

| Layer / Property | Queries |
| --- | --- |
| TerrainData / TerrainLayer | GeneratedTerrainAt, IsWaterAt, PassableAt |
| ResourceData / ResourceLayer | ResourceAt |
| FeatureData / FeatureLayer | FeatureAt |
| ReliefData / ReliefLayer | ReliefAt |
| ContinentData / ContinentLayer | ContinentAt |
| StartData / StartLayer | IsStartPositionAt, StartCells |
| LiquidData / LiquidLayer | LiquidResourceAt |
| UndergroundData / UndergroundLayer | UndergroundResourceAt, UndergroundRichnessAt, UndergroundDepthAt |

Queries take absolute cells. StartCells returns absolute cells too. Missing cells
return empty strings, zero or false. Underground richness uses four bands;
check the resource ID before interpreting richness or depth. ReliefAt returns
TerrainRelief bands, not the drawing Z value derived from terrain kind.

Atlas tiles are shared by distinct values, not mutable per-cell records.
GeneratedTerrainAt, FeatureAt and PassableAt describe the recipe, not subsequent
player edits. Use live GridCellDataComponent and GridNavigationComponent for
current terrain and movement; use GridSubsurfaceStoreComponent for remaining
underground stock and GridProspectingComponent for discovered deposits.

## Physical Ownership

UndergroundIdentity is a SHA-256 fingerprint of the published underground IDs,
richness bands, depth bands and absolute bounds. It is computed once per rebuild,
stays stable for the same deposit map and clears when the source is unavailable.
The hash streams through SHA-256 without retaining a map-sized byte buffer. It
is identical in direct-field and native modes.
GridSubsurfaceStoreComponent uses it to prevent depletion from leaking between maps.

All eight layers have collision and navigation disabled, with no physics or
navigation layers in their generated TileSets. The former body-generation
properties and square-body helpers were removed, not retained as compatibility
options. Recipe geometry could otherwise remain walkable after flooding and
occupy different world positions from an isometric view.

GridNavigationComponent remains the gameplay pathfinding authority. Native
CharacterBody2D collision needs explicitly authored geometry or a separate live,
projected physics integration; this metadata component does not supply it.

## Verification

terrain_data_storage_probe compares every query in both modes, including
off-map cells and negative origins, native-view creation/retirement, stable
underground identity and stale-source clearing. terrain_data_origin_probe
explicitly enables native mode and checks all eight layers are physically inert,
all queries survive a shifted origin, old cells clear, and shifted deposits can
be surveyed and extracted. Recipe, live-source, survey, navigation-height and
playground probes cover the consuming paths. This does not certify an external
game migration or a live physics bridge.
