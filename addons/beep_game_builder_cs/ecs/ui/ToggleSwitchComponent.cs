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
        [Export] public NodePath VisualPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void ToggledEventHandler(bool isOn);

        private Button? _checkbox;   // Button, not CheckBox — covers both (CheckBox : Button); both have Text + Toggled
        private KitSwitchVisual? _visual;
        private bool _createdVisual;
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
            // Hide the default button chrome, bind/build ours. Force ToggleMode so a plain Button parent
            // (which defaults off) actually emits Toggled like a CheckBox does.
            _checkbox.Text = "";
            _checkbox.ToggleMode = true;
            KitChrome.SetConstantOverrideIfChanged(_checkbox, "icon_separation", 0);
            if (!Engine.IsEditorHint() || BuildInEditor)
                SetupSwitch();
            _checkbox.Toggled += OnCheckboxToggled;
            // Seed the initial visual state WITHOUT emitting — otherwise a listener connected right
            // after construction sees a spurious Toggled(false) before any user interaction.
            SetState(_checkbox.ButtonPressed, emit: false);
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindSwitchVisual() == null)
                return new[] { "Add an authored KitSwitchVisual named SwitchVisual, set VisualPath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void OnCheckboxToggled(bool on) => SetState(on);

        private void SetupSwitch()
        {
            if (BindExistingSwitch())
            {
                StyleSwitch();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedSwitch();
        }

        private void BuildGeneratedSwitch()
        {
            if (_checkbox == null)
                return;

            _createdVisual = true;
            _visual = new KitSwitchVisual
            {
                Name = "SwitchVisual",
                Size = SwitchSize,
                CustomMinimumSize = SwitchSize,
                IsOn = IsOn,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            StyleSwitch();
            _checkbox.AddChild(_visual);
            SetEditedOwner(_visual);
        }

        private bool BindExistingSwitch()
        {
            _createdVisual = false;
            _visual = FindSwitchVisual();
            return _visual != null;
        }

        public bool UsesSceneControls()
            => FindSwitchVisual() != null;

        private KitSwitchVisual? FindSwitchVisual()
        {
            if (!VisualPath.IsEmpty && GetNodeOrNull<KitSwitchVisual>(VisualPath) is { } pathVisual)
                return pathVisual;

            if (FindChild("SwitchVisual", recursive: true, owned: false) is KitSwitchVisual childVisual)
                return childVisual;

            return GetParent()?.FindChild("SwitchVisual", recursive: true, owned: false) as KitSwitchVisual;
        }

        private void StyleSwitch()
        {
            if (_visual == null) return;
            _visual.Size = SwitchSize;
            _visual.CustomMinimumSize = SwitchSize;
            _visual.IsOn = IsOn;
            _visual.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
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
            if (_createdVisual && _visual != null && GodotObject.IsInstanceValid(_visual)) _visual.QueueFree();
            _visual = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
