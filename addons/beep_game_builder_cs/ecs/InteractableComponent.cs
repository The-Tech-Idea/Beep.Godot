using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Interactable component. Blind — attach to any Area2D to make it interactive.
    /// Works for doors, switches, NPCs, chests, terminals, levers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InteractableComponent : GameplayComponent
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
        private DialogComponent? _dialog;
        private Area2D? _area;

        public override void _Ready()
        {
            base._Ready();
            _area = GetParent() as Area2D;
            _dialog = GetSiblingComponent<DialogComponent>();

            if (_area != null)
            {
                _area.BodyEntered += OnBodyEntered;
                _area.BodyExited += OnBodyExited;
            }
        }

        /// <summary>Is this body the player? Accepts either the "players" group or a node
        /// named "Player" — nothing in the addon joins the group, and the generated scenes
        /// name the body "Player" (same convention DoorSwitchComponent uses), so relying on
        /// the group alone meant the prompt never fired.</summary>
        private static bool IsPlayer(Node n) => n.IsInGroup("players") || n.Name == "Player";

        private void OnBodyEntered(Node n)
        {
            if (IsPlayer(n))
            {
                _playerInRange = true;
                EmitSignal(SignalName.PlayerEnteredRange);
            }
        }

        private void OnBodyExited(Node n)
        {
            if (IsPlayer(n))
            {
                _playerInRange = false;
                EmitSignal(SignalName.PlayerExitedRange);
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsActive || !_playerInRange) return;
            if (@event.IsActionPressed(InputAction))
            {
                GetViewport().SetInputAsHandled();
                EmitSignal(SignalName.Interacted);

                if (_dialog != null)
                    _dialog.Interact();

                if (Toggleable)
                {
                    IsToggled = !IsToggled;
                    EmitSignal(SignalName.Toggled, IsToggled);
                }
            }
        }

        public override void _ExitTree()
        {
            if (_area != null)
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.BodyExited -= OnBodyExited;
            }
        }
    }
}
