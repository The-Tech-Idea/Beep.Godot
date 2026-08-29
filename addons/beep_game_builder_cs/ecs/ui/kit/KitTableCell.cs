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
            set { string next = value ?? ""; if (_text == next) return; _text = next; RefreshMinimumAndRedraw(); }
        }

        [Export]
        public HorizontalAlignment Align
        {
            get => _align;
            set { if (_align == value) return; _align = value; RefreshVisualAndRedraw(); }
        }
        private HorizontalAlignment _align = HorizontalAlignment.Left;

        [Export]
        public UiSurface.TextRole Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RefreshMinimumAndRedraw(); }
        }
        private UiSurface.TextRole _role = UiSurface.TextRole.Caption;

        private string _text = "";

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this, Role);
            float pad = Mathf.Max(4f, fs * 0.45f);
            float width = TextWidth(_text, Role) + pad * 2f;
            return new Vector2(Mathf.Max(fs * 4f, width), Mathf.Max(18f, fs * 1.55f));
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
            if (Size.X <= 2f || Size.Y <= 2f || string.IsNullOrEmpty(_text)) return;
            var font = KitFont();
            if (font == null) return;

            string draw = KitCase(_text);
            float pad = Mathf.Max(4f, UiSurface.FontSize(this) * 0.45f);
            var box = new Rect2(pad, 0, Size.X - pad * 2f, Size.Y);
            int fs = UiSurface.FitRole(this, Role, box.Size, draw, font);
            draw = KitChrome.EllipsizeText(font, draw, fs, box.Size.X);
            if (string.IsNullOrEmpty(draw)) return;
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
