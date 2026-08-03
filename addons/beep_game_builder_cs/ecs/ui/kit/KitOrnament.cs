using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Non-interactive decoration that promotes a plain plate into a reward or achievement:
    /// <b>crown, wings, laurel, trophy, starburst, ribbon tail</b>.
    ///
    /// PLAN.md phase D lists these as overhanging attachments and notes the golden-kit sheet
    /// "uses them constantly"; section E lists `StarburstBadge` separately, which is the same
    /// idea at a different silhouette, so it is a variant here rather than its own class.
    ///
    /// Deliberately inert: <see cref="Control.MouseFilterEnum.Ignore"/>, no signals, no states.
    /// An ornament that swallows a click is worse than no ornament, and these are always drawn
    /// over something the player is meant to be able to press.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitOrnament : KitControl
    {
        public enum OrnamentKind { Crown, Wings, Laurel, Trophy, Starburst, RibbonTail }

        [Export] public OrnamentKind Kind { get => _kind; set { _kind = value; QueueRedraw(); } }
        private OrnamentKind _kind = OrnamentKind.Crown;

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Warning;

        [Export(PropertyHint.Range, "0.35,1.0,0.05")] public float OrnamentScale { get; set; } = 0.62f;

        public override void _Ready()
        {
            base._Ready();
            // Inert by construction, not by the scene remembering to set it.
            MouseFilter = MouseFilterEnum.Ignore;
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 2.1f, fs * 1.35f);
            }
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 5f) return;
            Color c = UiSurface.Semantic(this, Role);
            Color ink = InkColor();
            // 0.09 of the short side put a ~5px black band around a 60px crown, and Outline()
            // draws an inner pass on top of that — the silhouette disappeared under its own
            // border. An ornament is a SHAPE; the outline only has to separate it from the
            // ground, so it stays thin and the form does the work.
            float w = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.032f);

            float scale = Mathf.Clamp(OrnamentScale, 0.35f, 1f);
            DrawSetTransform(Size * (1f - scale) * 0.5f, 0f, Vector2.One * scale);

            switch (_kind)
            {
                case OrnamentKind.Crown: Crown(c, ink, w); break;
                case OrnamentKind.Wings: Wings(c, ink, w); break;
                case OrnamentKind.Laurel: Laurel(c, ink, w); break;
                case OrnamentKind.Trophy: Trophy(c, ink, w); break;
                case OrnamentKind.Starburst: Starburst(c, ink, w); break;
                case OrnamentKind.RibbonTail: RibbonTail(c, ink, w); break;
            }
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        private void Outline(Vector2[] pts, Color fill, Color ink, float w)
        {
            DrawColoredPolygon(pts, fill);
            var closed = new Vector2[pts.Length + 1];
            pts.CopyTo(closed, 0);
            closed[^1] = pts[0];
            DrawPolyline(closed, ink, w);
            if (pts.Length < 3) return;

            Vector2 c = Vector2.Zero;
            foreach (var p in pts) c += p;
            c /= pts.Length;
            var inner = new Vector2[pts.Length + 1];
            if (Size.X >= 46f && Size.Y >= 32f)
            {
                for (int i = 0; i < pts.Length; i++)
                    inner[i] = c.Lerp(pts[i], 0.84f);
                inner[^1] = inner[0];
                DrawPolyline(inner, new Color(1, 1, 1, 0.12f), Mathf.Max(1f, w * 0.35f));
            }
        }

        private void Crown(Color c, Color ink, float w)
        {
            float h = Size.Y, x = Size.X;
            Outline(new[]
            {
                new Vector2(0f, h), new Vector2(0f, h * 0.35f),
                new Vector2(x * 0.25f, h * 0.65f), new Vector2(x * 0.5f, h * 0.12f),
                new Vector2(x * 0.75f, h * 0.65f), new Vector2(x, h * 0.35f),
                new Vector2(x, h),
            }, c, ink, w);
        }

        private void Wings(Color c, Color ink, float w)
        {
            float h = Size.Y, x = Size.X;
            // Two mirrored sweeps flanking a gap the host's own art shows through.
            Outline(new[]
            {
                new Vector2(0f, h * 0.35f), new Vector2(x * 0.38f, h * 0.15f),
                new Vector2(x * 0.42f, h * 0.55f), new Vector2(x * 0.10f, h * 0.85f),
            }, c, ink, w);
            Outline(new[]
            {
                new Vector2(x, h * 0.35f), new Vector2(x * 0.62f, h * 0.15f),
                new Vector2(x * 0.58f, h * 0.55f), new Vector2(x * 0.90f, h * 0.85f),
            }, c, ink, w);
        }

        private void Laurel(Color c, Color ink, float w)
        {
            // Two arcs of leaves curving up toward a gap at the top.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float t = i / 4f;
                    float ang = Mathf.Pi * (0.55f + 0.35f * t) * side;
                    var p = Size * 0.5f + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * Size.X * 0.36f;
                    float rr = Mathf.Max(1.5f, Size.Y * (0.13f - t * 0.04f));
                    DrawCircle(p, rr, c);
                    DrawCircle(p - new Vector2(rr * 0.22f, rr * 0.22f), rr * 0.30f,
                               new Color(1, 1, 1, 0.22f));
                }
            }
        }

        private void Trophy(Color c, Color ink, float w)
        {
            float h = Size.Y, x = Size.X;
            Outline(new[]
            {
                new Vector2(x * 0.28f, h * 0.10f), new Vector2(x * 0.72f, h * 0.10f),
                new Vector2(x * 0.64f, h * 0.55f), new Vector2(x * 0.36f, h * 0.55f),
            }, c, ink, w);
            DrawRect(new Rect2(x * 0.44f, h * 0.55f, x * 0.12f, h * 0.22f), c);
            var b = new Rect2(x * 0.28f, h * 0.77f, x * 0.44f, h * 0.16f);
            DrawRect(b, c);
            DrawRect(b, ink, false, w);
            // Handles.
            DrawArc(new Vector2(x * 0.28f, h * 0.26f), h * 0.14f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 12, ink, w);
            DrawArc(new Vector2(x * 0.72f, h * 0.26f), h * 0.14f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 12, ink, w);
            DrawCircle(new Vector2(x * 0.50f, h * 0.26f), h * 0.08f, new Color(1, 1, 1, 0.24f));
        }

        private void Starburst(Color c, Color ink, float w)
        {
            var ctr = Size * 0.5f;
            float r = Mathf.Min(Size.X, Size.Y) * 0.48f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float rad = (i % 2 == 0) ? r : r * 0.58f;
                float ang = -Mathf.Pi * 0.5f + i * Mathf.Tau / 10f;
                pts[i] = ctr + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            }
            Outline(pts, c, ink, w);
        }

        private void RibbonTail(Color c, Color ink, float w)
        {
            float h = Size.Y, x = Size.X;
            // Two tails with notched ends, hanging below whatever they are pinned to.
            Outline(new[]
            {
                new Vector2(x * 0.18f, 0f), new Vector2(x * 0.44f, 0f),
                new Vector2(x * 0.40f, h), new Vector2(x * 0.31f, h * 0.78f),
                new Vector2(x * 0.22f, h),
            }, c, ink, w);
            Outline(new[]
            {
                new Vector2(x * 0.56f, 0f), new Vector2(x * 0.82f, 0f),
                new Vector2(x * 0.78f, h), new Vector2(x * 0.69f, h * 0.78f),
                new Vector2(x * 0.60f, h),
            }, c, ink, w);
        }
    }
}
