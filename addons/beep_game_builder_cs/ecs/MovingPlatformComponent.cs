using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Moving platform. Attach to an AnimatableBody2D. Moves between waypoints
    /// (child Marker2D nodes) on a loop or ping-pong, with optional pause at each end.
    /// Reads speed from GameInfo.MoveSpeed if available.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MovingPlatformComponent : WorldComponent
    {
        public enum LoopMode { Loop, PingPong, Once }

        [Export] public float Speed { get; set; } = 80f;
        [Export] public LoopMode Mode { get; set; } = LoopMode.PingPong;
        [Export] public float PauseDuration { get; set; } = 0.5f;
        [Export] public bool AutoStart { get; set; } = true;

        private AnimatableBody2D? _body;
        private Vector2[] _points;
        private int _target;
        private bool _forward = true;
        private double _pauseTimer;
        private bool _paused;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as AnimatableBody2D;
            CollectWaypoints();
            if (AutoStart && !Engine.IsEditorHint()) _paused = false;
        }

        private void CollectWaypoints()
        {
            var list = new System.Collections.Generic.List<Vector2>();
            if (_body != null) list.Add(_body.GlobalPosition); // start = current pos
            foreach (var child in GetChildren())
            {
                if (child is Marker2D m)
                    list.Add(m.GlobalPosition);
            }
            _points = list.ToArray();
            _target = 1;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _body == null || Engine.IsEditorHint() || _points.Length < 2) return;

            if (_paused)
            {
                _pauseTimer -= delta;
                if (_pauseTimer <= 0) _paused = false;
                return;
            }

            Vector2 dest = _points[_target];
            Vector2 pos = _body.GlobalPosition;
            Vector2 dir = (dest - pos).Normalized();
            float step = Speed * (float)delta;

            if (pos.DistanceTo(dest) <= step)
            {
                _body.GlobalPosition = dest;
                AdvanceTarget();
                _paused = true;
                _pauseTimer = PauseDuration;
            }
            else
            {
                _body.GlobalPosition = pos + dir * step;
            }
        }

        private void AdvanceTarget()
        {
            if (Mode == LoopMode.Loop)
            {
                _target = (_target + 1) % _points.Length;
            }
            else if (Mode == LoopMode.PingPong)
            {
                if (_forward)
                {
                    _target++;
                    if (_target >= _points.Length - 1) { _target = _points.Length - 1; _forward = false; }
                }
                else
                {
                    _target--;
                    if (_target <= 0) { _target = 0; _forward = true; }
                }
            }
            else // Once
            {
                if (_target < _points.Length - 1) _target++;
                else IsActive = false; // reached end, stop
            }
        }
    }
}
