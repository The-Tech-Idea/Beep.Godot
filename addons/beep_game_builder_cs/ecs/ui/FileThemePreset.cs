using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A theme preset loaded from a theme.json file via <see cref="SkinCatalog"/>.
    /// Implements <see cref="IThemePreset"/> so it slots directly into the existing
    /// NodeTheming engine without any changes to the 23 Theme*() methods — they
    /// all read <c>_presetInstance.Colors</c> + <c>GetButtonNormal()</c>, which this
    /// class provides from the JSON data.
    ///
    /// This single class replaces all 22 hardcoded ThemePreset*.cs classes.
    /// </summary>
    internal sealed class FileThemePreset : IThemePreset
    {
        private readonly ThemeDef _def;

        public FileThemePreset(ThemeDef def) => _def = def;

        public string PresetName => _def.DisplayName;
        public string PresetType => _def.Id; // file-based id (e.g. "cartoon")
        public ColorSchema Colors => _def.Colors;
        public AnimationConfig Animation => _def.Animation;

        // ── Texture slots (Phase C) ── driven from theme.json's "textures" block.
        public bool UsesTextures => _def.Textures?.AnyTexture ?? false;
        public string? TexturePathNormal => _def.Textures?.ButtonNormal?.Path;
        public string? TexturePathHover  => _def.Textures?.ButtonHover?.Path;
        public string? TexturePathPressed => _def.Textures?.ButtonPressed?.Path;

        // Per-slot StyleBoxTexture accessors — return null when the slot is unset,
        // letting ThemePresetComponent's SkinOr() route to the inspector UISkin or
        // the procedural StyleBoxFlat as fallback.
        public StyleBox? GetButtonNormalTexture()   => _def.Textures?.ButtonNormal?.BuildStyleBox();
        public StyleBox? GetButtonHoverTexture()    => _def.Textures?.ButtonHover?.BuildStyleBox();
        public StyleBox? GetButtonPressedTexture()  => _def.Textures?.ButtonPressed?.BuildStyleBox();
        public StyleBox? GetButtonDisabledTexture() => _def.Textures?.ButtonDisabled?.BuildStyleBox();
        public StyleBox? GetButtonFocusTexture()    => _def.Textures?.ButtonFocus?.BuildStyleBox();
        public StyleBox? GetPanelTexture()          => _def.Textures?.Panel?.BuildStyleBox();
        public StyleBox? GetInputNormalTexture()    => _def.Textures?.InputNormal?.BuildStyleBox();
        public StyleBox? GetInputFocusTexture()     => _def.Textures?.InputFocus?.BuildStyleBox();
        public StyleBox? GetProgressBgTexture()     => _def.Textures?.ProgressBg?.BuildStyleBox();
        public StyleBox? GetProgressFillTexture()   => _def.Textures?.ProgressFill?.BuildStyleBox();
        public StyleBox? GetSliderGrabberTexture()  => _def.Textures?.SliderGrabber?.BuildStyleBox();
        public StyleBox? GetScrollGrabberTexture()  => _def.Textures?.ScrollGrabber?.BuildStyleBox();
        public StyleBox? GetSeparatorTexture()      => _def.Textures?.Separator?.BuildStyleBox();

        /// <summary>
        /// Build the button-normal StyleBox from the geometry block in theme.json.
        /// This is the geometry template that ExtractGeometry() reads to derive
        /// corner radius, border widths, shadow, and content margins for all nodes.
        /// </summary>
        public StyleBox GetButtonNormal()
        {
            var g = _def.Geometry;
            var sb = new StyleBoxFlat();
            sb.BgColor = _def.Colors.SurfacePrimary;
            sb.SetCornerRadiusAll(g.CornerRadius);
            sb.BorderWidthLeft = g.BorderLeft;
            sb.BorderWidthTop = g.BorderTop;
            sb.BorderWidthRight = g.BorderRight;
            sb.BorderWidthBottom = g.BorderBottom;
            sb.BorderColor = _def.Colors.BorderNormal;
            sb.ShadowSize = g.ShadowSize;
            sb.ShadowOffset = new Vector2(g.ShadowOffsetX, g.ShadowOffsetY);
            sb.ShadowColor = _def.Colors.ShadowColor;
            sb.ContentMarginLeft = g.PadLeft;
            sb.ContentMarginRight = g.PadRight;
            sb.ContentMarginTop = g.PadTop;
            sb.ContentMarginBottom = g.PadBottom;
            return sb;
        }

        // The NodeTheming engine rebuilds all states from the geometry template +
        // ColorSchema, so these factory methods just delegate to the normal state
        // with a different bg color. The per-state geometry is derived in
        // ExtractGeometry / NewBox, not here.
        public StyleBox GetButtonHover() => CloneWith(_def.Colors.SurfaceHover);
        public StyleBox GetButtonPressed() => CloneWith(_def.Colors.SurfacePressed);
        public StyleBox GetButtonDisabled() => CloneWith(_def.Colors.SurfaceDisabled);
        public StyleBox GetButtonFocus() => CloneWith(_def.Colors.SurfacePrimary);
        public StyleBox GetPrimaryButtonNormal() => CloneWith(_def.Colors.AccentPrimary);
        public StyleBox GetDangerButtonNormal() => CloneWith(_def.Colors.SemanticDanger);
        public StyleBox GetSuccessButtonNormal() => CloneWith(_def.Colors.SemanticSuccess);
        public StyleBox GetPanelBackground() => CloneWith(_def.Colors.BgPanel);
        public StyleBox GetLineEditNormal() => CloneWith(_def.Colors.SurfacePressed);

        private StyleBox CloneWith(Color bg)
        {
            var sb = (StyleBoxFlat)GetButtonNormal();
            sb.BgColor = bg;
            return sb;
        }
    }
}
