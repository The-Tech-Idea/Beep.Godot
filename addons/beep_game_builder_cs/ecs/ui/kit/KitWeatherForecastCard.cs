using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitWeatherForecastCard : KitControl
    {
        [Export] public string DayText { get; set; } = "";
        [Export] public string WeatherGlyph { get; set; } = "";
        [Export] public string TemperatureText { get; set; } = "";
        [Export] public string WindText { get; set; } = "";
        [Export] public UiSurface.Role WeatherRole { get; set; } = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
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
            Vector2 m = font.GetStringSize(draw, HorizontalAlignment.Left, -1, size);
            DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                       r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                     draw, size, ink);
        }
    }
}
