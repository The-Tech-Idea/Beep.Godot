using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Advanced jump component. Attach to a CharacterBody2D (alongside a movement
    /// controller like PlatformerController). Provides double-jump, variable jump
    /// height (release early = shorter jump), and apex hang (brief slow-down at
    /// the top of the arc for floaty feel).
    ///
    /// Composable — stack alongside Slide, Dash, Glide, Hover, WallJump.
    /// All parameters are [Export] so the user tunes them in the inspector.
    /// Signals let other systems react (particles on jump, sound on land).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class JumpComponent : ControllerComponent
    {
        [ExportGroup("Jump")]
        [Export] public float JumpForce { get; set; } = -450f;
        [Export] public int MaxJumps { get; set; } = 2;
        [Export] public float VariableJumpMultiplier { get; set; } = 0.5f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public float JumpBufferTime { get; set; } = 0.1f;

        [ExportGroup("Apex Hang")]
        [Export] public float ApexHangMultiplier { get; set; } = 0.5f;
        [Export] public float ApexThreshold { get; set; } = 30f;

        [Signal] public delegate void JumpedEventHandler(int jumpsRemaining);
        [Signal] public delegate void DoubleJumpedEventHandler();

        private CharacterBody2D? _body;
        private int _jumpsRemaining;
        private float _coyoteTimer;
        private float _bufferTimer;
        private bool _jumpHeld;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            _jumpsRemaining = MaxJumps;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;
            bool onFloor = _body.IsOnFloor();

            if (onFloor)
            {
                _jumpsRemaining = MaxJumps;
                _coyoteTimer = CoyoteTime;
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (Input.IsActionJustPressed("jump"))
                _bufferTimer = JumpBufferTime;
            else
                _bufferTimer -= dt;

            _jumpHeld = Input.IsActionPressed("jump");

            // Apex hang — reduce gravity near the top of the jump.
            if (!onFloor && Mathf.Abs(_body.Velocity.Y) < ApexThreshold)
            {
                float slowFactor = 1f - (1f - ApexHangMultiplier) * (1f - Mathf.Abs(_body.Velocity.Y) / ApexThreshold);
                _body.Velocity = new Vector2(_body.Velocity.X, _body.Velocity.Y * slowFactor);
            }

            // Variable jump height — cut upward velocity when jump is released.
            if (!_jumpHeld && _body.Velocity.Y < 0)
                _body.Velocity = new Vector2(_body.Velocity.X, _body.Velocity.Y * VariableJumpMultiplier * dt * 10f + _body.Velocity.Y * (1f - dt * 10f));

            // Execute buffered jump.
            if (_bufferTimer > 0 && _jumpsRemaining > 0 && (onFloor || _coyoteTimer > 0 || _jumpsRemaining < MaxJumps))
            {
                _body.Velocity = new Vector2(_body.Velocity.X, JumpForce);
                _jumpsRemaining--;
                _bufferTimer = 0;
                _coyoteTimer = 0;
                if (_jumpsRemaining == MaxJumps - 1)
                    EmitSignal(SignalName.Jumped, _jumpsRemaining);
                else
                    EmitSignal(SignalName.DoubleJumped);
            }
        }

        /// <summary>Manually trigger a jump (e.g. from a bounce pad).</summary>
        public void ForceJump(float force)
        {
            if (_body == null) return;
            _body.Velocity = new Vector2(_body.Velocity.X, force);
        }
    }
}
