using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// How a panel ATTACHES TO THE SCREEN — CATALOGUE-FROM-ART.md section F.1, an entire family
    /// (`ChainHang`, `RopeHang`, `NailPin`, `TapeCorner`, `ScrollRoll`, `VineFrame`) that the kit
    /// had nothing for.
    ///
    /// It is one widget with variants for the same reason <see cref="KitChip"/> is: they are one
    /// idea — a fixing drawn ABOVE or ACROSS a panel's edge so the panel reads as a physical
    /// object hung in the world rather than a rectangle floating in screen space. `ui5.png`
    /// proves the axis by drawing one dialog geometry in ~10 materials with no layout change.
    ///
    /// Draw it as a sibling positioned over the panel's top edge, or parent it to the panel and
    /// let it overhang: like every attachment in this kit it deliberately draws outside its own
    /// rect's "content", so the HOST must reserve headroom — the lesson `KitPanel` paid for when
    /// its banner covered the row above.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPanelHanger : KitControl
    {
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        public enum HangerKind { Chain, Rope, Nail, Tape, ScrollRoll, Vine }

        [Export] public HangerKind Kind { get => _kind; set { _kind = value; QueueRedraw(); } }
        private HangerKind _kind = HangerKind.Chain;

        /// <summary>Horizontal inset of the two fixings, as a fraction of width. Chains and ropes
        /// hang from two points; a nail or a scroll roll uses the full span.</summary>
        [Export(PropertyHint.Range, "0.0,0.45,0.01")] public float Inset { get; set; } = 0.18f;

        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 8f, fs * 2.4f);
            }
        }

        public override void _Draw()
        {
            if (Size.X < 8f || Size.Y < 6f) return;

            Color face = FaceColor();
            Color ink = InkColor();

            // A fixing is hardware: it must read against BOTH the panel it hangs and the world
            // behind it. The first version tinted the surface by 1.15x, which on a mid-tone
            // background was invisible — a hanger you cannot see is not a hanger. Now it pushes
            // firmly away from the surface's own luminance instead of nudging it.
            Color acc;
            if (Accent == UiSurface.Role.Neutral)
            {
                float l = UiSurface.Luminance(face);
                acc = l > 0.5f
                    ? new Color(face.R * 0.42f, face.G * 0.43f, face.B * 0.48f, 1f)
                    : new Color(Mathf.Lerp(face.R, 1f, 0.62f), Mathf.Lerp(face.G, 1f, 0.60f),
                                Mathf.Lerp(face.B, 1f, 0.54f), 1f);
            }
            else acc = UiSurface.Semantic(this, Accent);

            float w = Mathf.Max(3f, Size.Y * 0.20f);

            float lx = Size.X * Inset, rx = Size.X * (1f - Inset);

            switch (_kind)
            {
                case HangerKind.Chain: DrawChain(lx, acc, ink, w); DrawChain(rx, acc, ink, w); break;
                case HangerKind.Rope: DrawRope(lx, acc, ink, w); DrawRope(rx, acc, ink, w); break;
                case HangerKind.Nail: DrawNail(Size.X * 0.5f, acc, ink); break;
                case HangerKind.Tape: DrawTape(true, acc, ink); DrawTape(false, acc, ink); break;
                case HangerKind.ScrollRoll: DrawRoll(acc, ink, w); break;
                case HangerKind.Vine: DrawVine(lx, acc, ink, w); DrawVine(rx, acc, ink, w); break;
            }
        }

        /// <summary>Discrete links, because a chain that is one line reads as a rope.</summary>
        private void DrawChain(float x, Color c, Color ink, float w)
        {
            // Links alternate their long axis, which is what makes a chain read as a chain
            // rather than a dotted line.
            float link = Size.Y * 0.34f;
            bool flip = false;
            for (float y = 0f; y + link <= Size.Y + link * 0.3f; y += link * 0.72f)
            {
                var r = flip
                    ? new Rect2(x - link * 0.42f, y + link * 0.18f, link * 0.84f, link * 0.62f)
                    : new Rect2(x - link * 0.30f, y, link * 0.60f, link);
                DrawShape(r, KitShape.Pill, new Color(0, 0, 0, 0), c, Mathf.Max(2.5f, w * 0.85f));
                flip = !flip;
            }
        }

        private void DrawRope(float x, Color c, Color ink, float w)
        {
            // A slight lean, so two ropes converge to a fixing above rather than running parallel.
            float lean = (x < Size.X * 0.5f ? 1f : -1f) * Size.X * 0.03f;
            DrawLine(new Vector2(x + lean, 0f), new Vector2(x, Size.Y), c, w * 1.2f);
            DrawCircle(new Vector2(x, Size.Y - w), w * 0.9f, ink);
        }

        private void DrawNail(float x, Color c, Color ink)
        {
            float r = Mathf.Min(Size.X, Size.Y) * 0.22f;
            var at = new Vector2(x, Size.Y * 0.55f);
            DrawCircle(at, r, c);
            DrawArc(at, r, 0f, Mathf.Tau, 20, ink, Mathf.Max(1.5f, r * 0.28f));
            DrawCircle(at - new Vector2(r * 0.3f, r * 0.3f), r * 0.28f, new Color(1, 1, 1, 0.45f));
        }

        /// <summary>A torn strip across the corner at an angle — the "taped to the wall" look.</summary>
        private void DrawTape(bool left, Color c, Color ink)
        {
            float tw = Size.X * 0.26f, th = Size.Y * 0.55f;
            float x = left ? -tw * 0.15f : Size.X - tw * 0.85f;
            var r = new Rect2(x, Size.Y * 0.2f, tw, th);
            var pts = new[]
            {
                r.Position + new Vector2(left ? 0f : th * 0.35f, 0f),
                r.Position + new Vector2(r.Size.X - (left ? th * 0.35f : 0f), 0f),
                r.End - new Vector2(left ? 0f : th * 0.35f, 0f),
                new Vector2(r.Position.X + (left ? th * 0.35f : 0f), r.End.Y),
            };
            DrawColoredPolygon(pts, new Color(c.R, c.G, c.B, 0.72f));
        }

        /// <summary>A rolled top edge spanning the full width — parchment and scroll panels.</summary>
        private void DrawRoll(Color c, Color ink, float w)
        {
            var r = new Rect2(0f, Size.Y * 0.28f, Size.X, Size.Y * 0.62f);
            DrawShape(r, KitShape.Pill, c, ink, Mathf.Max(1.5f, w));
            // A highlight along the top of the roll gives it a cylinder's read.
            var hl = new Rect2(r.Position.X + r.Size.X * 0.03f, r.Position.Y + r.Size.Y * 0.16f,
                               r.Size.X * 0.94f, r.Size.Y * 0.26f);
            if (hl.Size.Y > 1f)
                DrawShape(hl, KitShape.Pill, new Color(1, 1, 1, 0.20f), new Color(0, 0, 0, 0), 0f);
        }

        private void DrawVine(float x, Color c, Color ink, float w)
        {
            // A wavy stem with two leaves — drawn, not tiled, so it scales with the panel.
            int steps = 8;
            var prev = new Vector2(x, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = new Vector2(x + Mathf.Sin(t * Mathf.Pi * 2f) * Size.X * 0.02f, Size.Y * t);
                DrawLine(prev, p, c, w);
                prev = p;
            }
            float lr = Size.Y * 0.16f;
            DrawCircle(new Vector2(x + lr * 0.8f, Size.Y * 0.35f), lr, c);
            DrawCircle(new Vector2(x - lr * 0.8f, Size.Y * 0.65f), lr * 0.85f, c);
        }
    }
}
