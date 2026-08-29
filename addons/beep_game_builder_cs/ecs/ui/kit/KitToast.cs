using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitToast : KitControl
    {
        [Export(PropertyHint.MultilineText)] public string Message { get => _message; set { string next = value ?? ""; if (_message == next) return; _message = next; RefreshMinimumAndRedraw(); } }
        [Export] public string IconGlyph { get => _icon; set { string next = value ?? ""; if (_icon == next) return; _icon = next; RefreshMinimumAndRedraw(); } }
        [Export] public UiSurface.Role Role { get => _role; set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); } }

        private string _message = "";
        private string _icon = "";
        private UiSurface.Role _role = UiSurface.Role.Info;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            Font? font = KitFont();
            string text = KitCase(ToastText());
            float pad = Mathf.Max(6f, textFs * 0.65f);
            float textW = Mathf.Clamp(LongestLineWidth(font, text, textFs), fs * 10f, fs * 24f);
            int lines = EstimateWrappedLineCount(font, text, textFs, textW);
            float lineH = font?.GetHeight(textFs) * 1.08f ?? textFs * 1.25f;
            return new Vector2(Mathf.Max(fs * 18f, textW + pad * 2f),
                               Mathf.Max(fs * 3f, lineH * lines + pad * 2f));
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

        private string ToastText()
            => string.IsNullOrEmpty(_icon) ? _message : $"{_icon}  {_message}";

        private static float LongestLineWidth(Font? font, string text, int fs)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fs * 8f;

            float width = 0f;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                width = Mathf.Max(width, TextWidth(font, line, fs));
            return width;
        }

        private static int EstimateWrappedLineCount(Font? font, string text, int fs, float width)
        {
            if (string.IsNullOrWhiteSpace(text) || width <= 1f)
                return 1;

            int count = 0;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                count += Mathf.Max(1, Mathf.CeilToInt(TextWidth(font, line, fs) / width));
            return Mathf.Clamp(count, 1, 2);
        }

        private static float TextWidth(Font? font, string text, int fs)
            => font?.GetStringSize(text ?? "", HorizontalAlignment.Left, -1, fs).X ?? (text ?? "").Length * fs * 0.55f;

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            Color fill = UiSurface.Semantic(this, Role);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            var r = new Rect2(Vector2.Zero, Size);
            DrawShape(r, ActiveShape, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim));

            var font = KitFont();
            if (font == null) return;
            string text = KitCase(ToastText());
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, r.Size * 0.38f, text, font);
            Color ink = UiSurface.Luminance(fill) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawWrappedText(this, KitChrome.GenreOf(this), font,
                                      r.Grow(-Mathf.Max(6f, fs * 0.65f)), text, fs, ink,
                                      HorizontalAlignment.Center, maxLines: 2);
        }
    }
}
