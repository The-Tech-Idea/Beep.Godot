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
    public partial class KitKnob : HSlider
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export]
        public UiSurface.Role Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _role = UiSurface.Role.Accent;

        [Export(PropertyHint.Range, "0,24,1")]
        public int Ticks
        {
            get => _ticks;
            set
            {
                int next = Mathf.Clamp(value, 0, 24);
                if (_ticks == next) return;
                _ticks = next;
                RefreshVisualAndRedraw();
            }
        }
        private int _ticks = 11;

        /// <summary>Sweep of the dial, degrees. 270 leaves a gap at the bottom.</summary>
        [Export(PropertyHint.Range, "90,360,1")]
        public float SweepDegrees
        {
            get => _sweepDegrees;
            set
            {
                float next = Mathf.Clamp(value, 90f, 360f);
                if (Mathf.IsEqualApprox(_sweepDegrees, next)) return;
                _sweepDegrees = next;
                RefreshVisualAndRedraw();
            }
        }
        private float _sweepDegrees = 270f;


        private bool _drag;
        private float _dragStart, _startValue;
        private string _genre = "";
        private bool _suppressing;
        private bool _eventsHooked;

        public KitKnob()
        {
            MinValue = 0.0;
            MaxValue = 1.0;
            Step = 0.001;
        }

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            // Range gives Value/MinValue/MaxValue/ValueChanged; the DIAL's interaction is still
            // ours, because a knob is dragged VERTICALLY and Slider's own handling is horizontal.
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            SuppressNativeChrome();
            if (!_eventsHooked)
            {
                ValueChanged += _ => QueueRedraw();
                _eventsHooked = true;
            }

            int fs = UiSurface.FontSize(this);
            // Blanking the theme art removes the size it was providing -- restate it, or the
            // control collapses and _Draw's own size guard makes it vanish silently.
            KitChrome.SetAutoMinimumSize(this, new Vector2(fs * 3.6f, fs * 3.6f));
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 3.6f, fs * 3.6f);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            SuppressNativeChrome();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
        }

        private void SuppressNativeChrome()
        {
            if (_suppressing) return;
            _suppressing = true;
            foreach (string sb in new[] { "slider", "grabber_area", "grabber_area_highlight" })
                KitChrome.SetEmptyStyleboxOverride(this, sb);
            foreach (string ic in new[] { "grabber", "grabber_highlight", "grabber_disabled", "tick" })
                KitChrome.SetBlankIconOverride(this, ic);
            _suppressing = false;
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Editable)
            {
                _drag = false;
                return;
            }

            switch (@event)
            {
                case InputEventKey key:
                    Vector2I dir = KitChrome.DirectionFromKey(key);
                    if (dir.X <= -9999) { Value = MinValue; AcceptEvent(); }
                    else if (dir.X >= 9999) { Value = MaxValue; AcceptEvent(); }
                    else if (dir.X < 0 || dir.Y > 0) { Value = Mathf.Max(MinValue, Value - Step * 12.0); AcceptEvent(); }
                    else if (dir.X > 0 || dir.Y < 0) { Value = Mathf.Min(MaxValue, Value + Step * 12.0); AcceptEvent(); }
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                    _drag = mb.Pressed;
                    if (mb.Pressed) { GrabFocus(); _dragStart = mb.Position.Y; _startValue = (float)Value; }
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _drag:
                    // Match pointer movement: dragging down increases the dial in screen-space.
                    Value = _startValue + (mm.Position.Y - _dragStart) / Mathf.Max(24f, Size.Y)
                                            * (float)Mathf.Max(0.0001, MaxValue - MinValue);
                    AcceptEvent();
                    break;
            }
        }

        private float NormalizedValue()
        {
            double span = MaxValue - MinValue;
            if (span <= 0.000001) return 0f;
            return Mathf.Clamp((float)((Value - MinValue) / span), 0f, 1f);
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 14f) return;

            var c = Size * 0.5f;
            float r = d * 0.5f * 0.74f;
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            Color acc = UiSurface.SemanticOrDerived(this, Role);
            KitState state = Editable ? KitState.Normal : KitState.Disabled;
            face = KitChrome.StateFace(face, state);
            acc = KitChrome.StateFace(acc, state);
            var font = KitChrome.Font(this, _genre);

            float sweep = Mathf.DegToRad(Mathf.Clamp(SweepDegrees, 90f, 360f));
            float start = Mathf.Pi * 0.5f + (Mathf.Tau - sweep) * 0.5f;

            // Tick ring outside the body, so the body can be gripped without hiding the scale.
            float tr = d * 0.5f * 0.95f;
            for (int i = 0; i < Ticks; i++)
            {
                float t = Ticks <= 1 ? 0f : i / (float)(Ticks - 1);
                float a = start + sweep * t;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool past = t <= NormalizedValue() + 0.0001f;
                DrawLine(c + dir * (tr * 0.86f), c + dir * tr,
                         past ? acc : new Color(ink.R, ink.G, ink.B, Editable ? 0.55f : 0.28f),
                         Mathf.Max(1.5f, d * 0.035f));
            }

            DrawCircle(c, r, face);
            DrawArc(c, r, 0f, Mathf.Tau, 48, ink, Mathf.Max(2f, d * 0.045f));

            // Pointer: a spoke from centre to rim, the only accented part of the body.
            float ang = start + sweep * NormalizedValue();
            var pd = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            DrawLine(c + pd * (r * 0.25f), c + pd * (r * 0.86f), acc, Mathf.Max(2.5f, d * 0.06f));
            DrawCircle(c, r * 0.16f, acc);

            if (font != null && d > 42f)
            {
                string value = Mathf.RoundToInt(NormalizedValue() * 100f).ToString();
                int vf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(r * 0.74f, r * 0.34f), value, font, min: 7);
                Vector2 m = font.GetStringSize(value, HorizontalAlignment.Left, -1, vf);
                var b = new Rect2(c.X - m.X * 0.5f - vf * 0.35f, c.Y + r * 0.35f - vf * 0.52f,
                                  m.X + vf * 0.70f, vf * 1.18f);
                KitChrome.Fill(this, KitShape.Pill, b, KitGeometry.ForGenre(_genre),
                               new Color(face.R * 0.58f, face.G * 0.56f, face.B * 0.62f, 1f),
                               ink, Mathf.Max(1f, vf * 0.08f));
                KitChrome.DrawText(this, _genre, font,
                                   new Vector2(c.X - m.X * 0.5f, b.Position.Y + (b.Size.Y + m.Y * 0.58f) * 0.5f),
                                   value, vf, UiSurface.Text(this));
            }

            KitChrome.DrawFocusRing(this, _genre, new Rect2(Vector2.Zero, Size), KitShape.Ellipse, 0.8f);
        }
    }
}
