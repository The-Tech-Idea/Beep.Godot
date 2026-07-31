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

        [Signal] public delegate void ActivatedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 18f, fs * 3f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Selected = true;
                EmitSignal(SignalName.Activated);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X < 24f || Size.Y < 10f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);
            var r = new Rect2(Vector2.Zero, Size);

            Color plate = _sel
                ? UiSurface.Semantic(this, UiSurface.Role.Accent)          // fill: the row class's cue
                : Alternate
                    ? new Color(face.R * 0.86f, face.G * 0.86f, face.B * 0.90f, 1f)
                    : new Color(face.R * 0.94f, face.G * 0.94f, face.B * 0.97f, 1f);

            DrawShape(r, ActiveShape, plate, ink, Mathf.Max(1f, g.Rim * 0.5f * (fs / 14f)));
            if (font == null) return;

            Color txt = _sel && UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : UiSurface.Text(this);

            float pad = fs * 0.8f;
            float x = pad;

            if (!string.IsNullOrEmpty(_rank))
            {
                Vector2 m = font.GetStringSize(_rank, HorizontalAlignment.Left, -1, fs);
                DrawString(font, new Vector2(x, (Size.Y + m.Y * 0.6f) * 0.5f),
                           _rank, HorizontalAlignment.Left, -1, fs, txt with { A = 0.7f });
                x += Mathf.Max(m.X, fs * 1.4f) + pad;
            }

            // Title, with the subtitle beneath it when there is room for two lines.
            bool twoLine = !string.IsNullOrEmpty(_sub) && Size.Y > fs * 2.6f;
            Vector2 tm = font.GetStringSize(_title, HorizontalAlignment.Left, -1, fs);
            float ty = twoLine ? Size.Y * 0.44f : (Size.Y + tm.Y * 0.6f) * 0.5f;
            DrawString(font, new Vector2(x, ty), _title, HorizontalAlignment.Left, -1, fs, txt);
            if (twoLine)
            {
                int ss = Mathf.Max(8, Mathf.RoundToInt(fs * 0.8f));
                DrawString(font, new Vector2(x, Size.Y * 0.78f), _sub,
                           HorizontalAlignment.Left, -1, ss, txt with { A = 0.65f });
            }

            // Value hugs the right edge; the state chip sits just inside it.
            float rx = Size.X - pad;
            if (!string.IsNullOrEmpty(_value))
            {
                Vector2 vm = font.GetStringSize(_value, HorizontalAlignment.Left, -1, fs);
                DrawString(font, new Vector2(rx - vm.X, (Size.Y + vm.Y * 0.6f) * 0.5f),
                           _value, HorizontalAlignment.Left, -1, fs, txt);
                rx -= vm.X + pad;
            }

            if (string.IsNullOrEmpty(_state)) return;
            int cs = Mathf.Max(8, Mathf.RoundToInt(fs * 0.72f));
            Vector2 cm = font.GetStringSize(_state, HorizontalAlignment.Left, -1, cs);
            float cw = cm.X + cs * 1.1f, ch = cs * 1.5f;
            var chip = new Rect2(rx - cw, (Size.Y - ch) * 0.5f, cw, ch);
            Color cc = UiSurface.Semantic(this, StateRole);
            DrawShape(chip, KitShape.Pill, cc, ink, 1.5f);
            DrawString(font, new Vector2(chip.Position.X + (cw - cm.X) * 0.5f,
                                         chip.Position.Y + (ch + cm.Y * 0.6f) * 0.5f),
                       _state, HorizontalAlignment.Left, -1, cs,
                       UiSurface.Luminance(cc) > 0.5f
                           ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }
    }
}
