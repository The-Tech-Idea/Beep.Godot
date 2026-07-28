using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Circular progress ring component. Extends Control to use _Draw().
    /// Works for loading spinners, cooldowns, timers, radial health.
    /// Extends Control directly by design (not a category base) — it IS the drawable node.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ProgressRingComponent : Godot.Control
    {
        private float _value = 0.7f;
        // Backed property so a programmatic Value assignment also fires ValueChanged, matching
        // SetValue — auto-property setters emitted nothing, so bindings missed direct writes.
        [Export] public float Value
        {
            get => _value;
            set { if (Mathf.IsEqualApprox(_value, value)) return; _value = value; EmitSignal(SignalName.ValueChanged, value); }
        }
        [Export] public float MaxValue { get; set; } = 1f;
        [Export] public float RingThickness { get; set; } = 6f;
        // The ring is the theme's accent and its track the ink derived from the surface it
        // sits on, instead of a fixed blue on a fixed near-black.
        /// <summary>What this ring MEANS. Callers set a role, not a colour, so a buff ring and
        /// a debuff ring stay green and red under a skin that redefines what green and red are.
        /// This is the same shape as ResourceBadgeComponent.Accent.</summary>
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

        private Color RingColor => UiSurface.Semantic(this, Accent);
        private Color BgColor => UiSurface.Ink(UiSurface.Of(this));
        [Export] public float AnimSpeed { get; set; } = 3f;

        [Signal] public delegate void ValueChangedEventHandler(float value);

        private float _displayValue;

        public override void _Ready() { _displayValue = Value; }

        public override void _Process(double delta)
        {
            // Don't run the lerp/repaint loop at edit time — it would repaint every frame in-editor.
            if (Engine.IsEditorHint()) return;
            _displayValue = Mathf.Lerp(_displayValue, MaxValue > 0f ? Value / MaxValue : 0f, AnimSpeed * (float)delta);
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

        // Value's setter already emits ValueChanged; assign through it (guarded against a no-op).
        public void SetValue(float value) { Value = value; }
    }
}
