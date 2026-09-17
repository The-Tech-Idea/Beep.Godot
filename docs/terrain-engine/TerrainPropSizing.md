# TerrainPropSizing

One size policy for terrain props in every presentation. `TerrainWorldComponent.PropSizing`
is passed to the flat feature, flat relief and isometric feature renderers. Standalone
renderers default to `textures/terrain/terrain_prop_sizing.tres`, the same resource used by
the lab. `TerrainMapArt` selects artwork and filtering, not object dimensions.

## Units

Limits measure the longest **visible** sprite dimension in cell-edge units. Transparent
padding is excluded before fitting. Aspect ratio is preserved. Camera zoom does not change
these dimensions. Isometric fitting uses the diamond edge length, not its wider diagonal.

| Category | Cells | Pixels at a 64-pixel cell edge |
| --- | --- | --- |
| Trees and oasis palms | 1.75-2.25 | 112-144 |
| Bushes | 0.35-0.65 | 22-42 |
| Reeds | 0.20-0.35 | 13-22 |
| Small rocks | 0.25-0.40 | 16-26 |
| Large rocks | 0.45-0.65 | 29-42 |

Bushes are drawn by the flat feature renderer among the trees of woods and forest - the
understory (`BushesSheetPath`, `BushesPerWoodsTile`, `TerrainMapArt.Bushes`; see
`TerrainFeatureRendererComponent.md`) - through `SizeInCells("bush", ...)`, which until then had
no caller. Buildings and units retain their own gameplay footprints and are not terrain props.

`SizeInCells(kind, jitter)` varies around the category midpoint, then enforces both bounds.
It handles reversed ranges and non-finite input. `VisibleRegion` caches alpha bounds once
per source texture/layout; no image scan occurs per map cell. Call `ClearArtCache()` after
editing pixels or an atlas region in an existing runtime texture, then redraw the world.

## Authoring

Edit the shared resource for project-wide defaults or assign one sizing resource to several
worlds. Redraw after changing it. Old per-renderer SpriteScale, HillsScale, MountainsScale
and vegetation scale multipliers were removed; style resources no longer own size limits.
Density and placement are separate from dimensions and retain their existing controls.

Ground-texture repeat is art calibration rather than prop geometry. Both shipped profiles cut
their ground from the same 310-pixel atlas regions. `pixel_art.tres` repeats them every 4.8 cells,
one art pixel to one screen pixel at 64 pixels a cell, which is what pixel art is drawn at.
`cartoon.tres` repeats them every 1.6 cells (2026-09-16): at 4.8 the painted cobbles were 15-70
pixels and the starfish about 25, beside a 68-pixel pickup in Oilfield Days, so ground detail read
as objects the size of the units. `tools/build_terrain_style_assets.gd` writes both values; it
used to write 1.5 for both while the resources said 4.8. The original ground materials remain
intact. Changing art or size does not regenerate terrain, change cell records, move logical anchors
or alter navigation.

## Verification

`terrain_prop_sizing_probe.gd` checks transparent padding, finite bounds, measured flat/tile/
isometric sprite extents, shared-resource edits and unchanged live cells. The flat view draws two
size categories, so it is measured one kind at a time through `GetStampBoundsOfKind`: trees
(`woods`, `forest`, `jungle`) against the Trees range and bushes against the Bushes range. The isometric
autotile view draws the flat feature and relief renderers on its diamond cells (VIEW-01). Their
sprites are sized from the grid's cell corners, a path this probe does not measure separately.
`terrain_art_styles_probe.gd` checks original/pixel/cartoon switches, extreme jitter and
pixel-identical restoration of the original presentation within the updated sizing policy.
These checks establish consistent dimensions, not completion of visual art direction.
