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
        [Export]
        public bool AutoRole
        {
            get => _autoRole;
            set { if (_autoRole == value) return; _autoRole = value; RequestApplyKitText(); }
        }
        private bool _autoRole = true;

        [Export]
        public UiSurface.TextRole Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RequestApplyKitText(); }
        }
        private UiSurface.TextRole _role = UiSurface.TextRole.Body;

        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RequestApplyKitText(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        private string _genre = "";

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            ApplyKitText();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = KitChrome.GenreOf(this);
                ApplyKitText();
            }
        }

        private void RequestApplyKitText()
        {
            if (!IsInsideTree()) return;
            _genre = KitChrome.GenreOf(this);
            ApplyKitText();
        }

        private void ApplyKitText()
        {
            if (!IsInsideTree()) return;

            UiSurface.TextRole role = AutoRole ? InferRole() : Role;
            int fs = Mathf.Max(1, UiSurface.FontSize(this, role));
            Color ink = Accent == UiSurface.Role.Neutral ? UiSurface.Text(this) : UiSurface.SemanticOrDerived(this, Accent);
            if (ink.A < 0.02f) ink = new Color(0.96f, 0.94f, 0.88f);

            ApplyOverrideChanges(fs, ink, role);
        }

        private void ApplyOverrideChanges(int fs, Color ink, UiSurface.TextRole role)
        {
            bool metricChanged = false;
            bool visualChanged = false;

            Font? font = KitChrome.Font(this, _genre);
            if (font != null) metricChanged |= KitChrome.SetFontOverrideIfChanged(this, "font", font);
            metricChanged |= KitChrome.SetFontSizeOverrideIfChanged(this, "font_size", fs);
            visualChanged |= KitChrome.SetColorOverrideIfChanged(this, "font_color", ink);
            bool depthChanged = ApplyTextDepth(fs, role);

            if (metricChanged || depthChanged)
                UpdateMinimumSize();
            if (metricChanged || visualChanged || depthChanged)
                QueueRedraw();
        }

        private bool ApplyTextDepth(int fs, UiSurface.TextRole role)
        {
            KitGeometry geo = KitGeometry.ForGenre(_genre);
            bool isDisplay = role is UiSurface.TextRole.Title or UiSurface.TextRole.Subtitle;
            bool shadowed = geo.Register is KitRegister.Carved or KitRegister.Casual;
            bool outlined = geo.TextTreatment == KitTextTreat.Outlined
                         || (geo.Register == KitRegister.Casual && isDisplay);

            int shadowOffset = shadowed ? Mathf.Max(1, fs / 20) : 0;
            int outline = outlined ? (role == UiSurface.TextRole.Title ? 2 : 1) : 0;

            bool changed = false;
            changed |= KitChrome.SetColorOverrideIfChanged(this, "font_shadow_color",
                shadowOffset > 0 ? new Color(0, 0, 0, 0.54f) : Colors.Transparent);
            changed |= KitChrome.SetConstantOverrideIfChanged(this, "shadow_offset_x", shadowOffset);
            changed |= KitChrome.SetConstantOverrideIfChanged(this, "shadow_offset_y", shadowOffset);
            changed |= KitChrome.SetColorOverrideIfChanged(this, "font_outline_color",
                outline > 0 ? new Color(0, 0, 0, 0.66f) : Colors.Transparent);
            changed |= KitChrome.SetConstantOverrideIfChanged(this, "outline_size", outline);
            return changed;
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
