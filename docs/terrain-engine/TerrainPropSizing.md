# TerrainPropSizing

One size policy for terrain props in every presentation. `TerrainWorldComponent.PropSizing`
is passed to the flat feature, flat relief and isometric feature renderers. Standalone
renderers default to `textures/terrain/terrain_prop_sizing.tres`, the same resource used by
the lab. `TerrainMapArt` selects artwork, not object dimensions — and not filtering either: how
each view samples its art is stated per renderer, in `TEXTURE_FILTERS.md`.

## Units

Limits measure the longest **visible** sprite dimension in cell-edge units. Transparent
padding is excluded before fitting. Aspect ratio is preserved. Camera zoom does not change
these dimensions. Isometric fitting uses the diamond edge length, not its wider diagonal.

## Art is never magnified

`DrawnPixels(visiblePixels, tilePixels, kind, jitter)` is the one place a prop's size is decided,
and it caps the category size at the art's own resolution. A sprite drawn larger than the pixels it
holds cannot gain detail, only blur — so the renderers scale art down and never up, which is how 2D
strategy engines keep sprites crisp: the art is authored at the size it is drawn at.

That cap matters because the category size is normalised by each frame's longest side, so the frames
holding the fewest pixels were stretched hardest. Measured on the shipped cartoon sheets at a
64-pixel cell (2026-09-18, after the owner reported blurry trees): seven of the eight tree frames
hold 58x69 to 58x105 visible pixels and were drawn at 1.45x to 2.09x, bushes and rocks up to 1.49x.
They are now drawn at their own 58x120 and 28x26, and `forest_trees.png`, whose frames hold 314x314,
is unaffected — it still takes the full 1.75-2.25 cells, being larger than any size asked of it.

Those sheets were replaced later the same day by the owner's four-style set (see `PROP_ART_BRIEF.md`).
The frame grid is identical, so the cap behaves the same: a tree frame now holds 39x69 to 60x124
visible pixels and is drawn at exactly that.

Capping the size is half of keeping a sprite crisp; the other half is the filter it is sampled
with, because a camera can still magnify what the renderer did not. The prop renderers therefore
sample nearest above their mip chain — see `TEXTURE_FILTERS.md`.

The consequence is that art too small for its category is drawn smaller than the category asks.
That is the honest outcome: a tree sheet meant to fill 2 cells at a 64-pixel tile has to carry
128 pixels, and more than that for a camera that zooms in. `PROP_ART_BRIEF.md` states the sizes.

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

Ground-texture repeat is art calibration rather than prop geometry, and it is the same for every
style: **1.6 cells**. All four profiles cut their ground from the same 310-pixel atlas regions, so
one repeat covers 102 screen pixels at a 64-pixel cell and the art is minified about three times.

That number is what keeps ground art reading as ground. `cartoon.tres` was moved to it on
2026-09-16 - at 4.8 (one art pixel to one screen pixel, which is what pixel art is drawn at) the
painted cobbles were 15-70 pixels beside a 68-pixel pickup in Oilfield Days, so ground detail read
as objects the size of the units. `pixel_art.tres` was left at 4.8, and on 2026-09-18 the owner
reported the other half of the same defect: at 1:1 the atlas's sand draws its **starfish at 22
pixels and its shells at 12-18**, against a bush this resource draws at 22-42. Measured, not
guessed. At 1.6 that starfish is 7 pixels.

The cost is a tighter repeat, which the shader's art-style pass hides by mirroring alternate
repeats. The clean fix is ground art with no prop-sized objects painted into it.
`tools/build_terrain_style_assets.gd` writes every profile, so the value lives there. The original ground materials remain
intact. Changing art or size does not regenerate terrain, change cell records, move logical anchors
or alter navigation.

## Verification

`terrain_prop_sizing_probe.gd` checks transparent padding, finite bounds, measured flat/tile/
isometric sprite extents, shared-resource edits and unchanged live cells. The flat view draws two
size categories, so it is measured one kind at a time through `GetStampBoundsOfKind`: trees
(`woods`, `forest`, `jungle`) and bushes. It measures both rules. Against the shipped cartoon art,
no stamp may exceed the largest visible frame its own sheet holds — each view against its own sheet,
since the isometric view binds 109x150 tree art where the flat view binds 58x120 — and the largest
stamp must actually reach it, so the rule cannot pass by drawing everything tiny. Against
`forest_trees.png`, whose frames are larger than any size asked of them, every stamp must land inside
the category range, in the flat view and the isometric one. **Mutation:** dropping the cap fails it
with "art smaller than the category was magnified to (107.6, 128.0)" — a 58x69 tree blown up. The isometric
autotile view draws the flat feature and relief renderers on its diamond cells (VIEW-01). Their
sprites are sized from the grid's cell corners, a path this probe does not measure separately.
`terrain_art_styles_probe.gd` checks original/pixel/cartoon switches, extreme jitter and
pixel-identical restoration of the original presentation within the updated sizing policy.
These checks establish consistent dimensions, not completion of visual art direction.
