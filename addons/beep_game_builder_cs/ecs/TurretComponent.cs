using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Stationary turret. Attach to a Node2D (the turret base). Rotates to aim at the
    /// nearest node in <see cref="TargetGroup"/>, fires projectiles from <see cref="MuzzlePath"/>
    /// at <see cref="FireRate"/> intervals, and respects a line-of-sight ray check.
    /// Uses an ObjectPoolComponent sibling for projectile instantiation if present,
    /// otherwise instantiates the ProjectileScene directly.
    /// Replaces turret.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TurretComponent : GameplayComponent
    {
        [Export] public string TargetGroup { get; set; } = "players";
        [Export] public NodePath MuzzlePath { get; set; } = new("Muzzle");
        [Export] public PackedScene? ProjectileScene { get; set; }
        [Export] public float FireRate { get; set; } = 1.0f;
        [Export] public float Range { get; set; } = 400f;
        [Export] public float RotationSpeed { get; set; } = 3f;
        [Export] public bool RequireLineOfSight { get; set; } = true;
        [Export] public uint CollisionMask { get; set; } = 1;

        private Node2D? _turret;
        private Marker2D? _muzzle;
        private Node2D? _target;
        private double _cooldown;

        public override void _Ready()
        {
            base._Ready();
            _turret = GetParent() as Node2D;
            _muzzle = GetNodeOrNull<Marker2D>(MuzzlePath);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _turret == null || Engine.IsEditorHint()) return;

            AcquireTarget();
            if (_target == null || !GodotObject.IsInstanceValid(_target)) return;

            float dist = _turret.GlobalPosition.DistanceTo(_target.GlobalPosition);
            if (dist > Range) return;

            // Aim.
            float targetAngle = (_target.GlobalPosition - _turret.GlobalPosition).Angle();
            _turret.Rotation = Mathf.LerpAngle(_turret.Rotation, targetAngle, RotationSpeed * (float)delta);

            // LOS check.
            if (RequireLineOfSight)
            {
                var space = _turret.GetWorld2D().DirectSpaceState;
                var exclude = new Godot.Collections.Array<Rid>();
                if (_turret is CollisionObject2D co) exclude.Add(co.GetRid());
                var query = PhysicsRayQueryParameters2D.Create(
                    _turret.GlobalPosition, _target.GlobalPosition, CollisionMask, exclude);
                var hit = space.IntersectRay(query);
                if (hit.Count > 0 && hit["collider"].AsGodotObject() != _target) return;
            }

            // Fire.
            _cooldown -= delta;
            if (_cooldown <= 0)
            {
                _cooldown = 1.0 / FireRate;
                Fire();
            }
        }

        private void AcquireTarget()
        {
            if (_target != null && GodotObject.IsInstanceValid(_target) &&
                _turret!.GlobalPosition.DistanceTo(_target.GlobalPosition) <= Range) return;

            _target = null;
            foreach (var n in GetTree().GetNodesInGroup(TargetGroup))
            {
                if (n is Node2D candidate && GodotObject.IsInstanceValid(candidate))
                {
                    float d = _turret!.GlobalPosition.DistanceTo(candidate.GlobalPosition);
                    if (d <= Range)
                    {
                        _target = candidate;
                        return; // first in range
                    }
                }
            }
        }

        private void Fire()
        {
            if (ProjectileScene == null) return;
            Vector2 muzzlePos = _muzzle?.GlobalPosition ?? _turret!.GlobalPosition;
            Vector2 dir = Vector2.FromAngle(_turret!.Rotation);

            // Use an ObjectPoolComponent sibling if available.
            var pool = GetSiblingComponent<ObjectPoolComponent>();
            Node proj;
            if (pool != null)
            {
                proj = pool.Get() ?? ProjectileScene.Instantiate();
                var root = GetTree().CurrentScene.GetNodeOrNull("Projectiles") ?? GetTree().CurrentScene;
                if (proj.GetParent() == null) root.AddChild(proj);
            }
            else
            {
                proj = ProjectileScene.Instantiate();
                GetTree().CurrentScene.AddChild(proj);
            }

            if (proj is Node2D n2d)
            {
                n2d.GlobalPosition = muzzlePos;
                n2d.Rotation = dir.Angle();
            }
        }
    }
}
