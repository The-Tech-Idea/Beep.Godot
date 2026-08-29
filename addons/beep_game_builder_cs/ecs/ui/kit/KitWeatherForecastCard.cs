using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitWeatherForecastCard : KitControl
    {
        [Export] public string DayText { get => _dayText; set { string next = value ?? ""; if (_dayText == next) return; _dayText = next; RefreshMinimumAndRedraw(); } }
        [Export] public string WeatherGlyph { get => _weatherGlyph; set { string next = value ?? ""; if (_weatherGlyph == next) return; _weatherGlyph = next; RefreshMinimumAndRedraw(); } }
        [Export] public string TemperatureText { get => _temperatureText; set { string next = value ?? ""; if (_temperatureText == next) return; _temperatureText = next; RefreshMinimumAndRedraw(); } }
        [Export] public string WindText { get => _windText; set { string next = value ?? ""; if (_windText == next) return; _windText = next; RefreshMinimumAndRedraw(); } }
        [Export] public UiSurface.Role WeatherRole { get => _weatherRole; set { if (_weatherRole == value) return; _weatherRole = value; RefreshVisualAndRedraw(); } }

        private string _dayText = "";
        private string _weatherGlyph = "";
        private string _temperatureText = "";
        private string _windText = "";
        private UiSurface.Role _weatherRole = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float width = Mathf.Max(fs * 5.8f, TextWidth(_weatherGlyph, UiSurface.TextRole.Subtitle) + fs * 1.4f);
            width = Mathf.Max(width, TextWidth(_temperatureText, UiSurface.TextRole.Caption) + fs * 1.4f);
            width = Mathf.Max(width, TextWidth(_dayText, UiSurface.TextRole.Small) + fs * 1.4f);
            width = Mathf.Max(width, TextWidth(_windText, UiSurface.TextRole.Small) + fs * 1.4f);
            return new Vector2(width, Mathf.Max(76f, fs * 6.1f));
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

        private float TextWidth(string text, UiSurface.TextRole role)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            Font? font = KitFont();
            int fs = UiSurface.FontSize(this, role);
            string draw = KitCase(text);
            return font?.GetStringSize(draw, HorizontalAlignment.Left, -1, fs).X ?? draw.Length * fs * 0.56f;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            var body = new Rect2(Vector2.Zero, Size);
            DrawMaterial(body, ActiveShape);

            Color accent = UiSurface.Semantic(this, WeatherRole);
            if (accent.A < 0.02f) accent = UiSurface.Semantic(this, UiSurface.Role.Neutral);
            DrawRect(new Rect2(Geo.FramePx(Size.Y), Geo.FramePx(Size.Y), Size.X - Geo.FramePx(Size.Y) * 2f, Mathf.Max(2f, UiSurface.FontSize(this) * 0.22f)), accent);

            var font = KitFont();
            if (font == null) return;
            Color ink = UiSurface.Text(this);
            float fs = UiSurface.FontSize(this);
            DrawCentered(font, DayText, new Rect2(0, fs * 0.55f, Size.X, fs * 1.15f), UiSurface.TextRole.Small, ink);
            DrawCentered(font, WeatherGlyph, new Rect2(0, fs * 1.65f, Size.X, fs * 2.0f), UiSurface.TextRole.Subtitle, ink);
            DrawCentered(font, TemperatureText, new Rect2(0, fs * 3.45f, Size.X, fs * 1.2f), UiSurface.TextRole.Caption, ink);
            DrawCentered(font, WindText, new Rect2(0, fs * 4.45f, Size.X, fs * 1.1f), UiSurface.TextRole.Small, ink with { A = 0.82f });
        }

        private void DrawCentered(Font font, string text, Rect2 r, UiSurface.TextRole role, Color ink)
        {
            if (string.IsNullOrEmpty(text)) return;
            string draw = KitCase(text);
            int size = UiSurface.FitRole(this, role, r.Size, draw, font);
            draw = KitChrome.EllipsizeText(font, draw, size, r.Size.X);
            if (string.IsNullOrEmpty(draw)) return;
            Vector2 m = font.GetStringSize(draw, HorizontalAlignment.Left, -1, size);
            DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                       r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                     draw, size, ink);
        }
    }
}
