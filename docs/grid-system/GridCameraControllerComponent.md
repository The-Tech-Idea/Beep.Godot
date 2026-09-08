# GridCameraControllerComponent

Grid-system component (a `ControllerComponent`, `[Tool][GlobalClass]`) that provides pan/zoom camera control for authored top-down and isometric 2D maps. It is attached as a child of a `Camera2D` and drives that camera directly (`_camera.GlobalPosition`, `_camera.Zoom`) rather than through any grid or TileMap API — the doc comment is explicit that it works "without depending on TileMap." Input sources are independently toggleable: mouse drag, mouse-wheel zoom (optionally zooming toward the cursor rather than the viewport center), keyboard pan (configurable keys plus optional arrow-key support), and screen-edge pan. An optional world-space bounds rectangle can constrain both pan and zoom so the camera never shows past the authored map edge.

All motion is expressed as a target position/zoom (`_targetPosition`, `_targetZoom`) that the camera eases toward every `_Process` frame using an exponential-decay smoothing weight (`1 - e^{-smoothing * delta}`), which is frame-rate independent and snaps immediately once the weight reaches 1 (`smoothing <= 0` or a large `delta`). Every exported numeric value has an `Effective*` accessor or an inline sanitizer (`FiniteVector`, `PositiveVector`, `NonNegativeFinite`) that substitutes a safe fallback for non-finite or non-positive input before it reaches any math — the same defensive pattern this batch's other components use for their own tunables, applied consistently across pan speed, zoom step, smoothing rates, zoom range, and bounds size. `ZoomAtWorldPoint` keeps whatever world point is under the cursor fixed on screen while the zoom level changes, by solving for the new target position from the old/new zoom ratio rather than just changing zoom in place.

## Public API
- `[Signal] CameraMoved(Vector2 position)` / `ZoomChanged(Vector2 zoom)` — emitted from `_Process` only when the camera's actual position/zoom changed since the last emission (debounced via `IsEqualApprox` against cached last-emitted values).
- `[Export] bool UseMouseDrag`, `MouseButton DragButton` (default `Middle`), `bool UseWheelZoom`, `bool ZoomTowardMouse`, `bool UseKeyboardPan`, `bool UseEdgePan` (default `false`).
- `[ExportGroup("Motion")] float PanSpeed`, `float ZoomStep`, `Vector2 MinZoom/MaxZoom`, `float PositionSmoothing`, `float ZoomSmoothing`.
- `[ExportGroup("Keyboard")] Key PanUpKey/PanDownKey/PanLeftKey/PanRightKey` (WASD by default), `bool ArrowKeysAlsoPan`.
- `[ExportGroup("Edge Pan")] int EdgePanPixels`, `float EdgePanMultiplier`.
- `[ExportGroup("Bounds")] bool UseBounds`, `Vector2 BoundsPosition/BoundsSize`, `bool KeepViewportInsideBounds`.
- `public float EffectivePanSpeed / EffectiveZoomStep / EffectivePositionSmoothing / EffectiveZoomSmoothing`, `public Vector2 EffectiveBoundsSize` — sanitized reads of the corresponding exports.
- `public override void _Ready()` — requires a `Camera2D` parent (pushes a runtime `GD.PushWarning` and bails, still updating configuration warnings, if the parent isn't one); otherwise initializes targets from the camera's current position/zoom and applies them immediately.
- `public override string[] _GetConfigurationWarnings()` — flags a missing `Camera2D` parent, invalid min/max zoom, or an invalid `BoundsSize` while `UseBounds` is on.
- `public override void _UnhandledInput(InputEvent @event)` — handles mouse-button drag start/stop and wheel zoom; marks input handled when consumed.
- `public override void _Process(double delta)` — combines keyboard and edge-pan direction into a world-space pan delta, applies target smoothing, emits change signals.
- `public void FocusWorld(Vector2 worldPosition, bool immediate = false)` — sets the pan target (clamped to bounds), optionally snapping instantly.
- `public void PanByWorldDelta(Vector2 worldDelta)` / `public void PanByScreenDelta(Vector2 screenDelta)` — nudge the pan target; the screen-delta form divides by the current average zoom to convert to world units.
- `public void SetZoomLevel(float uniformZoom, bool immediate = false)` / `public void SetZoom(Vector2 zoom, bool immediate = false)` — set the zoom target, clamped to `MinZoom`/`MaxZoom`, re-clamping position to bounds under the new zoom.
- `public void ZoomAtWorldPoint(Vector2 worldPoint, float zoomDelta, bool immediate = false)` — zoom while keeping `worldPoint` fixed on screen.
- `public Vector2 ClampPosition(Vector2 worldPosition, Vector2 zoom)` — clamps a world position into `BoundsPosition/BoundsSize`, additionally shrinking the allowed range by half the viewport size (scaled by zoom) when `KeepViewportInsideBounds` is on, so the viewport itself never shows past the bounds edge.

## Dependencies
- Requires a `Camera2D` as its direct parent (`GetParent() as Camera2D`); reads/writes that camera's `GlobalPosition`/`Zoom` and reads `Camera2D.GetGlobalMousePosition()`.
- Reads `Input` (key/mouse state) and `Viewport` (mouse position, visible rect) directly from the Godot engine — no other file in this batch, and nothing else read in the grid folder as part of this batch, is referenced.
- Not established from this batch alone whether anything else calls into this component; it behaves as a leaf UI controller driven purely by engine input callbacks (`_UnhandledInput`, `_Process`), not by other grid components.

## Notes
- This is the only file in this batch that pushes a `GD.PushWarning` at *runtime* (not just an editor configuration warning) when misconfigured — a missing `Camera2D` parent. `GridPathFollowerComponent` and `GridNavigationComponent` instead fail through signals/return values, and `GridRoadComponent` fails through its `RoadRejected` signal; this component's misconfiguration path is comparatively louder.
- `EmitChanges`'s debounce against cached last-emitted position/zoom (`_lastEmittedPosition`/`_lastEmittedZoom`, seeded to `NaN` so the first frame always emits) is a signal-spam guard not present in this form in the other three files of this batch.
- Nothing surprising in the zoom/pan math itself — `ZoomAtWorldPoint`'s ratio-based recentring and the exponential smoothing weight are both standard, self-contained techniques and match their doc comments.
