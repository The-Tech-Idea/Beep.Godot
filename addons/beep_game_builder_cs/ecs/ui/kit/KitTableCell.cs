using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitTableCell : KitControl
    {
        [Export]
        public string CellText
        {
            get => _text;
            set { _text = value ?? ""; QueueRedraw(); }
        }

        [Export] public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Left;
        [Export] public UiSurface.TextRole Role { get; set; } = UiSurface.TextRole.Caption;

        private string _text = "";

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            if (Size.X <= 2f || Size.Y <= 2f || string.IsNullOrEmpty(_text)) return;
            var font = KitFont();
            if (font == null) return;

            string draw = KitCase(_text);
            float pad = Mathf.Max(4f, UiSurface.FontSize(this) * 0.45f);
            var box = new Rect2(pad, 0, Size.X - pad * 2f, Size.Y);
            int fs = UiSurface.FitRole(this, Role, box.Size, draw, font);
            Vector2 m = font.GetStringSize(draw, Align, -1, fs);
            float x = Align switch
            {
                HorizontalAlignment.Right => box.Position.X + box.Size.X - m.X,
                HorizontalAlignment.Center => box.Position.X + (box.Size.X - m.X) * 0.5f,
                _ => box.Position.X,
            };
            DrawText(font, new Vector2(x, (Size.Y + m.Y * 0.62f) * 0.5f), draw, fs, UiSurface.Text(this));
        }
    }
}
