using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Drop-in skinned ItemList for older templates and debug-style list widgets that appear in
    /// game-facing screens.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitItemList : ItemList
    {
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

        private string _genre = "";
        private bool _applying;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            Apply();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = KitChrome.GenreOf(this);
                Apply();
            }
        }

        private void Apply()
        {
            if (_applying) return;
            _applying = true;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            Color surface = UiSurface.Of(this);
            Color accent = UiSurface.Semantic(this, Accent);
            if (accent.A < 0.02f) accent = surface;
            Color ink = UiSurface.Ink(surface);

            AddThemeFontOverride("font", KitFonts.Resolve(KitGeometry.ForGenre(_genre).Font) ?? GetThemeDefaultFont());
            AddThemeFontSizeOverride("font_size", fs);
            AddThemeColorOverride("font_color", UiSurface.Text(this));
            AddThemeColorOverride("font_selected_color", UiSurface.Ink(accent));
            AddThemeColorOverride("guide_color", new Color(ink.R, ink.G, ink.B, 0.18f));
            AddThemeStyleboxOverride("panel", Box(surface, ink, fs, 1f));
            AddThemeStyleboxOverride("selected", Box(accent, ink, fs, 0.75f));
            AddThemeStyleboxOverride("selected_focus", Box(KitChrome.StateFace(accent, KitState.Hover), ink, fs, 0.95f));
            AddThemeConstantOverride("h_separation", Mathf.Max(4, fs / 2));
            AddThemeConstantOverride("v_separation", Mathf.Max(3, fs / 3));

            _applying = false;
        }

        private StyleBoxFlat Box(Color fill, Color ink, int fs, float rimScale)
        {
            var g = KitGeometry.ForGenre(_genre);
            int rim = Mathf.Max(1, Mathf.RoundToInt(g.Rim * rimScale));
            int corner = Mathf.RoundToInt(Mathf.Max(2f, fs * 1.8f * g.Corner));
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = ink with { A = 0.55f } };
            box.SetCornerRadiusAll(corner);
            box.SetBorderWidthAll(rim);
            box.SetContentMarginAll(Mathf.Max(4, fs / 2));
            return box;
        }
    }
}
