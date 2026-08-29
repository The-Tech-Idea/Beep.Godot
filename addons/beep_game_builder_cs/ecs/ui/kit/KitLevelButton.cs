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

        [Export] public string LevelText { get => _levelText; set { SetText(ref _levelText, value); } }
        private string _levelText = "1";

        [Export(PropertyHint.Range, "0,3,1")] public int Stars { get => _stars; set { int next = Mathf.Clamp(value, 0, 3); if (_stars == next) return; _stars = next; RefreshContentAndRedraw(); } }
        private int _stars = 3;

        [Export] public bool Locked { get => _locked; set { if (_locked == value) return; _locked = value; SetState(value ? KitState.Locked : KitState.Normal); RefreshContentAndRedraw(); } }
        private bool _locked;

        [Export] public UiSurface.Role Accent { get => _accent; set { if (_accent == value) return; _accent = value; RefreshContentAndRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Warning;
        private bool _eventsHooked;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseEntered += () => { if (!_locked) SetState(KitState.Hover); };
                MouseExited += () => { if (!_locked) SetState(KitState.Normal); };
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearPointerState();
        }

        private void SetText(ref string target, string? value)
        {
            string next = value ?? "";
            if (target == next) return;
            target = next;
            RefreshContentAndRedraw();
        }

        private void RefreshContentAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void ClearPointerState()
        {
            if (_locked || State == KitState.Normal) return;
            SetState(KitState.Normal);
        }

        public override void _GuiInput(InputEvent @event)
        {
            KitChrome.ActivateOnClickOrConfirm(this, @event,
                () => EmitSignal(SignalName.Pressed),
                interactive: !_locked);
        }

        public override Vector2 _GetMinimumSize()
        {
            float s = Mathf.Clamp(UiSurface.FontSize(this) * 3.65f, 46f, 68f);
            return new Vector2(s, s);
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 body = new(0, 0, Size.X, Size.Y * 0.82f);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : UiSurface.Semantic(this, _accent);
            if (face.A < 0.02f) face = FaceColor();

            DrawShape(body, ActiveShape, face, RimColor(), rim);
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), body, ActiveShape, 0.8f);

            Font? font = KitFont();
            if (font != null)
            {
                string text = KitCase(_locked ? "LOCK" : _levelText);
                Rect2 textBox = body.Grow(-Mathf.Clamp(Size.X * 0.16f, 6f, 12f));
                float textWidth = textBox.Size.X * 0.66f;
                int tf = UiSurface.FitRole(this, _locked ? UiSurface.TextRole.Small : UiSurface.TextRole.Value,
                                           new Vector2(textWidth, textBox.Size.Y * 0.62f), text, font, min: 8);
                text = KitChrome.EllipsizeText(font, text, tf, textWidth);
                if (string.IsNullOrEmpty(text)) return;
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                Color ink = UiSurface.Luminance(face) > 0.52f ? new Color(0.10f, 0.08f, 0.06f) : new Color(0.98f, 0.96f, 0.92f);
                DrawText(font, textBox.Position + new Vector2((textBox.Size.X - m.X) * 0.5f, (textBox.Size.Y + m.Y * 0.62f) * 0.5f), text, tf, ink);

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
