# TerrainCollisionComponent

Runtime native collision consumer of GridProjectionComponent and live
GridCellDataComponent. It creates no terrain model, recipe or navigation map.

## Wiring

Author a Node2D with this script. Assign GridPath and CellDataPath plus absolute
BoundsOrigin and BoundsSize. Alternatively set TerrainWorldComponent.CollisionPath:
the world supplies the live source, bounds and active grid, then rebuilds collision
after switching the view. Set RefreshOnReady=false when the world owns initialization.

LandCollisionLayer defaults to 0 (no land shapes), WaterCollisionLayer to 4 and
SteepCollisionLayer to 8. Set the moving body's collision mask accordingly.
The grid playground's truck uses mask 12 to collide with water and rock/lava.
Ground classification uses TerrainTileSets.GroundOf and normalized live terrain.
An empty terrain kind produces no shape. Layer masks of zero suppress that category.

## Implementation

One StaticBody2D per enabled ground category holds ConvexPolygonShape2D shapes
through native shape owners. There is no Node per cell. Cell corners come from
GridProjectionComponent and are transformed into body-local coordinates, including
the active elevated surface. Collision masks on the terrain bodies themselves are 0.

Shape owners are grouped into 32x32 gameplay chunks. Manual top-down and flat
isometric grids merge same-category rectangles within each chunk; native TileMap
and elevated grids retain exact per-cell polygons. Individual CellChanged events
queue affected chunks. Bulk CellsChanged compares chunk revisions and availability;
grid GeometryChanged queues a full rebuild. Process-frame updates coalesce edits
and avoid changing physics shapes inside collision callbacks. Rebuild can be called
explicitly outside physics callbacks after changing paths, bounds, masks or independent
parent transforms. Missing sources or missing surface geometry remove stale shapes.
ShapeCount reports native shapes, which may cover multiple cells.
ChunksRebuiltLastUpdate reports incremental work. Unavailable chunks have no shapes.

RequestRebuild schedules replacement using ChunksPerFrame (default 4, clamped
to 1..64). PendingChunkCount reports queued chunks; IsUpdating also includes
pending full/revision scans. Each process callback builds or retires at most the
chunk budget. Scheduling scans chunk metadata, not every cell. Old shapes remain
until their chunk is processed, but pending chunks are not ready for motion.
Use TerrainMotionGateComponent for supported direct movers during replacement.
Rebuild remains immediate and cancels queued work. Source/path, bounds, mask and
independent transform changes require either explicit rebuild method.

IsChunkReady(chunk) requires current content/availability and a physics frame after
shape publication. TerrainMotionGateComponent uses this to hold direct movement
until an archive-loaded chunk has usable collision. IsReady checks the entire
tracked set, including pending work, failures, availability and physics readiness.
Initial Rebuild remains synchronous; background world generation uses RequestRebuild
and waits for IsReady. This is not yet camera/actor-demand collision streaming.

Readiness is recorded only if every required polygon in the chunk was built.
Missing polygons, singular transforms, nonfinite coordinates and zero-area
polygons reject that chunk. Any shapes already created for a failed chunk are
removed, and FailedChunkCount exposes the result. Correct the geometry source or
transform and call Rebuild to retry; a successful rebuild clears the failure.
No substitute rectangle or default terrain is published for failed geometry.

## Verified Scope

terrain_collision_probe uses actual physics point queries and CharacterBody2D motion
tests. It covers nonzero/negative bounds, transformed native square/isometric grids,
live flood/unflood, category masks, source removal, raised terrain, live flattening,
and missing elevated projection. The authored grid playground includes the bridge.

This represents top-surface terrain categories only. It does not generate cliff-side
barriers, enforce ramp transitions, add building occupancy collision, or replace
GridNavigationComponent's movement rules. Moving parent transforms independently
requires an explicit rebuild/geometry notification. A uniform million-cell manual
grid is verified to merge into 1024 shapes. This is not a worst-case shape-count
or frame-time guarantee: native/elevated chunks can still contain 1024 polygons.
terrain_collision_budget_probe covers budget clamping, edit coalescing, pending
readiness, bounds shrink, synchronous cancellation, missing sources, automatic
scheduling and detach/reattach.
