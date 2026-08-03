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

        [Export] public string Text { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        private string _text = "Hint";

        [Export] public TailSide Tail { get => _tail; set { _tail = value; QueueRedraw(); } }
        private TailSide _tail = TailSide.Bottom;

        /// <summary>Where the tail sits along its edge, 0..1. Lets one tooltip point at a control
        /// that is not centred under it.</summary>
        [Export(PropertyHint.Range, "0.05,0.95,0.01")] public float TailOffset { get; set; } = 0.5f;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;   // a hint never takes the click
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 9f, fs * 2.35f);
            }
        }

        private float TailSize => Mathf.Clamp(UiSurface.FontSize(this) * 0.42f, 5f, 9f);

        public override void _Draw()
        {
            if (Size.X < 10f || Size.Y < 8f) return;

            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float t = TailSize;

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
                                       new Vector2(body.Size.X * 0.82f, body.Size.Y * 0.38f),
                                       _text, font, min: 8);
            Vector2 m = font.GetStringSize(_text, HorizontalAlignment.Left, -1, tf);
            DrawText(font, new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f, body.Position.Y + (body.Size.Y + m.Y * 0.6f) * 0.5f),
                       _text, tf, txt);
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
