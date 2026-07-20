using Godot;

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
        [Export] public Color OnColor { get; set; } = new(0.3f, 0.7f, 0.3f, 1f);
        [Export] public Color OffColor { get; set; } = new(0.3f, 0.3f, 0.3f, 1f);
        [Export] public Color KnobColor { get; set; } = Colors.White;
        [Export] public float AnimationDuration { get; set; } = 0.2f;
        [Export] public Vector2 SwitchSize { get; set; } = new(52, 28);

        [Signal] public delegate void ToggledEventHandler(bool isOn);

        private Button? _checkbox;   // Button, not CheckBox — covers both (CheckBox : Button); both have Text + Toggled
        private ColorRect? _bg;
        private ColorRect? _knob;
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
            _bg = new ColorRect { Size = SwitchSize, Color = IsOn ? OnColor : OffColor };
            _bg.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;

            _knob = new ColorRect { Size = new Vector2(SwitchSize.Y - 6, SwitchSize.Y - 6), Color = KnobColor };
            _knob.Position = new Vector2(3, 3);

            _bg.AddChild(_knob);
            _checkbox?.AddChild(_bg);
        }

        public void SetState(bool on, bool emit = true)
        {
            if (!IsActive) return;
            IsOn = on;
            _tween?.Kill();

            float targetX = on ? SwitchSize.X - SwitchSize.Y + 3 : 3;
            var targetBg = on ? OnColor : OffColor;

            _tween = _knob?.CreateTween().SetParallel(true);
            if (_tween != null && _knob != null && _bg != null)
            {
                _tween.TweenProperty(_knob, "position:x", targetX, AnimationDuration)
                    .SetEase(Tween.EaseType.Out);
                _tween.TweenProperty(_bg, "color", targetBg, AnimationDuration);
            }
            if (emit) EmitSignal(SignalName.Toggled, on);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            if (_checkbox != null && GodotObject.IsInstanceValid(_checkbox))
                _checkbox.Toggled -= OnCheckboxToggled;
            // _bg is AddChild'd to the parent Button — free it or the toggle visual is orphaned.
            if (_bg != null && GodotObject.IsInstanceValid(_bg)) _bg.QueueFree();
            _bg = null;
        }
    }
}
