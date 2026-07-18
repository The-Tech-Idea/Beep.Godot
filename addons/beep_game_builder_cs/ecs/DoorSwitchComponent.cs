using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Switch/lever + key-gated door. Attach to the Area2D switch trigger; point
    /// <see cref="DoorPath"/> at the door body (StaticBody2D/AnimatableBody2D). When a body
    /// enters the switch zone and (optionally) holds the required key item, the door opens
    /// (becomes non-colliding + hides or animates). Emits SwitchToggled(isOpen). Can be wired
    /// to other components via the signal. Replaces door_switch.gd.template.
    ///
    /// Parent resolution + body-signal wiring live in <see cref="AreaTriggerComponent"/>; this
    /// used to hand-roll a guarded <c>GetParent() is Area2D</c> that no-opped in silence when
    /// the parent was wrong.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DoorSwitchComponent : AreaTriggerComponent
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
        private uint _doorLayer, _doorMask;   // saved so an opened door can be made solid again

        public override void _Ready()
        {
            base._Ready();
            if (!DoorPath.IsEmpty)
                _door = GetNodeOrNull<CollisionObject2D>(DoorPath);
            if (_door != null) { _doorLayer = _door.CollisionLayer; _doorMask = _door.CollisionMask; }
        }

        protected override void OnBodyEntered(Node2D body) => _inRange = true;
        protected override void OnBodyExited(Node2D body) => _inRange = false;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive || !_inRange || (_isOpen && !Toggleable)) return;
            if (@event.IsActionPressed(ActivateAction))
            {
                GetViewport().SetInputAsHandled();

                if (!string.IsNullOrEmpty(RequiredItem))
                {
                    var playerBody = GetTree().CurrentScene?.FindChild("Player", false, false);
                    // Compare against the actual carried item, not "has any inventory". Before,
                    // this only checked that *a* PickupComponent existed on the player, so any
                    // player opened any gated door — RequiredItem was never consulted.
                    var inventory = playerBody != null
                        ? EntityComponent.FindComponent<InventoryComponent>(playerBody, false)
                        : null;
                    if (inventory == null || !inventory.HasItem(RequiredItem))
                    {
                        GD.Print($"[DoorSwitch] Missing required item '{RequiredItem}' to open");
                        return;
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
                // Actually make the door passable. `monitoring` is an Area2D-only property, so the
                // old code left a StaticBody2D door invisible-but-solid; clear its collision
                // layer/mask when open, restore them when closed. Deferred: safe inside a physics step.
                _door.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, _isOpen ? 0u : _doorLayer);
                _door.SetDeferred(CollisionObject2D.PropertyName.CollisionMask, _isOpen ? 0u : _doorMask);
                if (_door is CanvasItem ci)
                    ci.Visible = !_isOpen;
            }
            EmitSignal(SignalName.SwitchToggled, _isOpen);
        }

        public void Open() { if (!_isOpen) Toggle(); }
        public void Close() { if (_isOpen) Toggle(); }
    }
}
