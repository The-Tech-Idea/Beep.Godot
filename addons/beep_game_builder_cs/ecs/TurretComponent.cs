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
        [Export] public float ProjectileDamage { get; set; } = 10f;
        [Export] public float ProjectileSpeed { get; set; } = 400f;
        [Export] public float Range { get; set; } = 400f;
        [Export] public float RotationSpeed { get; set; } = 3f;
        [Export] public bool RequireLineOfSight { get; set; } = true;
        [Export] public uint CollisionMask { get; set; } = 1;

        private Node2D? _turret;
        private Marker2D? _muzzle;
        private Node2D? _target;
        private double _cooldown;
        private ObjectPoolComponent? _pool;

        public float EffectiveFireRate => Mathf.Max(0.01f, float.IsFinite(FireRate) ? FireRate : 1f);
        public float EffectiveProjectileDamage => NonNegative(ProjectileDamage);
        public float EffectiveProjectileSpeed => NonNegative(ProjectileSpeed);
        public float EffectiveRange => NonNegative(Range);
        public float EffectiveRotationSpeed => NonNegative(RotationSpeed);

        public override void _Ready()
        {
            base._Ready();
            _turret = GetParent() as Node2D;
            if (_turret == null)
                GD.PushWarning($"[{Name}] parent is not a Node2D — the turret has no position to fire from and will do nothing. Parent it to the turret body.");
            // A null ProjectileScene means the turret acquires, aims, and ticks cooldown but Fire()
            // returns silently forever — it looks alive but shoots nothing. Say so up front (runtime
            // only — don't warn at design time before the dev has wired the scene).
            if (ProjectileScene == null && !Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] has no ProjectileScene — the turret will aim at targets but never fire. Assign a projectile scene.");
            _muzzle = GetNodeOrNull<Marker2D>(MuzzlePath);
            // A sibling pool is used opportunistically. NOTE: the stock ProjectileComponent frees
            // itself on hit/expiry, so it never Release()s back — the pool self-heals (Get() purges
            // freed slots) and effectively falls back to per-shot Instantiate past the preloaded set.
            // To truly recycle, give the projectile a pool-return path instead of QueueFree.
            _pool = GetSiblingComponent<ObjectPoolComponent>();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive || _turret == null || !GodotObject.IsInstanceValid(_turret) || Engine.IsEditorHint()) return;

            AcquireTarget();
            if (_target == null || !GodotObject.IsInstanceValid(_target)) return;

            Vector2 turretPosition = IsFinite(_turret.GlobalPosition) ? _turret.GlobalPosition : Vector2.Zero;
            Vector2 targetPosition = IsFinite(_target.GlobalPosition) ? _target.GlobalPosition : turretPosition;
            float dist = turretPosition.DistanceTo(targetPosition);
            if (!float.IsFinite(dist) || dist > EffectiveRange) return;

            // Aim.
            float dt = DeltaSeconds(delta);
            Vector2 aim = targetPosition - turretPosition;
            if (aim.LengthSquared() > 0.0001f)
            {
                float targetAngle = aim.Angle();
                float rotation = float.IsFinite(_turret.Rotation) ? _turret.Rotation : targetAngle;
                _turret.Rotation = Mathf.LerpAngle(rotation, targetAngle, Mathf.Clamp(EffectiveRotationSpeed * dt, 0f, 1f));
            }

            // LOS check.
            if (RequireLineOfSight)
            {
                var space = _turret.GetWorld2D().DirectSpaceState;
                var exclude = new Godot.Collections.Array<Rid>();
                if (_turret is CollisionObject2D co) exclude.Add(co.GetRid());
                var query = PhysicsRayQueryParameters2D.Create(
                    turretPosition, targetPosition, CollisionMask, exclude);
                var hit = space.IntersectRay(query);
                if (hit.Count > 0 && hit["collider"].AsGodotObject() != _target) return;
            }

            // Fire.
            _cooldown = System.Math.Max(0.0, (double.IsFinite(_cooldown) ? _cooldown : 0.0) - dt);
            if (_cooldown <= 0)
            {
                _cooldown = 1.0 / EffectiveFireRate;
                Fire();
            }
        }

        private void AcquireTarget()
        {
            if (_turret == null || !GodotObject.IsInstanceValid(_turret)) return;
            if (_target != null && GodotObject.IsInstanceValid(_target) &&
                IsFinite(_turret.GlobalPosition) &&
                IsFinite(_target.GlobalPosition) &&
                _turret.GlobalPosition.DistanceTo(_target.GlobalPosition) <= EffectiveRange) return;

            _target = null;
            Vector2 turretPosition = IsFinite(_turret.GlobalPosition) ? _turret.GlobalPosition : Vector2.Zero;
            foreach (var n in GetTree().GetNodesInGroup(TargetGroup))
            {
                if (n is Node2D candidate && GodotObject.IsInstanceValid(candidate))
                {
                    Vector2 candidatePosition = IsFinite(candidate.GlobalPosition) ? candidate.GlobalPosition : turretPosition;
                    float d = turretPosition.DistanceTo(candidatePosition);
                    if (float.IsFinite(d) && d <= EffectiveRange)
                    {
                        _target = candidate;
                        return; // first in range
                    }
                }
            }
        }

        private void Fire()
        {
            if (ProjectileScene == null || _turret == null || !GodotObject.IsInstanceValid(_turret)) return;
            Vector2 muzzlePos = _muzzle != null && IsFinite(_muzzle.GlobalPosition)
                ? _muzzle.GlobalPosition
                : IsFinite(_turret.GlobalPosition) ? _turret.GlobalPosition : Vector2.Zero;
            Vector2 dir = Vector2.FromAngle(_turret.Rotation);

            Node proj = _pool?.Get() ?? ProjectileScene.Instantiate();
            // Recursive lookup: the Projectiles pool is nested under the level (LevelContainer/
            // Level1/Projectiles), so a direct-child GetNodeOrNull never found it and bullets fell
            // back to the scene root and outlived their level. Matches ShooterController.
            var currentScene = GetTree().CurrentScene;
            if (currentScene == null || !GodotObject.IsInstanceValid(currentScene)) return;
            var host = currentScene.FindChild("Projectiles", recursive: true, owned: false)
                       ?? currentScene;
            if (proj.GetParent() == null) host.AddChild(proj);

            if (proj is Node2D n2d)
            {
                n2d.GlobalPosition = muzzlePos;
                n2d.Rotation = dir.Angle();

                var projComp = EntityComponent.FindComponent<ProjectileComponent>(n2d, false);
                if (projComp != null)
                {
                    projComp.Damage = EffectiveProjectileDamage;
                    projComp.Speed = EffectiveProjectileSpeed;
                    projComp.Launch(dir);
                }
            }
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
