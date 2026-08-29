using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Free-flight movement component for top-down shooters, flying enemies, or
    /// zero-gravity sections. Attach to a CharacterBody2D. Provides full 360-degree
    /// movement with acceleration, friction, and optional banking (visual tilt
    /// based on turn direction).
    ///
    /// Composable — can replace TopDownController or ShooterController for flying
    /// sections, or stack alongside Dash for air-dash.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlyComponent : ControllerComponent
    {
        [ExportGroup("Flight")]
        [Export] public float MaxSpeed { get; set; } = 400f;
        [Export] public float Acceleration { get; set; } = 800f;
        [Export] public float Friction { get; set; } = 400f;
        [Export] public float TurnSpeed { get; set; } = 8f;

        [ExportGroup("Boost")]
        [Export] public bool EnableBoost { get; set; } = true;
        [Export] public float BoostMultiplier { get; set; } = 1.5f;
        [Export] public string BoostAction { get; set; } = "dash";
        [Export] public float BoostDuration { get; set; } = 2f;

        [ExportGroup("Banking")]
        [Export] public bool EnableBanking { get; set; } = true;
        [Export] public float MaxBankAngle { get; set; } = 30f;
        [Export] public float BankSpeed { get; set; } = 5f;

        [Signal] public delegate void MovedEventHandler(Vector2 velocity);

        private CharacterBody2D? _body;
        private float _targetRotation;
        private float _boostTimer;
        // Child sprite that gets the visual bank/lean. A 2D body can't roll, so banking
        // is expressed as a Skew on the sprite — MaxBankAngle/BankSpeed drive it.
        private Node2D? _bankSprite;
        private StatusEffectComponent? _statusEffects;

        /// <summary>Freeze flight while the "stun" status effect is active — matches the ground
        /// controllers, which already honor stun (FlyComponent ignored it entirely before).</summary>
        [Export] public bool StunBlocksMovement { get; set; } = true;

        public float EffectiveMaxSpeed => NonNegative(MaxSpeed);
        public float EffectiveAcceleration => NonNegative(Acceleration);
        public float EffectiveFriction => NonNegative(Friction);
        public float EffectiveTurnSpeed => NonNegative(TurnSpeed);
        public float EffectiveBoostMultiplier => float.IsFinite(BoostMultiplier) ? Mathf.Max(1f, BoostMultiplier) : 1f;
        public float EffectiveBoostDuration => NonNegative(BoostDuration);
        public float EffectiveMaxBankAngle => NonNegative(MaxBankAngle);
        public float EffectiveBankSpeed => NonNegative(BankSpeed);

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();

            if (EnableBanking && _body != null)
            {
                _bankSprite = FindBankSprite(_body);
                if (_bankSprite == null)
                    GD.PushWarning($"[{Name}] EnableBanking is on but the body has no Sprite2D/AnimatedSprite2D child to lean; banking will do nothing. Add a sprite child or turn EnableBanking off.");
            }
        }

        // Prefer an actual sprite; fall back to the first Node2D child that isn't a body.
        private Node2D? FindBankSprite(Node body)
        {
            foreach (var child in body.GetChildren())
                if (child is Sprite2D or AnimatedSprite2D) return (Node2D)child;
            foreach (var child in body.GetChildren())
                if (child is Node2D n2d and not PhysicsBody2D) return n2d;
            return null;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || !GodotObject.IsInstanceValid(_body) || !IsActive) return;
            if (EnableBoost
                ? !InputActionsAvailable("move_left", "move_right", "move_up", "move_down", BoostAction)
                : !InputActionsAvailable("move_left", "move_right", "move_up", "move_down")) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;

            _boostTimer = Mathf.Max(0, _boostTimer - dt);

            // Read 8-directional input (zeroed while stunned, like the ground controllers).
            bool stunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");
            if (stunned)
                _boostTimer = 0f;
            float x = stunned ? 0f : Input.GetAxis("move_left", "move_right");
            float y = stunned ? 0f : Input.GetAxis("move_up", "move_down");
            Vector2 inputDir = new(x, y);

            // Check for boost activation.
            if (!stunned && EnableBoost && Input.IsActionJustPressed(BoostAction) && _boostTimer <= 0)
                _boostTimer = EffectiveBoostDuration;

            float currentMaxSpeed = EffectiveMaxSpeed;
            if (_boostTimer > 0)
                currentMaxSpeed *= EffectiveBoostMultiplier;

            if (inputDir.Length() > 0)
            {
                inputDir = inputDir.Normalized();
                _body.Velocity = _body.Velocity.MoveToward(inputDir * currentMaxSpeed, EffectiveAcceleration * dt);
                _targetRotation = inputDir.Angle();
            }
            else
            {
                _body.Velocity = _body.Velocity.MoveToward(Vector2.Zero, EffectiveFriction * dt);
            }

            // Banking — smoothly rotate the body toward the movement direction, then
            // lean the sprite into the turn (clamped to MaxBankAngle, eased at BankSpeed).
            if (EnableBanking && _body.Velocity.Length() > 1f)
            {
                _body.Rotation = Mathf.LerpAngle(_body.Rotation, _targetRotation, Mathf.Clamp(EffectiveTurnSpeed * dt, 0f, 1f));

                if (_bankSprite != null)
                {
                    // How sharply we're turning → lean amount, clamped to the max bank angle.
                    float turnDelta = Mathf.AngleDifference(_body.Rotation, _targetRotation);
                    float maxRad = Mathf.DegToRad(EffectiveMaxBankAngle);
                    float targetBank = Mathf.Clamp(turnDelta, -maxRad, maxRad);
                    _bankSprite.Skew = Mathf.Lerp(_bankSprite.Skew, targetBank, Mathf.Clamp(EffectiveBankSpeed * dt, 0f, 1f));
                }
            }
            else if (_bankSprite != null && !Mathf.IsZeroApprox(_bankSprite.Skew))
            {
                // Level out when not turning/flying.
                _bankSprite.Skew = Mathf.Lerp(_bankSprite.Skew, 0f, Mathf.Clamp(EffectiveBankSpeed * dt, 0f, 1f));
            }

            _body.MoveAndSlide();

            if (_body.Velocity.Length() > 1f)
                EmitSignal(SignalName.Moved, _body.Velocity);
        }

        /// <summary>Set velocity directly (for knockback, wind, conveyor belts).</summary>
        public void ApplyExternalForce(Vector2 force)
        {
            if (_body == null || !IsFinite(force)) return;
            if (!IsFinite(_body.Velocity)) _body.Velocity = Vector2.Zero;
            _body.Velocity += force;
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
