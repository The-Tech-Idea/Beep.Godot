using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Interactable component. Blind — attach to any Area2D to make it interactive.
    /// Works for doors, switches, NPCs, chests, terminals, levers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InteractableComponent : EntityComponent
    {
        [Export] public string PromptText { get; set; } = "Press E to interact";
        [Export] public string InputAction { get; set; } = "interact";
        [Export] public bool Toggleable { get; set; } = false;
        [Export] public bool IsToggled { get; set; } = false;

        [Signal] public delegate void InteractedEventHandler();
        [Signal] public delegate void ToggledEventHandler(bool state);
        [Signal] public delegate void PlayerEnteredRangeEventHandler();
        [Signal] public delegate void PlayerExitedRangeEventHandler();

        private bool _playerInRange;

        public override void _Ready()
        {
            base._Ready();
            var area = GetParent<Area2D>();
            if (area != null)
            {
                area.BodyEntered += n => { if (n.IsInGroup("players")) { _playerInRange = true; EmitSignal(SignalName.PlayerEnteredRange); } };
                area.BodyExited += n => { if (n.IsInGroup("players")) { _playerInRange = false; EmitSignal(SignalName.PlayerExitedRange); } };
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsActive || !_playerInRange) return;
            if (@event.IsActionPressed(InputAction))
            {
                EmitSignal(SignalName.Interacted);
                if (Toggleable) { IsToggled = !IsToggled; EmitSignal(SignalName.Toggled, IsToggled); }
            }
        }
    }
}
