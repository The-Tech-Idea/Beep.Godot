using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Projectile behavior modifier. Attach to a projectile Area2D/CharacterBody2D alongside
    /// a ProjectileComponent (or instead of one). One component, one mode from the enum:
    /// • Straight — constant velocity (the default, basically no modifier).
    /// • Homing — steers toward the nearest node in TargetGroup.
    /// • Bounce — reflects velocity off collision normals (bounces off walls).
    /// • Spread — fans out N projectiles on spawn (used by the spawner, not the projectile).
    /// Replaces projectile_variants.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ProjectileModifierComponent : GameplayComponent
    {
        public enum ModifierMode { Straight, Homing, Bounce, Spread }

        [Export] public ModifierMode Mode { get; set; } = ModifierMode.Straight;
        [Export] public float Speed { get; set; } = 400f;
        [Export] public string TargetGroup { get; set; } = "enemies";
        [Export] public float HomingStrength { get; set; } = 5f;
        [Export] public int MaxBounces { get; set; } = 3;

        private Node2D? _body;
        private Vector2 _velocity;
        private int _bounces;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as Node2D;
            if (_body != null)
                _velocity = Vector2.FromAngle(_body.Rotation) * Speed;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _body == null || Engine.IsEditorHint()) return;

            switch (Mode)
            {
                case ModifierMode.Homing:
                    var target = FindNearestTarget();
                    if (target != null)
                    {
                        Vector2 desired = (target.GlobalPosition - _body.GlobalPosition).Normalized() * Speed;
                        _velocity = _velocity.Lerp(desired, HomingStrength * (float)delta);
                        _body.Rotation = _velocity.Angle();
                    }
                    _body.GlobalPosition += _velocity * (float)delta;
                    break;

                case ModifierMode.Bounce:
                    _body.GlobalPosition += _velocity * (float)delta;
                    // Check collision for bounce.
                    if (_body is CharacterBody2D cb)
                    {
                        cb.Velocity = _velocity;
                        cb.MoveAndSlide();
                        if (cb.GetSlideCollisionCount() > 0 && _bounces < MaxBounces)
                        {
                            var col = cb.GetSlideCollision(0);
                            _velocity = _velocity.Bounce(col.GetNormal());
                            _bounces++;
                        }
                        else if (cb.GetSlideCollisionCount() > 0 && _bounces >= MaxBounces)
                        {
                            cb.QueueFree();
                        }
                    }
                    break;

                default: // Straight
                    _body.GlobalPosition += _velocity * (float)delta;
                    break;
            }
        }

        private Node2D? FindNearestTarget()
        {
            Node2D? nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var n in GetTree().GetNodesInGroup(TargetGroup))
            {
                if (n is not Node2D node || !GodotObject.IsInstanceValid(node)) continue;
                float d = _body!.GlobalPosition.DistanceSquaredTo(node.GlobalPosition);
                if (d < nearestDist) { nearestDist = d; nearest = node; }
            }
            return nearest;
        }
    }
}
