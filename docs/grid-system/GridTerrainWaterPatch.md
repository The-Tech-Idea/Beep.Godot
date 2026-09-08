# GridTerrainWaterPatch

Internal immutable sub-cell water mask stored in `GridCellDataComponent` records.
This is surface detail for an existing grid cell, not another world or generator.
`TerrainWorldComponent` coordinates generation; `TerrainGeneratorComponent`
transfers the fine masks together with the cell-level terrain.

## Representation

One bit per sample, row-major, with resolution from 1 to 24. Uniform generated
cells collapse to resolution 1. Snapshot encoding is Base64 containing a one-byte
resolution followed by the packed bits. The format uses a string so it also
survives JSON transports without packed-array type conversion. Decode validates
resolution and exact payload length.

## Surface Contract

At a cell border, queries retain generated sub-tile water membership. Inside the
central half-cell square (local X and Y strictly between 0.25 and 0.75), the
cell-level water classification wins. This keeps movement targets on their grid
side of a shoreline without flattening the whole cell. It is not a guarantee for
arbitrary collision radii or multi-cell building footprints.

The encoded mask retains the original bits; the central constraint is applied by
the sampler. This distinction matters when comparing raw generator-only captures
with live-grid captures. Generation majority/river reduction can disagree with
the raw fine field at individual centres.

## Editing And Saving

- Cell flags/crops/metadata preserve the patch.
- Explicit terrain painting replaces the patch with ordinary live-cell terrain.
- Grid snapshots include the patch; full and partial restore recover it.
- Coast caches compare immutable patch identities as well as cell water flags.
  Replacing/removing a patch invalidates the cache even if a cell stays dry.

`tests/terrain_water_surface_probe.gd` verifies generated samples, centre
constraints, same-kind painting, cache invalidation, binary snapshots, partial
restore, invalid-input atomicity and vegetation anchors across four seeds.
Its rendered mode checks 1024 cell centres at three zoom levels per seed.
