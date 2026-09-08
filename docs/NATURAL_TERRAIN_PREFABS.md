# Natural Terrain Prefabs

Instance `addons/beep_game_builder_cs/templates/scenes/natural_terrain_prefab.tscn`
or add `NaturalTerrainPrefabComponent` to a Node2D. The tool component previews
immediately in the editor. The gallery scene in the same folder contains all 12
material/elevation combinations.

## Inspector

- MaterialTheme: sandstone, basalt, limestone or granite.
- Elevation: full plateau or a separate small half/quarter-height piece.
- ArtScale: uniform scale; keep this equal across pieces to retain authored size differences.
- HideGreenBackground: shader-only chroma removal; source PNGs are never modified.
- ShowPlacementAnchors: editor crosses at the ground, front lip and lateral feet.
- SurfaceRisePixels: optional front-lip calibration in unscaled source pixels.
  Zero uses estimated defaults (250 full, 150 half, 70 quarter).

The node origin is the bottom-center of the visible rock bounds, excluding the
green canvas. Changing material or elevation keeps that ground contact fixed.
The left/right foot anchors mark the overall footprint extents, not individually
traced rock contact points. All anchor getters return local coordinates; use
`ToGlobal(...)` to compare anchors from different nodes.

Artwork is an internal child rebuilt at runtime, not baked into the scene.
Developer-added children survive refreshes. Put decorations under the component
or use sibling objects as appropriate. A common parent with Y Sort Enabled can
sort terrain and actors by their ground origins. This does not implement jumping,
elevation-aware occlusion or character surface transitions. The source image is
not a collision map.

## Optional Geometry

`BlockingEnabled` defaults to false. Enable it to create a StaticBody2D ground
footprint and choose `BlockingLayer` to match your character's collision mask.
The blocker is solid: it does not automatically let a jumping character land on
top or distinguish actors on different elevations. Leave it off until your
controller's collision-layer rules are configured.

`FootprintOutline` and `SurfaceOutline` are independent Vector2 arrays in the
Inspector. Coordinates are normalized within the visible rock bounding box:
(0,0) is top-left, (1,1) is bottom-right. List points around the perimeter without
repeating the first point. Empty arrays use approximate eight-point defaults.
Custom points persist across material/height changes and scale with ArtScale.
They are edited through the Inspector array, not draggable viewport handles.
Enable ShowPlacementAnchors to see both outlines in the editor.

The default surface covers the estimated top; the default ground footprint is
that outline projected downward by the cliff rise. These are starting estimates,
not exact rock-edge tracing. Adjust the points for each authored sprite. Invalid
polygons are rejected with a configuration warning; an invalid footprint cannot
block movement. Refresh changes only internal physics nodes, not user children.

`GetSurfacePolygon()` and `GetFootprintPolygon()` return local-space points.
`ContainsSurfacePoint(globalPoint)` tests whether a world-space point falls
inside the top outline. It does not move actors or establish their elevation.

## Export

Use Godot's Export all resources mode, or explicitly include all 12 PNG resources
under `generated/natural_rock_walls/v1` and `small_plateaus`, plus the
`natural_terrain_green.gdshader` resource. Paths are selected dynamically. Keep the
imported PNG resources in the exported pack, not just loose source image files.

The chroma shader is specific to these four rock-only materials. It is not suitable
for grass artwork. Height labels are authored visual targets, not measured world units.

## Verification

Run `tests/NaturalTerrainPrefabProbe/run.ps1` with a Godot Mono executable via
`-Godot`; add `-Render` for a GPU preview. The isolated probe covers all 12 choices,
ground alignment, refresh ownership, selection updates, anchor scale, surface
queries, collision-layer filtering, disabling collision and invalid outlines. It does
not load unrelated main-project plugins or autoloads.
