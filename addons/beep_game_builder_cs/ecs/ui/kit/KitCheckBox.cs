using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A <see cref="CheckBox"/> that draws a real BOX WITH A TICK in the kit's material.
    /// Migration drop-in — see <see cref="KitChrome"/>.
    ///
    /// CheckBox vs CheckButton is a real distinction in Godot and the kit must keep it:
    /// a CheckButton is a SWITCH (track + sliding knob, see <see cref="KitCheckButton"/>) and a
    /// CheckBox is a BOX that gets a tick. They are not interchangeable, and rendering a
    /// checkbox as a switch — or leaving it as Godot's stock 16px blue glyph on a themed
    /// surface, which is what shipped — makes it read as a foreign control.
    ///
    /// Because it IS a CheckBox, `Find&lt;CheckBox&gt;`, `.ButtonPressed`, `.SetPressedNoSignal`
    /// and `.Toggled +=` keep working; `ThemeGallery.cs` binds one by type.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitCheckBox : CheckBox
    {
        /// <summary>Palette role for the tick and the checked box. Accent by default: in the
        /// reference sheets a ticked box takes the palette while an empty one stays neutral —
        /// the settled "palette goes on ONE element" rule.</summary>
        [Export] public UiSurface.Role OnRole { get; set; } = UiSurface.Role.Accent;

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
            float box = BoxSize(fs);
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                AddThemeStyleboxOverride(s, new StyleBoxEmpty
                {
                    // Room on the LEFT for the box this class draws; the label follows it.
                    ContentMarginLeft = box + fs * 0.55f,
                    ContentMarginRight = 2f,
                    ContentMarginTop = fs * 0.3f,
                    ContentMarginBottom = fs * 0.3f,
                });

            // CheckBox's tick is a set of ICONS, so blanking styleboxes alone still leaves
            // Godot's stock 16px glyph drawn on top of ours — that is precisely what the
            // theme_gallery "Textures" box was showing on a themed gold plate.
            foreach (string i in new[]
                     {
                         "checked", "unchecked", "checked_disabled", "unchecked_disabled",
                         "radio_checked", "radio_unchecked",
                         "radio_checked_disabled", "radio_unchecked_disabled",
                     })
                AddThemeIconOverride(i, KitChrome.Blank);

            _suppressing = false;
        }

        private static float BoxSize(int fs) => Mathf.Max(16f, fs * 1.25f);

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled
                : IsHovered() ? KitState.Hover : KitState.Normal;

            int fs = UiSurface.FontSize(this);
            float b = BoxSize(fs);
            Color surface = UiSurface.Of(this);
            Color on = UiSurface.Semantic(this, OnRole);
            if (on.A < 0.02f) on = surface;

            var box = new Rect2(0f, (Size.Y - b) * 0.5f, b, b);

            // An EMPTY box is a recessed well, not a flat square: the same dark tint of the
            // surface's own hue the slider track and toggle use, so the three read as one family.
            Color face = ButtonPressed
                ? KitChrome.StateFace(on, state)
                : new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);

            KitChrome.DrawPlate(this, _genre, box, face, state, 0.55f);
            if (state == KitState.Hover && !ButtonPressed)
            {
                Color h = UiSurface.Semantic(this, OnRole);
                DrawRect(box.Grow(-b * 0.18f), new Color(h.R, h.G, h.B, 0.18f), false,
                         Mathf.Max(1.5f, b * 0.09f));
            }

            if (ButtonPressed)
            {
                // A drawn tick, sized off the box, so it scales with the theme's font rather
                // than being a fixed 16px glyph.
                Color ink = UiSurface.Ink(face);
                float lum = UiSurface.Luminance(face);
                if (lum < 0.4f) ink = new Color(1f, 1f, 1f, 0.92f);
                float w = Mathf.Max(2f, b * 0.14f);
                var c = box.Position;
                DrawPolyline(new[]
                {
                    c + new Vector2(b * 0.22f, b * 0.52f),
                    c + new Vector2(b * 0.42f, b * 0.72f),
                    c + new Vector2(b * 0.78f, b * 0.28f),
                }, ink, w);
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
