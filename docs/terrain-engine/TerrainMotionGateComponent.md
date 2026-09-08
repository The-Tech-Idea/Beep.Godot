# TerrainMotionGateComponent

Optional native movement readiness gate for floating CharacterBody2D actors.
It requests terrain through existing chunk pins and never generates another world.

## Wiring

Author this Node directly under the CharacterBody2D, beside its controller.
Assign GridPath, CellDataPath and CollisionPath to the same services used by
TerrainCollisionComponent. Enable AutoLoadPinnedChunks on the existing
GridCellArchiveComponent when unavailable chunks should load automatically.
The companion prefab demonstrates this wiring in the party lab.

Existing controllers call the gate through CharacterMotion after final dash and
knockback velocity arbitration. Custom movers must call PrepareMotion(motion)
each physics tick before native integration; motion is a world-space displacement,
not velocity. A false result means do not integrate the body this tick.

## Behavior

- Enabled defaults to true. Disabling or detaching releases this gate's pins.
- MaximumDemandChunks defaults to 16 and is clamped to 1..256 at use.
- DemandedChunkCount reports the current lease size.
- WaitReason is empty after successful preparation. Failure reasons include
  missing_sources, unsupported_projection_or_body, invalid_motion,
  invalid_footprint, outside_world, motion_demand_limit, pin_rejected,
  terrain_loading and collision_pending.
- PrepareMotion reads enabled native collision shapes and their transforms,
  expands conservatively for movement and sliding, and pins intersected chunks.
- Collision readiness requires matching cell content and availability, then a
  physics-frame boundary after collision shape publication.
- Stationary preparation sheds unnecessary forward demand. No preparation for
  two physics ticks expires the gate's lease. Actor/job pins are independent.
- Missing files never become traversable through a fallback; the archive's
  existing retry policy applies while demand is maintained.

## Scope

Supports floating bodies on manual top-down/isometric or native square/isometric
TileMap grids. Elevated terrain and grounded/platformer bodies fail closed and
need a height-aware query. Moving platforms and external teleports are not
qualified. This gate does not replace native collision, navigation, simulation,
or frame-budgeted collision generation. The conservative footprint can request
nearby chunks beyond the straight-line destination.

The terrain_motion_gate_probe covers an actual archived chunk, controller motion,
water collision after load, dash/knockback, shape bounds, edits and lease cleanup.
The actor_party_lab_probe covers the authored scene and native TileMap wiring.

Reference: [Godot Shape2D bounds](https://docs.godotengine.org/en/stable/classes/class_shape2d.html)
and [CharacterBody2D integration](https://docs.godotengine.org/en/stable/classes/class_characterbody2d.html).
