using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Fixed-square world/level selector button with lock and star state.
    /// Based on the repeated level-node buttons in Example_Art/gameui4.png and mobile UI sheets.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLevelButton : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Button;

        [Export] public string LevelText { get => _levelText; set { _levelText = value ?? ""; QueueRedraw(); } }
        private string _levelText = "1";

        [Export(PropertyHint.Range, "0,3,1")] public int Stars { get => _stars; set { _stars = Mathf.Clamp(value, 0, 3); QueueRedraw(); } }
        private int _stars = 3;

        [Export] public bool Locked { get => _locked; set { _locked = value; SetState(value ? KitState.Locked : KitState.Normal); QueueRedraw(); } }
        private bool _locked;

        [Export] public UiSurface.Role Accent { get => _accent; set { _accent = value; QueueRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Warning;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += () => { if (!_locked) SetState(KitState.Hover); };
            MouseExited += () => { if (!_locked) SetState(KitState.Normal); };
            if (CustomMinimumSize == Vector2.Zero)
            {
                float s = Mathf.Clamp(UiSurface.FontSize(this) * 3.65f, 46f, 68f);
                CustomMinimumSize = new Vector2(s, s);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_locked) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.Pressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 body = new(0, 0, Size.X, Size.Y * 0.82f);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : FaceColor();
            Color accent = _locked ? Desaturate(UiSurface.Semantic(this, _accent), 0.90f) : UiSurface.Semantic(this, _accent);

            DrawShape(body, ActiveShape, face, RimColor(), rim);
            Rect2 inner = body.Grow(-Mathf.Clamp(Size.X * 0.12f, 5f, 10f));
            DrawShape(inner, ActiveShape, accent, InkColor(), Mathf.Max(1f, rim * 0.45f));

            Font? font = KitFont();
            if (font != null)
            {
                string text = _locked ? "LOCK" : _levelText;
                int tf = UiSurface.FitRole(this, _locked ? UiSurface.TextRole.Small : UiSurface.TextRole.Value,
                                           inner.Size * 0.58f, text, font, min: 8);
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                Color ink = UiSurface.Luminance(accent) > 0.52f ? new Color(0.10f, 0.08f, 0.06f) : new Color(0.98f, 0.96f, 0.92f);
                DrawText(font, inner.Position + new Vector2((inner.Size.X - m.X) * 0.5f, (inner.Size.Y + m.Y * 0.62f) * 0.5f), text, tf, ink);

                string stars = new string('*', _locked ? 0 : _stars);
                if (!string.IsNullOrEmpty(stars))
                {
                    Rect2 starBox = new(0, Size.Y * 0.72f, Size.X, Size.Y * 0.28f);
                    int sf = UiSurface.FitRole(this, UiSurface.TextRole.Small, starBox.Size * 0.82f, stars, font, min: 7);
                    Vector2 sm = font.GetStringSize(stars, HorizontalAlignment.Left, -1, sf);
                    DrawText(font, starBox.Position + new Vector2((starBox.Size.X - sm.X) * 0.5f, (starBox.Size.Y + sm.Y * 0.58f) * 0.5f), stars, sf, UiSurface.Semantic(this, UiSurface.Role.Warning));
                }
            }
        }

        private static Color Desaturate(Color c, float amount)
        {
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, amount), Mathf.Lerp(c.G, l, amount), Mathf.Lerp(c.B, l, amount), c.A);
        }
    }
}
