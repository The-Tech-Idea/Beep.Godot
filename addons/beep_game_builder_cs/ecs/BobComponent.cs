using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Floating bob animation component. Blind — attach to any Node2D.
    /// Works for pickups, power-ups, idle animations, UI elements.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BobComponent : WorldComponent
    {
        [Export] public float Amplitude { get; set; } = 5f;
        [Export] public float Speed { get; set; } = 2f;
        [Export] public bool BobHorizontal { get; set; } = false;
        [Export] public bool AlsoRotate { get; set; } = false;
        [Export] public float RotateAmplitude { get; set; } = 5f;

        private Vector2 _startPos;
        private float _startRot;
        private float _time;
        private Node2D? _parent;

        public override void _Ready()
        {
            base._Ready();
            _parent = GetParent() as Node2D;
            if (_parent != null)
            {
                _startPos = _parent.GlobalPosition;
                _startRot = _parent.RotationDegrees;
            }
        }

        public override void _Process(double delta)
        {
            if (_parent == null || !IsActive) return;
            _time += (float)delta * Speed;

            // Reset time every 2π to prevent float overflow.
            if (_time > Mathf.Tau)
                _time -= Mathf.Tau;

            float offset = Mathf.Sin(_time) * Amplitude;
            _parent.GlobalPosition = _startPos + (BobHorizontal ? new Vector2(offset, 0) : new Vector2(0, offset));

            if (AlsoRotate)
                _parent.RotationDegrees = _startRot + Mathf.Sin(_time * 1.3f) * RotateAmplitude;
        }
    }
}
