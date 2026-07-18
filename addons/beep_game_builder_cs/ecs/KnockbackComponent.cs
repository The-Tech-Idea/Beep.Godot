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
        // True when no sibling already integrates the body (a controller / mover). Knockback is
        // blind — it also runs on crates and simple enemies with no controller — so on those it
        // must drive MoveAndSlide itself; when a controller is present, calling MoveAndSlide here
        // too moved the body twice per frame.
        private bool _ownsIntegration;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            if (_body == null)
                GD.PushError($"[Knockback] Parent must be CharacterBody2D, got {GetParent()?.GetType().Name}");
            _ownsIntegration = !HasMovementAuthoritySibling();
        }

        // A sibling that owns Velocity + MoveAndSlide each frame (a main controller or mover),
        // as opposed to the set-only ability components (Dash/Jump/Slide/...).
        private bool HasMovementAuthoritySibling()
        {
            if (GetParent() is not Node parent) return false;
            foreach (var child in parent.GetChildren())
                if (child is PlatformerController or TopDownController or ShooterController
                    or AIController or MovementComponent or FlyComponent)
                    return true;
            return false;
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

            if (_ownsIntegration)
            {
                // No controller to integrate for us — drive the body directly (SET, not +=, so a
                // controller-less body doesn't accumulate velocity across frames).
                _body.Velocity = _knockbackVelocity;
                _body.MoveAndSlide();
            }
            else
            {
                // A controller owns MoveAndSlide; add the decaying impulse on top of its input
                // velocity and let it integrate — no second MoveAndSlide here.
                _body.Velocity += _knockbackVelocity;
            }
        }
    }
}
