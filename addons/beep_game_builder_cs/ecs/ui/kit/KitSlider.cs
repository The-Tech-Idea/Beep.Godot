using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A slider with a chunky bar knob — CATALOGUE-FROM-ART.md sections C, D and E all list one,
    /// and `settings1.png` specifies the game form's version: a <b>vertical bar knob</b>, not the
    /// circular grabber a desktop toolkit draws.
    ///
    /// Two rules carried over from the theme engine, each a defect already paid for:
    ///  - the <b>track is a dark tint of the fill's own hue</b>, never a neutral grey (4x rule);
    ///  - the knob does not change HUE on focus. Stage 28 found the settings slider rendering
    ///    green while the two beneath it stayed blue, because grabber and grabber_highlight came
    ///    from different palette roles. Here the highlight is a LIGHTENED fill, same hue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSlider : KitControl
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

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
        private float _value = 0.5f;

        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Accent;

        [Signal] public delegate void ValueChangedEventHandler(float value);

        private bool _dragging;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 10f, fs * 1.9f);
            }
        }

        private float KnobW => Mathf.Max(6f, Size.Y * 0.38f);

        public override void _GuiInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                    _dragging = mb.Pressed;
                    if (mb.Pressed) { SetFromX(mb.Position.X); SetState(KitState.Pressed); }
                    else SetState(KitState.Normal);
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _dragging:
                    SetFromX(mm.Position.X);
                    AcceptEvent();
                    break;
            }
        }

        private void SetFromX(float x)
        {
            float half = KnobW * 0.5f;
            float span = Mathf.Max(1f, Size.X - KnobW);
            Value = Mathf.Clamp((x - half) / span, 0f, 1f);
        }

        public override void _Draw()
        {
            if (Size.X <= 6 || Size.Y <= 4) return;

            var g = Geo;
            Color fill = UiSurface.Semantic(this, Fill);
            Color ink = InkColor();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (fs / 14f));

            // Track: the fill's own hue driven dark. A grey track is the clearest tell of a
            // themed form rather than a game.
            float th = Mathf.Max(4f, Size.Y * 0.34f);
            var track = new Rect2(0f, (Size.Y - th) * 0.5f, Size.X, th);
            DrawShape(track, KitShape.Pill,
                      new Color(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f), ink, rimPx);

            float half = KnobW * 0.5f;
            float span = Mathf.Max(1f, Size.X - KnobW);
            float kx = half + span * _value;

            if (kx - half > 1f)
            {
                var done = new Rect2(track.Position, new Vector2(kx, track.Size.Y));
                DrawShape(done, KitShape.Pill, fill, ink, 0f);
            }

            // The bar knob: a chunky vertical plate, the game form's grabber.
            var knob = new Rect2(kx - half, 0f, KnobW, Size.Y);
            Color kc = State == KitState.Pressed
                ? new Color(Mathf.Lerp(fill.R, 1f, 0.28f), Mathf.Lerp(fill.G, 1f, 0.28f),
                            Mathf.Lerp(fill.B, 1f, 0.28f), 1f)   // lightened, SAME hue
                : fill;
            DrawShape(knob, ActiveShape, kc, ink, Mathf.Max(1.5f, rimPx));
        }
    }
}
