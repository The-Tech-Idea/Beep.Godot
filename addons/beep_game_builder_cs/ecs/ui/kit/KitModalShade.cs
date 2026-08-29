using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitModalShade : Godot.Control
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export] public Color OverlayColor { get => _overlayColor; set { if (_overlayColor == value) return; _overlayColor = value; RefreshVisualAndRedraw(); } }
        private Color _overlayColor = new(0, 0, 0, 0.55f);
        [Signal] public delegate void ShadePressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, MouseFilterEnum.Stop, FocusModeEnum.All);
            SetAnchorsPreset(LayoutPreset.FullRect);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
                RefreshVisualAndRedraw();
            if (what == NotificationVisibilityChanged && Visible && IsInsideTree())
                GrabFocus();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key && KitChrome.IsCancelKey(key))
            {
                EmitSignal(SignalName.ShadePressed);
                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                GrabFocus();
                EmitSignal(SignalName.ShadePressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(r, OverlayColor);

            Color rim = UiSurface.SemanticOrDerived(this, UiSurface.Role.Accent) with { A = 0.18f };
            float step = Mathf.Max(24f, UiSurface.FontSize(this) * 3f);
            for (float x = -Size.Y; x < Size.X; x += step)
                DrawLine(new Vector2(x, 0), new Vector2(x + Size.Y, Size.Y), rim, 1f);
        }
    }
}
