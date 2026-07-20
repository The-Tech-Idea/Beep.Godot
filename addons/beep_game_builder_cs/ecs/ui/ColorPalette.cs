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
        public ColorSchema TintSchema(ColorSchema s) => new()
        {
            SurfacePrimary = Tint(s.SurfacePrimary),
            SurfaceHover = Tint(s.SurfaceHover),
            SurfacePressed = Tint(s.SurfacePressed),
            SurfaceDisabled = Tint(s.SurfaceDisabled),
            TextPrimary = Tint(s.TextPrimary),
            TextHover = Tint(s.TextHover),
            TextDisabled = Tint(s.TextDisabled),
            TextOnDark = Tint(s.TextOnDark),
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
