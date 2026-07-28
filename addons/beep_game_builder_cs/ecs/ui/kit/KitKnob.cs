using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A rotary dial — CATALOGUE-FROM-ART.md section E's `RoundKnob`.
    ///
    /// Distinct from <see cref="KitSlider"/> rather than a round skin of it: a knob occupies a
    /// square, is dragged vertically rather than along its own track, and shows its value as an
    /// ANGLE plus a tick ring. Mixers, radios and vehicle-tuning screens use it where a slider
    /// would not fit the panel.
    ///
    /// Drag is vertical on purpose. Following the pointer's angle around the knob is the obvious
    /// implementation and the wrong one — it makes the value jump when the pointer crosses the
    /// centre, which is exactly where a user's hand passes.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitKnob : KitControl
    {
        [Export(PropertyHint.Range, "0.0,1.0,0.001")]
        public float Value
        {
            get => _value;
            set
            {
                float v = Mathf.Clamp(value, 0f, 1f);
                if (Mathf.IsEqualApprox(v, _value)) return;
                _value = v; QueueRedraw(); EmitSignal(SignalName.ValueChanged, v);
            }
        }
        private float _value = 0.35f;

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Accent;
        [Export(PropertyHint.Range, "0,24,1")] public int Ticks { get; set; } = 11;
        /// <summary>Sweep of the dial, degrees. 270 leaves a gap at the bottom.</summary>
        [Export(PropertyHint.Range, "90,360,1")] public float SweepDegrees { get; set; } = 270f;

        [Signal] public delegate void ValueChangedEventHandler(float value);

        private bool _drag;
        private float _dragStart, _valueStart;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 3.6f, fs * 3.6f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                    _drag = mb.Pressed;
                    if (mb.Pressed) { _dragStart = mb.Position.Y; _valueStart = _value; }
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _drag:
                    // Up increases. Full travel over roughly the knob's own height.
                    Value = _valueStart + (_dragStart - mm.Position.Y) / Mathf.Max(24f, Size.Y);
                    AcceptEvent();
                    break;
            }
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 14f) return;

            var c = Size * 0.5f;
            float r = d * 0.5f * 0.74f;
            Color face = FaceColor();
            Color ink = InkColor();
            Color acc = UiSurface.Semantic(this, Role);

            float sweep = Mathf.DegToRad(Mathf.Clamp(SweepDegrees, 90f, 360f));
            float start = Mathf.Pi * 0.5f + (Mathf.Tau - sweep) * 0.5f;

            // Tick ring outside the body, so the body can be gripped without hiding the scale.
            float tr = d * 0.5f * 0.95f;
            for (int i = 0; i < Ticks; i++)
            {
                float t = Ticks <= 1 ? 0f : i / (float)(Ticks - 1);
                float a = start + sweep * t;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool past = t <= _value + 0.0001f;
                DrawLine(c + dir * (tr * 0.86f), c + dir * tr,
                         past ? acc : new Color(ink.R, ink.G, ink.B, 0.55f),
                         Mathf.Max(1.5f, d * 0.035f));
            }

            DrawCircle(c, r, face);
            DrawArc(c, r, 0f, Mathf.Tau, 48, ink, Mathf.Max(2f, d * 0.045f));

            // Pointer: a spoke from centre to rim, the only accented part of the body.
            float ang = start + sweep * _value;
            var pd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            DrawLine(c + pd * (r * 0.25f), c + pd * (r * 0.86f), acc, Mathf.Max(2.5f, d * 0.06f));
            DrawCircle(c, r * 0.16f, acc);
        }
    }
}
