using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Parallax scrolling component. Blind — attach to any Node2D for depth-based scrolling.
    /// Works for backgrounds, clouds, mountains, distant objects.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ParallaxComponent : WorldComponent
    {
        [Export] public float ParallaxFactor { get; set; } = 0.5f;
        [Export] public bool HorizontalOnly { get; set; } = true;
        [Export] public string FollowCameraGroup { get; set; } = "main_camera";
        [Export] public bool AutoTile { get; set; } = false;
        [Export] public Vector2 TileSize { get; set; } = new(1920, 1080);

        private Camera2D? _cam;
        private Vector2 _camStartPos;
        private Vector2 _startPos;
        private Node2D? _parent;

        public override void _Ready()
        {
            base._Ready();
            _parent = GetParent() as Node2D;

            var cams = GetTree().GetNodesInGroup(FollowCameraGroup);
            if (cams.Count > 0 && cams[0] is Camera2D cam)
            {
                _cam = cam;
                _camStartPos = cam.GlobalPosition;
                if (_parent != null) _startPos = _parent.GlobalPosition;
            }
        }

        public override void _Process(double delta)
        {
            if (_cam == null || _parent == null || !IsActive) return;
            Vector2 camDelta = _cam.GlobalPosition - _camStartPos;
            Vector2 offset = camDelta * ParallaxFactor;
            if (HorizontalOnly) offset.Y = 0;
            _parent.GlobalPosition = _startPos + offset;

            if (AutoTile && _cam.IsInsideTree())
            {
                // Keep looping for infinite scrolling
            }
        }
    }
}
