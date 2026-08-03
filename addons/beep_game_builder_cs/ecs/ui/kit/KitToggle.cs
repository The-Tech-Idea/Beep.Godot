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

        [Export] public ToggleStyle Style { get; set; } = ToggleStyle.Switch;

        /// <summary>Palette role of the ON state.</summary>
        [Export] public UiSurface.Role OnRole { get; set; } = UiSurface.Role.Success;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            // BaseButton runs the state machine; without ToggleMode a CheckButton fires and
            // springs back instead of latching.
            ToggleMode = true;
            Suppress();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = Style == ToggleStyle.Box
                    ? new Vector2(fs * 1.7f, fs * 1.7f)
                    : new Vector2(fs * 3.4f, fs * 1.7f);
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
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
                AddThemeIconOverride(icon, KitChrome.Blank);
            _suppressing = false;
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 6f) return;

            bool _on = ButtonPressed;
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(face);
            Color on = UiSurface.Semantic(this, OnRole);
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
                return;
            }

            // Track keeps its hue whether on or off — off is a position, not a disabled state.
            Color track = _on
                ? new Color(on.R * 0.55f, on.G * 0.55f, on.B * 0.58f, 1f)
                : new Color(face.R * 0.42f, face.G * 0.42f, face.B * 0.46f, 1f);
            KitChrome.DrawShape(this, _genre, r, KitShape.Pill, track, ink, rimPx);

            float kw = Size.X * 0.46f;
            var knob = new Rect2(_on ? Size.X - kw : 0f, 0f, kw, Size.Y);
            KitChrome.DrawShape(this, _genre, knob, KitShape.Pill, _on ? on : new Color(face.R * 0.85f, face.G * 0.85f, face.B * 0.9f, 1f),
                      ink, rimPx);
            if (GetThemeDefaultFont() is { } font)
            {
                string mark = _on ? "ON" : "OFF";
                int mf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(knob.Size.X * 0.76f, knob.Size.Y * 0.44f),
                                           mark, font, min: 7);
                Vector2 m = font.GetStringSize(mark, HorizontalAlignment.Left, -1, mf);
                Color text = UiSurface.Luminance(_on ? on : face) > 0.5f
                    ? new Color(0.10f, 0.09f, 0.08f)
                    : new Color(0.98f, 0.96f, 0.92f);
                KitChrome.DrawText(this, _genre, font,
                                   new Vector2(knob.Position.X + (knob.Size.X - m.X) * 0.5f,
                                               knob.Position.Y + (knob.Size.Y + m.Y * 0.6f) * 0.5f),
                                   mark, mf, text);
            }
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
