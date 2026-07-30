using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A <see cref="TabContainer"/> skinned from the active genre. Migration drop-in — see
    /// <see cref="KitChrome"/> for why these derive from the Godot type.
    ///
    /// Because it IS a TabContainer, `Find&lt;TabContainer&gt;("Tabs")`, `.CurrentTab`,
    /// `.SetTabTitle` and child-as-page layout all keep working — `SettingsMenu.cs` resolves it
    /// by type and would have got null from a KitControl-derived tab strip.
    ///
    /// WHY THIS ONE USES StyleBoxes AND NOT _Draw
    /// ------------------------------------------
    /// Unlike Button or HSlider, TabContainer is a COMPOSITE: it delegates its tab row to an
    /// internal TabBar child, which draws its own labels in C++. A first version painted tabs and
    /// titles in this class's `_Draw`, and every tab rendered its title TWICE, offset — "AudioAudio",
    /// "DisplayDisplay" — because suppressing the container's styleboxes does nothing to the
    /// child's rendering.
    ///
    /// So this drop-in hands Godot real StyleBoxes built from the genre's geometry and palette,
    /// and lets TabBar lay the row out. The kit's rule of thumb: own the draw for LEAF controls,
    /// supply styleboxes for composite ones.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitTabPanel : TabContainer
    {
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

        private string _genre = "";
        private bool _applying;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Apply();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                Apply();
            }
        }

        private void Apply()
        {
            // AddThemeStyleboxOverride emits NotificationThemeChanged, which lands straight back
            // here — the same re-entry KitPanelContainer needs a guard for.
            if (_applying) return;
            _applying = true;

            var g = KitGeometry.ForGenre(_genre);
            int fs = UiSurface.FontSize(this);
            Color surface = UiSurface.Of(this);
            Color accent = UiSurface.Semantic(this, Accent);
            if (accent.A < 0.02f) accent = surface;
            Color ink = UiSurface.Ink(surface);

            float pad = Mathf.Max(8f, fs * 0.8f);
            int corner = Mathf.RoundToInt(Mathf.Max(2f, fs * 2.2f * g.Corner));
            float frame = Mathf.Max(1f, g.FramePx(fs * 2.4f));

            // SELECTED takes the accent and a full frame; UNSELECTED recedes and carries a
            // thinner one. The contrast is in LIGHTNESS as well as hue, so the distinction
            // survives greyscale — the test the rest of the kit is held to.
            AddThemeStyleboxOverride("tab_selected", Tab(accent, ink, corner, pad, fs, frame));
            AddThemeStyleboxOverride("tab_hovered",
                Tab(KitChrome.StateFace(accent, KitState.Hover), ink, corner, pad, fs, frame));

            Color dim = new(surface.R * 0.70f, surface.G * 0.68f, surface.B * 0.74f, 1f);
            AddThemeStyleboxOverride("tab_unselected", Tab(dim, ink, corner, pad, fs, frame * 0.5f));
            AddThemeStyleboxOverride("tab_disabled", Tab(dim, ink, corner, pad, fs, frame * 0.5f));

            var panel = new StyleBoxFlat
            {
                BgColor = surface,
                BorderColor = ink,
                ContentMarginLeft = pad, ContentMarginRight = pad,
                ContentMarginTop = pad, ContentMarginBottom = pad,
            };
            panel.SetCornerRadiusAll(corner);
            panel.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(frame)));
            AddThemeStyleboxOverride("panel", panel);

            AddThemeColorOverride("font_selected_color", UiSurface.Text(this));
            AddThemeColorOverride("font_unselected_color", UiSurface.Text(this) with { A = 0.72f });

            _applying = false;
        }

        private static StyleBoxFlat Tab(Color face, Color ink, int corner, float pad, int fs,
                                        float border)
        {
            var sb = new StyleBoxFlat
            {
                BgColor = face,
                BorderColor = ink,
                ContentMarginLeft = pad, ContentMarginRight = pad,
                ContentMarginTop = fs * 0.45f, ContentMarginBottom = fs * 0.45f,
                // Square the bottom so the tab reads as ATTACHED to the page below rather than
                // floating above it — what every reference tab strip does (ui9, gameui4).
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 0,
                CornerRadiusTopLeft = corner,
                CornerRadiusTopRight = corner,
            };
            sb.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(border)));
            sb.BorderWidthBottom = 0;
            return sb;
        }
    }
}
