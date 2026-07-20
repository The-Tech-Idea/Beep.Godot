using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Dash ability component. Attach to a CharacterBody2D. Provides a burst-speed
    /// directional dash with cooldown, optional i-frames (invincibility), and an
    /// afterimage trail effect.
    ///
    /// Input action: "dash" (defaults to Shift). Dash direction follows current
    /// input or facing direction if no input is held.
    ///
    /// Composable — stack alongside Jump, Slide, Glide, Hover, WallJump.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DashComponent : ControllerComponent
    {
        [ExportGroup("Dash")]
        [Export] public float DashSpeed { get; set; } = 800f;
        [Export] public float DashDuration { get; set; } = 0.15f;
        [Export] public float DashCooldown { get; set; } = 0.6f;
        [Export] public string DashAction { get; set; } = "dash";

        [ExportGroup("Invincibility")]
        [Export] public bool GrantIFrames { get; set; } = true;

        /// <summary>Block dashing while the "stun" status effect is active — matches the
        /// controllers and JumpComponent, which already honor stun.</summary>
        [Export] public bool StunBlocksDash { get; set; } = true;

        /// <summary>Stamina spent per dash. Only applies when a sibling HungerStaminaComponent is
        /// present — then a dash is refused while exhausted and costs this much. Gives the stamina
        /// system a real gate (it had none). 0 = free dash.</summary>
        [Export] public float StaminaCost { get; set; } = 20f;

        [Signal] public delegate void DashStartedEventHandler(Vector2 direction);
        [Signal] public delegate void DashEndedEventHandler();

        private CharacterBody2D? _body;
        private StatusEffectComponent? _statusEffects;
        private HungerStaminaComponent? _stamina;
        private float _dashTimer;
        private float _cooldownTimer;
        private Vector2 _dashDirection;

        public bool IsDashing => _dashTimer > 0;
        public bool IsOnCooldown => _cooldownTimer > 0;
        public bool IsInvincible => GrantIFrames && _dashTimer > 0;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _stamina = GetSiblingComponent<HungerStaminaComponent>();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            float dt = (float)delta;

            if (_dashTimer > 0)
            {
                _dashTimer -= dt;
                // Apply dash velocity. X always; Y only when the dash has vertical intent, so a
                // platformer's horizontal dash still leaves gravity to control Y, while a
                // top-down/fly vertical dash actually moves (the Y component used to be discarded).
                float vx = _dashDirection.X * DashSpeed;
                float vy = _dashDirection.Y != 0f ? _dashDirection.Y * DashSpeed : _body.Velocity.Y;
                _body.Velocity = new Vector2(vx, vy);
                // Set-only: the sibling controller owns MoveAndSlide. Calling it here too
                // integrated the body twice per frame (~2x dash distance) — the same fix
                // SlideComponent carries.

                // Apply invincibility effect during dash.
                if (GrantIFrames && _statusEffects != null && !_statusEffects.HasEffect("invincible"))
                    _statusEffects.ApplyEffect("invincible", DashDuration, isBuff: true, stackBehavior: StatusEffectComponent.StackBehavior.Refresh);

                if (_dashTimer <= 0)
                {
                    if (GrantIFrames && _statusEffects != null)
                        _statusEffects.RemoveEffect("invincible");
                    EmitSignal(SignalName.DashEnded);
                }
            }
            else
            {
                _cooldownTimer = Mathf.Max(0, _cooldownTimer - dt);

                // Check for dash input (blocked while stunned). Gate the reads so absent actions
                // don't spam a per-frame error before the input map is generated.
                bool stunned = StunBlocksDash && _statusEffects != null && _statusEffects.HasEffect("stun");
                if (InputActionsAvailable(DashAction, "move_left", "move_right", "move_up", "move_down")
                    && Input.IsActionJustPressed(DashAction) && _cooldownTimer <= 0 && !stunned)
                {
                    // Pay stamina if a HungerStaminaComponent is present — refuses when exhausted.
                    if (_stamina != null && !_stamina.TryConsumeStamina(StaminaCost))
                        return;
                    // Determine direction from input, fall back to facing.
                    float x = Input.GetAxis("move_left", "move_right");
                    float y = Input.GetAxis("move_up", "move_down");
                    _dashDirection = new Vector2(x, y);
                    if (_dashDirection == Vector2.Zero)
                        _dashDirection = new Vector2(_body.Velocity.X >= 0 ? 1f : -1f, 0f);
                    _dashDirection = _dashDirection.Normalized();

                    _dashTimer = DashDuration;
                    _cooldownTimer = DashCooldown;
                    EmitSignal(SignalName.DashStarted, _dashDirection);
                }
            }
        }

        /// <summary>Reset cooldown (e.g. on landing for "ground dash only" games).</summary>
        public void ResetCooldown() => _cooldownTimer = 0;
    }
}
