using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Follow target component. Blind — smoothly follows any Node2D target.
    /// Works for camera, pets, drones, UI elements, crosshairs.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FollowTargetComponent : ControllerComponent
    {
        [Export] public NodePath? TargetPath { get; set; }
        [Export] public float FollowSpeed { get; set; } = 5f;
        [Export] public Vector2 Offset { get; set; } = Vector2.Zero;
        [Export] public bool SnapOnStart { get; set; } = true;
        [Export] public float MaxDistance { get; set; } = 0f; // 0 = unlimited
        [Export] public bool LookAtTarget { get; set; } = false;

        [Signal] public delegate void TargetReachedEventHandler();
        [Signal] public delegate void TargetLostEventHandler();

        private Node2D? _target;
        private Node2D? _parent;

        public override void _Ready()
        {
            base._Ready();
            _parent = GetParent() as Node2D;
            if (TargetPath != null) SetTarget(GetNodeOrNull<Node2D>(TargetPath));
            if (_target != null && SnapOnStart && _parent != null)
                _parent.GlobalPosition = _target.GlobalPosition + Offset;
        }

        public void SetTarget(Node2D? target) => _target = target;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_parent == null || !IsActive) return;

            if (_target == null || !GodotObject.IsInstanceValid(_target))
            {
                EmitSignal(SignalName.TargetLost);
                _target = null;
                return;
            }

            Vector2 desired = _target.GlobalPosition + Offset;
            if (MaxDistance > 0 && _parent.GlobalPosition.DistanceTo(desired) > MaxDistance)
            {
                desired = _parent.GlobalPosition + (desired - _parent.GlobalPosition).Normalized() * MaxDistance;
            }

            _parent.GlobalPosition = _parent.GlobalPosition.Lerp(desired, FollowSpeed * (float)delta);

            if (_parent.GlobalPosition.DistanceTo(desired) < 1f)
                EmitSignal(SignalName.TargetReached);

            if (LookAtTarget)
                _parent.Rotation = (_target.GlobalPosition - _parent.GlobalPosition).Angle();
        }
    }
}
