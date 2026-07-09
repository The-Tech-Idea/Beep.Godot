using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Pulse animation. Attach to any Control for breathing/pulsing scale.
    /// Blind — works for attention grab, loading indicators, idle animations.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PulseComponent : EntityComponent
    {
        [Export] public float MinScale { get; set; } = 0.95f;
        [Export] public float MaxScale { get; set; } = 1.05f;
        [Export] public float Speed { get; set; } = 2f;
        [Export] public bool AutoStart { get; set; } = true;

        private Control? _control;
        private float _time;

        public override void _Ready() { base._Ready(); _control = GetParent<Control>(); }

        public override void _Process(double delta)
        {
            if (_control == null || !IsActive || !AutoStart) return;
            _time += (float)delta * Speed;
            float s = Mathf.Lerp(MinScale, MaxScale, (Mathf.Sin(_time) + 1f) / 2f);
            _control.Scale = new Vector2(s, s);
        }
    }
}
