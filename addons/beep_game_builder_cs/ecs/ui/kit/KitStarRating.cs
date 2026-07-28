using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A star rating — CATALOGUE-FROM-ART.md F.2 (`StarRating`), and the score readout every
    /// level-complete and level-select screen in the puzzle/platformer families uses.
    ///
    /// The framework already ships star art in `level_complete`, `level_results` and
    /// `level_select`, drawn per scene; this is the widget those screens should share so three
    /// stars mean the same thing and are lit the same way everywhere.
    ///
    /// An unearned star DRAINS SATURATION rather than vanishing (the 7x settled rule): the
    /// player must be able to see how many stars a level HAS, not just how many they earned, or
    /// the readout says nothing about what is left to do.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitStarRating : KitControl
    {
        [Export(PropertyHint.Range, "1,10,1")]
        public int Total { get => _total; set { _total = Mathf.Max(1, value); QueueRedraw(); } }
        private int _total = 3;

        [Export(PropertyHint.Range, "0,10,1")]
        public int Earned { get => _earned; set { _earned = Mathf.Max(0, value); QueueRedraw(); } }
        private int _earned = 2;

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Warning;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 1.9f * _total, fs * 2f);
            }
        }

        public override void _Draw()
        {
            if (Size.X < 8f || Size.Y < 6f) return;

            Color lit = UiSurface.Semantic(this, Role);
            float l = UiSurface.Luminance(lit);
            // Unearned: same colour, saturation drained. Not hidden, not a different hue.
            Color dim = new(Mathf.Lerp(lit.R, l, 0.92f) * 0.6f,
                            Mathf.Lerp(lit.G, l, 0.92f) * 0.6f,
                            Mathf.Lerp(lit.B, l, 0.92f) * 0.6f, 1f);
            Color ink = InkColor();

            float pitch = Size.X / _total;
            float r = Mathf.Min(pitch, Size.Y) * 0.42f;

            for (int i = 0; i < _total; i++)
            {
                var c = new Vector2(pitch * (i + 0.5f), Size.Y * 0.5f);
                // Earned stars sit slightly higher — the reference screens lift them so the row
                // reads even in a thumbnail.
                if (i < _earned) c.Y -= Size.Y * 0.06f;
                DrawStar(c, r, i < _earned ? lit : dim, ink);
            }
        }

        private void DrawStar(Vector2 c, float r, Color fill, Color ink)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float rad = (i % 2 == 0) ? r : r * 0.44f;
                float ang = -Mathf.Pi * 0.5f + i * Mathf.Pi / 5f;
                pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            }
            DrawColoredPolygon(pts, fill);
            var closed = new Vector2[11];
            pts.CopyTo(closed, 0);
            closed[10] = pts[0];
            DrawPolyline(closed, ink, Mathf.Max(1.5f, r * 0.12f));
        }
    }
}
