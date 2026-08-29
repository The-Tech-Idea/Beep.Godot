using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A <see cref="CheckButton"/> that draws the kit's chrome: a track with a sliding knob.
    /// Migration drop-in — see <see cref="KitChrome"/>.
    ///
    /// Because it IS a CheckButton, `Find&lt;CheckButton&gt;`, `.ButtonPressed`,
    /// `.SetPressedNoSignal` and `.Toggled +=` keep working — `SettingsMenu.cs` uses all four.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitCheckButton : CheckButton
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        /// <summary>Palette role for the ON state. Success reads as "enabled" without needing a
        /// label, which is what every reference toggle does.</summary>
        [Export]
        public UiSurface.Role OnRole
        {
            get => _onRole;
            set { if (_onRole == value) return; _onRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _onRole = UiSurface.Role.Success;

        private string _genre = "";
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Suppress();
            if (!_eventsHooked)
            {
                KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
                Toggled += _ => RefreshVisualAndRedraw();
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

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            int fs = UiSurface.FontSize(this);
            float h = TrackHeight(fs);
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                // Room on the RIGHT for the switch this class draws; the label keeps the left.
                KitChrome.SetEmptyStyleboxOverride(
                    this,
                    s,
                    2f,
                    h * 2.05f + fs * 0.8f,
                    fs * 0.35f,
                    fs * 0.35f);
            KitChrome.SetFontSizeOverrideIfChanged(this, "font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
            foreach (string c in new[] { "font_color", "font_hover_color", "font_pressed_color",
                                         "font_hover_pressed_color", "font_focus_color",
                                         "font_disabled_color" })
                KitChrome.SetColorOverrideIfChanged(this, c, new Color(0, 0, 0, 0));

            // Restate the height the blanked icons were providing, or the row collapses and the
            // switch renders as an unreadable sliver -- the same failure the slider had. A toggle
            // in a settings list came out ~40x20 with no visible knob.
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            // CheckButton's on/off art is a set of ICONS, so blanking styleboxes is not enough.
            foreach (string i in new[]
                     {
                         "checked", "unchecked", "checked_disabled", "unchecked_disabled",
                         "checked_mirrored", "unchecked_mirrored",
                     })
                KitChrome.SetBlankIconOverride(this, i);
            _suppressing = false;
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float h = TrackHeight(fs);
            float width = h * 2.05f + fs * 2.2f;
            if (!string.IsNullOrEmpty(Text)
                && KitFonts.Fallback(this, KitGeometry.ForGenre(KitChrome.GenreOf(this)).Font) is { } font)
            {
                string caption = KitChrome.Case(Text, KitChrome.GenreOf(this));
                int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
                width += font.GetStringSize(caption, HorizontalAlignment.Left, -1, textFs).X;
            }
            else
            {
                width += fs * 3.8f;
            }

            return new Vector2(Mathf.Max(width, h * 2.05f + fs * 4f), h * 1.5f);
        }

        /// <summary>Track height, floored so the switch stays a readable control. Was
        /// min(Size.Y*0.72, fs*1.35), which on a tight settings row produced a ~20px sliver.</summary>
        private static float TrackHeight(int fs) => Mathf.Max(22f, fs * 1.5f);

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled
                : IsHovered() ? KitState.Hover : KitState.Normal;

            int fs = UiSurface.FontSize(this);
            Color surface = UiSurface.Of(this);
            Color on = UiSurface.SemanticOrDerived(this, OnRole);
            if (on.A < 0.02f) on = surface;

            float h = TrackHeight(fs);
            float w = h * 2.05f;
            var track = new Rect2(Size.X - w - 2f, (Size.Y - h) * 0.5f, w, h);

            Color trackCol = ButtonPressed
                ? new Color(on.R * 0.54f + surface.R * 0.20f,
                            on.G * 0.54f + surface.G * 0.20f,
                            on.B * 0.54f + surface.B * 0.20f,
                            state == KitState.Disabled ? 0.55f : 1f)
                : KitChrome.WellFace(surface);
            KitShape trackShape = KitChrome.Shape(_genre, KitWidgetClass.Bar);
            KitChrome.DrawShape(this, _genre, track, trackShape, trackCol, UiSurface.Ink(surface),
                                Mathf.Max(1f, h * 0.07f), KitWidgetClass.Bar);

            float inset = Mathf.Max(2f, h * 0.14f);
            float kh = Mathf.Max(8f, h - inset * 2f);
            float kw = Mathf.Max(kh * 1.16f, w * 0.34f);
            float x0 = track.Position.X + inset;
            float x1 = track.End.X - inset - kw;
            var knob = new Rect2(new Vector2(ButtonPressed ? x1 : x0, track.Position.Y + inset),
                                 new Vector2(kw, kh));
            Color knobFace = ButtonPressed
                ? new Color(Mathf.Lerp(on.R, 1f, 0.12f),
                            Mathf.Lerp(on.G, 1f, 0.12f),
                            Mathf.Lerp(on.B, 1f, 0.12f),
                            state == KitState.Disabled ? 0.70f : 1f)
                : new Color(surface.R * 0.92f, surface.G * 0.92f, surface.B * 0.95f, 1f);
            KitChrome.DrawShape(this, _genre, knob, KitShape.Pill,
                                KitChrome.StateFace(knobFace, state),
                                UiSurface.Ink(knobFace) with { A = state == KitState.Disabled ? 0.24f : 0.34f },
                                Mathf.Max(1f, h * 0.035f), KitWidgetClass.Bar);

            Color mark = UiSurface.Ink(knobFace) with { A = state == KitState.Disabled ? 0.40f : 0.58f };
            float mx = knob.Position.X + knob.Size.X * 0.5f;
            DrawLine(new Vector2(mx, knob.Position.Y + kh * 0.28f),
                     new Vector2(mx, knob.End.Y - kh * 0.28f), mark, Mathf.Max(1f, kh * 0.10f));

            if (KitChrome.Font(this, _genre) is { } font)
                DrawCaption(font, track, state);

            KitChrome.DrawFocusRing(this, _genre, track, trackShape, 0.8f);
        }

        private void DrawCaption(Font font, Rect2 track, KitState state)
        {
            if (string.IsNullOrEmpty(Text)) return;

            int themeFs = UiSurface.FontSize(this);
            float pad = Mathf.Max(2f, themeFs * 0.35f);
            float gap = Mathf.Max(7f, themeFs * 0.55f);
            float available = Mathf.Max(1f, track.Position.X - gap - pad);
            string caption = KitChrome.Case(Text, _genre);
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                       new Vector2(available, Mathf.Max(1f, Size.Y * 0.70f)),
                                       caption, font, min: 8);
            caption = KitChrome.EllipsizeText(font, caption, fs, available);
            if (string.IsNullOrEmpty(caption)) return;

            Color ink = state == KitState.Disabled
                ? UiSurface.Text(this) with { A = 0.45f }
                : UiSurface.Text(this);
            Vector2 at = new(pad, (Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs));
            KitChrome.DrawText(this, _genre, font, at, caption, fs, ink);
        }
    }
}
