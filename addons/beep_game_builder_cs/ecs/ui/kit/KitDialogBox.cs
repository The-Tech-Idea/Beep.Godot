using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitDialogBox : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export] public string Speaker { get => _speaker; set { _speaker = value ?? ""; QueueRedraw(); } }
        private string _speaker = "";

        [Export(PropertyHint.MultilineText)] public string Body { get => _body; set { _body = value ?? ""; QueueRedraw(); } }
        private string _body = "";

        [Export] public int VisibleCharacters { get => _visibleCharacters; set { _visibleCharacters = value; QueueRedraw(); } }
        private int _visibleCharacters = -1;

        [Export] public bool ContinueVisible { get => _continueVisible; set { _continueVisible = value; QueueRedraw(); } }
        private bool _continueVisible = true;

        public string[] Choices { get => _choices; set { _choices = value ?? System.Array.Empty<string>(); QueueRedraw(); } }
        private string[] _choices = System.Array.Empty<string>();

        [Export] public bool ChoicesVisible { get => _choicesVisible; set { _choicesVisible = value; QueueRedraw(); } }
        private bool _choicesVisible;

        [Signal] public delegate void ChoiceSelectedEventHandler(int index);

        private int _hoverChoice = -1;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 42f, fs * 11f);
            }
        }

        public void SetChoices(string[] choices)
        {
            Choices = choices;
            ChoicesVisible = choices.Length > 0;
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 42f, fs * 11f);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!ChoicesVisible) return;
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.Y != 0)
                {
                    MoveChoice(dir.Y);
                    AcceptEvent();
                    return;
                }
                if (KitChrome.IsConfirmKey(key) && _hoverChoice >= 0)
                {
                    EmitSignal(SignalName.ChoiceSelected, _hoverChoice);
                    AcceptEvent();
                    return;
                }
            }

            if (@event is InputEventMouseMotion mm)
            {
                int hit = HitChoice(mm.Position);
                if (_hoverChoice != hit) { _hoverChoice = hit; QueueRedraw(); }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = HitChoice(mb.Position);
                if (hit >= 0)
                {
                    GrabFocus();
                    EmitSignal(SignalName.ChoiceSelected, hit);
                    AcceptEvent();
                }
            }
        }

        private void MoveChoice(int delta)
        {
            if (!ChoicesVisible || Choices.Length == 0) return;
            int next = _hoverChoice < 0 ? 0 : _hoverChoice + delta;
            _hoverChoice = Mathf.Clamp(next, 0, Choices.Length - 1);
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X < 16f || Size.Y < 16f) return;

            var host = new Rect2(Vector2.Zero, Size);
            DrawMaterial(host, ActiveShape);
            if (!string.IsNullOrEmpty(Speaker)) DrawBanner(host, Speaker, KitShape.Ribbon, 0.18f, 0.42f, 0.72f);

            var font = KitFont();
            if (font == null) return;

            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(12f, fs * 1.1f);
            float top = string.IsNullOrEmpty(Speaker) ? pad : pad + fs * 0.8f;
            float choiceArea = ChoicesVisible ? Mathf.Min(Size.Y * 0.42f, Choices.Length * fs * 2.15f + pad) : 0f;
            var textBox = new Rect2(pad, top, Size.X - pad * 2f, Size.Y - top - pad - choiceArea);
            DrawBodyText(font, textBox);

            if (ChoicesVisible) DrawChoices(font, fs, pad);
            else if (ContinueVisible)
            {
                string mark = "v";
                int mfs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, new Vector2(fs * 2f, fs * 1.5f), mark, font);
                Vector2 m = font.GetStringSize(mark, HorizontalAlignment.Left, -1, mfs);
                DrawText(font, new Vector2(Size.X - pad - m.X, Size.Y - pad * 0.55f), mark, mfs,
                         UiSurface.Semantic(this, UiSurface.Role.Accent));
            }

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), host, ActiveShape, 0.8f);
        }

        private void DrawBodyText(Font font, Rect2 box)
        {
            string text = _visibleCharacters >= 0 && _visibleCharacters < _body.Length
                ? _body[.._visibleCharacters]
                : _body;
            if (string.IsNullOrEmpty(text)) return;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            KitChrome.DrawWrappedText(this, KitChrome.GenreOf(this), font, box, text, fs,
                                      UiSurface.Text(this));
        }

        private void DrawChoices(Font font, int fs, float pad)
        {
            float rowH = Mathf.Max(fs * 1.85f, 28f);
            float total = Choices.Length * rowH;
            float y = Size.Y - pad - total;
            for (int i = 0; i < Choices.Length; i++)
            {
                var r = new Rect2(pad, y + i * rowH, Size.X - pad * 2f, rowH - fs * 0.25f);
                Color fill = UiSurface.Semantic(this, i == _hoverChoice ? UiSurface.Role.Info : UiSurface.Role.Accent);
                DrawShape(r, ActiveShape, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.6f));
                string choice = KitCase(Choices[i]);
                int cfs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, r.Size - new Vector2(pad, 0), choice, font, min: 8);
                Vector2 m = font.GetStringSize(choice, HorizontalAlignment.Left, -1, cfs);
                DrawText(font, new Vector2(r.Position.X + pad * 0.65f, r.Position.Y + (r.Size.Y + m.Y * 0.60f) * 0.5f),
                         choice, cfs, UiSurface.Text(this));
            }
        }

        private int HitChoice(Vector2 p)
        {
            if (!ChoicesVisible || Choices.Length == 0) return -1;
            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(12f, fs * 1.1f);
            float rowH = Mathf.Max(fs * 1.85f, 28f);
            float y = Size.Y - pad - Choices.Length * rowH;
            int hit = Mathf.FloorToInt((p.Y - y) / rowH);
            return hit >= 0 && hit < Choices.Length && p.X >= pad && p.X <= Size.X - pad ? hit : -1;
        }
    }
}
