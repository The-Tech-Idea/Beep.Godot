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
                    //
                    // The gap has to clear the box AND everything DrawPlate puts around it — its
                    // shadow, and the hover ring drawn on top. This script's _Draw runs AFTER
                    // Button has already laid out and drawn the label, so anything of ours that
                    // reaches the label's first glyph paints over it, and hover is when it shows
                    // because that is when the plate is brightest. 0.55 left too little.
                    ContentMarginLeft = box + fs * 0.72f,
                    ContentMarginRight = fs * 0.25f,
                    ContentMarginTop = fs * 0.18f,
                    ContentMarginBottom = fs * 0.18f,
                });
            AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
            foreach (string c in new[] { "font_color", "font_hover_color", "font_pressed_color",
                                         "font_hover_pressed_color", "font_focus_color",
                                         "font_disabled_color" })
                AddThemeColorOverride(c, new Color(0, 0, 0, 0));

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

        private static float BoxSize(int fs) => Mathf.Clamp(fs * 1.08f, 15f, 22f);

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

            var box = new Rect2(Mathf.Max(1f, fs * 0.10f), (Size.Y - b) * 0.5f, b, b);

            // An EMPTY box is a recessed well, not a flat square: the same dark tint of the
            // surface's own hue the slider track and toggle use, so the three read as one family.
            Color face = ButtonPressed
                ? KitChrome.StateFace(on, state)
                : new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);

            KitChrome.DrawPlate(this, _genre, box, face, state, 0.55f);
            if (state == KitState.Hover && !ButtonPressed)
            {
                Color h = UiSurface.Semantic(this, OnRole);
                DrawRect(box.Grow(-b * 0.12f), new Color(h.R, h.G, h.B, 0.20f), false,
                         Mathf.Max(1f, b * 0.07f));
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


            DrawCaption(box);
        }

        private void DrawCaption(Rect2 box)
        {
            if (string.IsNullOrEmpty(Text)) return;
            Font? font = ThemeDB.FallbackFont;
            if (font == null) return;

            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                       new Vector2(Mathf.Max(1f, Size.X - box.End.X - fsPadding()),
                                                   Mathf.Max(1f, Size.Y * 0.70f)),
                                       Text, font, min: 8);
            Vector2 labelPos = new(box.End.X + Mathf.Max(7f, fs * 0.45f),
                                   (Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs));
            DrawString(font, labelPos, Text, HorizontalAlignment.Left, -1f, fs,
                       Disabled ? UiSurface.Text(this) with { A = 0.45f } : UiSurface.Text(this));

            float fsPadding() => UiSurface.FontSize(this) * 0.35f;
        }
    }
}
