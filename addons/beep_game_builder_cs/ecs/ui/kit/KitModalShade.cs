using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitModalShade : Godot.Control
    {
        [Export] public Color OverlayColor { get; set; } = new(0, 0, 0, 0.55f);
        [Signal] public delegate void ShadePressedEventHandler();

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true })
            {
                EmitSignal(SignalName.ShadePressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(r, OverlayColor);

            Color rim = UiSurface.Semantic(this, UiSurface.Role.Accent) with { A = 0.18f };
            float step = Mathf.Max(24f, UiSurface.FontSize(this) * 3f);
            for (float x = -Size.Y; x < Size.X; x += step)
                DrawLine(new Vector2(x, 0), new Vector2(x + Size.Y, Size.Y), rim, 1f);
        }
    }
}
