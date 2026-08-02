using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An <see cref="HSlider"/> that draws the kit's chrome. Migration drop-in — see
    /// <see cref="KitChrome"/> for why these derive from the Godot type.
    ///
    /// Because it IS an HSlider, `Find&lt;HSlider&gt;`, `.Value`, `.ValueChanged +=` and every
    /// Range binding keep working. `SettingsMenu.cs` binds four of these by type.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSliderBar : HSlider
    {
        /// <summary>Palette role for the FILLED portion. Accent by default — the reference sheets
        /// put the palette on the fill and leave the track neutral.</summary>
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

        private string _genre = "";
        private bool _suppressing;
        private bool _dragging;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
            DragStarted += () => { _dragging = true; QueueRedraw(); };
            DragEnded += _ => { _dragging = false; QueueRedraw(); };
            ValueChanged += _ => QueueRedraw();
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

        /// <summary>Slider's grabber is an ICON, not a StyleBox, so blanking the styleboxes alone
        /// still leaves Godot's default knob drawn on top of ours. It needs a transparent
        /// texture — which is the whole reason <see cref="KitChrome.Blank"/> exists.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            foreach (string s in new[] { "slider", "grabber_area", "grabber_area_highlight" })
                AddThemeStyleboxOverride(s, new StyleBoxEmpty());
            foreach (string i in new[] { "grabber", "grabber_highlight", "grabber_disabled", "tick" })
                AddThemeIconOverride(i, KitChrome.Blank);

            // HSlider derives its MINIMUM SIZE from the grabber icon and the slider StyleBox.
            // Blanking both collapses it to about a pixel tall, `_Draw` hits its own
            // `Size.Y <= 4` guard and returns, and the control vanishes completely — which is
            // exactly what happened: settings_menu rendered "Master Volume ... 80%" with no
            // slider between them, and nothing was logged. Anything that blanks a control's
            // theme art has to restate the size that art was providing.
            int fs = UiSurface.FontSize(this);
            CustomMinimumSize = new Vector2(Mathf.Max(CustomMinimumSize.X, fs * 8f),
                                            Mathf.Max(fs * 2.0f, 22f));
            _suppressing = false;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            var g = KitGeometry.ForGenre(_genre);
            KitState state = Editable ? KitState.Normal : KitState.Disabled;

            Color accent = UiSurface.Semantic(this, Accent);
            if (accent.A < 0.02f) accent = UiSurface.Of(this);
            Color surface = UiSurface.Of(this);

            // TRACK: a dark tint of the surface's OWN hue, never grey — the settled rule, 4
            // references. A grey track under a coloured fill is the clearest "themed form" tell.
            float trackH = Mathf.Max(6f, Size.Y * 0.34f);
            var track = new Rect2(0, (Size.Y - trackH) * 0.5f, Size.X, trackH);
            Color trackCol = new(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);
            KitChrome.Fill(this, KitShape.Pill, track, g, trackCol,
                           UiSurface.Ink(surface), Mathf.Max(1f, g.Rim * 0.6f));

            float t = (float)((Value - MinValue) / Mathf.Max(0.0001, MaxValue - MinValue));
            t = Mathf.Clamp(t, 0f, 1f);

            // FILL, drawn with the full plate stack so the slider is made of the same material
            // as every other widget in the genre rather than being a flat coloured bar.
            if (t > 0.001f)
            {
                var fill = new Rect2(track.Position, new Vector2(track.Size.X * t, track.Size.Y));
                if (fill.Size.X > 2f)
                    KitChrome.DrawPlate(this, _genre, fill, KitChrome.StateFace(accent, state),
                                        state, 0.7f);
            }

            // KNOB
            float kr = Mathf.Max(6f, Size.Y * 0.44f);
            var kc = new Vector2(Mathf.Lerp(kr, Size.X - kr, t), Size.Y * 0.5f);
            var knob = new Rect2(kc - new Vector2(kr, kr), new Vector2(kr * 2f, kr * 2f));
            Color knobFace = _dragging
                ? new Color(Mathf.Lerp(surface.R, accent.R, 0.28f),
                            Mathf.Lerp(surface.G, accent.G, 0.28f),
                            Mathf.Lerp(surface.B, accent.B, 0.28f), 1f)
                : surface;
            KitChrome.DrawPlate(this, _genre, knob,
                                KitChrome.StateFace(knobFace, state), state, 0.8f);
            DrawLine(kc + new Vector2(0f, -kr * 0.45f), kc + new Vector2(0f, kr * 0.45f),
                     UiSurface.Ink(knobFace) with { A = 0.50f }, Mathf.Max(1f, kr * 0.12f));
        }
    }
}
