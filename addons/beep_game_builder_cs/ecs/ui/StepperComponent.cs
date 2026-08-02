using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Number stepper component. Attach to a Container with [-][value][+] layout.
    /// Creates +/- buttons with a value label between them.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StepperComponent : UIComponent
    {
        [Export] public int Value { get; set; } = 0;
        [Export] public int MinValue { get; set; } = 0;
        [Export] public int MaxValue { get; set; } = 99;
        [Export] public int Step { get; set; } = 1;
        [Export] public string LabelFormat { get; set; } = "D2";
        [Export] public int ButtonSize { get; set; } = 36;

        [Signal] public delegate void ValueChangedEventHandler(int newValue);

        private Container? _container;
        private Button? _minusBtn;
        private Button? _plusBtn;
        private KitLabelValue? _valueLabel;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] StepperComponent needs a Container parent to build steps; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to an HBoxContainer.");
                return;
            }
            BuildStepper();
            UpdateDisplay();
        }

        private void BuildStepper()
        {
            if (Engine.IsEditorHint()) return;
            _minusBtn = new KitIconButton
            {
                Glyph = "-",
                CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter
            };
            _minusBtn.Pressed += OnMinusPressed;

            _valueLabel = new KitLabelValue
            {
                Label = "",
                Value = Value.ToString(LabelFormat),
                LabelValueRatio = 0.0f,
                Accent = UiSurface.Role.Neutral,
                CustomMinimumSize = new Vector2(Mathf.Max(48, ButtonSize * 1.55f), ButtonSize),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter
            };

            _plusBtn = new KitIconButton
            {
                Glyph = "+",
                CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter
            };
            _plusBtn.Pressed += OnPlusPressed;

            _container?.AddChild(_minusBtn);
            _container?.AddChild(_valueLabel);
            _container?.AddChild(_plusBtn);
        }

        private void OnMinusPressed() => SetValue(Value - Step);
        private void OnPlusPressed() => SetValue(Value + Step);

        public void SetValue(int value)
        {
            Value = Mathf.Clamp(value, MinValue, MaxValue);
            UpdateDisplay();
            EmitSignal(SignalName.ValueChanged, Value);
        }

        private void UpdateDisplay()
        {
            if (_valueLabel != null) _valueLabel.Value = Value.ToString(LabelFormat);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_minusBtn != null)
                _minusBtn.Pressed -= OnMinusPressed;
            if (_plusBtn != null)
                _plusBtn.Pressed -= OnPlusPressed;
        }
    }
}
