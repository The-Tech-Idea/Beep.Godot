# TerrainIsometricFeatureRendererComponent

Draws woods, forest, jungle, marsh and oasis props on the current isometric
block surface. It is a view consumer, not a separate map model.

## Sources And Lifecycle

IsometricRendererPath selects TerrainIsometricRendererComponent. Its resolved
ITerrainSurfaceData supplies live cells or generated preview data. The feature
renderer uses field-local cells; the surface owner handles BoundsOrigin.

SurfaceRebuilt refreshes visible props synchronously. Hidden props defer the work and catch up
when shown. Explicit Rebuild remains available while hidden. Rebinding disconnects the old surface;
exit clears subscriptions/pending work, and reattachment reconnects previously attempted views.
RefreshOnReady optionally defers initialization; turn it off when TerrainWorldComponent owns the
build. The controller sets prop visibility/bounds before rebuilding the surface, so projection
switches retain synchronous prop updates without rebuilding the hidden projection.

BoundsSize limits the scan and is checked against the surface owner's bounds.
Seed controls deterministic per-cell/per-slot frame, scale and scatter hashes,
keyed by the surface owner's absolute cell rather than view-local coordinates.
TerrainGeneratorPath is currently resolved for failure diagnostics; it is not an
independent feature-data source.

## Placement

SurfacePosition, SurfaceLevel and SurfaceCorners supply the actual top face.
Corners are transformed through the terrain and feature nodes. Their edge vectors
define scatter axes; the shorter transformed cell edge defines sprite fit. There is
no second isometric projection formula in the feature renderer.

Trunks remain anchored to that surface; canopies extend above them. PropSizing,
PositionJitter and ScaleJitter control their appearance. SpritesPerTile sets the
base count; ForestExtraSprites adds clump density for forest and jungle.
Both projections use TerrainFeatureScatter: twelve candidates per requested stamp,
chosen for separation from earlier anchors in the cell and checked against fine
water membership from the surface owner. A single dry-centre fallback is allowed
when candidates fail; fully wet positions are not drawn. This does not constrain
an entire canopy or give decorative sprites physical collision footprints.

One LevelProps node draws each elevation band's stamps at TerrainLayers.ZForProps.
Stamps are sorted by local trunk Y within each band. This is batched decoration,
not a collection of physical or selectable tree objects.

## Sheets

WoodsSheetPath, JungleSheetPath, MarshSheetPath and OasisSheetPath select art.
WoodsColumns and WoodsRows currently define the frame grid for all four sheets.
Jungle/oasis fall back to woods if their sheet is absent; marsh does not.

WoodsFrameBindings accepts entries such as `tundra,snow=7`. The shared
TerrainFeatureFrameBindings parser restricts frames whenever woods is the chosen
sheet, including jungle/oasis falling back to woods. Malformed or out-of-range
entries warn; unbound terrain falls back to the whole sheet. Dedicated
jungle/marsh/oasis sheets use all their own frames.

Changing sheet paths invalidates the texture cache on Rebuild. Clearing a path
removes its cached art. Zero-sized atlas frames are skipped. Editing image contents
at an unchanged path is not a custom hot-reload mechanism.

## Diagnostics And Verification

GetLayerDiagnostics reports level, Z, relative-Z and stamp count.
GetStampAnchors returns actual cached trunk anchors in feature-local coordinates.

terrain_surface_height_probe verifies negative/nonzero origins, hills, native
surface queries, deterministic trunk anchors under separate rotations and
nonuniform scales, sheet removal/restoration, live flattening and flooding.
World-live-source and view-grid probes also pass.
TerrainWaterSurfaceSmoke additionally compares normalized flat/isometric anchors
over 32 seeds on a mixed-water cell. See FEATURE_SCATTER.md for shared behavior.

Transform-only changes require Rebuild or the owning surface's rebuild notification;
automatic parent-transform tracking is not implemented. These probes do not
certify canopy occlusion for every art sheet or provide physical tree collision.
