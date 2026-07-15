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

            _owner = _area.GetParent() as Node2D;
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

        private void OnCollision(Node n)
        {
            if (n == _owner || _area == null) return;

            var health = n.FindChild(nameof(HealthComponent), false, false) as HealthComponent;
            if (health != null)
            {
                health.TakeDamage(Damage);

                var knockback = n.FindChild(nameof(KnockbackComponent), false, false) as KnockbackComponent;
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
