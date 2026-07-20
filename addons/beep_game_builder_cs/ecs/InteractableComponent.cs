using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Interactable component. Blind — attach to an Area2D to make it interactive.
    /// Works for doors, switches, NPCs, chests, terminals, levers.
    ///
    /// Parent resolution + body-signal wiring live in <see cref="AreaTriggerComponent"/>.
    /// This used to do <c>GetParent() as Area2D</c> itself, so on the CharacterBody2D
    /// parents in the shipped scenes it silently did nothing; the base now warns instead.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InteractableComponent : AreaTriggerComponent
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
        private UI.InteractionPromptComponent? _prompt;
        private bool _promptResolved;

        public override void _Ready()
        {
            base._Ready();
            // Prefer a sibling DialogComponent, but fall back to anywhere in the entity's subtree.
            // Interactables are often parented under a DetectionArea, so a DialogComponent placed on
            // the NPC root (the intuitive spot) is NOT a sibling — the sibling-only lookup silently
            // missed it, and the "Talk" prompt led nowhere.
            _dialog = GetSiblingComponent<DialogComponent>();
            if (_dialog == null && Owner is Node owner)
                _dialog = EntityComponent.FindComponent<DialogComponent>(owner, true);
        }

        /// <summary>Resolve the HUD prompt lazily and once. It is a CROSS-TREE collaborator
        /// (prompt on the HUD CanvasLayer, this on a world Area2D), so a sibling lookup won't
        /// reach it — search from the scene root, the pattern WeatherHUDComponent uses. A null
        /// result is fine: a prompt is optional UI; interaction still fires without one.</summary>
        private UI.InteractionPromptComponent? Prompt()
        {
            if (_promptResolved) return _prompt;
            _promptResolved = true;
            _prompt = FindComponent<UI.InteractionPromptComponent>(GetTree().Root);
            return _prompt;
        }

        /// <summary>Is this body the player? Accepts either the "players" group or a node
        /// named "Player" — the generated scenes name the body "Player" (same convention
        /// DoorSwitchComponent uses), and only some are grouped, so relying on the group
        /// alone meant the prompt never fired.</summary>
        private static bool IsPlayer(Node n) => n.IsInGroup("players") || n.Name == "Player";

        protected override void OnBodyEntered(Node2D body)
        {
            if (!IsPlayer(body)) return;
            _playerInRange = true;
            Prompt()?.Show(PromptText);
            EmitSignal(SignalName.PlayerEnteredRange);
        }

        protected override void OnBodyExited(Node2D body)
        {
            if (!IsPlayer(body)) return;
            _playerInRange = false;
            Prompt()?.Hide();
            EmitSignal(SignalName.PlayerExitedRange);
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsActive || !_playerInRange) return;
            // Guard the action read — an absent action (template run before the input map is
            // generated) logs a per-event Godot error otherwise.
            if (!InputMap.HasAction(InputAction)) return;
            if (@event.IsActionPressed(InputAction))
            {
                GetViewport().SetInputAsHandled();
                EmitSignal(SignalName.Interacted);

                _dialog?.Interact();

                if (Toggleable)
                {
                    IsToggled = !IsToggled;
                    EmitSignal(SignalName.Toggled, IsToggled);
                }
            }
        }
    }
}
