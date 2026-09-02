# TerrainWorldCameraComponent

Game-facing component: frames a `Camera2D` (via a `GridCameraControllerComponent`) around whatever a `TerrainWorldComponent` just built.

Consolidates camera-framing arithmetic that, per its own header comment, used to be written three times (once per demo controller) and disagreed — one framed a flat rectangle for every projection, which put half of an isometric map's diamond off-screen. This component computes zoom/focus purely from the world component's own `PreviewExtent()`/`StartPositionView()`, so the projection-specific geometry is never duplicated here; it exists solely to turn that geometry into camera state on a `WorldBuilt` signal or an explicit re-frame key.

## Public API

- `NodePath WorldPath` `[Export]` — the `TerrainWorldComponent` this reads extent/start-position from and subscribes to `WorldBuilt` on.
- `NodePath CameraPath` `[Export]` — the `Camera2D` made current in `_Ready`.
- `NodePath CameraControllerPath` `[Export]` — the `GridCameraControllerComponent` actually driven (zoom, focus, bounds).
- `TerrainCameraFraming Framing` `[Export]` (`WholeMap` / `StartPosition`) — which framing `OnWorldBuilt` applies after a build.
- `float SceneZoom` `[Export(Range 0.1..4)]` — zoom used for `StartPosition` framing; `1.0` is the art's own scale.
- `Vector2 FitMargin` `[Export]` (default `48,96`) — viewport pixels left around the map for `WholeMap` framing.
- `Key FrameMapKey` `[Export]` (default `R`) — key that re-triggers `FrameWholeMap()` from `_UnhandledInput`, regardless of the current `Framing` mode.
- `_Ready()` — no-ops in the editor; resolves node refs, calls `_camera?.MakeCurrent()`, subscribes to `_world.WorldBuilt`.
- `_ExitTree()` — unsubscribes from `WorldBuilt` if `_world` is still a valid instance.
- `_GetConfigurationWarnings()` — warns if `WorldPath` or `CameraControllerPath` is empty; does **not** check `CameraPath`.
- `_UnhandledInput(InputEvent)` — on `FrameMapKey` press, calls `FrameWholeMap()`.
- `void FrameWholeMap()` — reads `_world.PreviewExtent()`, computes the zoom that fits `(viewport - FitMargin)` around that extent, sets it on the controller immediately, and centres the controller on the extent's midpoint.
- `void FrameStartPosition()` — applies the world's bounds to the controller, sets zoom to `SceneZoom`, and focuses on `_world.StartPositionView()`, both immediately.
- `OnWorldBuilt(Vector2I size)` *(private, signal handler)* — dispatches to `FrameStartPosition()` or `FrameWholeMap()` per `Framing`; the `size` argument is discarded (`_ = size;`) since the extent is re-derived from the world component instead.
- `ApplyBounds()` *(private)* — pushes `_world.PreviewExtent()` onto the controller's `BoundsPosition`/`BoundsSize` and returns it.
- `Resolve()` *(private)* — re-resolves `_world`/`_controller` from their NodePaths whenever the cached reference is null or invalid; resolves `_camera` once via `??=` with no later validity check.

## Dependencies

- Reads `TerrainWorldComponent.PreviewExtent()` and `TerrainWorldComponent.StartPositionView()` (defined in `TerrainWorldComponent.Drawing.cs`), and subscribes to `TerrainWorldComponent.WorldBuilt` (defined in `TerrainWorldComponent.cs`).
- Writes `GridCameraControllerComponent.SetZoomLevel(...)`, `.FocusWorld(...)`, `.BoundsPosition`, `.BoundsSize` — defined in `GridCameraControllerComponent.cs`, outside this batch.

## Notes

- `_camera` is resolved with a bare `??=` in `Resolve()` and, unlike `_world`/`_controller`, is never re-checked with `GodotObject.IsInstanceValid` — if the referenced `Camera2D` is freed and replaced, this component keeps the stale reference.
- `_GetConfigurationWarnings()` doesn't flag an empty `CameraPath`, even though `_camera?.MakeCurrent()` in `_Ready()` depends on it (it just silently no-ops when unset).
- `OnWorldBuilt` receiving-then-discarding the signal's `Vector2I size` matches the class's stated design (extent is asked of the world component, never re-derived locally) — flagged only because it's easy to misread as dead code; it isn't.
