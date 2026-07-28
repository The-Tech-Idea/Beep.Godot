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
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitToggle : KitControl
    {
        public enum ToggleStyle { Switch, Box }

        [Export] public ToggleStyle Style { get; set; } = ToggleStyle.Switch;

        [Export] public bool Pressed
        {
            get => _on;
            set { if (_on == value) return; _on = value; QueueRedraw(); EmitSignal(SignalName.Toggled, value); }
        }
        private bool _on = true;

        [Export] public UiSurface.Role OnRole { get; set; } = UiSurface.Role.Success;

        [Signal] public delegate void ToggledEventHandler(bool pressed);

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = Style == ToggleStyle.Box
                    ? new Vector2(fs * 1.7f, fs * 1.7f)
                    : new Vector2(fs * 3.4f, fs * 1.7f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Pressed = !Pressed;
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 6f) return;

            Color face = FaceColor();
            Color ink = InkColor();
            Color on = UiSurface.Semantic(this, OnRole);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, Geo.Rim * 0.7f * (fs / 14f));
            var r = new Rect2(Vector2.Zero, Size);

            if (Style == ToggleStyle.Box)
            {
                DrawShape(r, ActiveShape, _on ? on : new Color(face.R * 0.55f, face.G * 0.55f, face.B * 0.6f, 1f),
                          ink, rimPx);
                if (_on) DrawTick(r, UiSurface.Luminance(on) > 0.5f
                                        ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                return;
            }

            // Track keeps its hue whether on or off — off is a position, not a disabled state.
            Color track = _on
                ? new Color(on.R * 0.55f, on.G * 0.55f, on.B * 0.58f, 1f)
                : new Color(face.R * 0.42f, face.G * 0.42f, face.B * 0.46f, 1f);
            DrawShape(r, KitShape.Pill, track, ink, rimPx);

            float kw = Size.X * 0.46f;
            var knob = new Rect2(_on ? Size.X - kw : 0f, 0f, kw, Size.Y);
            DrawShape(knob, KitShape.Pill, _on ? on : new Color(face.R * 0.85f, face.G * 0.85f, face.B * 0.9f, 1f),
                      ink, rimPx);
        }

        private void DrawTick(Rect2 r, Color col)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.24f;
            float w = Mathf.Max(2f, a * 0.45f);
            DrawLine(c + new Vector2(-a, 0f), c + new Vector2(-a * 0.25f, a * 0.8f), col, w);
            DrawLine(c + new Vector2(-a * 0.25f, a * 0.8f), c + new Vector2(a, -a * 0.75f), col, w);
        }
    }
}
