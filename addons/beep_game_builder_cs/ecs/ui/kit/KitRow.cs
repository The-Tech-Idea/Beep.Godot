using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A list row — CATALOGUE-FROM-ART.md section B's `MissionRow` and `PlayerRow`, which are one
    /// widget with different payloads: a rank or index, a title with a subtitle, a value, and an
    /// optional state chip.
    ///
    /// Selection is a FILL, per the art pass's convention-by-widget-class finding: "card
    /// carousels use an outline, tab strips use fill/elevation, <b>list rows use a fill</b>"
    /// (racing1: "fill the row with the only saturated colour"). Using the card's outline here
    /// would be the wrong mechanism for the class.
    ///
    /// Rows alternate their plate very slightly so a long list stays readable without needing a
    /// separator per row — the "tile separator 0.50 x face" note in gameui2 is the alternative,
    /// and banding is cheaper and survives a restyle.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRow : KitControl
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

        [Export] public string Rank { get => _rank; set { _rank = value ?? ""; QueueRedraw(); } }
        private string _rank = "1";

        [Export] public string Title { get => _title; set { _title = value ?? ""; QueueRedraw(); } }
        private string _title = "Recover the Cargo";

        [Export] public string Subtitle { get => _sub; set { _sub = value ?? ""; QueueRedraw(); } }
        private string _sub = "";

        [Export] public string Value { get => _value; set { _value = value ?? ""; QueueRedraw(); } }
        private string _value = "1,240";

        /// <summary>Short state word — NEW, DONE, LOCKED. Empty hides the chip.</summary>
        [Export] public string State_ { get => _state; set { _state = value ?? ""; QueueRedraw(); } }
        private string _state = "";

        [Export] public UiSurface.Role StateRole { get; set; } = UiSurface.Role.Success;

        [Export] public bool Selected { get => _sel; set { _sel = value; QueueRedraw(); } }
        private bool _sel;

        /// <summary>Odd rows take a slightly different plate. Set by the list, not the row.</summary>
        [Export] public bool Alternate { get; set; }
        private bool _hover;

        [Signal] public delegate void ActivatedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            MouseEntered += () => { _hover = true; QueueRedraw(); };
            MouseExited += () => { _hover = false; QueueRedraw(); };
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 18f, fs * 3f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key && KitChrome.IsConfirmKey(key))
            {
                Selected = true;
                EmitSignal(SignalName.Activated);
                AcceptEvent();
                return;
            }
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Selected = true;
                GrabFocus();
                EmitSignal(SignalName.Activated);
                AcceptEvent();
            }
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 18f, fs * 3f);
        }

        public override void _Draw()
        {
            if (Size.X < 24f || Size.Y < 10f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            var r = new Rect2(Vector2.Zero, Size);

            Color plate = _sel
                ? UiSurface.Semantic(this, UiSurface.Role.Accent)          // fill: the row class's cue
                : Alternate
                    ? new Color(face.R * 0.86f, face.G * 0.86f, face.B * 0.90f, 1f)
                    : new Color(face.R * 0.94f, face.G * 0.94f, face.B * 0.97f, 1f);
            if (_hover && !_sel)
                plate = new Color(Mathf.Lerp(plate.R, UiSurface.Semantic(this, UiSurface.Role.Info).R, 0.18f),
                                  Mathf.Lerp(plate.G, UiSurface.Semantic(this, UiSurface.Role.Info).G, 0.18f),
                                  Mathf.Lerp(plate.B, UiSurface.Semantic(this, UiSurface.Role.Info).B, 0.18f), 1f);

            DrawShape(r, ActiveShape, plate, ink, Mathf.Max(1f, g.Rim * 0.5f * (fs / 14f)));
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), r, ActiveShape, 0.75f);
            if (font == null) return;

            Color txt = _sel && UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : UiSurface.Text(this);

            float pad = fs * 0.8f;
            float x = pad;

            if (!string.IsNullOrEmpty(_rank))
            {
                Vector2 m = font.GetStringSize(_rank, HorizontalAlignment.Left, -1, fs);
                DrawText(font, new Vector2(x, (Size.Y + m.Y * 0.6f) * 0.5f),
                           _rank, fs, txt with { A = 0.7f });
                x += Mathf.Max(m.X, fs * 1.4f) + pad;
            }

            // Value hugs the right edge; the state chip sits just inside it.
            float rx = Size.X - pad;
            if (!string.IsNullOrEmpty(_value))
            {
                int vf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                           new Vector2(Size.X * 0.24f, Size.Y * 0.62f),
                                           _value, font, min: 8);
                Vector2 vm = font.GetStringSize(_value, HorizontalAlignment.Left, -1, vf);
                DrawText(font, new Vector2(rx - vm.X, (Size.Y + vm.Y * 0.6f) * 0.5f),
                           _value, vf, txt);
                rx -= vm.X + pad;
            }

            if (!string.IsNullOrEmpty(_state))
            {
                int cs = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(Size.X * 0.18f, Size.Y * 0.48f),
                                           _state, font, min: 8);
                Vector2 cm = font.GetStringSize(_state, HorizontalAlignment.Left, -1, cs);
                float cw = cm.X + cs * 1.1f, ch = cs * 1.5f;
                var chip = new Rect2(rx - cw, (Size.Y - ch) * 0.5f, cw, ch);
                Color cc = UiSurface.Semantic(this, StateRole);
                DrawShape(chip, KitShape.Pill, cc, ink, 1.5f);
                DrawText(font, new Vector2(chip.Position.X + (cw - cm.X) * 0.5f, chip.Position.Y + (ch + cm.Y * 0.6f) * 0.5f),
                           _state, cs, UiSurface.Luminance(cc) > 0.5f
                               ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                rx = chip.Position.X - pad;
            }

            // Title, with the subtitle beneath it when there is room for two lines.
            float titleW = Mathf.Max(fs * 2f, rx - x);
            bool twoLine = !string.IsNullOrEmpty(_sub) && Size.Y > fs * 2.6f;
            int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body,
                                       new Vector2(titleW, Size.Y * (twoLine ? 0.34f : 0.62f)),
                                       _title, font, min: 8);
            Vector2 tm = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf);
            float ty = twoLine ? Size.Y * 0.44f : (Size.Y + tm.Y * 0.6f) * 0.5f;
            DrawText(font, new Vector2(x, ty), _title, tf, txt);
            if (twoLine)
            {
                int ss = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                           new Vector2(titleW, Size.Y * 0.28f),
                                           _sub, font, min: 8);
                DrawText(font, new Vector2(x, Size.Y * 0.78f), _sub, ss, txt with { A = 0.65f });
            }
        }
    }
}
