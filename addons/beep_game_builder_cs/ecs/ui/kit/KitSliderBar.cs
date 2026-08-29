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
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        /// <summary>Palette role for the FILLED portion. Accent by default — the reference sheets
        /// put the palette on the fill and leave the track neutral.</summary>
        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Accent;

        private string _genre = "";
        private bool _suppressing;
        private bool _dragging;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Suppress();
            if (!_eventsHooked)
            {
                DragStarted += () => { _dragging = true; QueueRedraw(); };
                DragEnded += _ => { _dragging = false; QueueRedraw(); };
                ValueChanged += _ => QueueRedraw();
                _eventsHooked = true;
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearDragState();
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

        private void ClearDragState()
        {
            if (!_dragging) return;
            _dragging = false;
            QueueRedraw();
        }

        /// <summary>Slider's grabber is an ICON, not a StyleBox, so blanking the styleboxes alone
        /// still leaves Godot's default knob drawn on top of ours. It needs a transparent
        /// texture — which is the whole reason <see cref="KitChrome.Blank"/> exists.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            foreach (string s in new[] { "slider", "grabber_area", "grabber_area_highlight" })
                KitChrome.SetEmptyStyleboxOverride(this, s);
            foreach (string i in new[] { "grabber", "grabber_highlight", "grabber_disabled", "tick" })
                KitChrome.SetBlankIconOverride(this, i);

            // HSlider derives its MINIMUM SIZE from the grabber icon and the slider StyleBox.
            // Blanking both collapses it to about a pixel tall, `_Draw` hits its own
            // `Size.Y <= 4` guard and returns, and the control vanishes completely — which is
            // exactly what happened: settings_menu rendered "Master Volume ... 80%" with no
            // slider between them, and nothing was logged. Anything that blanks a control's
            // theme art has to restate the size that art was providing.
            int fs = UiSurface.FontSize(this);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            _suppressing = false;
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 8f, Mathf.Max(fs * 2.0f, 22f));
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            var g = KitGeometry.ForGenre(_genre);
            KitState state = Editable ? KitState.Normal : KitState.Disabled;
            bool dragging = Editable && _dragging;

            Color accent = UiSurface.SemanticOrDerived(this, Accent);
            if (accent.A < 0.02f) accent = UiSurface.Of(this);
            Color surface = UiSurface.Of(this);
            Color ink = KitChrome.StateFace(UiSurface.Ink(surface), state);

            // TRACK: a dark tint of the surface's OWN hue, never grey — the settled rule, 4
            // references. A grey track under a coloured fill is the clearest "themed form" tell.
            float trackH = Mathf.Max(6f, Size.Y * 0.34f);
            var track = new Rect2(0, (Size.Y - trackH) * 0.5f, Size.X, trackH);
            Color trackCol = new(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);
            KitChrome.Fill(this, KitShape.Pill, track, g, trackCol,
                           ink, Mathf.Max(1f, g.Rim * 0.6f));

            float t = (float)((Value - MinValue) / Mathf.Max(0.0001, MaxValue - MinValue));
            t = Mathf.Clamp(t, 0f, 1f);

            // Fill belongs inside the track, so it uses the track silhouette instead of drawing
            // another full plate inside the slider.
            if (t > 0.001f)
            {
                var fill = new Rect2(track.Position, new Vector2(track.Size.X * t, track.Size.Y));
                if (fill.Size.X > 2f)
                    KitChrome.DrawShape(this, _genre, fill, KitShape.Pill,
                                        KitChrome.StateFace(accent, state), ink with { A = 0.18f },
                                        0f, KitWidgetClass.Bar);
            }

            // KNOB
            float kr = Mathf.Max(6f, Size.Y * 0.44f);
            var kc = new Vector2(Mathf.Lerp(kr, Size.X - kr, t), Size.Y * 0.5f);
            var knob = new Rect2(kc - new Vector2(kr, kr), new Vector2(kr * 2f, kr * 2f));
            Color knobFace = dragging
                ? new Color(Mathf.Lerp(surface.R, accent.R, 0.28f),
                            Mathf.Lerp(surface.G, accent.G, 0.28f),
                            Mathf.Lerp(surface.B, accent.B, 0.28f), 1f)
                : surface;
            KitChrome.DrawShape(this, _genre, knob, KitShape.Pill,
                                KitChrome.StateFace(knobFace, state),
                                ink with { A = Editable ? 0.36f : 0.24f },
                                Mathf.Max(1f, kr * 0.06f), KitWidgetClass.Bar);
            DrawLine(kc + new Vector2(0f, -kr * 0.45f), kc + new Vector2(0f, kr * 0.45f),
                     ink with { A = Editable ? 0.50f : 0.30f }, Mathf.Max(1f, kr * 0.12f));
            KitChrome.DrawFocusRing(this, _genre, new Rect2(Vector2.Zero, Size), KitShape.Pill, 0.8f);
        }
    }
}
