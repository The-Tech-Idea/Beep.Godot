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
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                AddThemeStyleboxOverride(s, new StyleBoxEmpty
                {
                    // Room on the RIGHT for the switch this class draws; the label keeps the left.
                    ContentMarginLeft = 2f,
                    ContentMarginRight = fs * 3.4f,
                    ContentMarginTop = fs * 0.35f,
                    ContentMarginBottom = fs * 0.35f,
                });
            // CheckButton's on/off art is a set of ICONS, so blanking styleboxes is not enough.
            foreach (string i in new[]
                     {
                         "checked", "unchecked", "checked_disabled", "unchecked_disabled",
                         "checked_mirrored", "unchecked_mirrored",
                     })
                AddThemeIconOverride(i, KitChrome.Blank);
            _suppressing = false;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled
                : IsHovered() ? KitState.Hover : KitState.Normal;

            int fs = UiSurface.FontSize(this);
            Color surface = UiSurface.Of(this);
            Color on = UiSurface.Semantic(this, OnRole);
            if (on.A < 0.02f) on = surface;

            float h = Mathf.Min(Size.Y * 0.72f, fs * 1.35f);
            float w = h * 2.05f;
            var track = new Rect2(Size.X - w - 2f, (Size.Y - h) * 0.5f, w, h);

            // OFF is a dark tint of the surface's own hue, not grey — same settled rule the
            // slider track follows, so the two read as the same material.
            Color trackCol = ButtonPressed
                ? KitChrome.StateFace(on, state)
                : new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);
            KitChrome.Fill(this, KitShape.Pill, track, KitGeometry.ForGenre(_genre),
                           trackCol, UiSurface.Ink(surface), Mathf.Max(1f, h * 0.09f));

            float kr = h * 0.40f;
            float kx = ButtonPressed ? track.Position.X + track.Size.X - kr - h * 0.12f
                                     : track.Position.X + kr + h * 0.12f;
            var knob = new Rect2(kx - kr, track.Position.Y + h * 0.5f - kr, kr * 2f, kr * 2f);
            KitChrome.DrawPlate(this, _genre, knob,
                                KitChrome.StateFace(surface, state), state, 0.55f);

            // The label LAST — the plate above would otherwise paint over what CheckButton drew.
            Color ink = UiSurface.Text(this);
            if (state == KitState.Disabled) ink = ink with { A = 0.45f };
            KitChrome.DrawLabel(this, this, Text,
                                new Rect2(2f, 0, Mathf.Max(4f, track.Position.X - 6f), Size.Y),
                                ink, 0f, HorizontalAlignment.Left);
        }
    }
}
