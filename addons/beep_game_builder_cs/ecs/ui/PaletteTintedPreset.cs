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
