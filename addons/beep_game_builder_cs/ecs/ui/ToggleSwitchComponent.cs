using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Animated toggle switch. Attach to a CheckBox or Button.
    /// Creates a sliding toggle with on/off states.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ToggleSwitchComponent : UIComponent
    {
        [Export] public bool IsOn { get; set; } = false;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color OnColor => UiSurface.Semantic(this, UiSurface.Role.Success);
        public Color OffColor => UiSurface.Ink(UiSurface.Of(this));
        public Color KnobColor => UiSurface.Text(this);
        [Export] public float AnimationDuration { get; set; } = 0.2f;
        [Export] public Vector2 SwitchSize { get; set; } = new(52, 28);

        [Signal] public delegate void ToggledEventHandler(bool isOn);

        private Button? _checkbox;   // Button, not CheckBox — covers both (CheckBox : Button); both have Text + Toggled
        private KitSwitchVisual? _visual;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            _checkbox = GetParent() as Button;
            if (_checkbox == null)
            {
                GD.PushWarning($"[{Name}] parent is not a Button/CheckBox — the toggle switch cannot build. Parent it to one.");
                return;
            }
            // Hide the default button chrome, build ours. Force ToggleMode so a plain Button parent
            // (which defaults off) actually emits Toggled like a CheckBox does.
            _checkbox.Text = "";
            _checkbox.ToggleMode = true;
            _checkbox.AddThemeConstantOverride("icon_separation", 0);
            BuildSwitch();
            _checkbox.Toggled += OnCheckboxToggled;
            // Seed the initial visual state WITHOUT emitting — otherwise a listener connected right
            // after construction sees a spurious Toggled(false) before any user interaction.
            SetState(_checkbox.ButtonPressed, emit: false);
        }

        private void OnCheckboxToggled(bool on) => SetState(on);

        private void BuildSwitch()
        {
            if (Engine.IsEditorHint()) return;
            _visual = new KitSwitchVisual
            {
                Size = SwitchSize,
                CustomMinimumSize = SwitchSize,
                IsOn = IsOn,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _checkbox?.AddChild(_visual);
        }

        public void SetState(bool on, bool emit = true)
        {
            if (!IsActive) return;
            IsOn = on;
            _tween?.Kill();

            if (_visual != null) _visual.IsOn = on;
            if (emit) EmitSignal(SignalName.Toggled, on);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == Godot.Control.NotificationThemeChanged && _visual != null) _visual.QueueRedraw();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            if (_checkbox != null && GodotObject.IsInstanceValid(_checkbox))
                _checkbox.Toggled -= OnCheckboxToggled;
            if (_visual != null && GodotObject.IsInstanceValid(_visual)) _visual.QueueFree();
            _visual = null;
        }
    }
}
