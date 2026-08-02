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
        /// <summary>Palette role for the ON state. Success reads as "enabled" without needing a
        /// label, which is what every reference toggle does.</summary>
        [Export] public UiSurface.Role OnRole { get; set; } = UiSurface.Role.Success;

        private string _genre = "";
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                Suppress();
                QueueRedraw();
            }
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            int fs = UiSurface.FontSize(this);
            float h = TrackHeight(fs);
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                AddThemeStyleboxOverride(s, new StyleBoxEmpty
                {
                    // Room on the RIGHT for the switch this class draws; the label keeps the left.
                    ContentMarginLeft = 2f,
                    ContentMarginRight = h * 2.05f + fs * 0.8f,
                    ContentMarginTop = fs * 0.35f,
                    ContentMarginBottom = fs * 0.35f,
                });

            // Restate the height the blanked icons were providing, or the row collapses and the
            // switch renders as an unreadable sliver -- the same failure the slider had. A toggle
            // in a settings list came out ~40x20 with no visible knob.
            CustomMinimumSize = new Vector2(Mathf.Max(CustomMinimumSize.X, h * 2.05f + fs * 4f),
                                            Mathf.Max(CustomMinimumSize.Y, h * 1.5f));
            // CheckButton's on/off art is a set of ICONS, so blanking styleboxes is not enough.
            foreach (string i in new[]
                     {
                         "checked", "unchecked", "checked_disabled", "unchecked_disabled",
                         "checked_mirrored", "unchecked_mirrored",
                     })
                AddThemeIconOverride(i, KitChrome.Blank);
            _suppressing = false;
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
            Color on = UiSurface.Semantic(this, OnRole);
            if (on.A < 0.02f) on = surface;

            float h = TrackHeight(fs);
            float w = h * 2.05f;
            var track = new Rect2(Size.X - w - 2f, (Size.Y - h) * 0.5f, w, h);

            // OFF is a dark tint of the surface's own hue, not grey — same settled rule the
            // slider track follows, so the two read as the same material.
            Color trackCol = ButtonPressed
                ? KitChrome.StateFace(on, state)
                : new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);
            KitChrome.Fill(this, KitShape.Pill, track, KitGeometry.ForGenre(_genre),
                           trackCol, UiSurface.Ink(surface), Mathf.Max(1f, h * 0.09f));

            // The KNOB must be legible against its own track, so it is derived from the track's
            // luminance rather than taking the panel surface. Drawn as a plain disc with a rim:
            // at ~17px the full band stack has no room and consumed the knob entirely, which is
            // why the toggles read as featureless pills.
            float kr = h * 0.38f;
            float kx = ButtonPressed ? track.Position.X + track.Size.X - kr - h * 0.14f
                                     : track.Position.X + kr + h * 0.14f;
            var kc = new Vector2(kx, track.Position.Y + h * 0.5f);

            // Threshold well above mid-grey ON PURPOSE. At 0.45 a mid-tone track (the success
            // green) flipped the knob to dark while the off-state track kept a light one, so the
            // knob changed colour AND position between states and read as two different controls.
            // A switch should move its knob, not swap it. Only a genuinely light track (a pale
            // parchment palette) inverts.
            float trackLum = UiSurface.Luminance(trackCol);
            Color knobCol = trackLum < 0.62f
                ? new Color(0.96f, 0.96f, 0.94f, 1f)
                : new Color(surface.R * 0.30f, surface.G * 0.29f, surface.B * 0.33f, 1f);
            if (state == KitState.Disabled) knobCol = knobCol with { A = 0.55f };

            DrawCircle(kc, kr, knobCol);
            DrawArc(kc, kr, 0f, Mathf.Tau, 20, UiSurface.Ink(trackCol),
                    Mathf.Max(1f, kr * 0.18f));

            if (GetThemeDefaultFont() is { } font)
            {
                string mark = ButtonPressed ? "ON" : "OFF";
                int mf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(track.Size.X * 0.34f, track.Size.Y * 0.42f),
                                           mark, font, min: 7);
                Vector2 m = font.GetStringSize(mark, HorizontalAlignment.Left, -1, mf);
                float tx = ButtonPressed
                    ? track.Position.X + h * 0.24f
                    : track.End.X - h * 0.24f - m.X;
                Color text = UiSurface.Luminance(trackCol) > 0.5f
                    ? new Color(0.10f, 0.09f, 0.08f, 0.78f)
                    : new Color(0.98f, 0.96f, 0.92f, 0.78f);
                DrawString(font, new Vector2(tx, track.Position.Y + (track.Size.Y + m.Y * 0.6f) * 0.5f),
                           mark, HorizontalAlignment.Left, -1, mf, text);
            }


            // NO label drawn here. The plate above covers only the box/switch, so the base
            // class's own text is still visible — drawing it again renders "Textures" twice,
            // overlapping. The content margin set in Suppress() is what reserves space for the
            // box; Button lays the label out after it.
            //
            // This differs from KitPushButton, whose plate covers the WHOLE control and therefore
            // paints over the base text, so that one must redraw it. The rule is: redraw the
            // label only if your plate hid it.
        }
    }
}
