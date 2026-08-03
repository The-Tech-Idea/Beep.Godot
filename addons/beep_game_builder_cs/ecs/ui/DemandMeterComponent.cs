using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// The RCI demand meter — three bars that diverge from a centre line, showing whether
    /// Residential, Commercial and Industrial zoning is under- or over-supplied.
    ///
    /// This is the city builder's core feedback loop: it is what tells the player which zone
    /// to paint next, and SimCity/Cities: Skylines keep it on screen permanently for exactly
    /// that reason. Our HUD had no equivalent at all.
    ///
    /// Drawn rather than composed: the value is signed (-1..+1) and grows from a centre line
    /// in both directions, which a <see cref="ProgressBar"/> cannot express — it fills from
    /// one end. Three ProgressBars with swapped anchors would fake it and break the moment a
    /// value crossed zero.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DemandMeterComponent : Godot.Control
    {
        /// <summary>Demand for each zone, -1..+1. Negative = oversupplied.</summary>
        [Export(PropertyHint.Range, "-1,1,0.01")] public float Residential { get => _r; set { _r = value; QueueRedraw(); } }
        [Export(PropertyHint.Range, "-1,1,0.01")] public float Commercial { get => _c; set { _c = value; QueueRedraw(); } }
        [Export(PropertyHint.Range, "-1,1,0.01")] public float Industrial { get => _i; set { _i = value; QueueRedraw(); } }
        private float _r, _c, _i;

        // RES/COM/IND take the palette's meaning colours rather than three literals. Residential,
        // commercial and industrial demand are green / blue / amber in every city-builder
        // reference, which is exactly what success / info / warning already encode — so the
        // meter now reskins with the theme instead of staying the same three colours in all 50.
        // Computed rather than cached: read at draw time, so a skin change is picked up with
        // no invalidation step.
        private Color ResidentialColor => UiSurface.Semantic(this, UiSurface.Role.Success);
        private Color CommercialColor => UiSurface.Semantic(this, UiSurface.Role.Info);
        private Color IndustrialColor => UiSurface.Semantic(this, UiSurface.Role.Warning);
        [Export] public bool ShowLetters { get; set; } = true;

        /// <summary>Paint the theme's panel stylebox behind the bars. Without it the meter is
        /// thin coloured marks floating directly on the world, which is unreadable over a busy
        /// city — the one thing this widget cannot afford to be.</summary>
        [Export] public bool DrawBackdrop { get; set; } = true;
        /// <summary>Demand label size as a multiple of the theme's body font — the reserved
        /// letter strip is computed from it, so larger type widens the strip instead of
        /// overprinting the bars.</summary>
        [Export(PropertyHint.Range, "0.4,2.0,0.05")] public float LetterFontScale { get; set; } = 0.85f;

        private int LetterFontSize => UiSurface.FontSize(this, LetterFontScale);

        public override void _Ready()
        {
            // Chrome, never a click target — the toolbar beneath it must keep receiving input.
            MouseFilter = MouseFilterEnum.Ignore;
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = new Vector2(LetterFontSize * 8f, LetterFontSize * 11f);
        }

        /// <summary>The plate and the centre line are pulled from the theme, and this HUD's
        /// skin is applied at runtime by ThemePresetComponent — after the first draw. Without
        /// this the meter keeps the boot theme's colours for the rest of the session.</summary>
        public override void _Notification(int what)
        {
            if (what == NotificationThemeChanged) QueueRedraw();
        }

        public void SetDemand(float residential, float commercial, float industrial)
        {
            _r = Mathf.Clamp(residential, -1f, 1f);
            _c = Mathf.Clamp(commercial, -1f, 1f);
            _i = Mathf.Clamp(industrial, -1f, 1f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 s = Size;
            if (s.X <= 0 || s.Y <= 0) return;

            // Backdrop first, so the bars sit ON the skin rather than on the city behind it.
            // Uses the theme's own panel box, so the meter matches whichever of the 50 skins
            // is active instead of introducing a colour of its own.
            if (DrawBackdrop && GetThemeStylebox("panel", "PanelContainer") is { } plate)
                DrawStyleBox(plate, new Rect2(Vector2.Zero, s));

            var font = GetThemeDefaultFont();
            // Reserve a strip for the letters instead of drawing them at s.Y — previously a
            // negative bar grew down THROUGH its own label and both became unreadable.
            float letterStrip = ShowLetters && font != null ? LetterFontSize + 6f : 0f;
            const float pad = 8f;

            float top = pad;
            float bottom = s.Y - pad - letterStrip;
            if (bottom - top < 12f) { top = 0f; bottom = s.Y - letterStrip; }   // tiny-size fallback

            float mid = (top + bottom) * 0.5f;
            float maxH = (bottom - top) * 0.5f;
            float slot = (s.X - pad * 2f) / 3f;
            float barW = Mathf.Min(slot * 0.62f, 18f);
            float round = Mathf.Max(2f, barW * 0.45f);

            // Centre line first: without it a short bar is ambiguous — the player cannot tell
            // a small positive from a small negative.
            var line = GetThemeColor("font_color", "Label") with { A = 0.35f };
            DrawLine(new Vector2(pad, mid), new Vector2(s.X - pad, mid), line, 1f);

            Bar(0, _r, ResidentialColor, "RES");
            Bar(1, _c, CommercialColor, "COM");
            Bar(2, _i, IndustrialColor, "IND");

            void Bar(int index, float value, Color colour, string letter)
            {
                float cx = pad + slot * (index + 0.5f);
                float h = Mathf.Abs(value) * maxH;

                // A zero-demand bar would be invisible, leaving the player unsure whether the
                // meter is at rest or simply broken. Draw the empty channel behind every bar.
                var channel = new Rect2(cx - barW / 2f, top, barW, bottom - top);
                DrawStyleBox(Box(colour with { A = 0.14f }, UiSurface.Ink(UiSurface.Of(this)) with { A = 0.28f }, round, 1), channel);

                // Positive grows UP from the centre, negative grows DOWN — the SimCity reading.
                if (h > 0.5f)
                {
                    var rect = value >= 0
                        ? new Rect2(cx - barW / 2f, mid - h, barW, h)
                        : new Rect2(cx - barW / 2f, mid, barW, h);
                    DrawStyleBox(Box(colour, UiSurface.Ink(colour), round, 1), rect);
                    float shineY = value >= 0 ? rect.Position.Y + 2f : rect.End.Y - Mathf.Max(3f, h * 0.18f);
                    DrawLine(new Vector2(rect.Position.X + barW * 0.28f, shineY),
                             new Vector2(rect.End.X - barW * 0.28f, shineY),
                             new Color(1, 1, 1, 0.22f), Mathf.Max(1f, barW * 0.10f));
                }

                if (!ShowLetters || font == null) return;
                int labelFs = UiSurface.FitText(this, new Vector2(slot - 2f, LetterFontSize + 4f),
                                                LetterFontScale, letter, font, min: 7,
                                                themeMax: LetterFontScale);
                float w = font.GetStringSize(letter, HorizontalAlignment.Left, -1, labelFs).X;
                DrawString(font, new Vector2(cx - w / 2f, s.Y - pad * 0.5f), letter,
                           HorizontalAlignment.Left, -1, labelFs, colour);
            }
        }

        private static StyleBoxFlat Box(Color fill, Color rim, float radius, int border)
        {
            var sb = new StyleBoxFlat { BgColor = fill, BorderColor = rim };
            sb.SetCornerRadiusAll(Mathf.RoundToInt(radius));
            sb.SetBorderWidthAll(border);
            return sb;
        }
    }
}
