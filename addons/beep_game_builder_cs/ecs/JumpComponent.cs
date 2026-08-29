using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Advanced jump component. Attach as a child to a CharacterBody2D (alongside a movement
    /// controller like PlatformerController). Provides double-jump, variable jump
    /// height (release early = shorter jump), and apex hang (brief slow-down at
    /// the top of the arc for floaty feel).
    ///
    /// Does NOT apply gravity itself — must be composed alongside a controller that does
    /// (currently only PlatformerController). When present, PlatformerController's built-in
    /// jump logic automatically defers to this component.
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
        [Export] public float VariableJumpCutDuration { get; set; } = 0.1f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public float JumpBufferTime { get; set; } = 0.1f;

        [ExportGroup("Apex Hang")]
        [Export] public float ApexHangMultiplier { get; set; } = 0.5f;
        [Export] public float ApexThreshold { get; set; } = 30f;

        [ExportGroup("Status Effects")]
        [Export] public bool StunBlocksJump { get; set; } = true;

        [Signal] public delegate void JumpedEventHandler(int jumpsRemaining);
        [Signal] public delegate void DoubleJumpedEventHandler();

        public float EffectiveJumpForce => float.IsFinite(JumpForce) ? -Mathf.Abs(JumpForce) : -450f;
        public int EffectiveMaxJumps => Mathf.Max(0, MaxJumps);
        public float EffectiveVariableJumpMultiplier => float.IsFinite(VariableJumpMultiplier) ? Mathf.Clamp(VariableJumpMultiplier, 0f, 1f) : 1f;
        public float EffectiveVariableJumpCutDuration => float.IsFinite(VariableJumpCutDuration) ? Mathf.Max(0.001f, VariableJumpCutDuration) : 0.001f;
        public float EffectiveCoyoteTime => NonNegative(CoyoteTime);
        public float EffectiveJumpBufferTime => NonNegative(JumpBufferTime);
        public float EffectiveApexHangMultiplier => float.IsFinite(ApexHangMultiplier) ? Mathf.Clamp(ApexHangMultiplier, 0f, 1f) : 1f;
        public float EffectiveApexThreshold => float.IsFinite(ApexThreshold) ? Mathf.Max(0.001f, ApexThreshold) : 0.001f;

        private CharacterBody2D? _body;
        private StatusEffectComponent? _statusEffects;
        private int _jumpsRemaining;
        private float _coyoteTimer;
        private float _bufferTimer;
        private float _jumpCutTimer;
        private bool _jumpHeld;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _jumpsRemaining = EffectiveMaxJumps;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body) || !IsActive) return;
            // Gate the "jump" reads so an absent action doesn't spam a per-frame error before the
            // input map is generated (matches the controllers). No jump is possible without it anyway.
            if (!InputActionsAvailable("jump")) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;
            bool onFloor = _body.IsOnFloor();

            if (onFloor)
            {
                _jumpsRemaining = EffectiveMaxJumps;
                _coyoteTimer = EffectiveCoyoteTime;
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (Input.IsActionJustPressed("jump"))
                _bufferTimer = EffectiveJumpBufferTime;
            else
                _bufferTimer -= dt;

            _jumpHeld = Input.IsActionPressed("jump");

            // Apex hang — reduce gravity only when moving upward near threshold.
            float apexThreshold = EffectiveApexThreshold;
            if (!onFloor && _body.Velocity.Y < 0 && _body.Velocity.Y > -apexThreshold)
            {
                float slowFactor = 1f - (1f - EffectiveApexHangMultiplier) * (1f - (-_body.Velocity.Y / apexThreshold));
                _body.Velocity = new Vector2(_body.Velocity.X, _body.Velocity.Y * slowFactor);
            }

            // Variable jump height — cut upward velocity when jump is released (frame-independent).
            if (!_jumpHeld && _body.Velocity.Y < 0)
            {
                _jumpCutTimer += dt;
                float cutDuration = EffectiveVariableJumpCutDuration;
                if (_jumpCutTimer < cutDuration)
                {
                    float cutProgress = _jumpCutTimer / cutDuration;
                    _body.Velocity = new Vector2(_body.Velocity.X, _body.Velocity.Y * Mathf.Lerp(1f, EffectiveVariableJumpMultiplier, cutProgress));
                }
            }
            else
            {
                _jumpCutTimer = 0;
            }

            // Check for stun/freeze status effects.
            bool isStunned = StunBlocksJump && _statusEffects != null && _statusEffects.HasEffect("stun");
            if (isStunned)
                _bufferTimer = 0f;

            // Execute buffered jump.
            if (_bufferTimer > 0 && _jumpsRemaining > 0 && !isStunned && (onFloor || _coyoteTimer > 0 || _jumpsRemaining < EffectiveMaxJumps))
            {
                _body.Velocity = new Vector2(_body.Velocity.X, EffectiveJumpForce);
                _jumpsRemaining--;
                _bufferTimer = 0;
                _coyoteTimer = 0;
                _jumpCutTimer = 0;
                if (_jumpsRemaining == EffectiveMaxJumps - 1)
                    EmitSignal(SignalName.Jumped, _jumpsRemaining);
                else
                    EmitSignal(SignalName.DoubleJumped);
            }
        }

        /// <summary>Manually trigger a jump (e.g. from a bounce pad).</summary>
        public void ForceJump(float force)
        {
            if (!IsActive) return;
            if (_body == null) return;
            float jumpForce = float.IsFinite(force) ? -Mathf.Abs(force) : EffectiveJumpForce;
            float x = float.IsFinite(_body.Velocity.X) ? _body.Velocity.X : 0f;
            _body.Velocity = new Vector2(x, jumpForce);
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
