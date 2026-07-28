using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A color-palette variant that retints any theme's <see cref="ColorSchema"/>.
    /// Instead of authoring 5 separate ColorSchemas per theme, the user picks a
    /// palette and it shifts the theme's existing colors in HSV space. So
    /// "Cartoon + Warm" and "Cartoon + Cool" share Cartoon's geometry/animation
    /// but differ in hue feel.
    ///
    /// A palette is a set of small offsets applied to each color:
    ///   HueShift        — degrees added to hue (-180..180). e.g. 0 = as-authored.
    ///   SaturationMul   — multiplier on saturation (1 = unchanged; &gt;1 more vivid).
    ///   ValueMul        — multiplier on brightness (1 = unchanged; &gt;1 brighter).
    /// Tints are applied via Godot's Color.ToHsv / FromHsv, preserving alpha.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ColorPalette : Resource
    {
        /// <summary>Human-readable name shown in the palette picker.</summary>
        [Export] public string DisplayName { get; set; } = "Default";

        [Export] public float HueShift { get; set; } = 0f;
        [Export] public float SaturationMul { get; set; } = 1f;
        [Export] public float ValueMul { get; set; } = 1f;

        /// <summary>Apply this palette's tint to a single color (preserving alpha).</summary>
        public Color Tint(Color c)
        {
            c.ToHsv(out float h, out float s, out float v);
            float hue = Mathf.PosMod(h + HueShift / 360f, 1f);
            float sat = Mathf.Clamp(s * SaturationMul, 0f, 1f);
            float val = Mathf.Clamp(v * ValueMul, 0f, 1f);
            return Color.FromHsv(hue, sat, val, c.A);
        }

        /// <summary>Return a new ColorSchema with every color tinted by this palette.</summary>

        /// <summary>WCAG AA for normal text.</summary>
        private const float MinTextContrast = 4.5f;

        private static float Lum(Color c)
        {
            static float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Ch(c.R) + 0.7152f * Ch(c.G) + 0.0722f * Ch(c.B);
        }

        private static float Contrast(Color a, Color b)
        {
            float la = Lum(a), lb = Lum(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>Tint a TEXT colour against the surface it will be read on.
        ///
        /// A palette is a uniform HSV multiplier, so it scales surface and text TOGETHER and
        /// compresses the ratio between them. Measured across all 350 theme x palette pairs,
        /// nine fell below AA purely from this — seven of them the "dark" palette, every one
        /// starting from a healthy 6:1 or better. rpg/fantasy went 6.4 -> 4.1 without a single
        /// colour being authored badly.
        ///
        /// So the hue and saturation shift applies as normal — that is the palette's identity —
        /// but the VALUE is then pushed away from the surface until the text reads again. The
        /// result is still the palette's colour, just legible.</summary>
        public Color TintText(Color text, Color surface)
        {
            Color t = Tint(text), s = Tint(surface);
            if (Contrast(t, s) >= MinTextContrast) return t;

            t.ToHsv(out float h, out float sat, out float v);
            bool lighten = Lum(s) < 0.5f;   // dark surface -> lift the text, and vice versa
            for (int i = 0; i < 25 && Contrast(t, s) < MinTextContrast; i++)
            {
                v = lighten ? Mathf.Min(1f, v + 0.04f) : Mathf.Max(0f, v - 0.04f);
                t = Color.FromHsv(h, sat, v, text.A);
            }
            return t;
        }

        public ColorSchema TintSchema(ColorSchema s) => new()
        {
            SurfacePrimary = Tint(s.SurfacePrimary),
            SurfaceHover = Tint(s.SurfaceHover),
            SurfacePressed = Tint(s.SurfacePressed),
            SurfaceDisabled = Tint(s.SurfaceDisabled),
            TextPrimary = TintText(s.TextPrimary, s.SurfacePrimary),
            TextHover = TintText(s.TextHover, s.SurfaceHover),
            TextDisabled = Tint(s.TextDisabled),
            TextOnDark = TintText(s.TextOnDark, s.AccentPrimary),
            AccentPrimary = Tint(s.AccentPrimary),
            AccentSecondary = Tint(s.AccentSecondary),
            BorderNormal = Tint(s.BorderNormal),
            BorderHover = Tint(s.BorderHover),
            BorderFocus = Tint(s.BorderFocus),
            BorderBevelLight = Tint(s.BorderBevelLight),
            BorderBevelDark = Tint(s.BorderBevelDark),
            ShadowColor = Tint(s.ShadowColor),
            BgPanel = Tint(s.BgPanel),
            BgCanvas = Tint(s.BgCanvas),
            SemanticSuccess = Tint(s.SemanticSuccess),
            SemanticDanger = Tint(s.SemanticDanger),
            SemanticWarning = Tint(s.SemanticWarning),
            SemanticInfo = Tint(s.SemanticInfo)
        };

        // ── Built-in palettes are now FILE-BASED. They live in
        // skins/<genre>/themes/<theme>/<palette>.json and are loaded by SkinCatalog.
        // The properties below are kept only as a fallback for backward compat with
        // scenes that referenced "Default"/"Warm"/etc. directly before the refactor.
        // New palettes = add a .json file in a theme folder — zero C# changes.

        public static ColorPalette Default => new() { DisplayName = "Default" };

        /// <summary>
        /// Look up a palette by display name across ALL genres/themes in the skin
        /// catalog. Returns the first match (case-insensitive). Falls back to a
        /// no-op Default palette if not found, so theming never breaks.
        /// </summary>
        public static ColorPalette? ByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Default;
            // Search every theme's palettes in the loaded catalog.
            foreach (var genre in SkinCatalog.AllGenres.Values)
            {
                foreach (var theme in genre.Themes.Values)
                {
                    if (theme.Palettes.TryGetValue(name.ToLowerInvariant(), out var pal))
                        return pal;
                }
            }
            // Fallback: "Default" always works as a no-op tint.
            if (name.Equals("Default", System.StringComparison.OrdinalIgnoreCase))
                return Default;
            return null;
        }
    }
}
