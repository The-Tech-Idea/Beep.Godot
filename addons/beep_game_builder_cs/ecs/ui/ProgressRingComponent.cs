using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Circular progress ring component. Extends Control to use _Draw().
    /// Works for loading spinners, cooldowns, timers, radial health.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ProgressRingComponent : Godot.Control
    {
        [Export] public float Value { get; set; } = 0.7f;
        [Export] public float MaxValue { get; set; } = 1f;
        [Export] public float RingThickness { get; set; } = 6f;
        [Export] public Color RingColor { get; set; } = new(0.3f, 0.6f, 1f, 1f);
        [Export] public Color BgColor { get; set; } = new(0.15f, 0.15f, 0.2f, 1f);
        [Export] public float AnimSpeed { get; set; } = 3f;

        [Signal] public delegate void ValueChangedEventHandler(float value);

        private float _displayValue;

        public override void _Ready() { _displayValue = Value; }

        public override void _Process(double delta)
        {
            _displayValue = Mathf.Lerp(_displayValue, Value / MaxValue, AnimSpeed * (float)delta);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var center = Size / 2f;
            float radius = Mathf.Min(center.X, center.Y) - RingThickness;
            DrawArc(center, radius, 0, Mathf.Pi * 2, 64, BgColor, RingThickness, true);
            float angleFrom = -Mathf.Pi / 2f;
            float angleTo = angleFrom + Mathf.Pi * 2f * _displayValue;
            DrawArc(center, radius, angleFrom, angleTo, 64, RingColor, RingThickness, false);
        }

        public void SetValue(float value) { Value = value; EmitSignal(SignalName.ValueChanged, value); }
    }
}
