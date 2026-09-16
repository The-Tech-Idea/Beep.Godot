# TerrainTileSets

Shared TileSet construction and generated tile metadata schema. This utility
does not create physics bodies or native navigation regions.

## Schema

Cell defines fourteen custom-data names: terrain, resource, feature, relief,
is_water, passable, continent, start_position, liquid_resource,
underground_resource, underground_richness, underground_depth, start_index and
start_area. start_index (int) is a start cell's index in the generator's start
order; start_area (int) is the start-area id, 0 none and k+1 for start k. Both
are appended after underground_depth, because layer indices are what authored
tiles reference.

Custom data belongs to a tile shared by its cells. It does not represent mutable
per-cell stock, occupancy or player edits.

## Public API

- Create(tileSize, isometric) builds a new TileSet and defines its custom data.
  Isometric uses DiamondDown layout with horizontal offset axis.
- DefineCellData adds missing schema names; existing named layers remain unchanged.
- Describe writes terrain, resource and feature plus derived drawing relief,
  water and conventional ground passability.
- DescribeRelief writes the explicit TerrainRelief band, distinct from drawing Z.
- DescribeContinent, DescribeLiquid and DescribeUnderground write the
  corresponding generated metadata. Underground richness is clamped to 0..1.
- DescribeStart(data, index) sets start_position true and start_index to the
  start's index, so a materialised start tile says which start it is.
- DescribeStartArea(data, area) writes start_area (k+1 for start k) onto the
  tile that stands for that area.
- IsWaterKind recognizes deep_water, shallow_water and water.
- IsLandKind requires a nonempty kind that is not water.
- GroundOf classifies water as Water, rock/lava as Steep, other kinds as Land.
  This is descriptive classification, not live navigation policy.
- Kinds exposes the ordered terrain catalogue, including lava.
- Save creates the destination directory and uses ResourceSaver.Save, returning
  the resulting Error. Blank paths return InvalidParameter.

DefineBody and ShapeCell were removed with recipe-layer body generation.
Gameplay pathfinding uses GridNavigationComponent and live GridCellDataComponent;
physical geometry must not be inferred from a stale generated metadata atlas.
