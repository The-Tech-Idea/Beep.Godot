using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitHudText : KitControl
    {
        [Export] public string Text { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        [Export] public UiSurface.TextRole Role { get; set; } = UiSurface.TextRole.Caption;
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;
        [Export] public bool ShowPlate { get; set; }
        [Export] public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Center;

        private string _text = "";

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            if (Size.X <= 2f || Size.Y <= 2f || string.IsNullOrEmpty(_text)) return;
            if (ShowPlate) DrawMaterial(new Rect2(Vector2.Zero, Size), ActiveShape);

            var font = KitFont();
            if (font == null) return;
            string draw = KitCase(_text);
            var box = new Rect2(UiSurface.FontSize(this) * 0.35f, 0,
                                Mathf.Max(1f, Size.X - UiSurface.FontSize(this) * 0.7f), Size.Y);
            int fs = UiSurface.FitRole(this, Role, box.Size, draw, font);
            Vector2 m = font.GetStringSize(draw, HorizontalAlignment.Left, -1, fs);
            float x = Align switch
            {
                HorizontalAlignment.Left => box.Position.X,
                HorizontalAlignment.Right => box.End.X - m.X,
                _ => box.Position.X + (box.Size.X - m.X) * 0.5f,
            };
            Color ink = Accent == UiSurface.Role.Neutral ? UiSurface.Text(this) : UiSurface.Semantic(this, Accent);
            DrawText(font, new Vector2(x, (Size.Y + m.Y * 0.62f) * 0.5f), draw, fs, ink);
        }
    }
}
