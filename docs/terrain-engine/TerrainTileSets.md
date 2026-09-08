# TerrainTileSets

Shared TileSet construction and generated tile metadata schema. This utility
does not create physics bodies or native navigation regions.

## Schema

Cell defines twelve custom-data names: terrain, resource, feature, relief,
is_water, passable, continent, start_position, liquid_resource,
underground_resource, underground_richness and underground_depth.

Custom data belongs to a tile shared by its cells. It does not represent mutable
per-cell stock, occupancy or player edits.

## Public API

- Create(tileSize, isometric) builds a new TileSet and defines its custom data.
  Isometric uses DiamondDown layout with horizontal offset axis.
- DefineCellData adds missing schema names; existing named layers remain unchanged.
- Describe writes terrain, resource and feature plus derived drawing relief,
  water and conventional ground passability.
- DescribeRelief writes the explicit TerrainRelief band, distinct from drawing Z.
- DescribeContinent, DescribeStart, DescribeLiquid and DescribeUnderground write
  the corresponding generated metadata. Underground richness is clamped to 0..1.
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
