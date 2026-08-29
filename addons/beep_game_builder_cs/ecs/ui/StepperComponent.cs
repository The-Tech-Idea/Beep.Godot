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
        [Export] public NodePath MinusButtonPath { get; set; } = new("");
        [Export] public NodePath ValueDisplayPath { get; set; } = new("");
        [Export] public NodePath PlusButtonPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void ValueChangedEventHandler(int newValue);

        private Container? _container;
        private Button? _minusBtn;
        private Button? _plusBtn;
        private Control? _valueDisplay;
        private Control? _generatedRoot;

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildStepper));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set MinusButtonPath, ValueDisplayPath, and PlusButtonPath, add scene-authored MinusButton/ValueDisplay/PlusButton children, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        public void RebuildStepper()
        {
            DisconnectButtons();

            if (!BindExistingControls())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedStepper();
            }

            ConfigureControls();
            UpdateDisplay();
        }

        private void BuildGeneratedStepper()
        {
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();

            _generatedRoot = null;
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] StepperComponent needs a Container parent to generate steps; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to an HBoxContainer or bind authored controls by path.");
                return;
            }

            var row = new HBoxContainer { Name = "Stepper" };
            KitChrome.SetConstantOverrideIfChanged(row, "separation", 4);

            _minusBtn = new KitIconButton
            {
                Name = "MinusButton",
                Glyph = "-",
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter,
            };

            _valueDisplay = new KitLabelValue
            {
                Name = "ValueDisplay",
                Label = "",
                LabelValueRatio = 0.0f,
                Accent = UiSurface.Role.Neutral,
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter
            };

            _plusBtn = new KitIconButton
            {
                Name = "PlusButton",
                Glyph = "+",
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter
            };

            row.AddChild(_minusBtn);
            row.AddChild(_valueDisplay);
            row.AddChild(_plusBtn);
            _container.AddChild(row);
            _generatedRoot = row;
            SetEditedOwner(row);
            SetEditedOwner(_minusBtn);
            SetEditedOwner(_valueDisplay);
            SetEditedOwner(_plusBtn);
        }

        private bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            Button? minus = FindMinusButton();
            Control? value = FindValueDisplay();
            Button? plus = FindPlusButton();

            if (minus == null || value == null || plus == null)
                return false;

            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();

            _generatedRoot = null;
            _minusBtn = minus;
            _valueDisplay = value;
            _plusBtn = plus;
            return true;
        }

        public bool UsesSceneControls()
            => !MinusButtonPath.IsEmpty || !ValueDisplayPath.IsEmpty || !PlusButtonPath.IsEmpty
            || FindMinusButton() != null || FindValueDisplay() != null || FindPlusButton() != null;

        private bool HasAuthoredControls()
            => FindMinusButton() != null && FindValueDisplay() != null && FindPlusButton() != null;

        private Button? FindMinusButton()
        {
            if (!MinusButtonPath.IsEmpty && GetNodeOrNull<Button>(MinusButtonPath) is { } pathButton)
                return pathButton;

            if (FindChild("MinusButton", recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild("MinusButton", recursive: true, owned: false) as Button;
        }

        private Control? FindValueDisplay()
        {
            if (!ValueDisplayPath.IsEmpty && GetNodeOrNull<Control>(ValueDisplayPath) is { } pathDisplay)
                return pathDisplay;

            if (FindChild("ValueDisplay", recursive: true, owned: false) is Control childDisplay)
                return childDisplay;

            return GetParent()?.FindChild("ValueDisplay", recursive: true, owned: false) as Control;
        }

        private Button? FindPlusButton()
        {
            if (!PlusButtonPath.IsEmpty && GetNodeOrNull<Button>(PlusButtonPath) is { } pathButton)
                return pathButton;

            if (FindChild("PlusButton", recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild("PlusButton", recursive: true, owned: false) as Button;
        }

        private void ConfigureControls()
        {
            if (_minusBtn == null || _plusBtn == null || _valueDisplay == null)
                return;

            ConfigureButton(_minusBtn, "-", OnMinusPressed);
            ConfigureValueDisplay();
            ConfigureButton(_plusBtn, "+", OnPlusPressed);
        }

        private void ConfigureButton(Button button, string glyph, System.Action handler)
        {
            button.CustomMinimumSize = new Vector2(ButtonSize, ButtonSize);
            button.SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter;
            if (button is KitIconButton icon)
                icon.Glyph = glyph;
            else
                button.Text = glyph;

            button.Pressed += handler;
        }

        private void ConfigureValueDisplay()
        {
            _valueDisplay!.CustomMinimumSize = new Vector2(Mathf.Max(48, ButtonSize * 1.55f), ButtonSize);
            _valueDisplay.SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkCenter;
            if (_valueDisplay is KitLabelValue labelValue)
            {
                labelValue.Label = "";
                labelValue.LabelValueRatio = 0.0f;
                labelValue.Accent = UiSurface.Role.Neutral;
            }
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
            string text = Value.ToString(LabelFormat);
            if (_valueDisplay is KitLabelValue labelValue)
                labelValue.Value = text;
            else if (_valueDisplay is Label label)
                label.Text = text;
            else if (_valueDisplay is Button button)
                button.Text = text;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            DisconnectButtons();
        }

        private void DisconnectButtons()
        {
            if (_minusBtn != null)
                _minusBtn.Pressed -= OnMinusPressed;
            if (_plusBtn != null)
                _plusBtn.Pressed -= OnPlusPressed;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
