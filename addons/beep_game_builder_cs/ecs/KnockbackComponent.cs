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
        public float EffectiveStrength => NonNegative(Strength);
        public float EffectiveFriction => NonNegative(Friction);
        public float EffectiveDuration => NonNegative(Duration);
        public float EffectiveMaxKnockbackMagnitude => NonNegative(MaxKnockbackMagnitude);

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
            if (!IsFinite(fromPosition) || !IsFinite(_body.GlobalPosition)) return;
            Vector2 dir = (_body.GlobalPosition - fromPosition).Normalized();
            float strength = EffectiveStrength;
            if (strength <= 0f) return;
            Vector2 newKnockback = dir * strength;

            _knockbackVelocity += newKnockback;
            _knockbackVelocity = _knockbackVelocity.LimitLength(EffectiveMaxKnockbackMagnitude);
            _remaining = EffectiveDuration;
            EmitSignal(SignalName.KnockedBack, dir, strength);
        }

        public override void _PhysicsProcess(double delta)
        {
            // !IsActive included so a knockback in flight stops when the component is deactivated,
            // rather than continuing to drive the body. (Instant-set controllers like ShooterController
            // that write Velocity = input*speed each frame overwrite the impulse — a known limitation.)
            if (Engine.IsEditorHint() || _body == null || _remaining <= 0 || !IsActive) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            _remaining -= dt;
            if (!IsFinite(_knockbackVelocity)) _knockbackVelocity = Vector2.Zero;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, EffectiveFriction * dt);

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

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
