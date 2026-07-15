using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Knockback component. Blind — pushes any CharacterBody2D away from damage source.
    /// Works for players, enemies, physics objects.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KnockbackComponent : GameplayComponent
    {
        [Export] public float Strength { get; set; } = 200f;
        [Export] public float Friction { get; set; } = 600f;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public float MaxKnockbackMagnitude { get; set; } = 500f;

        [Signal] public delegate void KnockedBackEventHandler(Vector2 direction, float strength);

        private CharacterBody2D? _body;
        private Vector2 _knockbackVelocity;
        private float _remaining;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            if (_body == null)
                GD.PushError($"[Knockback] Parent must be CharacterBody2D, got {GetParent()?.GetType().Name}");
        }

        public void ApplyKnockback(Vector2 fromPosition)
        {
            if (_body == null || !IsActive) return;
            Vector2 dir = (_body.GlobalPosition - fromPosition).Normalized();
            Vector2 newKnockback = dir * Strength;

            _knockbackVelocity += newKnockback;
            _knockbackVelocity = _knockbackVelocity.LimitLength(MaxKnockbackMagnitude);
            _remaining = Duration;
            EmitSignal(SignalName.KnockedBack, dir, Strength);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || _remaining <= 0) return;
            _remaining -= (float)delta;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, Friction * (float)delta);
            _body.Velocity += _knockbackVelocity;
            _body.MoveAndSlide();
        }
    }
}
