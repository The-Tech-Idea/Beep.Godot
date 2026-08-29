using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An on/off switch — CATALOGUE-FROM-ART.md F.2 lists `OnOffSwitch` with the note
    /// "<b>this is the game checkbox</b>". Games do not draw a tick in a square; they draw a
    /// sliding plate in a track, because it reads at a glance and from a distance.
    ///
    /// CATALOGUE §D also corrects an earlier claim of mine: `gameui2`, `gameui4` and `gameui5`
    /// DO contain checkboxes, so <see cref="Style"/> offers the boxed form too — but the switch
    /// is the default because it is what the game sheets overwhelmingly use.
    ///
    /// Off is not "disabled": off keeps full saturation on its track and simply sits at the other
    /// end. Draining saturation is reserved for unavailable (the 7x rule), and using it for
    /// "off" would make every unset option look broken.
    ///
    /// IT IS A GODOT <see cref="CheckButton"/>.
    /// ---------------------------------------
    /// Godot already models "a two-state control you click": ButtonPressed, Toggled, Disabled,
    /// focus, keyboard activation, ButtonGroup, and the whole theme pipeline. This class used to
    /// reimplement the first three badly -- its own `Pressed` property, its own `Toggled` signal,
    /// its own `_GuiInput` -- so `GetNode&lt;CheckButton&gt;` failed against it, `ButtonPressed`
    /// did not exist, and a settings screen could not treat it like any other toggle.
    ///
    /// All of that is inherited now. What remains here is the only part Godot cannot do: draw the
    /// genre's plate, silhouette and material instead of a StyleBox.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitToggle : CheckButton
    {
        public enum ToggleStyle { Switch, Box }

        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export] public ToggleStyle Style { get => _style; set { if (_style == value) return; _style = value; RefreshMinimumAndRedraw(); } }
        private ToggleStyle _style = ToggleStyle.Switch;

        /// <summary>Palette role of the ON state.</summary>
        [Export]
        public UiSurface.Role OnRole
        {
            get => _onRole;
            set { if (_onRole == value) return; _onRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _onRole = UiSurface.Role.Success;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            // BaseButton runs the state machine; without ToggleMode a CheckButton fires and
            // springs back instead of latching.
            ToggleMode = true;
            Suppress();
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            if (!_eventsHooked)
            {
                KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
                Toggled += _ => RefreshVisualAndRedraw();
            }
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return Style == ToggleStyle.Box
                ? new Vector2(fs * 1.7f, fs * 1.7f)
                : new Vector2(fs * 3.7f, fs * 1.8f);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
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

        /// <summary>Blank the base chrome AND the check ICONS. CheckButton draws its on/off pill
        /// from theme icons, not a StyleBox — suppressing only the StyleBox leaves Godot's own
        /// switch floating next to the one this class draws.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            KitChrome.Suppress(this, new[] { "normal", "hover", "pressed", "disabled", "focus" },
                               0f, 0f, 0f);
            foreach (string icon in new[]
                     { "checked", "unchecked", "checked_disabled", "unchecked_disabled" })
                KitChrome.SetBlankIconOverride(this, icon);
            _suppressing = false;
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 6f) return;

            bool _on = ButtonPressed;
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(face);
            Color on = UiSurface.SemanticOrDerived(this, OnRole);
            if (on.A < 0.02f) on = face;
            if (Disabled)
            {
                on = KitChrome.StateFace(on, KitState.Disabled);
                face = KitChrome.StateFace(face, KitState.Disabled);
            }
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, Geo.Rim * 0.7f * (fs / 14f));
            var r = new Rect2(Vector2.Zero, Size);

            if (Style == ToggleStyle.Box)
            {
                KitChrome.DrawShape(this, _genre, r, KitChrome.Shape(_genre), _on ? on : new Color(face.R * 0.55f, face.G * 0.55f, face.B * 0.6f, 1f),
                          ink, rimPx);
                if (_on) DrawTick(r, UiSurface.Luminance(on) > 0.5f
                                        ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                else DrawOffMark(r, UiSurface.Text(this) with { A = 0.42f });
                KitChrome.DrawFocusRing(this, _genre, r, KitChrome.Shape(_genre), 0.8f);
                return;
            }

            float outerPad = Mathf.Max(1f, Size.Y * 0.06f);
            var trackRect = r.Grow(-outerPad);
            Color track = _on
                ? new Color(on.R * 0.54f + face.R * 0.20f,
                            on.G * 0.54f + face.G * 0.20f,
                            on.B * 0.54f + face.B * 0.20f,
                            Disabled ? 0.55f : 1f)
                : KitChrome.WellFace(face);
            KitShape trackShape = KitChrome.Shape(_genre, KitWidgetClass.Bar);
            KitChrome.DrawShape(this, _genre, trackRect, trackShape, track, ink, rimPx,
                                KitWidgetClass.Bar);

            float inset = Mathf.Max(2f, trackRect.Size.Y * 0.14f);
            float kh = Mathf.Max(8f, trackRect.Size.Y - inset * 2f);
            float kw = Mathf.Max(kh * 1.16f, Size.X * 0.34f);
            float x0 = trackRect.Position.X + inset;
            float x1 = trackRect.End.X - inset - kw;
            var knob = new Rect2(new Vector2(_on ? x1 : x0, trackRect.Position.Y + inset),
                                 new Vector2(kw, kh));
            Color knobFace = _on
                ? new Color(Mathf.Lerp(on.R, 1f, 0.12f),
                            Mathf.Lerp(on.G, 1f, 0.12f),
                            Mathf.Lerp(on.B, 1f, 0.12f),
                            Disabled ? 0.70f : 1f)
                : new Color(face.R * 0.92f, face.G * 0.92f, face.B * 0.95f, 1f);
            KitChrome.DrawShape(this, _genre, knob, KitShape.Pill,
                                KitChrome.StateFace(knobFace, Disabled ? KitState.Disabled : KitState.Normal),
                                UiSurface.Ink(knobFace) with { A = Disabled ? 0.24f : 0.34f },
                                Mathf.Max(1f, rimPx * 0.45f), KitWidgetClass.Bar);

            Color mark = UiSurface.Ink(knobFace) with { A = Disabled ? 0.40f : 0.58f };
            float markW = Mathf.Max(1f, kh * 0.10f);
            float mx = knob.Position.X + knob.Size.X * 0.5f;
            DrawLine(new Vector2(mx, knob.Position.Y + kh * 0.28f),
                     new Vector2(mx, knob.End.Y - kh * 0.28f), mark, markW);

            KitChrome.DrawFocusRing(this, _genre, trackRect, trackShape, 0.8f);
        }

        private void DrawTick(Rect2 r, Color col)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.24f;
            float w = Mathf.Max(2f, a * 0.45f);
            DrawLine(c + new Vector2(-a, 0f), c + new Vector2(-a * 0.25f, a * 0.8f), col, w);
            DrawLine(c + new Vector2(-a * 0.25f, a * 0.8f), c + new Vector2(a, -a * 0.75f), col, w);
        }

        private void DrawOffMark(Rect2 r, Color col)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.24f;
            float w = Mathf.Max(2f, a * 0.36f);
            DrawLine(c - new Vector2(a, a), c + new Vector2(a, a), col, w);
            DrawLine(c - new Vector2(a, -a), c + new Vector2(a, -a), col, w);
        }
    }
}
