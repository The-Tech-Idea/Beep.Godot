# TerrainShorelineStage

Internal generation stage, called after tile reduction and terrain scale cleanup,
before resources, vegetation and start positions are placed.

## Contract

For climate-driven presets, fine Euclidean distance to ocean samples classifies
dry samples within `BeachWidth` as sand. Widths are in cells, with no minimum
whole-cell ring. Zero disables the band. Explicit themed presets retain their
ground policy. Flat lake banks use their own fine Euclidean inset and
`LakeShoreWidth`, independently of ocean width. Rivers are not lake banks.

The stage updates both the cell material and its dry samples. It does not change
water samples, water-body labels, elevation, relief, land coverage or seed. It
runs only during generation, never during redraw or player terrain editing.

## Why It Exists

Sub-cell sand can lose a majority vote in gameplay cells. Generated live cells
therefore retain `terrain_shore_inland`, `terrain_beach_width`, and
`terrain_lake_shore_width` metadata. `lake_surface` stores bit-packed fine lake
membership separately from all-water geometry, including through save/load.
The painter uses these with the shared coast field to retain sub-cell beaches.
Its separate cached lake-distance field reconstructs lake banks without
reintroducing the cell-majority sand edge. `TerrainBiomeStage` no longer burns
lake sand into the underlying biome before this stage.
Explicit terrain painting clears this metadata in edited cells. The earlier
minimum whole-cell ring was rejected visually and has been replaced.

`terrain_coastal_grass_probe.gd` checks 36 seed/shape/width cases against an
independent distance oracle. `terrain_beach_footprint_probe.gd` checks fixed
land/water footprint. The display cache smooths distance before GPU sampling;
its output is not an exact metric offset. A generated-cell centre discrepancy
remains tracked in `plans/TERRAIN_VIEW_GRID_INTEGRATION.md`.

`terrain_lake_bank_probe.gd` checks an analytic circular lake at widths 0, 0.25,
1 and 2, separate ocean/river rules, saved patches, explicit edits, and 10,800
GPU radial samples across Original, Pixel Art and Cartoon.
