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

        [Signal] public delegate void RemovedEventHandler(string label);
        [Signal] public delegate void ClickedEventHandler(string label);

        private Container? _container;
        private KitRemovableChip? _chip;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] ChipComponent needs a Container parent to hold chips; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to an HFlowContainer.");
                return;
            }
            BuildChip();
        }

        private void BuildChip()
        {
            if (Engine.IsEditorHint()) return;
            _chip = new KitRemovableChip
            {
                ChipText = Label,
                Removable = Removable,
                Role = UiSurface.Role.Accent
            };
            int bodyFs = UiSurface.FontSize(this);
            _chip.CustomMinimumSize = new Vector2(0, bodyFs * 2.0f);
            _chip.RemovePressed += OnRemovePressed;
            _container?.AddChild(_chip);

            // Focusable so a keyboard/gamepad player can select and activate it (ui_accept),
            // not just click it.
            _chip.FocusMode = Godot.Control.FocusModeEnum.All;
            _chip.GuiInput += e =>
            {
                if ((e is InputEventMouseButton mb && mb.Pressed) || e.IsActionPressed("ui_accept"))
                {
                    EmitSignal(SignalName.Clicked, Label);
                    _chip?.AcceptEvent();
                }
            };
        }

        private void OnRemovePressed()
        {
            EmitSignal(SignalName.Removed, Label);
            _chip?.QueueFree();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // _chip is AddChild'd to the PARENT, so freeing this component doesn't take it along.
            // Its close-button and GuiInput lambdas capture `this` and EmitSignal — left behind
            // while the parent survives, a later click fires on this freed component (use-after-free).
            if (_chip != null && GodotObject.IsInstanceValid(_chip)) _chip.QueueFree();
            _chip = null;
        }
    }
}
