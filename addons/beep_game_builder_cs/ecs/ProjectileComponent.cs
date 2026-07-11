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

        public override void _Ready()
        {
            base._Ready();
            _area = GetParent() as Area2D;
            if (_area != null)
            {
                _area.BodyEntered += n => { EmitSignal(SignalName.Hit, n, _area.GlobalPosition); if (!Pierce && _area != null) _area.QueueFree(); };
                _area.AreaEntered += n => { EmitSignal(SignalName.Hit, n, _area.GlobalPosition); if (!Pierce && _area != null) _area.QueueFree(); };
            }
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
    }
}
