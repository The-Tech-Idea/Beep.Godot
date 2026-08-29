using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitColorOverlay : Control
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export]
        public Color Color
        {
            get => _color;
            set
            {
                if (_color == value) return;
                _color = value;
                RefreshVisualAndRedraw();
            }
        }

        private Color _color = new(0, 0, 0, 0);

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _Ready()
        {
            base._Ready();
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, MouseFilterEnum.Ignore);
        }

        public override void _Draw()
        {
            if (Size.X <= 0f || Size.Y <= 0f || _color.A <= 0f) return;
            DrawRect(new Rect2(Vector2.Zero, Size), _color);
        }
    }
}
