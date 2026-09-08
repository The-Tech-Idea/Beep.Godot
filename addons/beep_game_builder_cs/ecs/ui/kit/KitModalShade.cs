using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// The dim behind a modal: a full-screen plate that darkens the game and swallows pointer
    /// input, so a click meant for the dialog cannot land on the scene behind it.
    ///
    /// Separate from the dialog on purpose. The shade belongs to the screen, the dialog belongs to
    /// whoever opened it, and a dialog that painted its own backdrop could not cover a sibling
    /// drawn after it. <see cref="ModalComponent"/> owns the pair.
    ///
    /// Emits <c>ShadePressed</c> on a click or <c>ui_cancel</c> rather than closing itself — the
    /// decision of whether a modal is dismissible is the caller's, not the backdrop's.
    /// </summary>
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
            // Only ask for focus this control can actually take. The visibility notification can
            // arrive before _Ready has applied the input defaults that make it focusable, and can
            // arrive at all when a scene has set AutoInputDefaults = false to keep its own focus
            // policy — in both cases Godot answers with "This control can't grab focus", which is
            // a warning the kit was generating against itself on every modal that appeared.
            if (what == NotificationVisibilityChanged && Visible && IsInsideTree()
                && FocusMode != FocusModeEnum.None)
                GrabFocus();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (KitChrome.IsCancel(@event))
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
