using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Projectile component. Attach to any Area2D to make it a projectile.
    /// Blind — works for bullets, arrows, spell orbs, thrown items, sports balls.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ProjectileComponent : GameplayComponent
    {
        [Export] public float Speed { get; set; } = 400f;
        [Export] public float MaxLifetime { get; set; } = 5f;
        [Export] public float Damage { get; set; } = 10f;
        [Export] public bool UseGravity { get; set; } = false;
        [Export] public float GravityStrength { get; set; } = 980f;
        [Export] public bool Pierce { get; set; } = false;

        [Signal] public delegate void HitEventHandler(Node? hitNode, Vector2 point);
        [Signal] public delegate void ExpiredEventHandler();

        /// <summary>Who fired this. Set by the shooter before <see cref="Launch"/>; the
        /// projectile and everything under it is excluded from collision, so a shooter can't
        /// hit itself.
        ///
        /// Must be explicit because projectiles are normally parented to a pool node, not to
        /// the shooter — inferring the owner from GetParent() yields the pool, and the
        /// exclusion silently never matches. Falls back to the parent for the case where a
        /// projectile IS spawned as a child of its shooter.</summary>
        public Node2D? Shooter { get; set; }

        private Vector2 _velocity;
        private float _lifetime;
        private Area2D? _area;
        private Node2D? _owner;

        public override void _Ready()
        {
            base._Ready();
            _area = GetParent() as Area2D;
            if (_area == null)
            {
                GD.PushError($"[Projectile] Parent must be Area2D, got {GetParent()?.GetType().Name}");
                return;
            }

            _area.BodyEntered += OnBodyEntered;
            _area.AreaEntered += OnAreaEntered;
        }

        private void OnBodyEntered(Node n)
        {
            OnCollision(n);
        }

        private void OnAreaEntered(Area2D n)
        {
            OnCollision(n);
        }

        /// <summary>Whether a collided node belongs to whoever fired this. Covers descendants,
        /// not just the shooter node itself — a hurtbox/hitbox Area2D is a CHILD of the body,
        /// so an identity check alone would let a shooter hit its own hurtbox.
        ///
        /// Resolved here rather than in _Ready: AddChild fires _Ready, so a spawner can only
        /// set Shooter after that. By first collision it is always set.</summary>
        private bool IsOwnedByShooter(Node n)
        {
            _owner ??= Shooter ?? _area?.GetParent() as Node2D;
            return _owner != null && (n == _owner || _owner.IsAncestorOf(n));
        }

        private void OnCollision(Node n)
        {
            if (_area == null || IsOwnedByShooter(n)) return;

            var health = EntityComponent.FindComponent<HealthComponent>(n, false);
            if (health != null)
            {
                health.TakeDamage(Damage);

                var knockback = EntityComponent.FindComponent<KnockbackComponent>(n, false);
                if (knockback != null && n is Node2D hitNode)
                    knockback.ApplyKnockback(_area.GlobalPosition);
            }

            EmitSignal(SignalName.Hit, n, _area.GlobalPosition);
            if (!Pierce) _area.QueueFree();
        }

        public void Launch(Vector2 direction)
        {
            _velocity = direction.Normalized() * Speed;
            _lifetime = MaxLifetime;
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_area == null || !IsActive) return;
            if (UseGravity) _velocity.Y += GravityStrength * (float)delta;
            _area.Position += _velocity * (float)delta;
            _lifetime -= (float)delta;
            if (_lifetime <= 0)
            {
                EmitSignal(SignalName.Expired);
                _area?.QueueFree();
            }
        }

        public override void _ExitTree()
        {
            if (_area != null)
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.AreaEntered -= OnAreaEntered;
            }
        }
    }
}
