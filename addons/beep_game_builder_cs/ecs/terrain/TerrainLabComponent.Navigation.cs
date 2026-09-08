using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Moving and framing the preview: pan, zoom, and fit-to-panel.
    /// </summary>
    public partial class TerrainLabComponent
    {
        public override void _UnhandledInput(InputEvent @event)
        {
            if (_preview is null)
                return;

            if (@event is InputEventMouseButton mouseButton)
            {
                // LEFT as well as middle. Middle-drag alone is what a strategy
                // game does, but the panel is the only thing here a left click
                // can hit and it is a Control that takes its own clicks - so the
                // map has nothing to lose by panning on left-drag, and plenty of
                // people have no middle button to find.
                if (mouseButton.ButtonIndex is MouseButton.Middle or MouseButton.Left)
                {
                    _isPanning = mouseButton.Pressed;
                    return;
                }

                if (!mouseButton.Pressed)
                    return;

                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                    ZoomPreviewAt(mouseButton.Position, ZoomStep);
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                    ZoomPreviewAt(mouseButton.Position, 1.0f / Mathf.Max(1.01f, ZoomStep));
                return;
            }

            if (_isPanning && @event is InputEventMouseMotion motion)
            {
                PanPreviewBy(motion.Relative);
                return;
            }

            if (@event is InputEventPanGesture pan)
                PanPreviewBy(pan.Delta);
        }

        /// <summary>Zooms about a screen point, keeping what is under it still.</summary>
        public void ZoomPreviewAt(Vector2 screenPosition, float factor)
        {
            if (_preview is null || !screenPosition.IsFinite() || !float.IsFinite(factor) || factor <= 0)
                return;

            Vector2 previousLocalPosition = _preview.GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
            float currentZoom = _preview.Scale.X;
            float targetZoom = Mathf.Clamp(currentZoom * factor, MinimumZoom, MaximumZoom);
            if (Mathf.IsEqualApprox(currentZoom, targetZoom))
                return;

            _preview.Scale = Vector2.One * targetZoom;
            Vector2 movedScreenPosition = _preview.GetGlobalTransformWithCanvas() * previousLocalPosition;
            PanPreviewBy(screenPosition - movedScreenPosition);
        }

        /// <summary>Moves the preview by viewport pixels, independent of parent/canvas transforms.</summary>
        public void PanPreviewBy(Vector2 screenDelta)
        {
            if (_preview is null || !screenDelta.IsFinite()) return;
            Transform2D parentToScreen = _preview.GetGlobalTransformWithCanvas() * _preview.Transform.AffineInverse();
            Transform2D screenToParent = parentToScreen.AffineInverse();
            _preview.Position += screenToParent * screenDelta - screenToParent * Vector2.Zero;
        }

        private bool _hasFramedPreview;
        private Rect2 _framedExtent;

        /// <summary>Margins of the preview area inside the window, in pixels.</summary>
        private const float PreviewLeft = 340.0f;
        private const float PreviewTop = 40.0f;
        private const float PreviewBottomMargin = 70.0f;
        private const float PreviewRightMargin = 24.0f;

        /// <summary>
        /// Fits the whole map in the preview area.
        ///
        /// How big the map is and where its origin sits comes from the WORLD
        /// component, which knows the projection. This method used to work that
        /// out itself, from the renderers - which meant the panel had to know
        /// that an isometric map is a diamond extending to the left of its own
        /// origin, and any other creation screen had to know it too. The tile
        /// demo did not, and framed every view as a flat rectangle.
        /// </summary>
        private void ResetPreviewView()
        {
            if (_preview is null || _world is null)
                return;

            Rect2 extent = _world.PreviewExtent();
            if (!extent.Size.IsFinite() || extent.Size.X <= 0 || extent.Size.Y <= 0) return;
            _framedExtent = extent;
            _hasFramedPreview = true;
            _preview.Scale = Vector2.One;
            Rect2 unitScreenExtent = _preview.GetGlobalTransformWithCanvas() * extent;
            Vector2 terrainSize = unitScreenExtent.Size;
            if (!terrainSize.IsFinite() || terrainSize.X <= 0 || terrainSize.Y <= 0) return;

            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            Vector2 availableSize = new(
                Mathf.Max(1, viewportSize.X - PreviewLeft - PreviewRightMargin),
                Mathf.Max(1, viewportSize.Y - PreviewTop - PreviewBottomMargin));

            float zoom = Mathf.Clamp(
                Mathf.Min(availableSize.X / terrainSize.X, availableSize.Y / terrainSize.Y),
                MinimumZoom,
                MaximumZoom);

            _preview.Scale = Vector2.One * zoom;
            Vector2 desiredTopLeft = new Vector2(
                PreviewLeft + ((availableSize.X - (terrainSize.X * zoom)) * 0.5f),
                PreviewTop + ((availableSize.Y - (terrainSize.Y * zoom)) * 0.5f));
            Rect2 screenExtent = _preview.GetGlobalTransformWithCanvas() * extent;
            PanPreviewBy(desiredTopLeft - screenExtent.Position);
        }
    }
}
