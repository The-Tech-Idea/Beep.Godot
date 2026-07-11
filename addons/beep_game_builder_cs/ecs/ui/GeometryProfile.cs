using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Shape/geometry profile applied as an OVERRIDE layer on top of a theme
    /// preset — independent of color (handled by <see cref="ColorPalette"/>) and
    /// independent of the preset's own baked geometry. After the preset builds its
    /// StyleBoxFlats, <see cref="ThemePresetComponent"/> restamps each one with
    /// this profile's corner radius / border width / shadow / padding / font size.
    ///
    /// So the four dimensions are independent:
    ///   genre → suggests a geometry  ·  theme → colors/animation  ·  palette → color variant  ·  geometry → shape
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GeometryProfile : Resource
    {
        /// <summary>Corner radius in px applied to all four corners. -1 = leave the preset's value.</summary>
        [Export] public int CornerRadius { get; set; } = -1;
        /// <summary>Border width in px (all sides). -1 = leave the preset's value.</summary>
        [Export] public int BorderWidth { get; set; } = -1;
        /// <summary>Drop-shadow size in px. -1 = leave the preset's value.</summary>
        [Export] public int ShadowSize { get; set; } = -1;
        /// <summary>Drop-shadow Y offset in px. -1 = leave the preset's value.</summary>
        [Export] public float ShadowOffsetY { get; set; } = -1f;
        /// <summary>Content margin (padding) in px (all sides). -1 = leave the preset's value.</summary>
        [Export] public int ContentPadding { get; set; } = -1;
        /// <summary>Base font size for themed text. -1 = leave the preset's value.</summary>
        [Export] public int FontSize { get; set; } = -1;

        /// <summary>Per-node-type shape overrides from the source genre's geometry.json
        /// "shapes" block. Null = no per-node overrides (use legacy defaults).</summary>
        public ShapeOverrides? Shapes;

        /// <summary>Optional background image path (res://...) drawn behind the
        /// themed subtree root. Null = no background.</summary>
        public string? BackgroundImage;

        /// <summary>How to render the background: "stretch" (default), "tile", or "center".</summary>
        public string BackgroundMode = "stretch";

        /// <summary>Apply this profile's overrides to a StyleBoxFlat in place.</summary>
        public void ApplyTo(StyleBoxFlat sb)
        {
            if (CornerRadius >= 0) sb.SetCornerRadiusAll(CornerRadius);
            if (BorderWidth >= 0)
            {
                sb.BorderWidthLeft = BorderWidth;
                sb.BorderWidthRight = BorderWidth;
                sb.BorderWidthTop = BorderWidth;
                sb.BorderWidthBottom = BorderWidth;
            }
            if (ShadowSize >= 0) sb.ShadowSize = ShadowSize;
            if (ShadowOffsetY >= 0) sb.ShadowOffset = new Vector2(sb.ShadowOffset.X, ShadowOffsetY);
            if (ContentPadding >= 0)
            {
                sb.ContentMarginLeft = ContentPadding;
                sb.ContentMarginRight = ContentPadding;
                sb.ContentMarginTop = ContentPadding;
                sb.ContentMarginBottom = ContentPadding;
            }
        }

        /// <summary>True if this profile would change anything (i.e. isn't all "leave as-is").</summary>
        public bool HasOverrides =>
            CornerRadius >= 0 || BorderWidth >= 0 || ShadowSize >= 0
            || ShadowOffsetY >= 0 || ContentPadding >= 0 || FontSize >= 0;

        // ── Geometry profiles are now FILE-BASED. Each genre has a geometry.json
        // in skins/<genre>/geometry.json, loaded by SkinCatalog. The properties
        // below are kept only as no-op fallbacks for backward compat.

        [Export] public string DisplayName { get; set; } = "As-Authored";

        /// <summary>No geometry override — use each theme's own baked geometry.</summary>
        public static GeometryProfile AsAuthored => new() { /* all -1 */ };

        /// <summary>
        /// Look up a geometry profile by display name from the file-based skin
        /// catalog. Searches every genre's geometry.json. Falls back to AsAuthored
        /// (no override) if not found, so theming never breaks.
        /// </summary>
        public static GeometryProfile? ByName(string name)
        {
            foreach (var genre in SkinCatalog.AllGenres.Values)
            {
                if (genre.Geometry != null
                    && genre.Geometry.DisplayName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return genre.Geometry.ToProfile();
            }
            if (name.Equals("As-Authored", System.StringComparison.OrdinalIgnoreCase))
                return AsAuthored;
            return null;
        }

        /// <summary>
        /// Genre → suggested geometry profile. Now reads from the genre's
        /// geometry.json via the skin catalog (no hardcoded values).
        /// </summary>
        public static GeometryProfile ForGenre(GameBuilder.GameInfo.GameGenre genre)
        {
            string genreId = genre.ToString().ToLowerInvariant();
            var def = SkinCatalog.GetGeometry(genreId);
            return def?.ToProfile() ?? AsAuthored;
        }
    }
}
