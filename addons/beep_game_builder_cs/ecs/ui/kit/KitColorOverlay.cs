using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitColorOverlay : Control
    {
        [Export]
        public Color Color
        {
            get => _color;
            set { _color = value; QueueRedraw(); }
        }

        private Color _color = new(0, 0, 0, 0);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            if (Size.X <= 0f || Size.Y <= 0f || _color.A <= 0f) return;
            DrawRect(new Rect2(Vector2.Zero, Size), _color);
        }
    }
}
