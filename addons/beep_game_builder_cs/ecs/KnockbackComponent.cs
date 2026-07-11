using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Knockback component. Blind — pushes any CharacterBody2D away from damage source.
    /// Works for players, enemies, physics objects.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KnockbackComponent : EntityComponent
    {
        [Export] public float Strength { get; set; } = 200f;
        [Export] public float Friction { get; set; } = 600f;
        [Export] public float Duration { get; set; } = 0.3f;

        [Signal] public delegate void KnockedBackEventHandler(Vector2 direction, float strength);

        private CharacterBody2D? _body;
        private Vector2 _knockbackVelocity;
        private float _remaining;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent<CharacterBody2D>();
        }

        public void ApplyKnockback(Vector2 fromPosition)
        {
            if (_body == null || !IsActive) return;
            Vector2 dir = (_body.GlobalPosition - fromPosition).Normalized();
            _knockbackVelocity = dir * Strength;
            _remaining = Duration;
            EmitSignal(SignalName.KnockedBack, dir, Strength);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || _remaining <= 0) return;
            _remaining -= (float)delta;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, Friction * (float)delta);
            _body.Velocity = _knockbackVelocity;
            _body.MoveAndSlide();
        }
    }
}
