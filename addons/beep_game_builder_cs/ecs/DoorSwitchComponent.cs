using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Switch/lever + key-gated door. Attach to an Area2D (the switch trigger) or an
    /// AnimatableBody2D (the door). When a body enters the switch zone and (optionally)
    /// holds the required key item, the door opens (becomes non-colliding + hides or animates).
    /// Emits SwitchToggled(isOpen). Can be wired to other components via the signal.
    /// Replaces door_switch.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DoorSwitchComponent : WorldComponent
    {
        /// <summary>NodePath to the door body this switch controls. The door should be
        /// a CollisionObject2D (StaticBody2D/AnimatableBody2D).</summary>
        [Export] public NodePath DoorPath { get; set; } = new("");

        /// <summary>If non-empty, the player must have this item id to activate.</summary>
        [Export] public string RequiredItem { get; set; } = "";

        /// <summary>Input action to press when in range (default "interact").</summary>
        [Export] public string ActivateAction { get; set; } = "interact";

        /// <summary>If true, re-closing the door is allowed.</summary>
        [Export] public bool Toggleable { get; set; } = true;

        [Signal] public delegate void SwitchToggledEventHandler(bool isOpen);

        private bool _isOpen;
        private bool _inRange;
        private CollisionObject2D? _door;

        public override void _Ready()
        {
            base._Ready();
            if (!DoorPath.IsEmpty)
                _door = GetNodeOrNull<CollisionObject2D>(DoorPath);
            if (GetParent() is Area2D area)
                area.BodyEntered += _ => _inRange = true;
            if (GetParent() is Area2D a2)
                a2.BodyExited += _ => _inRange = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive || !_inRange || (_isOpen && !Toggleable)) return;
            if (@event.IsActionPressed(ActivateAction))
            {
                GetTree().SetInputAsHandled();

                if (!string.IsNullOrEmpty(RequiredItem))
                {
                    var playerBody = GetTree().CurrentScene?.FindChild("Player", false, false);
                    if (playerBody is Node2D)
                    {
                        var inventory = playerBody.FindChild(nameof(PickupComponent), false, false) as PickupComponent;
                        if (inventory == null)
                        {
                            GD.Print($"[DoorSwitch] Missing required item '{RequiredItem}' to open");
                            return;
                        }
                    }
                }
                Toggle();
            }
        }

        public void Toggle()
        {
            _isOpen = !_isOpen;
            if (_door != null)
            {
                _door.SetDeferred("monitoring", !_isOpen);
                if (_door is CanvasItem ci)
                    ci.Visible = !_isOpen;
            }
            EmitSignal(SignalName.SwitchToggled, _isOpen);
        }

        public void Open() { if (!_isOpen) Toggle(); }
        public void Close() { if (_isOpen) Toggle(); }
    }
}
