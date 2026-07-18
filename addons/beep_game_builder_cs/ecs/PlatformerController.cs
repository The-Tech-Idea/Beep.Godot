using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Platformer movement controller component. Add as a CHILD of the CharacterBody2D
    /// it drives (not as the body's own script — see ControllerComponent.ResolveBody2D).
    /// Blind — works for players, enemies, NPCs with platformer physics.
    /// Uses Input actions: move_left, move_right, jump.
    /// When a JumpComponent sibling is present, jump behavior (and Jumped/Landed signals) defer entirely to it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PlatformerController : ControllerComponent
    {
        [Export] public float Speed { get; set; } = 300f;
        [Export] public float Gravity { get; set; } = 980f;
        [Export] public float JumpVelocity { get; set; } = -450f;
        [Export] public float Acceleration { get; set; } = 1200f;
        [Export] public float Friction { get; set; } = 1000f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public float JumpBufferTime { get; set; } = 0.1f;
        [Export] public bool StunBlocksMovement { get; set; } = true;

        [Signal] public delegate void JumpedEventHandler();
        [Signal] public delegate void LandedEventHandler();
        [Signal] public delegate void MovedEventHandler(Vector2 direction);

        private CharacterBody2D? _body;
        private JumpComponent? _jumpComponent;
        private StatusEffectComponent? _statusEffects;
        private StatsComponent? _stats;
        private KnockbackComponent? _knockback;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _wasInAir;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _stats = GetSiblingComponent<StatsComponent>();
            _knockback = GetSiblingComponent<KnockbackComponent>();
            var info = GameBuilder.GameInfo.Instance;
            // GameInfo is the project default — a FALLBACK, not an override. Seed only values the
            // scene left at their type-default, so an inspector-authored Speed/Gravity/JumpVelocity
            // survives scene load (matches ShooterController; the old unconditional copy silently
            // discarded any tuned value).
            if (info != null)
            {
                if (Mathf.IsEqualApprox(Speed, 300f)) Speed = info.MoveSpeed;
                if (Mathf.IsEqualApprox(Gravity, 980f)) Gravity = info.Gravity;
                if (Mathf.IsEqualApprox(JumpVelocity, -450f)) JumpVelocity = info.JumpVelocity;
            }
            if (_body != null)
                foreach (var child in _body.GetChildren())
                    if (child is JumpComponent jc) { _jumpComponent = jc; break; }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            if (!InputActionsAvailable("move_left", "move_right", "jump")) return;

            float dt = (float)delta;
            bool isStunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");
            var input = isStunned ? 0f : Input.GetAxis("move_left", "move_right");
            bool onFloor = _body.IsOnFloor();

            // Gravity
            if (!onFloor) _body.Velocity += new Vector2(0, Gravity * dt);

            // Coyote time
            if (onFloor) _coyoteTimer = CoyoteTime;
            else _coyoteTimer -= dt;

            // Jump buffer
            if (Input.IsActionJustPressed("jump")) _jumpBufferTimer = JumpBufferTime;
            else _jumpBufferTimer -= dt;

            // Jump (skip if JumpComponent is handling it)
            if (_jumpComponent == null && _jumpBufferTimer > 0 && _coyoteTimer > 0)
            {
                _body.Velocity = new Vector2(_body.Velocity.X, JumpVelocity);
                _jumpBufferTimer = 0; _coyoteTimer = 0;
                EmitSignal(SignalName.Jumped);
            }

            // Horizontal movement (apply status effect modifiers)
            float finalSpeed = _stats?.GetValue("move_speed", Speed) ?? Speed;
            float targetX = input * finalSpeed;
            _body.Velocity = new Vector2(
                Mathf.MoveToward(_body.Velocity.X, targetX,
                    (input != 0 ? Acceleration : Friction) * dt),
                _body.Velocity.Y);
            _body.MoveAndSlide();

            if (input != 0) EmitSignal(SignalName.Moved, new Vector2(input, 0));

            // Land detection
            if (onFloor && _wasInAir) EmitSignal(SignalName.Landed);
            _wasInAir = !onFloor;
        }
    }
}
