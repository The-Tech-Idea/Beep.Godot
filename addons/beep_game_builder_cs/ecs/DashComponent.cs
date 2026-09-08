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

        public float EffectiveDashSpeed => NonNegative(DashSpeed);
        public float EffectiveDashDuration => NonNegative(DashDuration);
        public float EffectiveDashCooldown => NonNegative(DashCooldown);
        public float EffectiveStaminaCost => NonNegative(StaminaCost);

        private CharacterBody2D? _body;
        private StatusEffectComponent? _statusEffects;
        private HungerStaminaComponent? _stamina;
        private float _dashTimer;
        private float _cooldownTimer;
        private Vector2 _dashDirection;

        public bool IsDashing => _dashTimer > 0;
        public bool IsOnCooldown => _cooldownTimer > 0;
        public bool IsInvincible => IsActive && GrantIFrames && _dashTimer > 0;

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _stamina = GetSiblingComponent<HungerStaminaComponent>();
            ProcessPhysicsPriority = -10;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!IsActive) { CancelDash(); return; }
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body) || !IsActive) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            var actor = ActorComponent.ForBody(_body);
            if (actor is not null && (!actor.IsActive || actor.IsDead || actor.HasOrders))
            { actor.ConsumeDash(); CancelDash(); return; }
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;
            if (!IsFinite(_dashDirection)) _dashDirection = Vector2.Right;

            if (_dashTimer > 0)
            {
                _dashTimer -= dt;
                // CharacterMotion applies this after the controller computes ordinary velocity.
                if (_dashTimer <= 0)
                {
                    EmitSignal(SignalName.DashEnded);
                }
            }
            else
            {
                _cooldownTimer = Mathf.Max(0, _cooldownTimer - dt);

                // Check for dash input (blocked while stunned). Gate the reads so absent actions
                // don't spam a per-frame error before the input map is generated.
                bool stunned = StunBlocksDash && _statusEffects != null && _statusEffects.HasEffect("stun");
                bool requested = actor is not null ? actor.ConsumeDash()
                    : InputActionsAvailable(DashAction, "move_left", "move_right", "move_up", "move_down") && Input.IsActionJustPressed(DashAction);
                if (requested && _cooldownTimer <= 0 && !stunned)
                {
                    Vector2 direction = actor?.MoveIntent ?? Input.GetVector("move_left", "move_right", "move_up", "move_down");
                    if (direction == Vector2.Zero) direction = actor?.AimIntent ?? new Vector2(_body.Velocity.X >= 0 ? 1f : -1f, 0f);
                    TryDash(direction);
                }
            }
        }

        /// <summary>Reset cooldown (e.g. on landing for "ground dash only" games).</summary>
        public void ResetCooldown() => _cooldownTimer = 0;

        public bool TryDash(Vector2 direction)
        {
            if (!IsActive || !GodotObject.IsInstanceValid(_body) || !direction.IsFinite() || direction == Vector2.Zero
                || IsDashing || IsOnCooldown || EffectiveDashDuration <= 0 || EffectiveDashSpeed <= 0) return false;
            var actor = ActorComponent.ForBody(_body);
            if (actor is not null && (!actor.IsActive || actor.IsDead || actor.HasOrders)) return false;
            if (GetSiblingComponent<GridPathFollowerComponent>() is { IsMoving: true }) return false;
            if (StunBlocksDash && _statusEffects?.HasEffect("stun") == true) return false;
            if (_stamina is not null && !_stamina.TryConsumeStamina(EffectiveStaminaCost)) return false;
            _dashDirection = direction.Normalized();
            _dashTimer = EffectiveDashDuration; _cooldownTimer = EffectiveDashCooldown;
            EmitSignal(SignalName.DashStarted, _dashDirection);
            return true;
        }

        internal Vector2 ApplyVelocity(Vector2 ordinary)
        {
            bool preserveGravity = _dashDirection.Y == 0 && GetSiblingComponent<PlatformerController>() is { IsActive: true };
            return new(_dashDirection.X * EffectiveDashSpeed, preserveGravity ? ordinary.Y : _dashDirection.Y * EffectiveDashSpeed);
        }

        public void CancelDash()
        {
            if (!IsDashing) return;
            _dashTimer = 0;
            EmitSignal(SignalName.DashEnded);
        }

        public override void _ExitTree()
        {
            CancelDash();
            _body = null; _statusEffects = null; _stamina = null;
            _cooldownTimer = 0;
            RequestReady();
            base._ExitTree();
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
