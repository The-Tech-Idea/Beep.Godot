using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// RPG vital orb: a circular reservoir filled from bottom to top.
    /// Use for life/mana where a horizontal progress bar reads too generic for fantasy RPG HUDs.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitOrbMeter : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

        [Export(PropertyHint.Range, "0.0,1.0,0.001")]
        public float Value { get => _value; set { _value = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } }
        private float _value = 1f;

        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Success;

        [Export] public string CentreText { get => _centre; set { _centre = value ?? ""; QueueRedraw(); } }
        private string _centre = "";

        [Export] public string Symbol { get => _symbol; set { _symbol = value ?? ""; QueueRedraw(); } }
        private string _symbol = "";

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = _GetMinimumSize();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 5.2f, fs * 5.2f);
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 16f) return;

            Vector2 c = Size * 0.5f;
            float r = d * 0.44f;
            Color face = FaceColor();
            Color ink = InkColor();
            Color fill = UiSurface.Semantic(this, Fill);
            Color dark = new(fill.R * 0.22f, fill.G * 0.20f, fill.B * 0.24f, 1f);
            Color rim = KitChrome.Rim(face, Geo);

            DrawCircle(c + new Vector2(0, d * 0.035f), r * 1.06f, new Color(0, 0, 0, 0.42f));
            DrawCircle(c, r * 1.08f, rim);
            DrawCircle(c, r * 0.98f, dark);
            DrawFill(c, r * 0.94f, fill);

            DrawArc(c, r * 1.00f, 0, Mathf.Tau, 96, ink, Mathf.Max(2f, d * 0.035f));
            DrawArc(c - new Vector2(r * 0.16f, r * 0.20f), r * 0.52f,
                    Mathf.DegToRad(205), Mathf.DegToRad(292), 24,
                    new Color(1, 1, 1, 0.26f), Mathf.Max(2f, d * 0.030f));

            DrawCentreText(c, r);
        }

        private void DrawFill(Vector2 c, float r, Color fill)
        {
            if (_value <= 0f) return;
            if (_value >= 0.995f)
            {
                DrawCircle(c, r, fill);
                return;
            }

            float waterY = c.Y + r - r * 2f * _value;
            float dy = Mathf.Clamp((waterY - c.Y) / r, -1f, 1f);
            float xSpan = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy)) * r;
            float leftAngle = Mathf.Atan2(dy, -xSpan / r);
            float rightAngle = Mathf.Atan2(dy, xSpan / r);

            var points = new System.Collections.Generic.List<Vector2>
            {
                new(c.X - xSpan, waterY)
            };
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                float a = Mathf.Lerp(leftAngle, rightAngle + Mathf.Tau, t);
                points.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            points.Add(new Vector2(c.X + xSpan, waterY));
            DrawColoredPolygon(points.ToArray(), fill);

            DrawLine(new Vector2(c.X - xSpan, waterY), new Vector2(c.X + xSpan, waterY),
                     new Color(1, 1, 1, 0.18f), Mathf.Max(1f, r * 0.035f));
        }

        /// <summary>Named DrawCentreText, not DrawText: a DrawText member declared here would HIDE
        /// <see cref="KitControl.DrawText"/> by name (C# looks up the most-derived declaration and
        /// stops), so the routed helper became uncallable and this drew its own shadow instead.</summary>
        private void DrawCentreText(Vector2 c, float r)
        {
            var font = KitFont();
            if (font == null) return;

            string text = string.IsNullOrWhiteSpace(_centre) ? _symbol : _centre;
            if (string.IsNullOrWhiteSpace(text)) return;

            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                       new Vector2(r * 1.35f, r * 0.58f), text, font, min: 8);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Vector2 p = new(c.X - m.X * 0.5f, c.Y + m.Y * 0.34f);
            // Routed through KitControl.DrawText so the theme's text_treatment reaches the orb's
            // centre value. The hand-drawn 1px shadow it replaced was a treatment of its own.
            DrawText(font, p, text, fs, UiSurface.Text(this));
        }
    }
}
