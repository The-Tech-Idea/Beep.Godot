using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitToast : KitControl
    {
        [Export] public string Message { get => _message; set { _message = value ?? ""; QueueRedraw(); } }
        [Export] public string IconGlyph { get => _icon; set { _icon = value ?? ""; QueueRedraw(); } }
        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Info;

        private string _message = "";
        private string _icon = "";

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            Color fill = UiSurface.Semantic(this, Role);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            var r = new Rect2(Vector2.Zero, Size);
            DrawShape(r, ActiveShape, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim));

            var font = KitFont();
            if (font == null) return;
            string text = string.IsNullOrEmpty(_icon) ? _message : $"{_icon}  {_message}";
            text = KitCase(text);
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, r.Size * 0.82f, text, font);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Color ink = UiSurface.Luminance(fill) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : new Color(0.98f, 0.96f, 0.92f);
            DrawText(font, new Vector2((Size.X - m.X) * 0.5f, (Size.Y + m.Y * 0.62f) * 0.5f), text, fs, ink);
        }
    }
}
