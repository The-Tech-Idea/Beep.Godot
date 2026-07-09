using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Number stepper component. Attach to a Container with [-][value][+] layout.
    /// Creates +/- buttons with a value label between them.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StepperComponent : EntityComponent
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
        private Label? _valueLabel;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent<Container>();
            if (_container == null) return;
            BuildStepper();
            UpdateDisplay();
        }

        private void BuildStepper()
        {
            _minusBtn = new Button { Text = "−", CustomMinimumSize = new Vector2(ButtonSize, ButtonSize), Flat = true };
            _minusBtn.Pressed += () => SetValue(Value - Step);

            _valueLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(48, ButtonSize)
            };

            _plusBtn = new Button { Text = "+", CustomMinimumSize = new Vector2(ButtonSize, ButtonSize), Flat = true };
            _plusBtn.Pressed += () => SetValue(Value + Step);

            _container?.AddChild(_minusBtn);
            _container?.AddChild(_valueLabel);
            _container?.AddChild(_plusBtn);
        }

        public void SetValue(int value)
        {
            Value = Mathf.Clamp(value, MinValue, MaxValue);
            UpdateDisplay();
            EmitSignal(SignalName.ValueChanged, Value);
        }

        private void UpdateDisplay()
        {
            if (_valueLabel != null) _valueLabel.Text = Value.ToString(LabelFormat);
        }
    }
}
