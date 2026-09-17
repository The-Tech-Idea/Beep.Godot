# TerrainDataLayersComponent

Generated recipe metadata queried directly from the published generated field.
Runtime mode creates no TileMapLayer children and does not copy the field arrays.
Set `MaterializeTileLayers = true` and rebuild for an explicit native authoring
view with nine invisible TileMapLayer children and ordinary Godot TileData.
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
origin and size and invalidates its cache when those bindings change. It no longer
writes TileSize: that is this component's own atlas size, not the view's cell size
(VIEW-02, 2026-09-15), so a projection switch cannot rebuild the generated data.

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
| StartAreaData (no layer property) | StartAreaAt |
| none; the published field in both modes | StartDistanceAt |
| LiquidData / LiquidLayer | LiquidResourceAt |
| UndergroundData / UndergroundLayer | UndergroundResourceAt, UndergroundRichnessAt, UndergroundDepthAt |

Queries take absolute cells. StartCells returns absolute cells too, in the
generator's start order in both modes: element k is start k. The runtime mode
keeps an ordered list beside the start set; the materialised StartData tiles
carry start_index, and StartCells sorts the used cells by it. StartAreaAt
returns which start's reserved area a cell is in, 0 for none and k+1 for start
k; it is 0 everywhere when the generator's StartAreaRadius is 0. It is the
recipe's reservation; the live copy is GridCellDataComponent.GetStartArea.
StartDistanceAt (FEAT-14) returns a cell's distance to the nearest start in whole
cells, or -1 off the published map and on a map that measured none (the
generator's StartDistanceScaling at 0). It reads the published field in both
modes and is never materialised as tiles: a tile per distinct distance would be
hundreds of tiles for a dense fact the recipe regenerates, and the live cells do
not store it either.
Other missing cells return empty strings, zero or false. Underground richness uses four bands;
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

All nine layers have collision and navigation disabled, with no physics or
navigation layers in their generated TileSets. The former body-generation
properties and square-body helpers were removed, not retained as compatibility
options. Recipe geometry could otherwise remain walkable after flooding and
occupy different world positions from an isometric view.

GridNavigationComponent remains the gameplay pathfinding authority. Native
CharacterBody2D collision needs explicitly authored geometry or a separate live,
projected physics integration; this metadata component does not supply it.

## Verification

terrain_data_storage_probe compares every query in both modes, including
StartAreaAt on a fixture with start areas, StartDistanceAt on a fixture that
measured every one of its 768 cells (and reads -1 off the map), off-map cells and
negative origins, the StartCells order, native-view creation/retirement, stable
underground identity and stale-source clearing. terrain_data_origin_probe explicitly
enables native mode and checks all nine layers are physically inert, all
queries (StartAreaAt and StartDistanceAt included) survive a shifted origin, old
cells clear, and shifted deposits can be surveyed and extracted.
terrain_world_recipe_probe reads StartDistanceAt through materialised layers and
checks a restore brings back the same distances. terrain_start_area_probe checks
in both modes that StartAreaAt agrees with the generator on every cell and that
StartCells()[k] is start k, re-inserting the materialised start tiles in reverse
so the start_index sort is what keeps the order. Recipe, live-source, survey, navigation-height and
playground probes cover the consuming paths. This does not certify an external
game migration or a live physics bridge.
