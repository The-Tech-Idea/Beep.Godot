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
    public partial class KitStarRating : Godot.Range
    {
        /// <summary>A chip: takes the theme's chip corner, which the references vary
        /// independently of the button corner.</summary>
        private const KitWidgetClass Class = KitWidgetClass.Chip;

        /// <summary>How many stars there are. This is Range's MaxValue — a star rating is a
        /// value within a range, which is exactly what Range models, so it gets Range's
        /// MinValue/MaxValue/Step/Value and its ValueChanged signal instead of a private pair of
        /// ints nothing else can read.</summary>
        [Export(PropertyHint.Range, "1,10,1")]
        public int Total
        {
            get => Mathf.Max(1, (int)MaxValue);
            set { MaxValue = Mathf.Max(1, value); QueueRedraw(); }
        }

        /// <summary>How many are filled. This is Range's Value.</summary>
        [Export(PropertyHint.Range, "0,10,1")]
        public int Earned
        {
            get => (int)Value;
            set { Value = Mathf.Clamp(value, 0, MaxValue); QueueRedraw(); }
        }

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private int _hover = -1;

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Warning;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            MouseFilter = MouseFilterEnum.Stop;
            // Range has NO theme art of its own -- no stylebox, no icon -- so unlike Slider and
            // ProgressBar there is nothing to blank and nothing whose minimum size vanishes with
            // it. That is what makes it the right base here rather than a convenient one.
            MinValue = 0; Step = 1;
            if (MaxValue < 1) MaxValue = 3;
            ValueChanged += _ => QueueRedraw();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 1.9f * Total, fs * 2f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion mm)
            {
                int next = HitStar(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = HitStar(mb.Position);
                if (hit < 0) return;
                Earned = hit + 1;
                AcceptEvent();
            }
        }

        private int HitStar(Vector2 p)
        {
            int total = Total;
            if (total <= 0 || Size.X <= 1f) return -1;
            int i = Mathf.FloorToInt(p.X / (Size.X / total));
            return i >= 0 && i < total && p.Y >= 0f && p.Y <= Size.Y ? i : -1;
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
            Color ink = UiSurface.Ink(UiSurface.Of(this));

            float pitch = Size.X / Total;
            float r = Mathf.Min(pitch, Size.Y) * 0.42f;

            for (int i = 0; i < Total; i++)
            {
                var c = new Vector2(pitch * (i + 0.5f), Size.Y * 0.5f);
                // Earned stars sit slightly higher — the reference screens lift them so the row
                // reads even in a thumbnail.
                if (i < Earned) c.Y -= Size.Y * 0.06f;
                if (i == _hover) c.Y -= Size.Y * 0.04f;
                DrawStar(c, r, i < Earned ? lit : dim, ink);
                if (i == _hover)
                    DrawArc(c, r * 1.08f, 0f, Mathf.Tau, 24,
                            UiSurface.Semantic(this, UiSurface.Role.Info), Mathf.Max(1.2f, r * 0.08f));
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
