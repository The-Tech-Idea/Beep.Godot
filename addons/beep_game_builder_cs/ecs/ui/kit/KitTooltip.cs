using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A hint tooltip WITH A TAIL — CATALOGUE-FROM-ART.md section C (`HintTooltip`, "with a
    /// tail"). The tail is the whole point: it names which control the tip belongs to, which a
    /// floating rectangle cannot do when three controls sit close together.
    ///
    /// The art pass's polarity finding applies here — "one element class flips polarity"
    /// (5 references), and tooltips are one of the classes that does. So a tooltip is drawn at
    /// the OPPOSITE polarity to the surface it sits over, rather than as another panel in the
    /// same tone.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitTooltip : KitControl
    {
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        public enum TailSide { Bottom, Top, Left, Right }

        [Export]
        public string Text
        {
            get => _text;
            set
            {
                string next = value ?? "";
                if (_text == next) return;
                _text = next;
                RefreshMinimumAndRedraw();
            }
        }
        private string _text = "Hint";

        [Export] public TailSide Tail { get => _tail; set { if (_tail == value) return; _tail = value; RefreshMinimumAndRedraw(); } }
        private TailSide _tail = TailSide.Bottom;

        /// <summary>Where the tail sits along its edge, 0..1. Lets one tooltip point at a control
        /// that is not centred under it.</summary>
        [Export(PropertyHint.Range, "0.05,0.95,0.01")]
        public float TailOffset
        {
            get => _tailOffset;
            set
            {
                float next = Mathf.Clamp(value, 0.05f, 0.95f);
                if (Mathf.IsEqualApprox(_tailOffset, next)) return;
                _tailOffset = next;
                RefreshVisualAndRedraw();
            }
        }
        private float _tailOffset = 0.5f;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            Font? font = KitFont();
            string text = KitCase(_text);
            float pad = Mathf.Max(5f, textFs * 0.55f);
            float textW = Mathf.Clamp(LongestLineWidth(font, text, textFs), fs * 5.5f, fs * 16f);
            int lines = EstimateWrappedLineCount(font, text, textFs, textW);
            float lineH = font?.GetHeight(textFs) * 1.08f ?? textFs * 1.25f;

            float w = Mathf.Max(fs * 9f, textW + pad * 2f);
            float h = Mathf.Max(fs * 2.35f, lineH * lines + pad * 2f);
            float tail = TailSizeFor(fs);
            if (_tail == TailSide.Left || _tail == TailSide.Right)
                w += tail;
            else
                h += tail;
            return new Vector2(w, h);
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private static float TailSizeFor(int fs) => Mathf.Clamp(fs * 0.42f, 5f, 9f);

        private static float LongestLineWidth(Font? font, string text, int fs)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fs * 4f;

            float width = 0f;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                width = Mathf.Max(width, TextWidth(font, line, fs));
            return width;
        }

        private static int EstimateWrappedLineCount(Font? font, string text, int fs, float width)
        {
            if (string.IsNullOrWhiteSpace(text) || width <= 1f)
                return 1;

            int count = 0;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                count += Mathf.Max(1, Mathf.CeilToInt(TextWidth(font, line, fs) / width));
            return Mathf.Clamp(count, 1, 2);
        }

        private static float TextWidth(Font? font, string text, int fs)
            => font?.GetStringSize(text ?? "", HorizontalAlignment.Left, -1, fs).X ?? (text ?? "").Length * fs * 0.55f;

        public override void _Draw()
        {
            if (Size.X < 10f || Size.Y < 8f) return;

            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float t = TailSizeFor(fs);

            // Opposite polarity to the surface it overlays.
            bool lightSurface = UiSurface.Luminance(face) > 0.5f;
            Color plate = lightSurface
                ? new Color(face.R * 0.20f, face.G * 0.20f, face.B * 0.24f, 0.96f)
                : new Color(Mathf.Lerp(face.R, 1f, 0.86f), Mathf.Lerp(face.G, 1f, 0.86f),
                            Mathf.Lerp(face.B, 1f, 0.88f), 0.96f);
            Color txt = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);

            // The body is inset on the tail's edge so the tail has room inside our own rect —
            // the same containment rule KitPanel's banner had to learn.
            var body = _tail switch
            {
                TailSide.Top => new Rect2(0f, t, Size.X, Size.Y - t),
                TailSide.Left => new Rect2(t, 0f, Size.X - t, Size.Y),
                TailSide.Right => new Rect2(0f, 0f, Size.X - t, Size.Y),
                _ => new Rect2(0f, 0f, Size.X, Size.Y - t),
            };
            DrawShape(body, KitShape.Round, plate, ink, Mathf.Max(1f, Geo.Rim * 0.45f * (fs / 14f)));

            DrawTail(body, plate, t);

            if (font == null || string.IsNullOrEmpty(_text)) return;
            // A tooltip is quiet supporting text, not a headline. 0.86 x 0.58 of the body let the
            // caption grow until it filled the plate edge to edge with no breathing room, which
            // is what made it read as shouting.
            int tf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                       new Vector2(body.Size.X * 0.82f, body.Size.Y * 0.44f),
                                       _text, font, min: 8);
            Rect2 textBox = body.Grow(-Mathf.Max(5f, tf * 0.55f));
            KitChrome.DrawWrappedText(this, KitChrome.GenreOf(this), font, textBox, _text, tf, txt,
                                      HorizontalAlignment.Center, maxLines: 2);
        }

        private void DrawTail(Rect2 body, Color plate, float t)
        {
            float o = Mathf.Clamp(TailOffset, 0.05f, 0.95f);
            Vector2[] pts = _tail switch
            {
                TailSide.Top => new[]
                {
                    new Vector2(body.Position.X + body.Size.X * o - t, body.Position.Y),
                    new Vector2(body.Position.X + body.Size.X * o + t, body.Position.Y),
                    new Vector2(body.Position.X + body.Size.X * o, body.Position.Y - t),
                },
                TailSide.Left => new[]
                {
                    new Vector2(body.Position.X, body.Position.Y + body.Size.Y * o - t),
                    new Vector2(body.Position.X, body.Position.Y + body.Size.Y * o + t),
                    new Vector2(body.Position.X - t, body.Position.Y + body.Size.Y * o),
                },
                TailSide.Right => new[]
                {
                    new Vector2(body.End.X, body.Position.Y + body.Size.Y * o - t),
                    new Vector2(body.End.X, body.Position.Y + body.Size.Y * o + t),
                    new Vector2(body.End.X + t, body.Position.Y + body.Size.Y * o),
                },
                _ => new[]
                {
                    new Vector2(body.Position.X + body.Size.X * o - t, body.End.Y),
                    new Vector2(body.Position.X + body.Size.X * o + t, body.End.Y),
                    new Vector2(body.Position.X + body.Size.X * o, body.End.Y + t),
                },
            };
            DrawColoredPolygon(pts, plate);
        }
    }
}
