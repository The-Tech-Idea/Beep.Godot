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
        private HangerKind _kind = HangerKind.Nail;

        /// <summary>Horizontal inset of the two fixings, as a fraction of width. Chains and ropes
        /// hang from two points; a nail or a scroll roll uses the full span.</summary>
        [Export(PropertyHint.Range, "0.0,0.45,0.01")] public float Inset { get; set; } = 0.18f;

        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = _GetMinimumSize();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 7f, fs * 1.6f);
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

            float w = Mathf.Clamp(Size.Y * 0.12f, 2f, 4f);

            float lx = Size.X * Inset, rx = Size.X * (1f - Inset);

            switch (_kind)
            {
                case HangerKind.Chain: DrawBracket(acc, ink, w); break;
                case HangerKind.Rope: DrawBracket(acc, ink, w); break;
                case HangerKind.Nail: DrawNail(Size.X * 0.5f, acc, ink); break;
                case HangerKind.Tape: DrawTape(true, acc, ink); DrawTape(false, acc, ink); break;
                case HangerKind.ScrollRoll: DrawRoll(acc, ink, w); break;
                case HangerKind.Vine: DrawVine(lx, acc, ink, w); DrawVine(rx, acc, ink, w); break;
            }
        }

        private void DrawBracket(Color c, Color ink, float w)
        {
            float h = Size.Y;
            var rail = new Rect2(Size.X * 0.12f, h * 0.18f, Size.X * 0.76f, h * 0.30f);
            DrawShape(rail, KitShape.Pill, c, ink, Mathf.Max(1f, w * 0.65f));
            float r = Mathf.Max(3f, h * 0.18f);
            foreach (float x in new[] { Size.X * 0.24f, Size.X * 0.76f })
            {
                DrawLine(new Vector2(x, rail.End.Y - 1f), new Vector2(x, h * 0.86f), ink with { A = 0.75f }, w);
                DrawCircle(new Vector2(x, h * 0.86f), r, c);
                DrawArc(new Vector2(x, h * 0.86f), r, 0f, Mathf.Tau, 18, ink, Mathf.Max(1f, w * 0.55f));
            }
        }

        /// <summary>Discrete links, because a chain that is one line reads as a rope.</summary>
        private void DrawChain(float x, Color c, Color ink, float w)
        {
            // Links alternate their long axis, which is what makes a chain read as a chain
            // rather than a dotted line.
            // Stroke off the LINK, not the widget. `w` is Size.Y * 0.20 — a fifth of the whole
            // hanger — so it came out ~half the link's height and every open link filled in
            // solid: the chain rendered as two pale blobs. A link is an outline, so its stroke is
            // a fraction of the link itself. Links are also BIGGER and overlap more than the
            // first pass: thin rings spaced apart read as a dotted line, not as hardware.
            // A link has to read as a RING: thin wall, open hole, only just touching its
            // neighbour. Two earlier passes failed opposite ways — a stroke off the widget height
            // filled every link solid (pale blobs), and then stroke 0.26 with a 0.60 step merged
            // the rings into one bar with slots, which reads as a bolt. The hole is the feature,
            // so the wall stays ~1/7th of the link and the step nearly a whole link.
            // ROUND rings, overlapping, nudged alternately off-axis.
            //
            // Two shapes were tried and both failed for the same reason — they were not rings.
            // Tall-thin ovals draw as two parallel strokes, and pairing them with flat wide ovals
            // gave a rod with washers on it. A ring is round, its hole is as wide as it is tall,
            // and a chain is rings biting into each other; the small left/right nudge is what
            // suggests each one is turned 90 degrees from its neighbour without having to draw
            // the perspective.
            float d = Size.Y * 0.30f;
            float stroke = Mathf.Max(1.5f, d * 0.20f);
            bool flip = false;
            for (float y = -d * 0.10f; y < Size.Y; y += d * 0.66f)
            {
                float dx = flip ? d * 0.11f : -d * 0.11f;
                DrawShape(new Rect2(x - d * 0.5f + dx, y, d, d), KitShape.Pill,
                          new Color(0, 0, 0, 0), c, stroke);
                flip = !flip;
            }
        }

        private void DrawRope(float x, Color c, Color ink, float w)
        {
            // A slight lean, so two ropes converge to a fixing above rather than running parallel.
            float lean = (x < Size.X * 0.5f ? 1f : -1f) * Size.X * 0.03f;
            DrawLine(new Vector2(x + lean, 0f), new Vector2(x, Size.Y), c, w * 1.2f);
            DrawLine(new Vector2(x + lean + w * 0.7f, 0f), new Vector2(x + w * 0.7f, Size.Y),
                     new Color(1, 1, 1, 0.18f), Mathf.Max(1f, w * 0.35f));
            DrawCircle(new Vector2(x, Size.Y - w), w * 0.9f, ink);
        }

        private void DrawNail(float x, Color c, Color ink)
        {
            float r = Mathf.Min(Size.X, Size.Y) * 0.24f;
            var at = new Vector2(x, Size.Y * 0.48f);
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
            DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] },
                         new Color(ink.R, ink.G, ink.B, 0.38f), Mathf.Max(1f, th * 0.035f));
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
            DrawBracket(c, ink, w);
        }
    }
}
