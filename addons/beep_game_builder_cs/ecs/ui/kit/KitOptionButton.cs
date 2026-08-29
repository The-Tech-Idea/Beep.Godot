using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An <see cref="OptionButton"/> that draws the kit's chrome. Migration drop-in — see
    /// <see cref="KitChrome"/>.
    ///
    /// Because it IS an OptionButton, `Find&lt;OptionButton&gt;`, `.AddItem`, `.Selected`,
    /// `.ItemSelected +=` and the popup all keep working. `SettingsMenu.cs` binds
    /// `ResolutionOption` and `LanguageOption` this way, and `ThemeGallery.cs` three more.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitOptionButton : OptionButton
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        private string _genre = "";
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Suppress();
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            if (!_eventsHooked)
            {
                KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
                ItemSelected += _ => RefreshMinimumAndRedraw();
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = KitChrome.GenreOf(this);
                Suppress();
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
                RefreshVisualAndRedraw();
            }
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float frame = KitGeometry.ForGenre(_genre).FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            // The RIGHT margin is widened to reserve room for the arrow this class draws. Without
            // it a long item label runs straight under the chevron.
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                KitChrome.SetEmptyStyleboxOverride(
                    this,
                    s,
                    frame + pad,
                    frame + pad + fs * 1.6f,
                    frame * 0.5f + pad * 0.4f,
                    frame * 0.5f + pad * 0.4f);
            KitChrome.SetBlankIconOverride(this, "arrow");
            _suppressing = false;
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            var g = KitGeometry.ForGenre(KitChrome.GenreOf(this));
            float frame = g.FramePx(fs * 2.4f);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float width = (frame + pad) * 2f + fs * 2.6f;
            float height = Mathf.Max(fs * 2.35f, 28f);

            string label = Text;
            if (!string.IsNullOrEmpty(label)
                && KitFonts.Fallback(this, g.Font) is { } font)
            {
                label = KitChrome.Case(label, KitChrome.GenreOf(this));
                int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Value);
                width += Mathf.Min(font.GetStringSize(label, HorizontalAlignment.Left, -1, textFs).X, fs * 14f);
            }
            else
            {
                width += fs * 6f;
            }

            return new Vector2(Mathf.Max(width, fs * 10f), height);
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled
                : ButtonPressed || IsPressed() ? KitState.Pressed
                : IsHovered() ? KitState.Hover : KitState.Normal;

            Color plate = UiSurface.SemanticOrDerived(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);
            Color face = KitChrome.StateFace(plate, state);
            var body = new Rect2(Vector2.Zero, Size);

            KitChrome.DrawPlate(this, _genre, body, face, state,
                                UiSurface.FontSize(this) / 14f);

            // Label and chevron LAST: a script's _Draw runs AFTER the base class's, so the plate
            // above paints straight over anything OptionButton already drew.
            int fs = UiSurface.FontSize(this);
            Color ink = UiSurface.Text(this);
            if (state == KitState.Disabled) ink = ink with { A = 0.45f };
            float frame = KitGeometry.ForGenre(_genre).FramePx(Size.Y);
            float pad = Mathf.Max(6f, fs * 0.7f);

            var textBox = new Rect2(frame + pad, 0,
                                    Mathf.Max(4f, Size.X - frame - pad * 2f - fs * 2.0f), Size.Y);
            DrawSelectedText(textBox, ink);

            float ax = Size.X - frame - pad - fs * 0.55f;
            float ay = Size.Y * 0.5f;
            float s = fs * 0.34f;
            DrawColoredPolygon(new[]
            {
                new Vector2(ax - s, ay - s * 0.55f), new Vector2(ax + s, ay - s * 0.55f),
                new Vector2(ax, ay + s * 0.7f),
            }, ink with { A = state == KitState.Disabled ? 0.42f : 0.88f });

            KitChrome.DrawFocusRing(this, _genre, body, KitMaterial.WidgetShapeForGenre(_genre, KitWidgetClass.Button));
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private void DrawSelectedText(Rect2 textBox, Color ink)
        {
            if (string.IsNullOrEmpty(Text) || textBox.Size.X <= 1f || textBox.Size.Y <= 1f) return;
            Font? font = KitFonts.Fallback(this, KitGeometry.ForGenre(_genre).Font);
            if (font == null) return;

            string label = KitChrome.Case(Text, _genre);
            int fit = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                        new Vector2(textBox.Size.X, textBox.Size.Y * 0.72f),
                                        label, font, min: 8);
            label = KitChrome.EllipsizeText(font, label, fit, textBox.Size.X);
            if (string.IsNullOrEmpty(label)) return;

            Vector2 at = new(textBox.Position.X,
                             textBox.Position.Y + (textBox.Size.Y - font.GetHeight(fit)) * 0.5f
                             + font.GetAscent(fit));
            KitChrome.DrawText(this, _genre, font, at, label, fit, ink);
        }
    }
}
