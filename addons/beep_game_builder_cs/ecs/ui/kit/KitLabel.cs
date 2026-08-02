using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Drop-in skinned label for template scenes.
    ///
    /// It stays a Godot Label so existing scene paths, bindings, alignment, wrapping, and layout
    /// still work, but it takes its font scale and ink from the active Beep game skin instead of
    /// inheriting plain editor-style label defaults.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLabel : Label
    {
        [Export] public bool AutoRole { get; set; } = true;
        [Export] public UiSurface.TextRole Role { get; set; } = UiSurface.TextRole.Body;
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        private string _genre = "";
        private bool _applying;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            ApplyKitText();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged || what == NotificationResized)
            {
                _genre = KitChrome.GenreOf(this);
                ApplyKitText();
            }
        }

        private void ApplyKitText()
        {
            if (_applying) return;
            _applying = true;

            UiSurface.TextRole role = AutoRole ? InferRole() : Role;
            int fs = UiSurface.FontSize(this, role);
            AddThemeFontSizeOverride("font_size", fs);

            Font? font = KitFonts.Resolve(KitGeometry.ForGenre(_genre).Font);
            if (font != null) AddThemeFontOverride("font", font);

            Color ink = Accent == UiSurface.Role.Neutral ? UiSurface.Text(this) : UiSurface.Semantic(this, Accent);
            if (ink.A < 0.02f) ink = new Color(0.96f, 0.94f, 0.88f);
            AddThemeColorOverride("font_color", ink);
            AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.68f));
            AddThemeConstantOverride("shadow_offset_x", Mathf.Max(1, fs / 18));
            AddThemeConstantOverride("shadow_offset_y", Mathf.Max(1, fs / 18));
            AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.72f));
            AddThemeConstantOverride("outline_size", role == UiSurface.TextRole.Title ? 3 : 2);

            _applying = false;
        }

        private UiSurface.TextRole InferRole()
        {
            string name = Name.ToString().ToLowerInvariant();
            string variation = ThemeTypeVariation.ToString().ToLowerInvariant();
            string key = name + " " + variation;

            if (key.Contains("title") || key.Contains("banner") || key.Contains("pause"))
                return UiSurface.TextRole.Title;
            if (key.Contains("heading") || key.Contains("subtitle") || key.Contains("name"))
                return UiSurface.TextRole.Subtitle;
            if (key.Contains("value") || key.Contains("count") || key.Contains("gold") || key.Contains("score"))
                return UiSurface.TextRole.Value;
            if (key.Contains("caption") || key.Contains("hint") || key.Contains("label") || key.Contains("unit"))
                return UiSurface.TextRole.Caption;
            return Role;
        }
    }
}
