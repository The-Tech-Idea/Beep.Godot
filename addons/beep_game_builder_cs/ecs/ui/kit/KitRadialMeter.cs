using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A ring gauge — CATALOGUE-FROM-ART.md section E (`RadialMeter`), and the shape Don't
    /// Starve's health/hunger/sanity cluster and every racing rev counter are built from.
    ///
    /// Kept separate from <see cref="KitMeter"/> because the two do not share a layout problem:
    /// a bar consumes horizontal space and stacks vertically, a ring consumes a square and
    /// clusters radially. Genres pick one; `docs/hud/survival.md` names rings for Don't Starve
    /// and bars for Valheim, and both are legitimate.
    ///
    /// Segmented by default, like the bar — the same 7x settled rule ("segmented progress is the
    /// default, continuous is the exception") applies whatever the track's shape.
    /// The empty arc is a dark tint of the fill's own hue, never grey.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRadialMeter : KitControl
    {
        [Export(PropertyHint.Range, "0.0,1.0,0.001")]
        public float Value { get => _value; set { _value = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } }
        private float _value = 0.68f;

        [Export(PropertyHint.Range, "0,48,1")]
        public int Segments { get => _segments; set { _segments = Mathf.Max(0, value); QueueRedraw(); } }
        private int _segments = 16;

        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Success;

        /// <summary>Gap at the bottom, in degrees. 0 is a closed ring; ~70 gives the open dial
        /// a rev counter and most mobile gauges use.</summary>
        [Export(PropertyHint.Range, "0,180,1")] public float GapDegrees { get; set; } = 60f;

        /// <summary>Ring thickness as a fraction of the radius.</summary>
        [Export(PropertyHint.Range, "0.08,0.6,0.01")] public float Thickness { get; set; } = 0.26f;

        [Export] public string CentreText { get => _centre; set { _centre = value ?? ""; QueueRedraw(); } }
        private string _centre = "";

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 4.5f, fs * 4.5f);
            }
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 8f) return;

            var c = Size * 0.5f;
            float radius = d * 0.5f * 0.88f;
            float w = Mathf.Max(2f, radius * Thickness);
            Color fill = UiSurface.Semantic(this, Fill);
            Color track = new(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f);

            // Start at the bottom-left of the gap and sweep clockwise through the top.
            float gap = Mathf.DegToRad(Mathf.Clamp(GapDegrees, 0f, 180f));
            float start = Mathf.Pi * 0.5f + gap * 0.5f;
            float sweep = Mathf.Tau - gap;

            DrawArc(c, radius, start, start + sweep, 64, track, w);

            if (_value <= 0f) { DrawCentre(c, fill); return; }

            if (_segments <= 0)
            {
                DrawArc(c, radius, start, start + sweep * _value, 64, fill, w);
            }
            else
            {
                // A visible gap between segments, scaled so it survives at small sizes.
                float per = sweep / _segments;
                float pad = per * 0.18f;
                float lit = _value * _segments;
                for (int i = 0; i < _segments; i++)
                {
                    float amount = Mathf.Clamp(lit - i, 0f, 1f);
                    if (amount <= 0.001f) break;
                    float a0 = start + per * i + pad * 0.5f;
                    float a1 = a0 + (per - pad) * amount;
                    if (a1 > a0) DrawArc(c, radius, a0, a1, 8, fill, w);
                }
            }

            DrawCentre(c, fill);
        }

        private void DrawCentre(Vector2 c, Color fill)
        {
            if (string.IsNullOrEmpty(_centre)) return;
            var font = GetThemeDefaultFont();
            if (font == null) return;
            // The number inside the ring is a VALUE, and the ring's own inner diameter is the
            // box it has to fit. A flat 1.1x body size overflowed a small dial and looked lost in
            // a large one.
            float inner = Mathf.Min(Size.X, Size.Y) * 0.62f;
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                       new Vector2(inner, inner * 0.72f), _centre, font);
            Vector2 m = font.GetStringSize(_centre, HorizontalAlignment.Left, -1, fs);
            DrawString(font, new Vector2(c.X - m.X * 0.5f, c.Y + m.Y * 0.32f),
                       _centre, HorizontalAlignment.Left, -1, fs, UiSurface.Text(this));
        }
    }
}
