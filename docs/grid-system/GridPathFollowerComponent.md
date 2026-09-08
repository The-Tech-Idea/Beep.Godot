# GridPathFollowerComponent

HasReachedDestination distinguishes successful arrival from stopped movement. It becomes
true before DestinationReached, resets on installation of a new route or CancelMove,
and stays false after an active route fails. GridWorkerComponent checks this outcome
and the accepted job destination before starting work.

Grid-system component (a `GameplayComponent`, so `Node` → `EntityComponent` → `GameplayComponent`, `[Tool][GlobalClass]`) that moves its parent `Node2D`/`CharacterBody2D` along a path — either one it fetches itself from `GridNavigationComponent`, or one handed to it directly as cells or world points. The doc comment frames it as the ready-made movement loop for "workers, trucks, RTS units, town NPCs, or enemies that need simple top-down/isometric grid navigation without writing a movement loop." It is attached as a *child* of the body it drives (its `ResolveReferences` reads `GetParent() as Node2D`), the same attachment shape `ControllerComponent` uses for `ResolveBody2D`, though this class does not derive from `ControllerComponent` and does not reuse that helper.

Movement follows each segment rather than cutting across corners. `AdvancePath(delta)` is called automatically during physics processing; callers using simulation time must disable automatic physics processing before advancing it manually.

Direct `Node2D` movement consumes `Speed * delta` across as many waypoints as that distance permits. Every crossed cell edge is revalidated against live navigation, and every reached waypoint updates grid-object identity and emits its signal. Cancellation or route replacement from a callback stops the current update. Arrival is reported in the same update that reaches the final point. Direct movement reaches waypoint centers exactly, with a 0.0001-pixel floating-point tolerance; it does not use `StopDistance` to skip distance.

With `DriveCharacterBody` enabled on a `CharacterBody2D`, movement uses `Velocity` and `MoveAndSlide()` once per update. This branch uses the physics timestep and `StopDistance`; it does not implement the direct mover's multi-segment simulation-time budget.

Cell routes begin at their first cell, inserting the body's current cell when necessary and validating connectivity through navigation. Only arbitrary world-point routes use the closest starting waypoint; these routes do not provide terrain traversal validation. Loose Variant parsing delegates to `GridVariantReader`.

## Public API
- `[Signal] PathStarted(int length)` / `WaypointReached(int index, Vector2 position)` / `DestinationReached(int x, int y)` / `MoveFailed(int x, int y, string reason)`.
- `[Export] NodePath GridPath / NavigationPath` - explicit wires to `GridProjectionComponent` / `GridNavigationComponent`; there is no scene-wide fallback.
- `[Export] float Speed` (default 140) / `float StopDistance` (default 2) / `bool DriveCharacterBody` (default true) / `bool RotateToMovement` (default false) / `bool SetZIndexFromY` (default true) / `int ZIndexOffset` / `bool SnapToDestination` (default true).
- `public bool IsMoving { get; }`, `public Vector2I DestinationCell { get; }`, `public int CurrentWaypointIndex`.
- `public float EffectiveSpeed / EffectiveStopDistance` — sanitized (non-negative, finite) reads of `Speed`/`StopDistance`.
- `public override void _Ready()` — resolves references, disables physics processing in the editor, updates configuration warnings.
- `public override string[] _GetConfigurationWarnings()` — warns on non-positive `Speed` or negative `StopDistance`.
- `public bool MoveToCell(Vector2I goal)` — resolves the body's current cell via `GridProjectionComponent`, asks `GridNavigationComponent.FindCellPath`, and starts following the result; emits `MoveFailed` (`missing_body_grid_or_navigation` or `no_path`) and returns `false` on failure.
- `public bool MoveToWorld(Vector2 goalWorld)` — converts to a cell via the grid and calls `MoveToCell`.
- `public bool SetCellPath(Godot.Collections.Array cells)` — accepts loose `Variant` entries (`Vector2I`/`Vector2` via `GridVariantReader.TryReadCell`), converts to world points, sets `DestinationCell` to the last valid cell, and starts the walk.
- `public bool SetCellPath(Godot.Collections.Array<Vector2I> cells)` — typed convenience overload that re-wraps into the loose-array version above.
- `public bool SetWorldPath(Godot.Collections.Array points)` — accepts loose `Variant` world points, filters to finite ones, picks the closest starting index, sets `IsMoving = true`, emits `PathStarted`.
- `public bool SetWorldPath(Godot.Collections.Array<Vector2> points)` — typed convenience overload.
- `public void CancelMove()` — stops the walk, clears the path/index, zeroes `CharacterBody2D.Velocity` if present.
- `public Godot.Collections.Array<Vector2> GetWorldPath()` — a copy of the current world-space path.
- `public bool AdvancePath(double delta)` — one movement tick: advances toward the current waypoint, rotates/re-Z-indexes if configured, calls `FinishMove` when the last waypoint is reached. Called automatically from `_PhysicsProcess` but is public for manual driving.

## Dependencies
- Resolves its own parent as `Node2D` (and `CharacterBody2D` when applicable) — no warning is pushed if the parent isn't a `Node2D`; the failure instead surfaces later as a `MoveFailed("missing_body_grid_or_navigation")` signal from `MoveToCell`/`MoveToWorld`.
- Resolves `GridProjectionComponent` (`WorldToCell`, `CellToWorld`) and `GridNavigationComponent` only through the configured `NodePath` values.
- Confirmed from this batch: calls `GridNavigationComponent.FindCellPath(start, goal)` directly inside `MoveToCell`, making this file a caller of `GridNavigationComponent`, not a callee.

## Notes
- Unlike `ControllerComponent.ResolveBody2D` (used by other controller components in the addon), this component does not push a `GD.PushWarning` when its parent isn't the expected body type — it simply resolves `null` and lets the subsequent `MoveToCell`/`MoveToWorld` call fail via the `MoveFailed` signal instead. Not a bug, but a different failure-reporting style from the sibling `GridCameraControllerComponent` in this same batch, which does warn.
- `SetCellPath`/`SetWorldPath` each have a loose-`Array` overload and a typed `Array<T>` overload; the typed ones are thin wrappers that re-box into the loose form and delegate — this reads as a deliberate dual API (GDScript-callable loose array vs. C#-friendly typed array) rather than duplicated logic.
- The comment on cell/point parsing ("Cell and point parsing is delegated to GridVariantReader... this file used to carry its own copies of") is an explicit, source-confirmed record that a duplicate was found and resolved elsewhere in the addon; nothing further to flag here.
