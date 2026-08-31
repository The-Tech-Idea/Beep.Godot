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
                    ZoomAt(mouseButton.Position, ZoomStep);
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                    ZoomAt(mouseButton.Position, 1.0f / Mathf.Max(1.01f, ZoomStep));
                return;
            }

            if (_isPanning && @event is InputEventMouseMotion motion)
            {
                _preview.Position += motion.Relative;
                return;
            }

            if (@event is InputEventPanGesture pan)
                _preview.Position += pan.Delta;
        }

        /// <summary>Zooms about a screen point, keeping what is under it still.</summary>
        private void ZoomAt(Vector2 screenPosition, float factor)
        {
            if (_preview is null)
                return;

            Vector2 previousLocalPosition = _preview.ToLocal(screenPosition);
            float currentZoom = _preview.Scale.X;
            float targetZoom = Mathf.Clamp(currentZoom * factor, MinimumZoom, MaximumZoom);
            if (Mathf.IsEqualApprox(currentZoom, targetZoom))
                return;

            _preview.Scale = Vector2.One * targetZoom;
            Vector2 newLocalPosition = _preview.ToLocal(screenPosition);
            _preview.Position += (newLocalPosition - previousLocalPosition) * targetZoom;
        }

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
            Vector2 origin = extent.Position;
            Vector2 terrainSize = extent.Size;

            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            Vector2 availableSize = new(
                Mathf.Max(1, viewportSize.X - PreviewLeft - PreviewRightMargin),
                Mathf.Max(1, viewportSize.Y - PreviewTop - PreviewBottomMargin));

            float zoom = Mathf.Clamp(
                Mathf.Min(availableSize.X / terrainSize.X, availableSize.Y / terrainSize.Y),
                MinimumZoom,
                MaximumZoom);

            _preview.Scale = Vector2.One * zoom;
            _preview.Position = new Vector2(
                PreviewLeft + ((availableSize.X - (terrainSize.X * zoom)) * 0.5f),
                PreviewTop + ((availableSize.Y - (terrainSize.Y * zoom)) * 0.5f))
                - (origin * zoom);
        }
    }
}
