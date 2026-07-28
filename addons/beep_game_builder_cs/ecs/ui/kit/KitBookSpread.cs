using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A two-page book — CATALOGUE-FROM-ART.md F.2's `BookSpread`, the journal / codex / quest-log
    /// set piece in the rpg and survival families.
    ///
    /// The spine is the whole idea: two pages that meet at a shaded gutter read as ONE object a
    /// player has opened, where two panels side by side read as two panels. So this owns the
    /// spine shading and the page edges, and exposes <see cref="LeftRect"/> / <see cref="RightRect"/>
    /// for a screen to lay its content into — the same contract <see cref="KitPanel.ContentRect"/>
    /// offers, and for the same reason: a screen that re-derives the insets will drift from them.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitBookSpread : KitControl
    {
        [Export] public string LeftTitle { get => _lt; set { _lt = value ?? ""; QueueRedraw(); } }
        private string _lt = "";

        [Export] public string RightTitle { get => _rt; set { _rt = value ?? ""; QueueRedraw(); } }
        private string _rt = "";

        /// <summary>Ribbon bookmark hanging over the top edge. Empty hides it.</summary>
        [Export] public bool ShowRibbon { get; set; } = true;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 26f, fs * 17f);
            }
        }

        private float Gutter => Mathf.Max(6f, Size.X * 0.035f);

        /// <summary>Content area of the left page.</summary>
        public Rect2 LeftRect()
        {
            float pad = Size.Y * 0.08f;
            return new Rect2(pad, pad, Size.X * 0.5f - Gutter - pad, Size.Y - pad * 2f);
        }

        /// <summary>Content area of the right page.</summary>
        public Rect2 RightRect()
        {
            float pad = Size.Y * 0.08f;
            return new Rect2(Size.X * 0.5f + Gutter, pad, Size.X * 0.5f - Gutter - pad, Size.Y - pad * 2f);
        }

        public override void _Draw()
        {
            if (Size.X < 60f || Size.Y < 40f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, g.Rim * (fs / 14f));

            // Cover behind both pages, so the book has an edge the pages sit inside.
            DrawShape(new Rect2(Vector2.Zero, Size), ActiveShape, face, RimColor(), rimPx);

            float inset = Mathf.Max(3f, Size.Y * 0.035f);
            float gut = Gutter;
            // Pages take the raised plate shade — they sit ON the cover, not sunk into it.
            float ps = g.PlateShadeFor(KitElevation.Raised);
            var page = new Color(Mathf.Lerp(face.R, 1f, 0.72f) * ps,
                                 Mathf.Lerp(face.G, 1f, 0.70f) * ps,
                                 Mathf.Lerp(face.B, 1f, 0.62f) * ps, 1f);

            var lp = new Rect2(inset, inset, Size.X * 0.5f - gut - inset, Size.Y - inset * 2f);
            var rp = new Rect2(Size.X * 0.5f + gut, inset, Size.X * 0.5f - gut - inset, Size.Y - inset * 2f);
            DrawShape(lp, ActiveShape, page, ink, Mathf.Max(1f, rimPx * 0.6f));
            DrawShape(rp, ActiveShape, page, ink, Mathf.Max(1f, rimPx * 0.6f));

            // The spine: a shaded gutter, darkest at the centre, which is what makes the two
            // pages read as one opened object.
            int bands = 6;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                float a = (1f - Mathf.Abs(t - 0.5f) * 2f) * 0.34f;
                float x = Size.X * 0.5f - gut + (gut * 2f) * t;
                DrawLine(new Vector2(x, inset), new Vector2(x, Size.Y - inset),
                         new Color(0, 0, 0, a), Mathf.Max(1.5f, gut * 0.4f));
            }

            if (ShowRibbon)
            {
                float rw = Mathf.Max(6f, Size.X * 0.022f);
                float rx = Size.X * 0.72f;
                Color rc = UiSurface.Semantic(this, UiSurface.Role.Danger);
                DrawRect(new Rect2(rx, -Size.Y * 0.05f, rw, Size.Y * 0.30f), rc);
                DrawColoredPolygon(new[]
                {
                    new Vector2(rx, Size.Y * 0.25f),
                    new Vector2(rx + rw, Size.Y * 0.25f),
                    new Vector2(rx + rw * 0.5f, Size.Y * 0.32f),
                }, rc);
            }

            if (font == null) return;
            void Title(string t, Rect2 p)
            {
                if (string.IsNullOrEmpty(t)) return;
                Vector2 m = font.GetStringSize(t, HorizontalAlignment.Left, -1, fs);
                DrawString(font, new Vector2(p.Position.X + (p.Size.X - m.X) * 0.5f,
                                             p.Position.Y + fs * 1.6f),
                           t, HorizontalAlignment.Left, -1, fs, new Color(0.16f, 0.13f, 0.10f));
            }
            Title(_lt, lp);
            Title(_rt, rp);
        }
    }
}
