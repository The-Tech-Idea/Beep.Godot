using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Drop-in skinned Tree for game-facing hierarchy/list screens.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitGodotTree : Tree
    {
        [Export] public UiSurface.Role Accent
        {
            get => _accent;
            set
            {
                if (_accent == value) return;
                _accent = value;
                RequestApply();
            }
        }
        private UiSurface.Role _accent = UiSurface.Role.Accent;

        private string _genre = "";
        private bool _applying;

        public override void _Ready()
        {
            base._Ready();
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

        private void RequestApply()
        {
            if (!IsInsideTree()) return;
            _genre = KitChrome.GenreOf(this);
            Apply();
        }

        private void Apply()
        {
            if (_applying || !IsInsideTree()) return;
            _applying = true;
            try
            {
                int fs = Mathf.Max(1, UiSurface.FontSize(this, UiSurface.TextRole.Caption));
                Color surface = UiSurface.Of(this);
                Color accent = UiSurface.SemanticOrDerived(this, Accent);
                if (accent.A < 0.02f) accent = surface;
                Color ink = UiSurface.Ink(surface);

                Font? font = KitFonts.Fallback(this, KitGeometry.ForGenre(_genre).Font);
                bool changed = false;
                if (font != null) changed |= KitChrome.SetFontOverrideIfChanged(this, "font", font);
                changed |= KitChrome.SetFontSizeOverrideIfChanged(this, "font_size", fs);
                changed |= KitChrome.SetColorOverrideIfChanged(this, "font_color", UiSurface.Text(this));
                changed |= KitChrome.SetColorOverrideIfChanged(this, "font_selected_color", UiSurface.Ink(accent));
                changed |= KitChrome.SetColorOverrideIfChanged(this, "guide_color", new Color(ink.R, ink.G, ink.B, 0.22f));
                changed |= KitChrome.SetColorOverrideIfChanged(this, "relationship_line_color", new Color(ink.R, ink.G, ink.B, 0.38f));
                changed |= KitChrome.SetStyleboxOverrideIfChanged(this, "panel", Box(surface, ink, fs, 1f));
                changed |= KitChrome.SetStyleboxOverrideIfChanged(this, "selected", Box(accent, ink, fs, 0.75f));
                changed |= KitChrome.SetStyleboxOverrideIfChanged(this, "selected_focus", Box(KitChrome.StateFace(accent, KitState.Hover), ink, fs, 0.95f));
                changed |= KitChrome.SetConstantOverrideIfChanged(this, "h_separation", Mathf.Max(5, fs / 2));
                changed |= KitChrome.SetConstantOverrideIfChanged(this, "v_separation", Mathf.Max(3, fs / 4));
                changed |= KitChrome.SetConstantOverrideIfChanged(this, "item_margin", Mathf.Max(6, fs / 2));
                if (!changed) return;
            }
            finally
            {
                _applying = false;
            }
            UpdateMinimumSize();
            QueueRedraw();
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
