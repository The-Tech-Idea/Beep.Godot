using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Decorator over an <see cref="IThemePreset"/> that retints every color
    /// (ColorSchema fields AND every StyleBox color) through a <see cref="ColorPalette"/>.
    /// Used by <see cref="ThemePresetComponent"/> when a palette is selected, so the
    /// component's existing StyleBox/theme assembly code runs unchanged but on tinted output.
    /// </summary>
    internal sealed class PaletteTintedPreset : IThemePreset
    {
        private readonly IThemePreset _inner;
        private readonly ColorPalette _palette;

        public PaletteTintedPreset(IThemePreset inner, ColorPalette palette)
        {
            _inner = inner;
            _palette = palette;
        }

        public string PresetName => $"{_inner.PresetName} ({_palette.DisplayName})";
        public string PresetType => _inner.PresetType;
        public bool UsesTextures => _inner.UsesTextures;
        public string? TexturePathNormal => _inner.TexturePathNormal;
        public string? TexturePathHover => _inner.TexturePathHover;
        public string? TexturePathPressed => _inner.TexturePathPressed;

        public ColorSchema Colors => _palette.TintSchema(_inner.Colors);
        public AnimationConfig Animation => _inner.Animation;

        // StyleBox factories: duplicate the inner StyleBoxFlat and tint its colors.
        public StyleBox GetButtonNormal() => TintBox(_inner.GetButtonNormal());
        public StyleBox GetButtonHover() => TintBox(_inner.GetButtonHover());
        public StyleBox GetButtonPressed() => TintBox(_inner.GetButtonPressed());
        public StyleBox GetButtonDisabled() => TintBox(_inner.GetButtonDisabled());
        public StyleBox GetButtonFocus() => TintBox(_inner.GetButtonFocus());
        public StyleBox GetPrimaryButtonNormal() => TintBox(_inner.GetPrimaryButtonNormal());
        public StyleBox GetDangerButtonNormal() => TintBox(_inner.GetDangerButtonNormal());
        public StyleBox GetSuccessButtonNormal() => TintBox(_inner.GetSuccessButtonNormal());
        public StyleBox GetPanelBackground() => TintBox(_inner.GetPanelBackground());
        public StyleBox GetLineEditNormal() => TintBox(_inner.GetLineEditNormal());

        // Per-slot StyleBoxTexture pass-through — textures carry their own colors
        // and are immune to palette tinting.
        public StyleBox? GetButtonNormalTexture()   => _inner.GetButtonNormalTexture();
        public StyleBox? GetButtonHoverTexture()    => _inner.GetButtonHoverTexture();
        public StyleBox? GetButtonPressedTexture()  => _inner.GetButtonPressedTexture();
        public StyleBox? GetButtonDisabledTexture() => _inner.GetButtonDisabledTexture();
        public StyleBox? GetButtonFocusTexture()    => _inner.GetButtonFocusTexture();
        public StyleBox? GetPanelTexture()          => _inner.GetPanelTexture();
        public StyleBox? GetDialogTexture()         => _inner.GetDialogTexture();
        public StyleBox? GetInputNormalTexture()    => _inner.GetInputNormalTexture();
        public StyleBox? GetInputFocusTexture()     => _inner.GetInputFocusTexture();
        public StyleBox? GetProgressBgTexture()     => _inner.GetProgressBgTexture();
        public StyleBox? GetProgressFillTexture()   => _inner.GetProgressFillTexture();
        public StyleBox? GetSliderGrabberTexture()  => _inner.GetSliderGrabberTexture();
        public StyleBox? GetScrollGrabberTexture()  => _inner.GetScrollGrabberTexture();
        public StyleBox? GetSeparatorTexture()      => _inner.GetSeparatorTexture();

        /// <summary>Duplicate a StyleBoxFlat (so we don't mutate the preset's shared
        /// instance) and tint its bg/border/shadow colors.</summary>
        private StyleBox TintBox(StyleBox box)
        {
            if (box is not StyleBoxFlat src) return box;
            var sb = (StyleBoxFlat)src.Duplicate();
            sb.BgColor = _palette.Tint(sb.BgColor);
            sb.BorderColor = _palette.Tint(sb.BorderColor);
            sb.ShadowColor = _palette.Tint(sb.ShadowColor);
            // Tint any non-white, non-transparent content margins? No — those are sizes.
            return sb;
        }
    }
}
