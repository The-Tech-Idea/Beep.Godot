using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Chip/tag component. Attach to a Container to create styled tag chips with remove button.
    /// Blind — works for filters, categories, player positions, selected items.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ChipComponent : UIComponent
    {
        [Export] public string Label { get; set; } = "Tag";
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color ChipColor => UiSurface.Semantic(this, UiSurface.Role.Accent);
        [Export] public bool Removable { get; set; } = true;
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float FontScale { get; set; } = 0.76f;
        private int FontSize => UiSurface.FontSize(this, FontScale);
        [Export] public NodePath ChipPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void RemovedEventHandler(string label);
        [Signal] public delegate void ClickedEventHandler(string label);

        private Container? _container;
        private KitRemovableChip? _chip;
        private bool _createdChip;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null && GenerateControlsWhenPathsEmpty)
            {
                GD.PushWarning($"[{Name}] ChipComponent needs a Container parent to hold chips; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to an HFlowContainer.");
                return;
            }
            if (!Engine.IsEditorHint() || BuildInEditor)
                SetupChip();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindChip() == null)
                return new[] { "Add an authored KitRemovableChip named Chip, set ChipPath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void SetupChip()
        {
            if (BindExistingChip())
            {
                StyleChip();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedChip();
        }

        private void BuildGeneratedChip()
        {
            if (_container == null)
                return;

            _createdChip = true;
            _chip = new KitRemovableChip
            {
                Name = "Chip",
                ChipText = Label,
                Removable = Removable,
                Role = UiSurface.Role.Accent
            };
            StyleChip();
            _container.AddChild(_chip);
            SetEditedOwner(_chip);
        }

        private bool BindExistingChip()
        {
            _createdChip = false;
            _chip = FindChip();
            return _chip != null;
        }

        public bool UsesSceneControls()
            => FindChip() != null;

        private KitRemovableChip? FindChip()
        {
            if (!ChipPath.IsEmpty && GetNodeOrNull<KitRemovableChip>(ChipPath) is { } pathChip)
                return pathChip;

            if (FindChild("Chip", recursive: true, owned: false) is KitRemovableChip childChip)
                return childChip;

            return GetParent()?.FindChild("Chip", recursive: true, owned: false) as KitRemovableChip;
        }

        private void StyleChip()
        {
            if (_chip == null) return;
            _chip.ChipText = Label;
            _chip.Removable = Removable;
            _chip.Role = UiSurface.Role.Accent;
            int bodyFs = UiSurface.FontSize(this);
            _chip.CustomMinimumSize = new Vector2(0, bodyFs * 2.0f);
            if (!_chip.IsConnected(KitRemovableChip.SignalName.RemovePressed, Callable.From(OnRemovePressed)))
                _chip.RemovePressed += OnRemovePressed;

            // Focusable so a keyboard/gamepad player can select and activate it (ui_accept),
            // not just click it.
            _chip.FocusMode = Godot.Control.FocusModeEnum.All;
            if (!_chip.IsConnected(Godot.Control.SignalName.GuiInput, Callable.From<InputEvent>(OnChipGuiInput)))
                _chip.GuiInput += OnChipGuiInput;
        }

        private void OnChipGuiInput(InputEvent e)
        {
            if (_chip == null) return;
            if ((e is InputEventMouseButton mb && mb.Pressed) || e.IsActionPressed("ui_accept"))
            {
                EmitSignal(SignalName.Clicked, Label);
                _chip.AcceptEvent();
            }
        }

        private void OnRemovePressed()
        {
            EmitSignal(SignalName.Removed, Label);
            _chip?.QueueFree();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_chip != null && GodotObject.IsInstanceValid(_chip))
            {
                if (_chip.IsConnected(KitRemovableChip.SignalName.RemovePressed, Callable.From(OnRemovePressed)))
                    _chip.RemovePressed -= OnRemovePressed;
                if (_chip.IsConnected(Godot.Control.SignalName.GuiInput, Callable.From<InputEvent>(OnChipGuiInput)))
                    _chip.GuiInput -= OnChipGuiInput;
                if (_createdChip)
                    _chip.QueueFree();
            }
            _chip = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
