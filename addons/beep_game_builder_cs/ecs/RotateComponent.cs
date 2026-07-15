using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Continuous rotation component. Blind — attach to any Node2D.
    /// Works for gears, windmills, coins, loading spinners, propellers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RotateComponent : WorldComponent
    {
        [Export] public float DegreesPerSecond { get; set; } = 90f;
        [Export] public bool Clockwise { get; set; } = true;
        [Export] public bool PingPong { get; set; } = false;
        [Export] public float PingPongRange { get; set; } = 45f;

        private float _accumulated;
        private Node2D? _parent;

        public override void _Ready()
        {
            base._Ready();
            _parent = GetParent() as Node2D;
        }

        public override void _Process(double delta)
        {
            if (_parent == null || !IsActive) return;
            float dir = Clockwise ? 1f : -1f;
            float rot = DegreesPerSecond * dir * (float)delta;

            if (PingPong)
            {
                _accumulated += rot;
                if (Mathf.Abs(_accumulated) > PingPongRange)
                {
                    _accumulated = Mathf.Clamp(_accumulated, -PingPongRange, PingPongRange);
                    Clockwise = !Clockwise;
                    dir = Clockwise ? 1f : -1f;
                }
            }
            _parent.RotationDegrees += rot;

            // Normalize rotation to -180..180 to prevent overflow.
            while (_parent.RotationDegrees > 180f)
                _parent.RotationDegrees -= 360f;
            while (_parent.RotationDegrees < -180f)
                _parent.RotationDegrees += 360f;
        }
    }
}
