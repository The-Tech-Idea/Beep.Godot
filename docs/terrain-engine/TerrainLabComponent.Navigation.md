# TerrainLabComponent.Navigation

Pipeline position: **game-facing component (editor/demo UI)** — a partial-class continuation of `TerrainLabComponent` that owns only preview camera behaviour (pan, zoom, fit-to-panel); no generation or rendering logic.

This file is the second half of the `TerrainLabComponent` partial class (see `TerrainLabComponent.cs` for the primary declaration, exports, and fields it shares). It handles `_UnhandledInput` for panning (left- or middle-mouse drag, and touch pan gestures) and mouse-wheel zoom about the cursor, plus `ResetPreviewView()` which fits the whole generated map into the visible preview area using the world component's own extent rather than recomputing projection-specific bounds itself.

## Public API

- `override void _UnhandledInput(InputEvent @event)` — handles mouse-button press/release to start/stop panning (both `MouseButton.Middle` and `MouseButton.Left` toggle `_isPanning`, deliberately including left so touch/no-middle-button users can still pan), mouse wheel up/down to zoom via `ZoomAt`, mouse-motion while panning to translate `_preview.Position`, and `InputEventPanGesture` to translate it as well.
- `ZoomAt(Vector2 screenPosition, float factor)` *(private)* — rescales `_preview.Scale` by `factor` clamped to `[MinimumZoom, MaximumZoom]`, then repositions `_preview` so the world point under `screenPosition` stays fixed on screen.
- `ResetPreviewView()` *(private)* — reads `_world.PreviewExtent()` for the map's origin and size, computes the available panel area (viewport size minus fixed left/top/right/bottom margins: `PreviewLeft=340`, `PreviewTop=40`, `PreviewRightMargin=24`, `PreviewBottomMargin=70`), picks the largest zoom (clamped to min/max) that fits the map in that area, and sets `_preview.Scale`/`Position` accordingly, centering the map and offsetting for its origin.

## Dependencies

- Reads `TerrainWorldComponent.PreviewExtent()` (defined in `TerrainWorldComponent.Drawing.cs`) through the shared `_world` field to get the map's screen-space bounding rectangle regardless of active projection.
- Reads/writes the shared `_preview`, `_isPanning`, `MinimumZoom`, `MaximumZoom`, `ZoomStep` fields/properties declared in `TerrainLabComponent.cs` (same partial class).

## Notes

- The class comment on `TerrainLabComponent.cs` explicitly documents why `ResetPreviewView` now delegates to `_world.PreviewExtent()` instead of computing bounds from the renderers itself: an isometric map is a diamond extending left of its own origin, and a naive rectangular-fit (as the old "tile demo" did) framed it wrong. No such naive logic remains in this file.
- No dead code, no unused exports, no swallowed exceptions found in this file — it is small and does exactly what its one comment block says.
