using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// How a scene opens on a world it has just built.
    /// </summary>
    public enum TerrainCameraFraming
    {
        /// <summary>The whole map in frame - an overview, or a map screen.</summary>
        WholeMap = 0,

        /// <summary>
        /// Where a player would actually begin, at the art's own scale. Fitting
        /// the whole island in frame makes every tile a few pixels and reads as a
        /// diagram; a game is played close enough to recognise a single tree.
        /// </summary>
        StartPosition = 1,
    }

    /// <summary>
    /// Frames a <see cref="TerrainWorldComponent"/> through a camera.
    ///
    /// This was written three times, once per demo controller, and the copies
    /// disagreed. The tile demo framed a flat rectangle for every projection, so
    /// an isometric map - a diamond extending to the LEFT of its own origin -
    /// fell half off the screen. The isometric demo got that right and had its
    /// own copy of the arithmetic to keep in step with the renderer. None of it
    /// was demo-specific: a game needs exactly this to open on a generated map.
    ///
    /// The extent comes from the world component, which knows the projection, so
    /// there is no per-projection arithmetic here at all.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainWorldCameraComponent : Node
    {
        [Export] public NodePath WorldPath { get; set; } = new("");
        [Export] public NodePath CameraPath { get; set; } = new("");
        [Export] public NodePath CameraControllerPath { get; set; } = new("");

        [Export] public TerrainCameraFraming Framing { get; set; } = TerrainCameraFraming.WholeMap;

        /// <summary>Zoom a StartPosition framing opens at. One is the art's own scale.</summary>
        [Export(PropertyHint.Range, "0.1,4,0.05")] public float SceneZoom { get; set; } = 1.0f;

        /// <summary>Pixels of the viewport left around a WholeMap framing.</summary>
        [Export] public Vector2 FitMargin { get; set; } = new(48.0f, 96.0f);

        /// <summary>
        /// Re-frames the whole map. Useful even when the scene opens on a start
        /// position, which is why it is offered whatever the framing mode is.
        /// </summary>
        [Export] public Key FrameMapKey { get; set; } = Key.R;

        private TerrainWorldComponent? _world;
        private Camera2D? _camera;
        private GridCameraControllerComponent? _controller;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            Resolve();

            // Be explicit about which camera renders, rather than relying on it
            // happening to be the only one in the scene.
            _camera?.MakeCurrent();

            if (_world is not null)
                _world.WorldBuilt += OnWorldBuilt;
        }

        public override void _ExitTree()
        {
            if (_world is not null && GodotObject.IsInstanceValid(_world))
                _world.WorldBuilt -= OnWorldBuilt;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (WorldPath.IsEmpty)
                return new[] { "WorldPath should point to a TerrainWorldComponent." };
            if (CameraControllerPath.IsEmpty)
                return new[] { "CameraControllerPath should point to a GridCameraControllerComponent." };
            return System.Array.Empty<string>();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == FrameMapKey)
                FrameWholeMap();
        }

        private void OnWorldBuilt(Vector2I size)
        {
            _ = size;
            if (Framing == TerrainCameraFraming.StartPosition)
                FrameStartPosition();
            else
                FrameWholeMap();
        }

        /// <summary>
        /// Fits the whole map in frame, through the CAMERA rather than by scaling
        /// the world. Scaling the world would fight the camera controller and
        /// make zoom compound with it; a game views a map by moving a camera.
        /// </summary>
        public void FrameWholeMap()
        {
            Resolve();
            if (_world is null || _controller is null)
                return;

            Rect2 extent = ApplyBounds();
            Vector2 viewport = GetViewport().GetVisibleRect().Size;
            float fit = Mathf.Min(
                (viewport.X - FitMargin.X) / Mathf.Max(1.0f, extent.Size.X),
                (viewport.Y - FitMargin.Y) / Mathf.Max(1.0f, extent.Size.Y));

            _controller.SetZoomLevel(Mathf.Max(0.02f, fit), immediate: true);
            _controller.FocusWorld(extent.Position + (extent.Size * 0.5f), immediate: true);
        }

        /// <summary>Opens on the first start position, at the art's own scale.</summary>
        public void FrameStartPosition()
        {
            Resolve();
            if (_world is null || _controller is null)
                return;

            ApplyBounds();
            _controller.SetZoomLevel(Mathf.Max(0.02f, SceneZoom), immediate: true);
            _controller.FocusWorld(_world.StartPositionView(), immediate: true);
        }

        /// <summary>
        /// Keeps the camera inside the map it is actually looking at, and returns
        /// the extent it was given.
        /// </summary>
        private Rect2 ApplyBounds()
        {
            Rect2 extent = _world!.PreviewExtent();
            _controller!.BoundsPosition = extent.Position;
            _controller.BoundsSize = extent.Size;
            return extent;
        }

        private void Resolve()
        {
            if (_world is null || !GodotObject.IsInstanceValid(_world))
                _world = WorldPath.IsEmpty ? null : GetNodeOrNull<TerrainWorldComponent>(WorldPath);
            _camera ??= GetNodeOrNull<Camera2D>(CameraPath);
            if (_controller is null || !GodotObject.IsInstanceValid(_controller))
                _controller = CameraControllerPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridCameraControllerComponent>(CameraControllerPath);
        }
    }
}
